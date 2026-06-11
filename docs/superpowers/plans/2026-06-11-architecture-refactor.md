# SpriteBatchSlicer 架構重構 (I1/I2/I3) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 將兩個純靜態方法移至 `SpriteBatchEditorUtils`（SRP），把 `BatchSettings` 的資料夾欄位從 `List<DefaultAsset>` 改為 `List<string>`（分層）、並提取重複的資料夾掃描邏輯（DRY）。

**Architecture:** 新增 `SpriteBatchEditorUtils.cs` 持有無狀態工具方法；`SpriteBatchWindow` 保留 `_folderAssets: List<DefaultAsset>` 作為 UI 層綁定，並在 `RefreshTexturePaths` 中同步至 `BatchSettings.FolderPaths`；`SpriteBatchProcessor.CollectTexturePaths` 作為兩端共用的掃描入口。

**Tech Stack:** Unity 6 Editor, C# 9, NUnit EditMode Tests, UnityEditor.AssetDatabase, UnityEditorInternal.ReorderableList

**Spec:** `docs/superpowers/specs/2026-06-11-architecture-refactor-design.md`

---

## 涉及檔案

| 動作 | 路徑 |
|------|------|
| 新增 | `Packages/com.wenrong.spritebatchslicer/Editor/SpriteBatchEditorUtils.cs` |
| 修改 | `Packages/com.wenrong.spritebatchslicer/Editor/SpriteBatchData.cs` |
| 修改 | `Packages/com.wenrong.spritebatchslicer/Editor/SpriteBatchProcessor.cs` |
| 修改 | `Packages/com.wenrong.spritebatchslicer/Editor/SpriteBatchWindow.cs` |
| 修改 | `Packages/com.wenrong.spritebatchslicer/Tests/Editor/SpriteBatchWindowTests.cs` |

---

## Task 1：I1 — 新增 SpriteBatchEditorUtils，搬移靜態工具方法

**Files:**

- Create: `Packages/com.wenrong.spritebatchslicer/Editor/SpriteBatchEditorUtils.cs`
- Modify: `Packages/com.wenrong.spritebatchslicer/Tests/Editor/SpriteBatchWindowTests.cs`
- Modify: `Packages/com.wenrong.spritebatchslicer/Editor/SpriteBatchWindow.cs`

- [ ] **Step 1：更新測試呼叫前綴（讓測試先失敗）**

開啟 `Packages/com.wenrong.spritebatchslicer/Tests/Editor/SpriteBatchWindowTests.cs`，將全部 8 個呼叫點從 `SpriteBatchWindow.` 改為 `SpriteBatchEditorUtils.`：

```csharp
// 整個檔案替換後如下（完整內容）：
using NUnit.Framework;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SpriteBatch.Tests
{
    public class SpriteBatchWindowTests
    {
        [Test]
        public void FilterNewFolders_重複資料夾_不加入()
        {
            var folder = AssetDatabase.LoadAssetAtPath<DefaultAsset>("Assets/Sprites/Icon00_6_0win");
            Assume.That(folder, Is.Not.Null, "測試素材 Icon00_6_0win 不存在");
            var existing = new List<DefaultAsset> { folder };

            var result = SpriteBatchEditorUtils.FilterNewFolders(existing, new Object[] { folder });

            Assert.AreEqual(0, result.Count);
        }

        [Test]
        public void FilterNewFolders_新資料夾_加入()
        {
            var folder = AssetDatabase.LoadAssetAtPath<DefaultAsset>("Assets/Sprites/Icon00_6_0win");
            Assume.That(folder, Is.Not.Null, "測試素材 Icon00_6_0win 不存在");

            var result = SpriteBatchEditorUtils.FilterNewFolders(new List<DefaultAsset>(), new Object[] { folder });

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(folder, result[0]);
        }

        [Test]
        public void FilterNewFolders_非資料夾物件_略過()
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(
                "Assets/Sprites/Icon00_6_0win/Icon00_6_0win_00.png");
            Assume.That(texture, Is.Not.Null, "測試素材 Icon00_6_0win_00.png 不存在");

            var result = SpriteBatchEditorUtils.FilterNewFolders(new List<DefaultAsset>(), new Object[] { texture });

            Assert.AreEqual(0, result.Count);
        }

        [Test]
        public void FilterNewFolders_批次內重複資料夾_只加入一次()
        {
            var folder = AssetDatabase.LoadAssetAtPath<DefaultAsset>("Assets/Sprites/Icon00_6_0win");
            Assume.That(folder, Is.Not.Null, "測試素材 Icon00_6_0win 不存在");

            var result = SpriteBatchEditorUtils.FilterNewFolders(
                new List<DefaultAsset>(), new Object[] { folder, folder });

            Assert.AreEqual(1, result.Count);
        }

        // --- AlignmentToPivot ---

        [Test]
        public void AlignmentToPivot_TopLeft_回傳_0_1()
        {
            var result = SpriteBatchEditorUtils.AlignmentToPivot(SpriteAlignment.TopLeft, Vector2.zero);
            Assert.AreEqual(new Vector2(0f, 1f), result);
        }

        [Test]
        public void AlignmentToPivot_BottomRight_回傳_1_0()
        {
            var result = SpriteBatchEditorUtils.AlignmentToPivot(SpriteAlignment.BottomRight, Vector2.zero);
            Assert.AreEqual(new Vector2(1f, 0f), result);
        }

        [Test]
        public void AlignmentToPivot_Center_回傳_05_05()
        {
            var result = SpriteBatchEditorUtils.AlignmentToPivot(SpriteAlignment.Center, Vector2.zero);
            Assert.AreEqual(new Vector2(0.5f, 0.5f), result);
        }

        [Test]
        public void AlignmentToPivot_Custom_不改變Pivot()
        {
            var original = new Vector2(0.3f, 0.7f);
            var result = SpriteBatchEditorUtils.AlignmentToPivot(SpriteAlignment.Custom, original);
            Assert.AreEqual(original, result);
        }
    }
}
```

