# Sprite 批次設定 UI/UX 改善設計規格

## 概要

本規格涵蓋 `SpriteBatchWindow` 的 10 項 UI/UX 改善，目標是消除操作陷阱、強化視覺回饋、在 Domain Reload（編譯）後保留設定。所有改善集中在單一 PR 完成，依優先序實作：🔴 高 → 🟡 中 → 🟢 低。

**涉及檔案：**

| 動作 | 檔案 |
|------|------|
| 修改 | `Packages/com.wenrong.spritebatchslicer/Editor/SpriteBatchWindow.cs` |
| 新增 | `Packages/com.wenrong.spritebatchslicer/Editor/SpriteBatchWindowState.cs` |
| 不動 | `SpriteBatchProcessor.cs`、`SpriteBatchData.cs` |

---

## 改善項目

### 🔴 1. MaxTextureSize → IntPopup

**問題：** `EditorGUILayout.IntField` 允許輸入任意整數，使用者可輸入 300、999 等 Unity 不接受的值。

**設計：** 改用 `EditorGUILayout.IntPopup`，只提供 Unity 支援的 2 的冪次值。

```csharp
private static readonly int[] MaxTextureSizeValues  = { 32, 64, 128, 256, 512, 1024, 2048, 4096, 8192 };
private static readonly string[] MaxTextureSizeNames = { "32", "64", "128", "256", "512", "1024", "2048", "4096", "8192" };

// In DrawTextureSettingsSection:
_settings.MaxTextureSize = EditorGUILayout.IntPopup(
    "最大尺寸 (Max Size)", _settings.MaxTextureSize,
    MaxTextureSizeNames, MaxTextureSizeValues);
```

**預設值：** 2048（與現行 `SpriteBatchData.cs` 一致，無需異動）。

---

### 🔴 2. NameSuffix 自動遞增

**問題：** `onAddCallback` 使用 `new SpriteRectDef()`，每筆預設後綴都是 `_0`，新增後立即觸發「後綴重複」驗證錯誤。

**設計：** 新增時用當前清單長度作為索引產生唯一後綴。

```csharp
// In InitRectList:
onAddCallback = _ =>
    _settings.SpriteRects.Add(new SpriteRectDef
    {
        NameSuffix = $"_{_settings.SpriteRects.Count}"
    })
```

新增第 1 筆 → `_0`，第 2 筆 → `_1`，以此類推。使用者仍可手動修改後綴；此邏輯僅影響新增時的初始值。

---

### 🔴 3. 預覽圖片最大高度限制

**問題：** 縮放時只限制寬度，直式高解析度圖片（如 2048×4096）會佔滿整個視窗。

**設計：** 同時限制寬度與高度，取較小的縮放比例。

```csharp
private const float PreviewMaxHeight = 240f;

// In DrawPreviewSection (replace existing scale calculation):
float maxW = position.width - 24;
float scale = Mathf.Min(
    1f,
    maxW / _previewTexture.width,
    PreviewMaxHeight / _previewTexture.height);
float dispW = _previewTexture.width * scale;
float dispH = _previewTexture.height * scale;
```

預覽區高度上限為 240px。橫式圖片仍依寬度縮放，不受影響。

---

### 🟡 4. Alignment 變更自動同步 Pivot

**問題：** 從下拉選單選擇 `TopLeft` 後，Pivot 仍維持原值，需要手動修正，容易遺忘。

**設計：** 在 `DrawRectElement` 中偵測 Alignment 變更，自動更新 Pivot 為對應標準值。`Custom` 不觸發同步。

Alignment → Pivot 對照表（Unity 標準，Y 軸向上）：

| SpriteAlignment | Pivot |
|----------------|-------|
| `TopLeft`      | (0, 1)     |
| `TopCenter`    | (0.5, 1)   |
| `TopRight`     | (1, 1)     |
| `LeftCenter`   | (0, 0.5)   |
| `Center`       | (0.5, 0.5) |
| `RightCenter`  | (1, 0.5)   |
| `BottomLeft`   | (0, 0)     |
| `BottomCenter` | (0.5, 0)   |
| `BottomRight`  | (1, 0)     |
| `Custom`       | 不變       |

