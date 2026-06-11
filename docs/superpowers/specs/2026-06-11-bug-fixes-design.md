# SpriteBatchWindow Bug 修正設計規格

## 概要

修正 code review 發現的 8 項錯誤，分為 5 個修正群組。所有修改集中在 `SpriteBatchWindow.cs`，不觸及 `SpriteBatchProcessor.cs`、`SpriteBatchData.cs`、`SpriteBatchWindowState.cs`。

**涉及檔案：**

| 動作 | 檔案 |
|------|------|
| 修改 | `Packages/com.wenrong.spritebatchslicer/Editor/SpriteBatchWindow.cs` |

---

## Bug 群組

### 🔴 A. Domain Reload：LoadAssetAtPath 過早呼叫 + RefreshTexturePaths 未呼叫（Bug 1 & 2）

**問題 1（line 286）：** `LoadState()` 在 `OnEnable` 中直接呼叫 `AssetDatabase.LoadAssetAtPath<DefaultAsset>`。Domain Reload 期間，Unity 呼叫 `OnEnable` 時 AssetDatabase 尚未就緒，所有路徑回傳 `null`，資料夾清單全部靜默遺失。

**問題 2（line 45）：** `OnEnable` 呼叫 `LoadState()` 後未呼叫 `RefreshTexturePaths()`，導致 `_allTexturePaths` 維持空清單。即使資料夾成功還原，預覽區仍顯示「請先新增目標資料夾」，Apply 區顯示 0 張圖片。

**設計：** 將 `LoadState()` 拆成兩階段：

- **第一階段（立即，在 `OnEnable`）：** `LoadScalarSettings()` — 只載入非 asset 欄位（MaxTextureSize、FilterMode、AlphaIsTransparency、Compression、SpriteRects），這些在 domain reload 期間可安全讀取。

- **第二階段（延遲，使用 `EditorApplication.delayCall`）：** `DelayedLoadFolders()` — 等 AssetDatabase 就緒後，載入資料夾 asset，再呼叫 `RefreshTexturePaths()`，最後 `Repaint()`。

```csharp
private void OnEnable()
{
    LoadScalarSettings();
    InitFolderList();
    InitRectList();
    EditorApplication.delayCall += DelayedLoadFolders;
}

private void OnDisable()
{
    EditorApplication.delayCall -= DelayedLoadFolders;
    SaveState();
}

private void LoadScalarSettings()
{
    var s = SpriteBatchWindowState.instance;
    _settings.MaxTextureSize      = System.Array.IndexOf(MaxTextureSizeValues, s.MaxTextureSize) >= 0
                                     ? s.MaxTextureSize
                                     : 2048;
    _settings.FilterMode          = s.FilterMode;
    _settings.AlphaIsTransparency = s.AlphaIsTransparency;
    _settings.Compression         = s.Compression;
    _settings.SpriteRects         = new List<SpriteRectDef>(s.SpriteRects);
}

private void DelayedLoadFolders()
{
    if (this == null) return; // window may have been closed before callback fires
    var s = SpriteBatchWindowState.instance;
    _settings.TargetFolders.Clear();
    foreach (var path in s.FolderPaths)
    {
        var asset = AssetDatabase.LoadAssetAtPath<DefaultAsset>(path);
        if (asset != null)
            _settings.TargetFolders.Add(asset);
    }
    RefreshTexturePaths();
    Repaint();
}
```

現有的 `LoadState()` 方法整段刪除；`SaveState()` 不需修改。

---

### 🔴 B. MaxTextureSize 超出範圍值被 IntPopup 損壞（Bug 5）

**問題（line 197）：** 若持久化檔案中的 `MaxTextureSize` 不在 `MaxTextureSizeValues` 陣列內（例如舊版 `IntField` 儲存了 `3000`），Unity 的 `IntPopup` 第一幀就回傳陣列第一個值（32），立即覆蓋設定，下次 `SaveState` 便永久損壞。

**設計：** 在 `LoadScalarSettings`（見群組 A）讀取 MaxTextureSize 時加入驗證，若不在允許值內則退為預設值 2048（已包含在群組 A 的程式碼中）：

```csharp
_settings.MaxTextureSize = System.Array.IndexOf(MaxTextureSizeValues, s.MaxTextureSize) >= 0
                           ? s.MaxTextureSize
                           : 2048;
```

---

### 🟡 C. NameSuffix 刪除後重複（Bug 3）

**問題（line 91）：** `onAddCallback` 使用 `$"_{_settings.SpriteRects.Count}"` 作為初始後綴。刪除中間元素後再新增，`Count` 可能與現有後綴數字重複，導致兩個 Rect 共用相同 `NameSuffix`，`TextureImporter` 套用時第二個覆蓋第一個。

**設計：** 新增私有方法 `GetNextSuffix()`，掃描現有後綴集合，找到最小未使用的整數索引：

```csharp
private string GetNextSuffix()
{
    var existing = new HashSet<string>(_settings.SpriteRects.Select(r => r.NameSuffix));
    for (int i = 0; ; i++)
    {
        string candidate = $"_{i}";
        if (!existing.Contains(candidate))
            return candidate;
    }
}
```

`InitRectList` 的 `onAddCallback` 改為：