- [ ] **Step 2：在 Unity Test Runner 執行 EditMode 測試，確認 8 個 Window 測試失敗**

  Window → General → Test Runner → EditMode。
  期望：`SpriteBatchWindowTests` 的全部 8 個測試失敗（`SpriteBatchEditorUtils` 不存在）。

- [ ] **Step 3：建立 `SpriteBatchEditorUtils.cs`**

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

- [ ] **Step 4：更新 `SpriteBatchWindow.cs` 中的兩個呼叫點，並刪除兩個方法**

  4a. 第 121 行：`AlignmentToPivot` → `SpriteBatchEditorUtils.AlignmentToPivot`

  將：

  ```csharp
                  def.Pivot = AlignmentToPivot(newAlignment, def.Pivot);
  ```

  改為：

  ```csharp
                  def.Pivot = SpriteBatchEditorUtils.AlignmentToPivot(newAlignment, def.Pivot);
  ```

  4b. 第 184 行：`FilterNewFolders` → `SpriteBatchEditorUtils.FilterNewFolders`

  將：

  ```csharp
                  var newFolders = FilterNewFolders(_settings.TargetFolders, DragAndDrop.objectReferences);
  ```

  改為：

  ```csharp
                  var newFolders = SpriteBatchEditorUtils.FilterNewFolders(_settings.TargetFolders, DragAndDrop.objectReferences);
  ```

  4c. 刪除 `FilterNewFolders` 方法（第 385–406 行）——整段移除：

  ```csharp
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
  ```

  4d. 刪除 `AlignmentToPivot` 方法（第 421–434 行，刪除後行號會偏移）——整段移除：

  ```csharp
          public static Vector2 AlignmentToPivot(SpriteAlignment alignment, Vector2 current) =>
              alignment switch
              {
                  SpriteAlignment.TopLeft => new Vector2(0f, 1f),
                  SpriteAlignment.TopCenter => new Vector2(0.5f, 1f),
                  SpriteAlignment.TopRight => new Vector2(1f, 1f),
                  SpriteAlignment.LeftCenter => new Vector2(0f, 0.5f),
                  SpriteAlignment.Center => new Vector2(0.5f, 0.5f),
                  SpriteAlignment.RightCenter => new Vector2(1f, 0.5f),
                  SpriteAlignment.BottomLeft => new Vector2(0f, 0f),
                  SpriteAlignment.BottomCenter => new Vector2(0.5f, 0f),
                  SpriteAlignment.BottomRight => new Vector2(1f, 0f),
                  _ => current,
              };
  ```

- [ ] **Step 5：在 Unity Test Runner 執行全部 EditMode 測試**

  期望：全部通過。`SpriteBatchWindowTests` 8 個全部 PASS，其餘不退步。