```csharp
// In DrawRectElement, after EnumPopup:
var newAlignment = (SpriteAlignment)EditorGUI.EnumPopup(new Rect(x, y, 84, h), def.Alignment);
if (newAlignment != def.Alignment)
{
    def.Alignment = newAlignment;
    def.Pivot = AlignmentToPivot(newAlignment, def.Pivot);
}

// Helper method:
private static Vector2 AlignmentToPivot(SpriteAlignment alignment, Vector2 current) =>
    alignment switch
    {
        SpriteAlignment.TopLeft      => new Vector2(0f,   1f),
        SpriteAlignment.TopCenter    => new Vector2(0.5f, 1f),
        SpriteAlignment.TopRight     => new Vector2(1f,   1f),
        SpriteAlignment.LeftCenter   => new Vector2(0f,   0.5f),
        SpriteAlignment.Center       => new Vector2(0.5f, 0.5f),
        SpriteAlignment.RightCenter  => new Vector2(1f,   0.5f),
        SpriteAlignment.BottomLeft   => new Vector2(0f,   0f),
        SpriteAlignment.BottomCenter => new Vector2(0.5f, 0f),
        SpriteAlignment.BottomRight  => new Vector2(1f,   0f),
        _                            => current,
    };
```

---

### 🟡 5. 切割框各自不同顏色 + 後綴標籤

**問題：** 所有切割框一律黃色，多個 Rect 時無法快速對應到清單中的哪一筆。

**設計：** 使用 4 色調色盤（Material 柔和色），循環分配；每框左上角顯示 `NameSuffix` 標籤。

```csharp
// Class-level constant in SpriteBatchWindow:
private static readonly Color[] RectOverlayColors =
{
    new Color(0.298f, 0.686f, 0.314f), // Material Green 500
    new Color(0.129f, 0.588f, 0.953f), // Material Blue 500
    new Color(1.000f, 0.596f, 0.000f), // Material Orange 700
    new Color(0.957f, 0.263f, 0.212f), // Material Red 500
};
```

在 `DrawPreviewSection` 的切割框繪製迴圈中（`labelStyle` 在迴圈外建立一次，避免每幀 GC）：

```csharp
var labelStyle = new GUIStyle(EditorStyles.miniLabel);
for (int i = 0; i < _settings.SpriteRects.Count; i++)
{
    var def = _settings.SpriteRects[i];
    Color c = RectOverlayColors[i % RectOverlayColors.Length];

    float rx = previewRect.x + def.Rect.x * scale;
    float ry = previewRect.y + (_previewTexture.height - def.Rect.y - def.Rect.height) * scale;
    float rw = def.Rect.width * scale;
    float rh = def.Rect.height * scale;

    var overlay = new Rect(rx, ry, rw, rh);
    EditorGUI.DrawRect(overlay, new Color(c.r, c.g, c.b, 0.15f));
    // 4 borders
    EditorGUI.DrawRect(new Rect(rx,          ry,          rw,  1), c);
    EditorGUI.DrawRect(new Rect(rx,          ry + rh - 1, rw,  1), c);
    EditorGUI.DrawRect(new Rect(rx,          ry,          1,  rh), c);
    EditorGUI.DrawRect(new Rect(rx + rw - 1, ry,          1,  rh), c);
    // suffix label — mutate labelStyle.normal.textColor per iteration; style created once outside loop
    labelStyle.normal.textColor = c;
    GUI.Label(new Rect(rx + 2, ry + 1, rw - 4, 14), def.NameSuffix, labelStyle);
}
```

---

### 🟡 6. ReorderableList 標題欄向右偏移

**問題：** `drawHeaderCallback` 的 `rect` 起點是整個清單的左邊界，但 `drawElementCallback` 的 `rect` 起點已排除拖曳手柄寬度（~20px），導致標題文字向左錯位約 20px。

**設計：** 標題向右偏移 20px 對齊欄位。

```csharp
// In InitRectList:
drawHeaderCallback = rect =>
    EditorGUI.LabelField(
        new Rect(rect.x + 20, rect.y, rect.width - 20, rect.height),
        "Sprite 切割 (後綴 | X | Y | W | H | Pivot X | Pivot Y | 對齊)")
```

---

### 🟡 7. 預覽下拉選單顯示相對路徑

**問題：** 多資料夾情境下，若兩個資料夾各有 `hero.png`，下拉選單出現兩個相同的「hero」，無法分辨。

**設計：** 顯示名稱改為去除 `Assets/` 前綴與副檔名的相對路徑。

