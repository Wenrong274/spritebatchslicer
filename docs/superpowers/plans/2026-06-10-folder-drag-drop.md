# 目標資料夾 Drag & Drop 實作計畫

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 讓「目標資料夾」清單支援從 Project 視窗拖曳資料夾，一次可拖入多個，自動跳過重複，非資料夾物件拖入無效。

**Architecture:** 將 `DrawFolderSection` 由 `DoLayoutList()` 改為 `DoList(Rect)`，捕捉清單的完整 Rect，再於每幀結尾呼叫 `HandleFolderDrop(Rect)` 處理 Unity `DragAndDrop` 事件。過濾 + 去重邏輯提取為 `public static FilterNewFolders` 方法以利 EditMode 單元測試。

**Tech Stack:** Unity Editor API (`DragAndDrop`, `AssetDatabase`, `ReorderableList.DoList`), NUnit EditMode Tests

---

### Task 1: 提取 FilterNewFolders 並寫 EditMode 測試

**Files:**
- Modify: `Assets/Editor/SpriteBatchWindow.cs`
- Modify: `Assets/Tests/Editor/SpriteBatchProcessorTests.cs`

- [ ] **Step 1: 在測試檔新增三個失敗測試**

在 `Assets/Tests/Editor/SpriteBatchProcessorTests.cs` 的 `namespace SpriteBatch.Tests` 內，`SpriteBatchProcessorTests` class 結束後加入新 class：

```csharp
public class SpriteBatchWindowTests
{
    [Test]
    public void FilterNewFolders_重複資料夾_不加入()
    {
        var folder = AssetDatabase.LoadAssetAtPath<DefaultAsset>("Assets/Sprites/Icon00_6_0win");
        Assume.That(folder, Is.Not.Null, "測試素材 Icon00_6_0win 不存在");
        var existing = new List<DefaultAsset> { folder };

        var result = SpriteBatchWindow.FilterNewFolders(existing, new Object[] { folder });

        Assert.AreEqual(0, result.Count);
    }

    [Test]
    public void FilterNewFolders_新資料夾_加入()
    {
        var folder = AssetDatabase.LoadAssetAtPath<DefaultAsset>("Assets/Sprites/Icon00_6_0win");
        Assume.That(folder, Is.Not.Null, "測試素材 Icon00_6_0win 不存在");

        var result = SpriteBatchWindow.FilterNewFolders(new List<DefaultAsset>(), new Object[] { folder });

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual(folder, result[0]);
    }

    [Test]
    public void FilterNewFolders_非資料夾物件_略過()
    {
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(
            "Assets/Sprites/Icon00_6_0win/Icon00_6_0win_00.png");
        Assume.That(texture, Is.Not.Null, "測試素材 Icon00_6_0win_00.png 不存在");

        var result = SpriteBatchWindow.FilterNewFolders(new List<DefaultAsset>(), new Object[] { texture });

        Assert.AreEqual(0, result.Count);
    }
}
```

測試檔頂端需有以下 using（確認已存在或補上）：
```csharp
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
```

- [ ] **Step 2: 確認測試因缺少 FilterNewFolders 而無法編譯**

在 Unity Editor 開啟 Test Runner（Window > General > Test Runner > EditMode），確認出現編譯錯誤，三個測試無法執行。

- [ ] **Step 3: 實作 FilterNewFolders**

在 `Assets/Editor/SpriteBatchWindow.cs` 的 class 最後一個 `}` 前加入：

```csharp
public static List<DefaultAsset> FilterNewFolders(
    List<DefaultAsset> existing, IEnumerable<Object> dragged)
{
    var result = new List<DefaultAsset>();
    foreach (var obj in dragged)
    {
        if (obj is not DefaultAsset asset)
            continue;
        if (!AssetDatabase.IsValidFolder(AssetDatabase.GetAssetPath(obj)))
            continue;
        if (existing.Contains(asset))
            continue;
        result.Add(asset);
    }
    return result;
}
```

- [ ] **Step 4: 執行測試確認三個全為綠色**

Test Runner > EditMode，確認 `SpriteBatchWindowTests` 的三個測試全部 Pass。

- [ ] **Step 5: Commit**

```bash
git add Assets/Editor/SpriteBatchWindow.cs Assets/Tests/Editor/SpriteBatchProcessorTests.cs
git commit -m "feat: 提取 FilterNewFolders 並加入 EditMode 測試"
```

---

### Task 2: 實作拖曳事件處理

**Files:**
- Modify: `Assets/Editor/SpriteBatchWindow.cs`

- [ ] **Step 1: 修改 DrawFolderSection 捕捉清單 Rect**

將原本的：
```csharp
private void DrawFolderSection() { _folderList.DoLayoutList(); }
```

改為：
```csharp
private void DrawFolderSection()
{
    float listHeight = _folderList.GetHeight();
    Rect listRect = GUILayoutUtility.GetRect(0f, listHeight, GUILayout.ExpandWidth(true));
    _folderList.DoList(listRect);
    HandleFolderDrop(listRect);
}
```

- [ ] **Step 2: 新增 HandleFolderDrop 方法**

在 `FilterNewFolders` 方法上方加入：

```csharp
private void HandleFolderDrop(Rect dropRect)
{
    var evt = Event.current;
    if (!dropRect.Contains(evt.mousePosition))
        return;

    if (evt.type == EventType.DragUpdated)
    {
        bool hasFolder = DragAndDrop.objectReferences.Any(obj =>
            obj is DefaultAsset &&
            AssetDatabase.IsValidFolder(AssetDatabase.GetAssetPath(obj)));
        DragAndDrop.visualMode = hasFolder
            ? DragAndDropVisualMode.Copy
            : DragAndDropVisualMode.Rejected;
        evt.Use();
    }
    else if (evt.type == EventType.DragPerform)
    {
        DragAndDrop.AcceptDrag();
        var newFolders = FilterNewFolders(_settings.TargetFolders, DragAndDrop.objectReferences);
        if (newFolders.Count > 0)
        {
            _settings.TargetFolders.AddRange(newFolders);
            RefreshTexturePaths();
        }
        evt.Use();
    }
}
```

- [ ] **Step 3: 手動測試四個情境**

在 Unity Editor 開啟「工具 (Tools) > Sprite 批次設定」視窗，從 Project 視窗驗證：

1. **單一資料夾**：拖曳 `Assets/Sprites/Icon00_6_0win` → 清單增加一筆
2. **重複資料夾**：再次拖曳同一資料夾 → 清單不變（不重複）
3. **多個資料夾**：同時選取 `Icon01_6_0win` 與 `Icon02_6_0win` 一起拖入 → 兩筆全部加入
4. **非資料夾**：拖曳一張 Texture PNG → 游標顯示禁止圖示，放開後清單不變

- [ ] **Step 4: Commit**

```bash
git add Assets/Editor/SpriteBatchWindow.cs
git commit -m "feat: 目標資料夾清單支援拖曳資料夾（drag & drop）"
```
