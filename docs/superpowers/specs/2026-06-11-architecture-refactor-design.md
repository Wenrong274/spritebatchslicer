# SpriteBatchWindow 架構重構設計規格（I1 / I2 / I3）

## 概要

解決三項架構問題：

| 項目 | 問題 | 類別 |
|------|------|------|
| I1 | `FilterNewFolders`、`AlignmentToPivot` 放在 `SpriteBatchWindow`，測試需要依賴 EditorWindow | SRP |
| I2 | `BatchSettings.TargetFolders` 持有 `List<DefaultAsset>`（UnityEditor 型別），資料模型依賴 Editor pipeline | OCP / 分層 |
| I3 | 資料夾→紋理路徑掃描邏輯在 `SpriteBatchWindow.RefreshTexturePaths` 與 `SpriteBatchProcessor.ApplyToFolders` 中各寫一份 | DRY |

三個項目相互依存，建議一次重構：I2 修改後 I3 的提取才最乾淨（Processor 不再需要自行轉換 DefaultAsset → path）。

**涉及檔案：**

| 動作 | 檔案 |
|------|------|
| 新增 | `Packages/com.wenrong.spritebatchslicer/Editor/SpriteBatchEditorUtils.cs` |
| 修改 | `Packages/com.wenrong.spritebatchslicer/Editor/SpriteBatchData.cs` |
| 修改 | `Packages/com.wenrong.spritebatchslicer/Editor/SpriteBatchProcessor.cs` |
| 修改 | `Packages/com.wenrong.spritebatchslicer/Editor/SpriteBatchWindow.cs` |
| 修改 | `Packages/com.wenrong.spritebatchslicer/Tests/Editor/SpriteBatchWindowTests.cs` |

---

## I1：新增 `SpriteBatchEditorUtils`，搬移靜態方法

**問題：** `FilterNewFolders` 和 `AlignmentToPivot` 是無狀態純函式，但住在 `SpriteBatchWindow` 裡，導致測試套件需要引用 `EditorWindow` 子類別，降低可測試性。

**設計：** 新增 `SpriteBatchEditorUtils.cs`，將兩個方法以相同簽名搬入。

### 新增檔案：`SpriteBatchEditorUtils.cs`

```csharp
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SpriteBatch
{
    public static class SpriteBatchEditorUtils
    {
        public static List<DefaultAsset> FilterNewFolders(
            List<DefaultAsset> existing, IEnumerable<Object> dragged)
        {
            var result = new List<DefaultAsset>();
            foreach (var obj in dragged)
            {
                if (obj is not DefaultAsset asset)
                {
                    continue;
                }
                if (!AssetDatabase.IsValidFolder(AssetDatabase.GetAssetPath(obj)))
                {
                    continue;
                }
                if (existing.Contains(asset) || result.Contains(asset))
                {
                    continue;
                }
                result.Add(asset);
            }
            return result;
        }

        public static Vector2 AlignmentToPivot(SpriteAlignment alignment, Vector2 current) =>
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
    }
}
```

### `SpriteBatchWindow.cs` 修改

刪除 `FilterNewFolders` 與 `AlignmentToPivot` 方法全段。

兩個呼叫點更新：

```csharp
// DrawRectElement 中：
def.Pivot = SpriteBatchEditorUtils.AlignmentToPivot(newAlignment, def.Pivot);

// HandleFolderDrop 中：
var newFolders = SpriteBatchEditorUtils.FilterNewFolders(_folderAssets, DragAndDrop.objectReferences);
```

（`_folderAssets` 是 I2 引入的新欄位，見下節。）

### `SpriteBatchWindowTests.cs` 修改

將所有 `SpriteBatchWindow.AlignmentToPivot(...)` 改為 `SpriteBatchEditorUtils.AlignmentToPivot(...)`；  
將所有 `SpriteBatchWindow.FilterNewFolders(...)` 改為 `SpriteBatchEditorUtils.FilterNewFolders(...)`。

---

## I2：`BatchSettings.TargetFolders` → `FolderPaths: List<string>`

**問題：** `BatchSettings` 持有 `List<DefaultAsset>`，使 `SpriteBatchData.cs` 需要 `using UnityEditor`，讓資料模型與 Editor asset pipeline 耦合。`ApplyToFolders` 也因此要在 Processor 內部呼叫 `AssetDatabase.GetAssetPath`，應屬 UI 層的職責。

