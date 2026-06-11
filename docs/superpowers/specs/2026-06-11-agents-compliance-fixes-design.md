# AGENTS.md 合規修正設計規格

## 概要

本規格修正 code review 發現的合規缺口，使目前 Sprite Batch Slicer 功能更符合 `AGENTS.md` 中的開發規範：

- TDD：新增或改動的可測試行為必須先有 EditMode 測試。
- SOLID / 分層：UI、資料模型、AssetDatabase 存取、TextureImporter 套用邏輯保持清楚邊界。
- Functional Programming：可獨立測試的邏輯偏向純函式與明確輸入輸出。
- Modern C# / `.editorconfig`：遵守 Unity 2022 可用語法與現有格式規則。

**不納入範圍：**

- 不處理目前工作樹中的 `Assets/Sprites/**/*.png.meta` 變更。
- 不重寫整個 EditorWindow UI。
- 不新增 Runtime assembly；目前 package 仍維持 Editor-only。
- 不改變使用者可見的功能流程與選單位置。

**涉及檔案：**

| 動作 | 檔案 |
|------|------|
| 修改 | `Packages/com.wenrong.spritebatchslicer/Editor/SpriteBatchProcessor.cs` |
| 修改 | `Packages/com.wenrong.spritebatchslicer/Editor/SpriteBatchWindow.cs` |
| 修改 | `Packages/com.wenrong.spritebatchslicer/Editor/SpriteBatchData.cs` |
| 修改 | `Packages/com.wenrong.spritebatchslicer/Editor/SpriteBatchEditorUtils.cs` |
| 修改 | `Packages/com.wenrong.spritebatchslicer/Tests/Editor/SpriteBatchProcessorTests.cs` |
| 修改 | `Packages/com.wenrong.spritebatchslicer/Tests/Editor/SpriteBatchWindowTests.cs` |
| 新增 | `Packages/com.wenrong.spritebatchslicer/Tests/Editor/TestAssetFactory.cs` |

---

## 修正群組

### 🔴 A. 補足 `ApplyToFolders` EditMode 行為測試

**問題：** `SpriteBatchProcessor.ApplyToFolders` 是核心批次功能，負責掃描貼圖、驗證切割範圍、套用 `TextureImporter` 設定、保留 sprite GUID、處理 skip/fail/cancel 結果，但目前沒有直接測試。

**設計：** 以 TDD 補上 EditMode 測試。測試必須先失敗，再實作必要調整。

新增測試集中在 `SpriteBatchProcessorTests.cs`，只使用測試專用暫存資料夾，例如：

```text
Assets/Temp/SpriteBatchSlicerTests/
```

每個測試建立自己的 PNG asset，測試完成後清理測試資料夾。不得改動 `Assets/Sprites` 既有素材。

### 測試項目

| 測試 | 驗證 |
|------|------|
| `ApplyToFolders_有效設定_套用TextureImporter與SpriteRects` | 成功數為 1，貼圖變為 Sprite Multiple，max size、filter、compression、alpha、sprite rect 名稱正確 |
| `ApplyToFolders_Rect超出圖片_跳過且不計成功` | `SkippedPaths` 包含該貼圖，`SuccessCount == 0` |
| `ApplyToFolders_取消後_停止後續處理` | `WasCancelled == true`，取消點之後的貼圖不被處理 |
| `ApplyToFolders_既有同名Sprite_保留SpriteGUID` | 同名 sprite 的 `spriteID` 在重套用後不變 |

### 測試輔助設計

新增 `TestAssetFactory`，集中處理測試 PNG 建立與清理：

```csharp
internal static class TestAssetFactory
{
    public static string CreatePng(string assetPath, int width, int height, Color color)
    {
        // 建立 Texture2D、EncodeToPNG、寫入 Assets 底下、AssetDatabase.ImportAsset。
    }

    public static void DeleteFolder(string assetFolder)
    {
        // 使用 AssetDatabase.DeleteAsset 清除測試資產。
    }
}
```

測試工具只放在 `Tests/Editor`，不得進 production assembly。

---

### 🔴 B. 將 progress bar UI side effect 移出 `SpriteBatchProcessor`