```csharp
// In RefreshTexturePaths, replace existing _previewNames assignment:
_previewNames = _allTexturePaths
    .Select(p =>
    {
        string ext = System.IO.Path.GetExtension(p);
        return p.StartsWith("Assets/")
            ? p["Assets/".Length..^ext.Length]
            : System.IO.Path.GetFileNameWithoutExtension(p);
    })
    .ToArray();
```

範例：`Assets/Characters/hero.png` → `Characters/hero`；`Assets/Enemies/hero.png` → `Enemies/hero`。

---

### 🟡 8. 新增/移除資料夾後保持預覽選擇

**問題：** `RefreshTexturePaths` 末尾有 `_previewIndex = 0`，每次清單變更都會跳回第一張圖片，干擾預覽中的圖片。

**設計：** 重建路徑清單後嘗試恢復原來選取的路徑；只有在原路徑不再存在時才重置為 0。

```csharp
private void RefreshTexturePaths()
{
    string preservedPath = _previewIndex < _allTexturePaths.Count
        ? _allTexturePaths[_previewIndex]
        : null;

    var pathSet = new HashSet<string>();
    foreach (var folder in _settings.TargetFolders)
    {
        if (folder == null) continue;
        string folderPath = AssetDatabase.GetAssetPath(folder);
        if (string.IsNullOrEmpty(folderPath)) continue;
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { folderPath }))
            pathSet.Add(AssetDatabase.GUIDToAssetPath(guid));
    }

    _allTexturePaths = new List<string>(pathSet);
    _previewNames = _allTexturePaths
        .Select(p =>
        {
            string ext = System.IO.Path.GetExtension(p);
            return p.StartsWith("Assets/")
                ? p["Assets/".Length..^ext.Length]
                : System.IO.Path.GetFileNameWithoutExtension(p);
        })
        .ToArray();

    int restored = preservedPath != null ? _allTexturePaths.IndexOf(preservedPath) : -1;
    _previewIndex = restored >= 0 ? restored : 0;
    _previewTexture = null;
    Repaint();
}
```

---

### 🟢 9. Domain Reload 持久化（ScriptableSingleton）

**問題：** 編譯（Domain Reload）觸發 `OnDisable` 後，`SpriteBatchWindow` 的所有欄位重置為預設值，資料夾清單與切割設定全部遺失。

**設計：** 新增 `SpriteBatchWindowState` 作為 `ScriptableSingleton`，儲存在 `Library/` 資料夾（不進 git）。`OnEnable` 載入，`OnDisable` 儲存。

#### 新增檔案：`SpriteBatchWindowState.cs`

```csharp
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SpriteBatch
{
    [FilePath("SpriteBatchWindowState.asset", FilePathAttribute.Location.PreferencesFolder)]
    public class SpriteBatchWindowState : ScriptableSingleton<SpriteBatchWindowState>
    {
        public List<string> FolderPaths = new();
        public List<SpriteRectDef> SpriteRects = new();
        public int MaxTextureSize = 2048;
        public FilterMode FilterMode = FilterMode.Bilinear;
        public bool AlphaIsTransparency = true;
        public TextureImporterCompression Compression = TextureImporterCompression.Compressed;

        public void Save() => Save(true);
    }
}
```

資料夾以 `AssetDatabase` 路徑字串儲存（`DefaultAsset` 不可跨 Preferences 序列化），`OnEnable` 時透過 `AssetDatabase.LoadAssetAtPath<DefaultAsset>` 還原。

#### `SpriteBatchWindow` 修改

```csharp
private void OnEnable()
{
    LoadState();
    InitFolderList();
    InitRectList();
}

private void OnDisable()
{
    SaveState();
}

private void LoadState()
{
    var s = SpriteBatchWindowState.instance;
    _settings.MaxTextureSize    = s.MaxTextureSize;
    _settings.FilterMode        = s.FilterMode;
    _settings.AlphaIsTransparency = s.AlphaIsTransparency;
    _settings.Compression       = s.Compression;
    _settings.SpriteRects       = new List<SpriteRectDef>(s.SpriteRects);
    _settings.TargetFolders.Clear();
    foreach (var path in s.FolderPaths)
    {
        var asset = AssetDatabase.LoadAssetAtPath<DefaultAsset>(path);
        if (asset != null) _settings.TargetFolders.Add(asset);
    }
}

private void SaveState()
{
    var s = SpriteBatchWindowState.instance;
    s.MaxTextureSize      = _settings.MaxTextureSize;
    s.FilterMode          = _settings.FilterMode;
    s.AlphaIsTransparency = _settings.AlphaIsTransparency;
    s.Compression         = _settings.Compression;
    s.SpriteRects         = new List<SpriteRectDef>(_settings.SpriteRects);
    s.FolderPaths.Clear();
    foreach (var folder in _settings.TargetFolders)
    {
        if (folder != null) s.FolderPaths.Add(AssetDatabase.GetAssetPath(folder));
    }
    s.Save();
}
```