**設計：**

- `BatchSettings` 將 `List<DefaultAsset> TargetFolders` 改為 `List<string> FolderPaths`，移除 `using UnityEditor`。
- `SpriteBatchWindow` 新增 `private List<DefaultAsset> _folderAssets = new()` 作為 UI 層的資料夾資產清單。
- 所有需要路徑的操作，由 `RefreshTexturePaths`（每次資料夾變更後都會呼叫）負責將 `_folderAssets` 同步至 `_settings.FolderPaths`。

### `SpriteBatchData.cs` 修改

```csharp
// 移除 using UnityEditor;

[Serializable]
public class BatchSettings
{
    public List<string> FolderPaths = new();   // 取代 List<DefaultAsset> TargetFolders
    public int MaxTextureSize = 2048;
    public FilterMode FilterMode = FilterMode.Bilinear;
    public bool AlphaIsTransparency = true;
    public TextureImporterCompression Compression = TextureImporterCompression.Compressed;
    public List<SpriteRectDef> SpriteRects = new();
}
```

### `SpriteBatchWindow.cs` — 新增欄位、更新所有 `_settings.TargetFolders` 呼叫點

新增欄位（原 `_scrollPos` 下方）：

```csharp
private List<DefaultAsset> _folderAssets = new();
```

`InitFolderList` 綁定改為 `_folderAssets`：

```csharp
_folderList = new ReorderableList(
    _folderAssets, typeof(DefaultAsset), true, true, true, true)
{
    // ...
    drawElementCallback = (rect, index, isActive, isFocused) =>
    {
        // ...
        _folderAssets[index] = (DefaultAsset)EditorGUI.ObjectField(r, _folderAssets[index], typeof(DefaultAsset), false);
        if (EditorGUI.EndChangeCheck()) RefreshTexturePaths();
    },
    onAddCallback    = _ => { _folderAssets.Add(null); RefreshTexturePaths(); },
    onRemoveCallback = list => { _folderAssets.RemoveAt(list.index); RefreshTexturePaths(); }
};
```

`DrawFolderSection` 空清單判斷：

```csharp
if (_folderAssets.Count == 0) { /* hint */ }
```

`HandleFolderDrop`：

```csharp
var newFolders = SpriteBatchEditorUtils.FilterNewFolders(_folderAssets, DragAndDrop.objectReferences);
if (newFolders.Count > 0)
{
    _folderAssets.AddRange(newFolders);
    RefreshTexturePaths();
}
```

`DrawApplySection`：

```csharp
int folderCount = _settings.FolderPaths.Count;  // FolderPaths 由 RefreshTexturePaths 維護，始終同步
```

`DelayedLoadFolders`：

```csharp
private void DelayedLoadFolders()
{
    if (this == null) return;
    var s = SpriteBatchWindowState.instance;
    _folderAssets.Clear();
    foreach (var path in s.FolderPaths)
    {
        var asset = AssetDatabase.LoadAssetAtPath<DefaultAsset>(path);
        if (asset != null)
            _folderAssets.Add(asset);
    }
    RefreshTexturePaths();
    Repaint();
}
```

`SaveState`：

```csharp
s.FolderPaths.Clear();
foreach (var folder in _folderAssets)
{
    if (folder != null)
        s.FolderPaths.Add(AssetDatabase.GetAssetPath(folder));
}
```

`RefreshTexturePaths` 開頭加入同步段（I3 詳述其餘部分）：

```csharp
private void RefreshTexturePaths()
{
    string preservedPath = _previewIndex < _allTexturePaths.Count
        ? _allTexturePaths[_previewIndex]
        : null;

    // 同步 UI 資產 → 路徑清單
    _settings.FolderPaths.Clear();
    foreach (var folder in _folderAssets)
    {
        if (folder == null) continue;
        string p = AssetDatabase.GetAssetPath(folder);
        if (!string.IsNullOrEmpty(p))
            _settings.FolderPaths.Add(p);
    }

    _allTexturePaths = SpriteBatchProcessor.CollectTexturePaths(_settings.FolderPaths);
    // ... （下方 _previewNames 計算與 index 還原不變）
}
```