**問題：** `SpriteBatchProcessor.ApplyToFolders` 目前在 `finally` 中呼叫 `EditorUtility.DisplayProgressBar`，但 `EditorUtility.ClearProgressBar` 由 `SpriteBatchWindow.ApplyAll` 負責。這讓 processor 同時承擔批次處理與 UI 顯示責任，也使直接呼叫 processor 的測試或其他工具可能留下 progress bar。

**設計：**

- `SpriteBatchProcessor.ApplyToFolders` 不直接呼叫 `EditorUtility.DisplayProgressBar` 或 `EditorUtility.ClearProgressBar`。
- `ApplyToFolders` 只透過既有 `onProgress` callback 回報進度。
- `SpriteBatchWindow.ApplyAll` 保留所有 progress bar UI 控制。

### API 行為

`ApplyToFolders` 簽名不變：

```csharp
public static ApplyResult ApplyToFolders(
    BatchSettings settings,
    Action<float, string> onProgress = null,
    Func<bool> isCancelled = null)
```

`finally` 只保證 `AssetDatabase.StopAssetEditing()`：

```csharp
finally
{
    AssetDatabase.StopAssetEditing();
}
```

`SpriteBatchWindow.ApplyAll` 負責在 callback 中顯示進度，並在自己的 `finally` 清除：

```csharp
try
{
    result = SpriteBatchProcessor.ApplyToFolders(
        _settings,
        (progress, path) =>
        {
            cancelled = EditorUtility.DisplayCancelableProgressBar(
                "Sprite 批次設定", Path.GetFileName(path), progress);
        },
        () => cancelled);
}
finally
{
    EditorUtility.ClearProgressBar();
}
```

---

### 🟡 C. 降低 `BatchSettings` 對 UnityEditor importer enum 的耦合

**問題：** `BatchSettings` 已改用 `List<string> FolderPaths`，但仍直接使用 `TextureImporterCompression`，因此 `SpriteBatchData.cs` 仍需要 `using UnityEditor`。這與 `AGENTS.md` 中資料模型與 TextureImporter 套用邏輯要分層的方向不完全一致。

**設計：** 採用低風險分階段去耦合，不新增 Runtime assembly。

新增 package 內部 enum：

```csharp
public enum BatchTextureCompression
{
    Uncompressed,
    Compressed,
    CompressedHQ,
    CompressedLQ,
}
```

`BatchSettings` 改為：

```csharp
public BatchTextureCompression Compression = BatchTextureCompression.Compressed;
```

新增轉換 helper，放在 Editor assembly 中：

```csharp
public static class SpriteBatchImporterOptions
{
    public static TextureImporterCompression ToUnityCompression(BatchTextureCompression compression) =>
        compression switch
        {
            BatchTextureCompression.Uncompressed => TextureImporterCompression.Uncompressed,
            BatchTextureCompression.Compressed   => TextureImporterCompression.Compressed,
            BatchTextureCompression.CompressedHQ => TextureImporterCompression.CompressedHQ,
            BatchTextureCompression.CompressedLQ => TextureImporterCompression.CompressedLQ,
            _                                    => TextureImporterCompression.Compressed,
        };
}
```

`SpriteBatchWindow.DrawTextureSettingsSection` 顯示自訂 enum：

```csharp
_settings.Compression = (BatchTextureCompression)EditorGUILayout.EnumPopup(
    "壓縮 (Compression)", _settings.Compression);
```

`SpriteBatchProcessor.ApplyToFolders` 在套用 importer 時轉換：

```csharp
importer.textureCompression = SpriteBatchImporterOptions.ToUnityCompression(settings.Compression);
```

### 相容性

目前套件內沒有 `SpriteBatchWindowState` 或其他持久化 state 類別，因此本 spec 不包含 state migration。若後續新增持久化 state，compression 欄位必須使用 `BatchTextureCompression`，不能重新引入 `TextureImporterCompression` 到資料模型。

---

### 🟡 D. 修正 `.editorconfig` 明顯違規

**問題：** 目前有幾處和 `.editorconfig` 不一致：

- `System.*` using 未排在其他 using 前。
- 單行 block 不符合 Allman braces 與可讀性要求。
- 少數初始化或 object initializer 排版和現有規則不一致。

