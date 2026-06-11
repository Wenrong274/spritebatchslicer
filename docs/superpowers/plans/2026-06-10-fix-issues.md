# SpriteBatchSlicer 問題修正 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 修正 SpriteBatchSlicer 工具中四個已知問題：Sprite GUID 遺失（每次套用斷掉參考）、SpriteAlignment 無 UI 欄位、批次處理效能差、以及雜項規格偏離（MenuItem 路徑、AcceptDrag 時機、雙重 RefreshTexturePaths）。

**Architecture:** 修正集中在兩個檔案。`SpriteBatchProcessor.cs` 負責邏輯層：`BuildSpriteRects` 新增 `existingIds` 參數保留既有 GUID，`ApplyToFolders` 提取既有 GUID 並包覆 `StartAssetEditing`。`SpriteBatchWindow.cs` 負責 UI 層：`DrawRectElement` 加入 `SpriteAlignment` 欄位，同時修正 MenuItem 路徑、AcceptDrag 時機、重複 Refresh。

**Tech Stack:** Unity Editor C#、TextureImporter、`ISpriteEditorDataProvider`（`UnityEditor.U2D.Sprites`）、ReorderableList、NUnit EditMode Tests

---

## 受影響的檔案

| 路徑 | 變更類型 |
|---|---|
| `Packages/com.wenrong.spritebatchslicer/Editor/SpriteBatchProcessor.cs` | 修改 `BuildSpriteRects`（加 `existingIds` 參數）、`ApplyToFolders`（提取既有 GUID + `StartAssetEditing`） |
| `Packages/com.wenrong.spritebatchslicer/Editor/SpriteBatchWindow.cs` | 修改 `DrawRectElement`（加 Alignment 欄位）、`InitRectList` header、`ShowWindow` minSize、`InitFolderList`（移除重複 onChangedCallback）、`HandleFolderDrop`（AcceptDrag 時機）、`MenuItem` 路徑 |
| `Packages/com.wenrong.spritebatchslicer/Tests/Editor/SpriteBatchProcessorTests.cs` | 新增 3 個 GUID 相關測試 |

---

### Task 1：修正 Sprite GUID 遺失（Critical Bug）

**Files:**

- Modify: `Packages/com.wenrong.spritebatchslicer/Editor/SpriteBatchProcessor.cs`
- Modify: `Packages/com.wenrong.spritebatchslicer/Tests/Editor/SpriteBatchProcessorTests.cs`

**背景：** `BuildSpriteRects` 目前建立的 `SpriteRect` 沒有設定 `spriteID`，每次 Apply 時 Unity 會為所有 sprite 分配全新的 GUID，導致 Scene、Prefab、Animation 中的 sprite 參考全部斷掉（Missing Reference）。修正方式是先讀取現有 sprite 的 GUID，依名稱比對後傳入 `BuildSpriteRects` 保留。

- [ ] **Step 1：在測試檔加入三個失敗測試**

在 `SpriteBatchProcessorTests.cs` 的 `SpriteBatchProcessorTests` class 最後（最後一個 `}` 前）加入：

```csharp
[Test]
public void BuildSpriteRects_有既有GUID_保留既有GUID()
{
    var existingId = GUID.Generate();
    var existingIds = new Dictionary<string, GUID> { { "Icon_0", existingId } };
    var rects = new List<SpriteRectDef> { new() { NameSuffix = "_0" } };

    var result = SpriteBatchProcessor.BuildSpriteRects("Icon", rects, existingIds);

    Assert.AreEqual(existingId, result[0].spriteID);
}

[Test]
public void BuildSpriteRects_無既有GUID_產生非零GUID()
{
    var rects = new List<SpriteRectDef> { new() { NameSuffix = "_0" } };

    var result = SpriteBatchProcessor.BuildSpriteRects("Icon", rects, null);

    Assert.AreNotEqual(new GUID(), result[0].spriteID);
}

[Test]
public void BuildSpriteRects_兩筆切割_GUID各不相同()
{
    var rects = new List<SpriteRectDef>
    {
        new() { NameSuffix = "_0" },
        new() { NameSuffix = "_1" }
    };

    var result = SpriteBatchProcessor.BuildSpriteRects("Icon", rects, null);

    Assert.AreNotEqual(result[0].spriteID, result[1].spriteID);
}
```