- [ ] **Step 6：Commit**

  ```
  git add Packages/com.wenrong.spritebatchslicer/Editor/SpriteBatchEditorUtils.cs
  git add Packages/com.wenrong.spritebatchslicer/Editor/SpriteBatchEditorUtils.cs.meta
  git add Packages/com.wenrong.spritebatchslicer/Editor/SpriteBatchWindow.cs
  git add Packages/com.wenrong.spritebatchslicer/Tests/Editor/SpriteBatchWindowTests.cs
  git commit -m "refactor: 將 FilterNewFolders、AlignmentToPivot 移至 SpriteBatchEditorUtils (I1 SRP)"
  ```

---

## Task 2：I2 + I3 — BatchSettings 改用 List\<string\>，提取 CollectTexturePaths

**Files:**

- Modify: `Packages/com.wenrong.spritebatchslicer/Editor/SpriteBatchData.cs`
- Modify: `Packages/com.wenrong.spritebatchslicer/Editor/SpriteBatchProcessor.cs`
- Modify: `Packages/com.wenrong.spritebatchslicer/Editor/SpriteBatchWindow.cs`
- Modify: `Packages/com.wenrong.spritebatchslicer/Tests/Editor/SpriteBatchProcessorTests.cs`

- [ ] **Step 1：新增 `CollectTexturePaths` 測試（先讓測試失敗）**

  在 `Packages/com.wenrong.spritebatchslicer/Tests/Editor/SpriteBatchProcessorTests.cs` 的 `BuildSpriteRects_兩筆切割_GUID各不相同` 測試後，加入兩個新測試（在最後一個 `}` 之前）：

  ```csharp
          // --- CollectTexturePaths ---

          [Test]
          public void CollectTexturePaths_空清單_回傳空清單()
          {
              var result = SpriteBatchProcessor.CollectTexturePaths(new List<string>());
              Assert.AreEqual(0, result.Count);
          }

          [Test]
          public void CollectTexturePaths_包含不存在路徑_略過並回傳空清單()
          {
              var result = SpriteBatchProcessor.CollectTexturePaths(
                  new List<string> { "Assets/NonExistent/Folder" });
              Assert.AreEqual(0, result.Count);
          }
  ```

- [ ] **Step 2：在 Unity Test Runner 執行 EditMode 測試，確認新測試失敗**

  期望：`CollectTexturePaths_*` 兩個測試失敗（方法不存在）。