**設計：**

只修正觸及檔案與明顯違規，不做全專案格式化。

### 明確修正項

1. `SpriteBatchProcessor.cs`
   - `public struct ValidationError { public string Message; }` 展開為多行。
   - `using System.*` 保持在 `Unity*` using 前。

1. `SpriteBatchWindow.cs`
   - `private void DrawRectSection() { _rectList.DoLayoutList(); }` 展開為多行。
   - 保持 Allman braces。

1. `SpriteBatchProcessorTests.cs` / `SpriteBatchWindowTests.cs`
   - using 排序改為 `System.*` → `NUnit.Framework` → `Unity*`。

1. 新增檔案
   - namespace 使用 block-scoped，不使用 file-scoped namespace。
   - 不使用 C# 10+ / C# 12 語法。

---

### 🟢 E. 補強 `CollectTexturePaths` 與 utility 測試

**問題：** `CollectTexturePaths` 目前只測空清單與不存在路徑，沒有測「去重」與「排序」。`FilterNewFolders` 測試已有基本覆蓋，但可以補空集合輸入行為，使 utility 更接近純函式。

**設計：**

新增或補強下列測試：

| 測試 | 驗證 |
|------|------|
| `CollectTexturePaths_多資料夾重複結果_去重且排序` | 回傳路徑不重複，排序使用 `OrdinalIgnoreCase` |
| `FilterNewFolders_空拖曳集合_回傳空清單` | 不丟例外，回傳空清單 |

`FilterNewFolders` 不需要支援 `null` 參數；呼叫端維持傳入非 null 集合。

---

## TDD 流程要求

此修正必須依下列順序實作：

1. 先新增 `ApplyToFolders` 行為測試，執行 EditMode 測試確認失敗。
2. 實作最小修改讓測試通過。
3. 移除 processor progress bar side effect，補或調整測試確認 processor 不需 UI callback 也能安全完成。
4. 進行 `BatchSettings` compression 去耦合，先新增轉換 helper 測試，再改 production code。
5. 做 `.editorconfig` 格式修正。
6. 執行全部 EditMode 測試。

不得先改 production code 再補測試。

---

## 驗證策略

### 自動化驗證

- Unity Test Runner → EditMode → Run All。
- CI 使用 `.github/workflows/ci.yml` 的 GameCI EditMode 測試。

### 手動驗證

- [ ] 開啟 `Tools > Sprite 批次設定`。
- [ ] 新增測試資料夾與切割 Rect，按 `套用全部 (Apply All)`。
- [ ] 確認成功貼圖變為 Sprite Multiple。
- [ ] 確認 rect 超出圖片尺寸時該貼圖被跳過，對話框列出問題檔案。
- [ ] 取消 progress bar 後，結果顯示已取消且後續貼圖不再處理。
- [ ] 確認 progress bar 不會殘留在 Editor。
- [ ] 確認 `Assets/Sprites` 既有素材未因測試或手動驗證被修改。

---

## 風險與注意事項

1. `ApplyToFolders` 測試會建立真實 Unity assets，必須使用獨立暫存資料夾並在 teardown 清理。
2. `AssetDatabase.StartAssetEditing()` 若測試中發生例外，仍必須由 production code 的 `finally` 呼叫 `StopAssetEditing()`。
3. 測試 PNG 寫入後必須呼叫 `AssetDatabase.ImportAsset` 或 `AssetDatabase.Refresh`，否則 importer 可能尚未建立。
4. Compression enum 去耦合目前不需要 state migration；若後續新增 Editor window state，state 欄位必須使用 `BatchTextureCompression`。
5. 不處理 `Assets/Sprites` 現有 `.meta` 變更；實作前後都只應檢查它們是否被額外改動，不應在此修正中還原或提交。

---

## 完成條件

- `ApplyToFolders` 核心行為有 EditMode 測試覆蓋。
- `SpriteBatchProcessor` 不再直接控制 progress bar UI。
- `BatchSettings` 不再直接依賴 `TextureImporterCompression`。
- 觸及檔案符合 `.editorconfig` 的明顯格式規則。
- 全部 EditMode 測試通過。
- `Assets/Sprites` 既有測試素材未被此修正新增改動。