測試檔頂端需有 `using UnityEditor;` 和 `using System.Collections.Generic;`（確認已存在）。

- [ ] **Step 2：在 Test Runner 確認三個新測試因編譯錯誤而無法執行**

Unity Editor > Window > General > Test Runner > EditMode，確認出現編譯錯誤（`BuildSpriteRects` 參數不符），三個新測試無法執行。

- [ ] **Step 3：修改 `BuildSpriteRects` 加入 `existingIds` 參數**

將 `SpriteBatchProcessor.cs` 中的整個 `BuildSpriteRects` 方法替換為：

```csharp
public static SpriteRect[] BuildSpriteRects(
    string assetFileName,
    List<SpriteRectDef> spriteRects,
    Dictionary<string, GUID> existingIds = null)
{
    var rects = new SpriteRect[spriteRects.Count];
    for (int i = 0; i < spriteRects.Count; i++)
    {
        var def = spriteRects[i];
        string name = assetFileName + def.NameSuffix;
        GUID id = (existingIds != null && existingIds.TryGetValue(name, out GUID found))
            ? found
            : GUID.Generate();
        rects[i] = new SpriteRect
        {
            name = name,
            rect = def.Rect,
            pivot = def.Pivot,
            alignment = def.Alignment,
            spriteID = id
        };
    }
    return rects;
}
```

- [ ] **Step 4：在 `ApplyToFolders` 提取既有 GUID 並傳入 `BuildSpriteRects`**

在 `ApplyToFolders` 中找到：

```csharp
dataProvider.SetSpriteRects(BuildSpriteRects(
    Path.GetFileNameWithoutExtension(path), settings.SpriteRects));
```

替換為：

```csharp
var existingRects = dataProvider.GetSpriteRects();
var existingIds = new Dictionary<string, GUID>();
foreach (var r in existingRects)
    existingIds[r.name] = r.spriteID;

dataProvider.SetSpriteRects(BuildSpriteRects(
    Path.GetFileNameWithoutExtension(path), settings.SpriteRects, existingIds));
```

- [ ] **Step 5：在 Test Runner 確認三個新測試通過，舊測試不受影響**

Test Runner > EditMode > Run All。  
預期：`BuildSpriteRects_有既有GUID_保留既有GUID`、`BuildSpriteRects_無既有GUID_產生非零GUID`、`BuildSpriteRects_兩筆切割_GUID各不相同` 全部綠色，所有原有測試仍通過。

- [ ] **Step 6：Commit**

```bash
git add Packages/com.wenrong.spritebatchslicer/Editor/SpriteBatchProcessor.cs Packages/com.wenrong.spritebatchslicer/Tests/Editor/SpriteBatchProcessorTests.cs
git commit -m "fix: BuildSpriteRects 保留既有 SpriteRect GUID，防止 Apply 時 sprite 參考斷裂"
```

---

### Task 2：SpriteAlignment 加入 UI 欄位

**Files:**

- Modify: `Packages/com.wenrong.spritebatchslicer/Editor/SpriteBatchWindow.cs`

**背景：** `SpriteRectDef.Alignment` 欄位存在且有傳入 `BuildSpriteRects`，但 `DrawRectElement` 沒有繪製此欄位，使用者永遠只能用預設的 `SpriteAlignment.Center`。

- [ ] **Step 1：更新 `InitRectList` 的 header 文字**

在 `InitRectList()` 中，將 `drawHeaderCallback` 改為：

```csharp
drawHeaderCallback = rect =>
    EditorGUI.LabelField(rect, "Sprite 切割 (後綴 | X | Y | W | H | Pivot X | Pivot Y | 對齊)"),
```

- [ ] **Step 2：在 `DrawRectElement` 末尾加入 Alignment EnumPopup**

將整個 `DrawRectElement` 方法替換為：