```csharp
onAddCallback = _ => _settings.SpriteRects.Add(new SpriteRectDef
{
    NameSuffix = GetNextSuffix()
})
```

---

### 🟡 D. `_previewTexture` 未清除 + HashSet 順序不確定（Bug 4 & 7）

**問題 4（line 353）：** `RefreshTexturePaths` 只在 `newIndex != _previewIndex` 時清除 `_previewTexture`。當 `_previewIndex` 保持 0、`_allTexturePaths` 清空後重新填入不同的路徑時，index 仍為 0，`_previewTexture` 不被清除，導致顯示舊紋理。

**問題 7（line 340）：** `_allTexturePaths` 由 `HashSet<string>` 轉換而來，枚舉順序不確定。每次 `RefreshTexturePaths` 後預覽下拉選單的順序可能改變，造成操作困惑。

**設計：** 在 `RefreshTexturePaths` 中做兩個改動：

1. 建立 `_allTexturePaths` 後立即排序（確定性順序）：

```csharp
_allTexturePaths = new List<string>(pathSet);
_allTexturePaths.Sort(StringComparer.OrdinalIgnoreCase);
```

1. 無條件清除 `_previewTexture`（`DrawPreviewSection` 的 `_previewTexture == null` 判斷會自動重新載入）：

```csharp
int restored = preservedPath != null ? _allTexturePaths.IndexOf(preservedPath) : -1;
_previewIndex = restored >= 0 ? restored : 0;
_previewTexture = null;
Repaint();
```

---

### 🟢 E. GUIStyle 快取問題（Bug 6 & 8）

**問題 6（line 257）：** `_rectLabelStyle` 是快取的共享 `GUIStyle` 實例。`DrawPreviewSection` 的迴圈每次迭代都呼叫 `labelStyle.normal.textColor = c`，直接修改共享物件，上次的顏色在下一幀殘留。

**問題 8（lines 32-33）：** `_rectLabelStyle` 與 `_folderHintStyle` 均以 `??=` 初始化，不感知 Editor 外觀主題切換（Dark ↔ Light）。切換主題後這兩個樣式保留舊顏色，直到視窗重開。

**設計：** 兩個 field 均移除，各自改為在每次需要時建立 local 變數：

**`_rectLabelStyle`：** 在 `DrawPreviewSection` 迴圈前建立（每次 `OnGUI` 一次，不是每 rect 一次）：

```csharp
// 移除 field: private GUIStyle _rectLabelStyle;
// 在 DrawPreviewSection 的迴圈前：
var labelStyle = new GUIStyle(EditorStyles.miniLabel);
for (int i = 0; i < _settings.SpriteRects.Count; i++)
{
    // ...
    labelStyle.normal.textColor = c; // 安全：此 instance 每幀新建，不共享
    // ...
}
```

**`_folderHintStyle`：** 在 `DrawFolderSection` 的條件分支內建立（只在資料夾清單為空時執行，代價極低）：

```csharp
// 移除 field: private GUIStyle _folderHintStyle;
// 在 DrawFolderSection 條件分支內：
if (_settings.TargetFolders.Count == 0)
{
    var hintStyle = new GUIStyle(EditorStyles.centeredGreyMiniLabel)
    {
        wordWrap = false,
        normal   = { textColor = new Color(0.6f, 0.6f, 0.6f) }
    };
    // ...
    GUI.Label(hintRect, "將資料夾從 Project 窗口拖曳至此", hintStyle);
}
```

---

## 測試策略

### 單元測試

無新邏輯需要單元測試（`GetNextSuffix` 可驗證，但依賴 `_settings` 欄位，不是純函式，手動驗證即可）。

### 手動驗證清單

- [ ] 新增 3 個 Rect → 後綴 `_0`、`_1`、`_2`；刪除 `_1`，再新增 → 後綴為 `_3`（不重複）
- [ ] 開啟視窗，設好資料夾 + Rect，觸發 Domain Reload（修改任意 .cs 存檔）→ 視窗重新開啟後資料夾與 Rect 正確還原，預覽清單有圖片
- [ ] 刪除所有資料夾後再重新加入新資料夾，預覽顯示新資料夾的圖片（不顯示舊圖）
- [ ] 同一資料夾下多張圖，重複 Refresh，預覽下拉順序保持不變（字母排序）
- [ ] 切換 Editor 主題（Dark ↔ Light）後，拖曳提示文字與預覽標籤顏色正確更新
- [ ] 若持久化檔案的 MaxTextureSize 為非法值（如 3000），開啟後自動退回 2048

---

## 實作注意事項

1. `if (this == null) return;`：`EditorApplication.delayCall` callback 在 lambda 內必須加這行，因為視窗可能在 delayCall 觸發前已被關閉。
2. `EditorApplication.delayCall -= DelayedLoadFolders;`：必須在 `OnDisable` 取消訂閱，避免關閉視窗後 callback 仍觸發（雖然有 null check，但取消是更乾淨的作法）。
3. `RefreshTexturePaths` 中 `_previewTexture = null` 無條件清除是安全的，因為 `DrawPreviewSection` 的 `_previewTexture == null` guard 會觸發重新載入。
4. `GetNextSuffix` 的無窮迴圈在正常使用下不會成問題，因為清單長度有限；若日後清單可能非常大，可加上上限保護。