---

### 🟢 10. 空資料夾清單拖曳提示

**問題：** 清單為空時，新使用者不知道可以直接拖曳資料夾，只會嘗試點「+」按鈕（再手動選取）。

**設計：** 當 `TargetFolders` 為空時，在清單主體區域繪製虛線框提示。

```csharp
// In DrawFolderSection, after _folderList.DoList(listRect):
if (_settings.TargetFolders.Count == 0)
{
    // listRect.y + 21 skips the header row
    var hintRect = new Rect(listRect.x + 4, listRect.y + 21, listRect.width - 8, 36);
    EditorGUI.DrawRect(hintRect, new Color(1f, 1f, 1f, 0.03f));
    var style = new GUIStyle(EditorStyles.centeredGreyMiniLabel)
    {
        wordWrap = false,
        normal = { textColor = new Color(0.6f, 0.6f, 0.6f) }
    };
    GUI.Label(hintRect, "將資料夾從 Project 窗口拖曳至此", style);
}
```

提示文字顯示在空清單區域中央，不影響現有的 `HandleFolderDrop` 拖放邏輯。

---

## 測試策略

### 單元測試（NUnit EditMode）

在 `SpriteBatchProcessorTests.cs` 或新增 `SpriteBatchWindowTests.cs` 中加入：

| 測試 | 驗證 |
|------|------|
| `AutoIncrement_新增兩筆_後綴為_0和_1` | `onAddCallback` 呼叫兩次後，後綴分別為 `_0`、`_1` |
| `AlignmentToPivot_TopLeft_回傳_0_1` | `AlignmentToPivot(TopLeft, _)` == `(0, 1)` |
| `AlignmentToPivot_Custom_不改變Pivot` | `AlignmentToPivot(Custom, Vector2(0.3, 0.7))` == `(0.3, 0.7)` |
| `FilterNewFolders_已存在的資料夾_不重複加入` | 現有測試已覆蓋，確保未破壞 |

### 手動測試清單

- [ ] MaxTextureSize 下拉選單顯示 32~8192，且只能選這些值
- [ ] 連續新增 3 個 Rect，後綴分別為 `_0`、`_1`、`_2`
- [ ] 縮放後高度超過 240px 的直式圖片（如 512×2048）預覽高度不超過 240px
- [ ] 切換 Alignment 為 `TopLeft` 後 Pivot 自動變為 (0, 1)
- [ ] 切換 Alignment 為 `Custom` 後 Pivot 不變
- [ ] 4 個以上的 Rect 在預覽中顯示循環配色（第 5 個回到綠色）
- [ ] 每個切割框左上角有對應的後綴標籤
- [ ] 兩個資料夾各有 `hero.png`，下拉選單顯示 `FolderA/hero` 和 `FolderB/hero`
- [ ] 已選取預覽圖片，新增第二個資料夾後，預覽仍停留在原圖片
- [ ] 按下 `Ctrl+R`（重新編譯）後重新開啟視窗，設定（資料夾清單、Rect 清單）已還原
- [ ] 空清單狀態下可見拖曳提示文字，拖入資料夾後提示消失

---

## 實作注意事項

1. `AlignmentToPivot` 為純函式，放在 `SpriteBatchWindow` 內部即可，無需抽到 `SpriteBatchProcessor`。
2. `SpriteBatchWindowState` 使用 `PreferencesFolder` 而非 `ProjectFolder`，確保不進 git。
3. `DrawPreviewSection` 的 `labelStyle` 在迴圈外建立一次（見 §5 程式碼）。`DrawFolderSection` 的 hint `GUIStyle` 只在 `TargetFolders` 為空時建立，可接受。
4. 項目 8（保持預覽選擇）與項目 7（相對路徑名稱）都在 `RefreshTexturePaths` 中修改，須在同一個方法內合併實作。