```csharp
private void DrawRectElement(Rect rect, int index, bool isActive, bool isFocused)
{
    var def = _settings.SpriteRects[index];
    float x = rect.x, y = rect.y + 2, h = EditorGUIUtility.singleLineHeight;

    def.NameSuffix = EditorGUI.TextField(new Rect(x, y, 38, h), def.NameSuffix);
    x += 42;
    def.Rect.x = EditorGUI.FloatField(new Rect(x, y, 48, h), def.Rect.x);
    x += 52;
    def.Rect.y = EditorGUI.FloatField(new Rect(x, y, 48, h), def.Rect.y);
    x += 52;
    def.Rect.width = EditorGUI.FloatField(new Rect(x, y, 48, h), def.Rect.width);
    x += 52;
    def.Rect.height = EditorGUI.FloatField(new Rect(x, y, 48, h), def.Rect.height);
    x += 52;
    def.Pivot.x = EditorGUI.FloatField(new Rect(x, y, 38, h), def.Pivot.x);
    x += 42;
    def.Pivot.y = EditorGUI.FloatField(new Rect(x, y, 38, h), def.Pivot.y);
    x += 42;
    def.Alignment = (SpriteAlignment)EditorGUI.EnumPopup(new Rect(x, y, 84, h), def.Alignment);
}
```

- [ ] **Step 3：更新 `ShowWindow` 的 minSize**

將：

```csharp
window.minSize = new Vector2(440, 620);
```

改為：

```csharp
window.minSize = new Vector2(520, 620);
```

- [ ] **Step 4：手動驗證**

1. Unity Editor > Tools > Sprite 批次設定（注意：路徑將在 Task 4 修正，目前仍在 GameTools）
2. 在 Sprite 切割區塊點 `+` 新增一筆
3. 確認最右邊出現「對齊」EnumPopup，預設值為 `Center`
4. 將值改為 `BottomLeft`，再新增第二筆後切回第一筆，確認值仍是 `BottomLeft`（未被重置）

- [ ] **Step 5：Commit**

```bash
git add Packages/com.wenrong.spritebatchslicer/Editor/SpriteBatchWindow.cs
git commit -m "feat: DrawRectElement 加入 SpriteAlignment 欄位"
```

---

### Task 3：StartAssetEditing 批次匯入效能優化

**Files:**

- Modify: `Packages/com.wenrong.spritebatchslicer/Editor/SpriteBatchProcessor.cs`

**背景：** 每張圖片呼叫一次 `importer.SaveAndReimport()` 會立即觸發一次完整重新匯入。以 60 張圖為例，會觸發 60 次獨立匯入。用 `AssetDatabase.StartAssetEditing()` / `StopAssetEditing()` 包覆後，所有 `SaveAndReimport()` 呼叫會被排隊，等 `StopAssetEditing()` 時 Unity 一次批次執行，大幅減少重複 I/O。

- [ ] **Step 1：在 `ApplyToFolders` 的主迴圈外加入 StartAssetEditing**

在 `ApplyToFolders` 中找到 `var allPaths = new List<string>(pathSet);` 之後的 for 迴圈。
將迴圈及其後的 `return result;` 改為（迴圈主體完全不變，只加外層包覆）：

```csharp
AssetDatabase.StartAssetEditing();
try
{
    for (int i = 0; i < allPaths.Count; i++)
    {
        if (isCancelled is not null && isCancelled())
        {
            result.WasCancelled = true;
            break;
        }

        string path = allPaths[i];
        onProgress?.Invoke((i + 1f) / allPaths.Count, path);

        try
        {
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
            {
                Debug.LogWarning($"[Sprite 批次設定] 無法取得 TextureImporter：{path}");
                result.FailedPaths.Add(path);
                continue;
            }

            importer.GetSourceTextureWidthAndHeight(out int width, out int height);

            bool boundsOk = true;
            foreach (var rectDef in settings.SpriteRects)
            {
                if (!ValidateRectBounds(rectDef, width, height, out string boundsError))
                {
                    Debug.LogError($"[Sprite 批次設定] {Path.GetFileName(path)}: {boundsError}");
                    boundsOk = false;
                    break;
                }
            }
            if (!boundsOk)
            {
                result.SkippedPaths.Add(path);
                continue;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.filterMode = settings.FilterMode;
            importer.alphaIsTransparency = settings.AlphaIsTransparency;
            importer.maxTextureSize = settings.MaxTextureSize;
            importer.textureCompression = settings.Compression;

            var factory = new SpriteDataProviderFactories();
            factory.Init();
            var dataProvider = factory.GetSpriteEditorDataProviderFromObject(importer);
            dataProvider.InitSpriteEditorDataProvider();

            var existingRects = dataProvider.GetSpriteRects();
            var existingIds = new Dictionary<string, GUID>();
            foreach (var r in existingRects)
                existingIds[r.name] = r.spriteID;

            dataProvider.SetSpriteRects(BuildSpriteRects(
                Path.GetFileNameWithoutExtension(path), settings.SpriteRects, existingIds));

            dataProvider.Apply();

            importer.SaveAndReimport();
            result.SuccessCount++;
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[Sprite 批次設定] 處理失敗 {path}: {ex.Message}");
            result.FailedPaths.Add(path);
        }
    }
}
finally
{
    AssetDatabase.StopAssetEditing();
}

return result;
```