- [ ] **Step 3：在 `SpriteBatchProcessor.cs` 加入 `CollectTexturePaths` 方法**

  在 `ApplyToFolders` 方法之前加入：

  ```csharp
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

  同時在檔案頂部加入（若未有）：

  ```csharp
  using System.Collections.Generic;
  ```

  （此 using 已存在，確認即可。）

- [ ] **Step 4：在 Unity Test Runner 執行 EditMode 測試，確認新測試通過**

  期望：`CollectTexturePaths_空清單_回傳空清單` 和 `CollectTexturePaths_包含不存在路徑_略過並回傳空清單` 均 PASS。

- [ ] **Step 5：修改 `SpriteBatchData.cs`**

  將整個檔案替換為：

  ```csharp
  using System;
  using System.Collections.Generic;
  using UnityEngine;

  namespace SpriteBatch
  {
      [Serializable]
      public class SpriteRectDef
      {
          public string NameSuffix = "_0";
          public Rect Rect = new(0, 0, 100, 100);
          public Vector2 Pivot = new(0.5f, 0.5f);
          public SpriteAlignment Alignment = SpriteAlignment.Center;
      }

      [Serializable]
      public class BatchSettings
      {
          public List<string> FolderPaths = new();
          public int MaxTextureSize = 2048;
          public FilterMode FilterMode = FilterMode.Bilinear;
          public bool AlphaIsTransparency = true;
          public TextureImporterCompression Compression = TextureImporterCompression.Compressed;
          public List<SpriteRectDef> SpriteRects = new();
      }
  }
  ```

  這步會導致編譯錯誤（Window 和 Processor 仍引用舊欄位），後續步驟修復。

- [ ] **Step 6：更新 `SpriteBatchProcessor.cs` — `ApplyToFolders` 的掃描段**

  將 `ApplyToFolders` 中的 pathSet 建立段：

  ```csharp
              var pathSet = new HashSet<string>();
              foreach (var folder in settings.TargetFolders)
              {
                  if (folder == null)
                  {
                      continue;
                  }

                  string folderPath = AssetDatabase.GetAssetPath(folder);
                  if (string.IsNullOrEmpty(folderPath))
                  {
                      continue;
                  }

                  string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { folderPath });
                  foreach (string guid in guids)
                  {
                      _ = pathSet.Add(AssetDatabase.GUIDToAssetPath(guid));
                  }
              }

              var allPaths = new List<string>(pathSet);
  ```

  整段改為：

  ```csharp
              var allPaths = CollectTexturePaths(settings.FolderPaths);
  ```

- [ ] **Step 7：更新 `SpriteBatchWindow.cs` — 新增 `_folderAssets` 欄位**

  在第 33 行 `private Vector2 _scrollPos;` 之後加入一行：

  將：

  ```csharp
          private Vector2 _scrollPos;
  ```

  改為：

  ```csharp
          private Vector2 _scrollPos;
          private List<DefaultAsset> _folderAssets = new();
  ```

- [ ] **Step 8：更新 `InitFolderList` — 綁定 `_folderAssets`**

  將整個 `InitFolderList` 方法（第 57–78 行）替換為：

  ```csharp
          private void InitFolderList()
          {
              _folderList = new ReorderableList(
                  _folderAssets, typeof(DefaultAsset), true, true, true, true)
              {
                  drawHeaderCallback = rect =>
                      EditorGUI.LabelField(rect, "目標資料夾"),
                  drawElementCallback = (rect, index, isActive, isFocused) =>
                      {
                          var r = new Rect(rect.x, rect.y + 2, rect.width, EditorGUIUtility.singleLineHeight);
                          EditorGUI.BeginChangeCheck();
                          _folderAssets[index] = (DefaultAsset)EditorGUI.ObjectField(
                              r, _folderAssets[index], typeof(DefaultAsset), false);
                          if (EditorGUI.EndChangeCheck())
                          {
                              RefreshTexturePaths();
                          }
                      },
                  onAddCallback = _ => { _folderAssets.Add(null); RefreshTexturePaths(); },
                  onRemoveCallback = list => { _folderAssets.RemoveAt(list.index); RefreshTexturePaths(); }
              };
          }
  ```

- [ ] **Step 9：更新 `DrawFolderSection` — 空清單判斷**

  將：

  ```csharp
              if (_settings.TargetFolders.Count == 0)
  ```

  改為：

  ```csharp
              if (_folderAssets.Count == 0)
  ```

- [ ] **Step 10：更新 `HandleFolderDrop` — 兩個 `_settings.TargetFolders` 引用**

  將：

  ```csharp
                  var newFolders = SpriteBatchEditorUtils.FilterNewFolders(_settings.TargetFolders, DragAndDrop.objectReferences);
                  if (newFolders.Count > 0)
                  {
                      _settings.TargetFolders.AddRange(newFolders);
  ```

  改為：

  ```csharp
                  var newFolders = SpriteBatchEditorUtils.FilterNewFolders(_folderAssets, DragAndDrop.objectReferences);
                  if (newFolders.Count > 0)
                  {
                      _folderAssets.AddRange(newFolders);
  ```

- [ ] **Step 11：更新 `DrawApplySection` — 資料夾數量來源**

  將：

  ```csharp
              int folderCount = _settings.TargetFolders.Count(f => f != null);
  ```

  改為：

  ```csharp
              int folderCount = _settings.FolderPaths.Count;
  ```

- [ ] **Step 12：更新 `DelayedLoadFolders` — 使用 `_folderAssets`**

  將：

  ```csharp
              _settings.TargetFolders.Clear();
              foreach (string path in s.FolderPaths)
              {
                  var asset = AssetDatabase.LoadAssetAtPath<DefaultAsset>(path);
                  if (asset != null)
                  {
                      _settings.TargetFolders.Add(asset);
                  }
              }
  ```

  改為：

  ```csharp
              _folderAssets.Clear();
              foreach (string path in s.FolderPaths)
              {
                  var asset = AssetDatabase.LoadAssetAtPath<DefaultAsset>(path);
                  if (asset != null)
                  {
                      _folderAssets.Add(asset);
                  }
              }
  ```

- [ ] **Step 13：更新 `SaveState` — 從 `_folderAssets` 取路徑**

  將：

  ```csharp
              foreach (var folder in _settings.TargetFolders)
  ```

  改為：

  ```csharp
              foreach (var folder in _folderAssets)
  ```

- [ ] **Step 14：更新 `RefreshTexturePaths` — 同步路徑並呼叫 `CollectTexturePaths`**

  將整個 `RefreshTexturePaths` 方法替換為：

  ```csharp
          private void RefreshTexturePaths()
          {
              string preservedPath = _previewIndex < _allTexturePaths.Count
                  ? _allTexturePaths[_previewIndex]
                  : null;

              _settings.FolderPaths.Clear();
              foreach (var folder in _folderAssets)
              {
                  if (folder == null)
                  {
                      continue;
                  }
                  string p = AssetDatabase.GetAssetPath(folder);
                  if (!string.IsNullOrEmpty(p))
                  {
                      _settings.FolderPaths.Add(p);
                  }
              }

              _allTexturePaths = SpriteBatchProcessor.CollectTexturePaths(_settings.FolderPaths);
              _previewNames = _allTexturePaths
                  .Select(p =>
                  {
                      string ext = Path.GetExtension(p);
                      return p.StartsWith("Assets/")
                          ? (p["Assets/".Length..^ext.Length] is { Length: > 0 } rel ? rel : Path.GetFileNameWithoutExtension(p))
                          : Path.GetFileNameWithoutExtension(p);
                  })
                  .ToArray();

              int restored = preservedPath is not null ? _allTexturePaths.IndexOf(preservedPath) : -1;
              _previewIndex = restored >= 0 ? restored : 0;
              _previewTexture = null;
              Repaint();
          }
  ```

- [ ] **Step 15：更新 `ApplyAll` — 移除手動轉換，直接使用 `_settings.FolderPaths`**

  將整個 `ApplyAll` 方法替換為：

  ```csharp
          private void ApplyAll()
          {
              var errors = SpriteBatchProcessor.ValidatePreflight(_settings.FolderPaths, _settings.SpriteRects);
              if (errors.Count > 0)
              {
                  _ = EditorUtility.DisplayDialog("驗證失敗",
                      string.Join("\n", errors.Select(e => e.Message)), "確認");
                  return;
              }

              bool cancelled = false;
              SpriteBatchProcessor.ApplyResult result;
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

              string title = result.WasCancelled ? "已取消" : "套用完成";
              string report = result.WasCancelled ? "操作已被使用者取消。\n\n" : "";
              report += $"成功：{result.SuccessCount} 張\n" +
                          $"跳過（尺寸不符）：{result.SkippedPaths.Count} 張\n" +
                          $"失敗（其他錯誤）：{result.FailedPaths.Count} 張";

              if (result.SkippedPaths.Count > 0 || result.FailedPaths.Count > 0)
              {
                  report += "\n\n問題檔案：\n" +
                              string.Join("\n", result.SkippedPaths.Concat(result.FailedPaths));
              }

              _ = EditorUtility.DisplayDialog(title, report, "確認");
              RefreshTexturePaths();
          }
  ```

- [ ] **Step 16：在 Unity Test Runner 執行全部 EditMode 測試**

  期望：全部通過。若有編譯錯誤先修復（確認無殘留 `_settings.TargetFolders` 引用）。

  驗查指令（PowerShell）：

  ```powershell
  Select-String -Path "Packages/com.wenrong.spritebatchslicer/Editor/*.cs" -Pattern "TargetFolders"
  ```

  期望：無任何輸出。

- [ ] **Step 17：手動驗證（在 Unity 編輯器中）**

  - [ ] 開啟 Tools → Sprite 批次設定
  - [ ] 拖曳資料夾至清單，確認出現於清單中
  - [ ] 預覽下拉顯示貼圖路徑
  - [ ] Domain Reload（儲存任意腳本）後資料夾仍在清單中
  - [ ] 按「套用全部」，進度條正常，完成後顯示結果對話框

- [ ] **Step 18：Commit**

  ```
  git add Packages/com.wenrong.spritebatchslicer/Editor/SpriteBatchData.cs
  git add Packages/com.wenrong.spritebatchslicer/Editor/SpriteBatchProcessor.cs
  git add Packages/com.wenrong.spritebatchslicer/Editor/SpriteBatchWindow.cs
  git add Packages/com.wenrong.spritebatchslicer/Tests/Editor/SpriteBatchProcessorTests.cs
  git commit -m "refactor: BatchSettings 改用 List<string> FolderPaths，提取 CollectTexturePaths (I2/I3)"
  ```