`ApplyAll` 簡化（不再需要自行轉換）：

```csharp
var errors = SpriteBatchProcessor.ValidatePreflight(_settings.FolderPaths, _settings.SpriteRects);
// ...
result = SpriteBatchProcessor.ApplyToFolders(_settings, ...);
// 原本的 folderPaths 區域變數整段刪除
```

### `SpriteBatchProcessor.cs` — `ApplyToFolders` 修改

`ApplyToFolders` 內部的路徑掃描段（見 I3）由 `CollectTexturePaths` 取代後，原本的 `foreach (var folder in settings.TargetFolders)` 整段改為：

```csharp
var allPaths = CollectTexturePaths(settings.FolderPaths);
```

---

## I3：提取 `SpriteBatchProcessor.CollectTexturePaths`

**問題：** Window 的 `RefreshTexturePaths` 和 Processor 的 `ApplyToFolders` 各自實作了相同的「資料夾路徑 → Texture2D GUID → 絕對路徑（排序）」流程。

**設計：** 在 `SpriteBatchProcessor` 新增公開靜態方法 `CollectTexturePaths`，兩處統一使用。

```csharp
// 加入 SpriteBatchProcessor.cs
public static List<string> CollectTexturePaths(IEnumerable<string> folderPaths)
{
    var pathSet = new HashSet<string>();
    foreach (var folderPath in folderPaths)
    {
        if (string.IsNullOrEmpty(folderPath))
        {
            continue;
        }
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { folderPath }))
        {
            _ = pathSet.Add(AssetDatabase.GUIDToAssetPath(guid));
        }
    }
    var result = new List<string>(pathSet);
    result.Sort(System.StringComparer.OrdinalIgnoreCase);
    return result;
}
```

`ApplyToFolders` 中原有的 `var pathSet = new HashSet<string>()` 掃描段整段刪除，改為：

```csharp
var allPaths = CollectTexturePaths(settings.FolderPaths);
```

`RefreshTexturePaths` 中原有的掃描段整段刪除，改為（已在 I2 段落中展示）：

```csharp
_allTexturePaths = SpriteBatchProcessor.CollectTexturePaths(_settings.FolderPaths);
```

`_allTexturePaths.Sort(...)` 那一行同時刪除（排序已移入 `CollectTexturePaths`）。

---

## 測試策略

### 新增單元測試（在 `SpriteBatchProcessorTests.cs` 加入）

| 測試 | 驗證 |
|------|------|
| `CollectTexturePaths_空清單_回傳空清單` | `CollectTexturePaths([])` 回傳 `Count == 0` |
| `CollectTexturePaths_結果依字母排序` | 若有兩個路徑，回傳清單的第一個 `<` 第二個（IgnoreCase） |

`AlignmentToPivot` 與 `FilterNewFolders` 測試不變，只需更新呼叫前置為 `SpriteBatchEditorUtils`。

### 手動驗證清單

- [ ] 重構後工具仍可正常 Apply（選資料夾、設定 Rect、按套用全部）
- [ ] Domain Reload 後資料夾清單正確還原
- [ ] 拖曳資料夾仍可加入清單，重複資料夾被過濾
- [ ] 全部 NUnit 測試通過

---

## 實作注意事項

1. I2 中 `_settings.FolderPaths` 與 `SpriteBatchWindowState.FolderPaths` 同名但不同物件；`SaveState` 的邏輯不變（從 `_folderAssets` 轉換後寫入 `SpriteBatchWindowState`）。
2. `CollectTexturePaths` 傳入的是已驗證的資料夾路徑字串（從 `AssetDatabase.GetAssetPath` 取得），不需要在方法內部再次呼叫 `IsValidFolder`；空字串保護已由呼叫端（`RefreshTexturePaths` 的同步段）處理，`CollectTexturePaths` 也做一次簡易保護（`IsNullOrEmpty`）。
3. `SpriteBatchData.cs` 移除 `using UnityEditor` 後，需確保同一命名空間的其他類別（`SpriteRectDef`、`BatchSettings`）仍只需 `using UnityEngine` 與 `using System`。
4. `SpriteBatchEditorUtils` 持有 `using UnityEditor`（因 `FilterNewFolders` 需要 `AssetDatabase`），不影響 `SpriteBatchData.cs` 的清潔性。