- [ ] **Step 2：確認編譯無錯誤，Test Runner 所有測試通過**

Test Runner > EditMode > Run All，確認全部綠色。

- [ ] **Step 3：手動效能測試**

1. 選取含 60 張圖片的資料夾（`Assets/Sprites/Icon00_6_0win`）
2. 加入至少一筆切割設定
3. 點「套用全部」
4. 觀察進度條快速走完（設定 importer 階段），Unity 再統一執行批次匯入
5. 確認完成報告顯示「成功：60 張」

- [ ] **Step 4：Commit**

```bash
git add Packages/com.wenrong.spritebatchslicer/Editor/SpriteBatchProcessor.cs
git commit -m "perf: ApplyToFolders 包覆 StartAssetEditing 批次匯入，大幅減少重複 I/O"
```

---

### Task 4：雜項規格修正

**Files:**

- Modify: `Packages/com.wenrong.spritebatchslicer/Editor/SpriteBatchWindow.cs`

- [ ] **Step 1：修正 MenuItem 路徑**

將：

```csharp
[MenuItem("GameTools/Sprite 批次設定")]
```

改為：

```csharp
[MenuItem("Tools/Sprite 批次設定")]
```

- [ ] **Step 2：修正 `HandleFolderDrop` 中 `AcceptDrag` 呼叫時機**

在 `HandleFolderDrop` 的 `DragPerform` 分支，將：

```csharp
var newFolders = FilterNewFolders(_settings.TargetFolders, DragAndDrop.objectReferences);
if (newFolders.Count > 0)
{
    DragAndDrop.AcceptDrag();
    _settings.TargetFolders.AddRange(newFolders);
    RefreshTexturePaths();
}
evt.Use();
```

改為（`AcceptDrag` 移至 filter 前，無條件呼叫）：

```csharp
DragAndDrop.AcceptDrag();
var newFolders = FilterNewFolders(_settings.TargetFolders, DragAndDrop.objectReferences);
if (newFolders.Count > 0)
{
    _settings.TargetFolders.AddRange(newFolders);
    RefreshTexturePaths();
}
evt.Use();
```

- [ ] **Step 3：移除 `InitFolderList` 中重複的 `onChangedCallback`**

在 `InitFolderList()` 中刪除以下這一行（`onAddCallback` 與 `onRemoveCallback` 已各自呼叫 `RefreshTexturePaths`，`onChangedCallback` 在 add/remove 時會造成雙重呼叫）：

```csharp
onChangedCallback = _ => RefreshTexturePaths()
```

- [ ] **Step 4：手動驗證三項修正**

1. **MenuItem 路徑**：重新編譯後確認選單路徑在 `Tools > Sprite 批次設定`（不再是 `GameTools`）
2. **AcceptDrag 時機**：從 Project 視窗拖入一個「已存在於清單」的資料夾 → 清單不變，但拖放事件被正確消費（游標恢復正常，不會有異常行為）
3. **重複拖放去重**：同時拖入一個新資料夾 + 一個已存在的資料夾 → 只新增一筆

- [ ] **Step 5：Commit**

```bash
git add Packages/com.wenrong.spritebatchslicer/Editor/SpriteBatchWindow.cs
git commit -m "fix: MenuItem 路徑改為 Tools/，AcceptDrag 無條件呼叫，移除重複 RefreshTexturePaths"
```

---

## 完成確認清單

- [ ] Test Runner 所有 EditMode 測試通過（含三個新增 GUID 測試）
- [ ] 套用全部後，Scene/Prefab 中的 sprite 參考不斷裂（GUID 保留）
- [ ] Sprite 切割列表出現「對齊」欄位，可選 Center / BottomLeft / TopRight 等
- [ ] 批次套用 60 張圖明顯比修改前快（不再逐張觸發匯入）
- [ ] 選單路徑為 `Tools > Sprite 批次設定`
- [ ] 拖入重複資料夾時 drag 事件被正確消費
