# Sprite 批次設定工具 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 建立一個 Unity EditorWindow，讓使用者批次設定多個資料夾下所有 PNG 的 SpriteMode=Multiple 及切割 Rect。

**Architecture:** 資料類別 (`SpriteBatchData.cs`) 定義設定結構；處理器 (`SpriteBatchProcessor.cs`) 封裝可獨立測試的純邏輯（驗證、BuildMetaData）及 TextureImporter 套用邏輯；視窗 (`SpriteBatchWindow.cs`) 負責 UI 與協調，三者分離。

**Tech Stack:** Unity Editor C#、TextureImporter API、ReorderableList、EditorGUI、Unity Test Framework (EditMode)

---

## 檔案結構

| 路徑 | 職責 |
|---|---|
| `Assets/Editor/SpriteBatchData.cs` | SpriteRectDef、BatchSettings 資料類別 |
| `Assets/Editor/SpriteBatchProcessor.cs` | 驗證、BuildSpriteMetaData、ApplyToFolders |
| `Assets/Editor/SpriteBatchWindow.cs` | EditorWindow UI |
| `Assets/Editor/SpriteBatchEditor.asmdef` | Editor assembly，讓測試組件可參考 |
| `Assets/Tests/Editor/SpriteBatchProcessorTests.cs` | EditMode 單元測試 |
| `Assets/Tests/Editor/SpriteBatchTests.asmdef` | 測試 assembly |

---

### Task 1：資料結構

**Files:**
- Create: `Assets/Editor/SpriteBatchData.cs`

- [ ] **步驟 1：建立 SpriteBatchData.cs**

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace SpriteBatch
{
    [Serializable]
    public class SpriteRectDef
    {
        public string nameSuffix = "_0";
        public Rect rect = new Rect(0, 0, 100, 100);
        public Vector2 pivot = new Vector2(0.5f, 0.5f);
        public SpriteAlignment alignment = SpriteAlignment.Center;
    }

    [Serializable]
    public class BatchSettings
    {
        public List<DefaultAsset> targetFolders = new List<DefaultAsset>();
        public int maxTextureSize = 2048;
        public FilterMode filterMode = FilterMode.Bilinear;
        public bool alphaIsTransparency = true;
        public TextureImporterCompression compression = TextureImporterCompression.Compressed;
        public List<SpriteRectDef> spriteRects = new List<SpriteRectDef>();
    }
}
```

- [ ] **步驟 2：驗證編譯**

切換回 Unity Editor，等待自動編譯，確認 Console 無紅色錯誤。

- [ ] **步驟 3：Commit**

```bash
git add "Assets/Editor/SpriteBatchData.cs"
git commit -m "feat: add SpriteBatch data structures"
```

---

### Task 2：測試組件設定 (Assembly Definitions)

**Files:**
- Create: `Assets/Editor/SpriteBatchEditor.asmdef`
- Create: `Assets/Tests/Editor/SpriteBatchTests.asmdef`

- [ ] **步驟 1：建立 Editor Assembly Definition**

建立 `Assets/Editor/SpriteBatchEditor.asmdef`：

```json
{
    "name": "SpriteBatchEditor",
    "rootNamespace": "SpriteBatch",
    "references": [],
    "includePlatforms": ["Editor"],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": true,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}
```

- [ ] **步驟 2：建立 Tests/Editor 資料夾並設定 Test Assembly Definition**

建立 `Assets/Tests/Editor/SpriteBatchTests.asmdef`：

```json
{
    "name": "SpriteBatchTests",
    "rootNamespace": "SpriteBatch.Tests",
    "references": [
        "SpriteBatchEditor",
        "UnityEngine.TestRunner",
        "UnityEditor.TestRunner"
    ],
    "includePlatforms": ["Editor"],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": true,
    "precompiledReferences": ["nunit.framework.dll"],
    "autoReferenced": false,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}
```

- [ ] **步驟 3：驗證編譯**

切換回 Unity，確認 Console 無錯誤。開啟 `Window > General > Test Runner`，確認 EditMode tab 可見。

- [ ] **步驟 4：Commit**

```bash
git add "Assets/Editor/SpriteBatchEditor.asmdef" "Assets/Tests/Editor/SpriteBatchTests.asmdef"
git commit -m "feat: add assembly definitions for SpriteBatch and tests"
```

---

### Task 3：SpriteBatchProcessor — 驗證邏輯（TDD）

**Files:**
- Create: `Assets/Tests/Editor/SpriteBatchProcessorTests.cs`
- Create: `Assets/Editor/SpriteBatchProcessor.cs`（只實作 ValidatePreflight、ValidateRectBounds、BuildSpriteMetaData）

- [ ] **步驟 1：先寫失敗測試**

建立 `Assets/Tests/Editor/SpriteBatchProcessorTests.cs`：

```csharp
using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;
using SpriteBatch;

namespace SpriteBatch.Tests
{
    public class SpriteBatchProcessorTests
    {
        // --- ValidatePreflight ---

        [Test]
        public void ValidatePreflight_空資料夾清單_回傳資料夾錯誤()
        {
            var rects = new List<SpriteRectDef> { new SpriteRectDef() };
            var errors = SpriteBatchProcessor.ValidatePreflight(new List<string>(), rects);
            Assert.AreEqual(1, errors.Count);
            StringAssert.Contains("資料夾", errors[0].message);
        }

        [Test]
        public void ValidatePreflight_空切割清單_回傳切割錯誤()
        {
            var folders = new List<string> { "Assets/Sprites/Icon00_6_0win" };
            var errors = SpriteBatchProcessor.ValidatePreflight(folders, new List<SpriteRectDef>());
            Assert.AreEqual(1, errors.Count);
            StringAssert.Contains("切割", errors[0].message);
        }

        [Test]
        public void ValidatePreflight_兩者皆空_回傳兩個錯誤()
        {
            var errors = SpriteBatchProcessor.ValidatePreflight(new List<string>(), new List<SpriteRectDef>());
            Assert.AreEqual(2, errors.Count);
        }

        [Test]
        public void ValidatePreflight_兩者皆有_回傳無錯誤()
        {
            var folders = new List<string> { "Assets/Sprites/Icon00_6_0win" };
            var rects = new List<SpriteRectDef> { new SpriteRectDef() };
            var errors = SpriteBatchProcessor.ValidatePreflight(folders, rects);
            Assert.AreEqual(0, errors.Count);
        }

        // --- ValidateRectBounds ---

        [Test]
        public void ValidateRectBounds_範圍內_回傳true()
        {
            var def = new SpriteRectDef { rect = new Rect(0, 0, 160, 88) };
            bool valid = SpriteBatchProcessor.ValidateRectBounds(def, 160, 551, out string error);
            Assert.IsTrue(valid);
            Assert.IsNull(error);
        }

        [Test]
        public void ValidateRectBounds_超出寬度_回傳false()
        {
            var def = new SpriteRectDef { nameSuffix = "_0", rect = new Rect(0, 0, 200, 88) };
            bool valid = SpriteBatchProcessor.ValidateRectBounds(def, 160, 551, out string error);
            Assert.IsFalse(valid);
            Assert.IsNotNull(error);
        }

        [Test]
        public void ValidateRectBounds_超出高度_回傳false()
        {
            var def = new SpriteRectDef { nameSuffix = "_1", rect = new Rect(0, 0, 100, 600) };
            bool valid = SpriteBatchProcessor.ValidateRectBounds(def, 160, 551, out string error);
            Assert.IsFalse(valid);
            Assert.IsNotNull(error);
        }

        // --- BuildSpriteMetaData ---

        [Test]
        public void BuildSpriteMetaData_兩個切割_回傳正確名稱與Rect()
        {
            var rects = new List<SpriteRectDef>
            {
                new SpriteRectDef { nameSuffix = "_0", rect = new Rect(0, 463, 160, 88),  pivot = new Vector2(0.5f, 0.5f) },
                new SpriteRectDef { nameSuffix = "_1", rect = new Rect(0, 409, 160, 170), pivot = new Vector2(0f,   0f)   }
            };

            var metadata = SpriteBatchProcessor.BuildSpriteMetaData("Icon00_6_0win_00", rects);

            Assert.AreEqual(2, metadata.Length);
            Assert.AreEqual("Icon00_6_0win_00_0", metadata[0].name);
            Assert.AreEqual(new Rect(0, 463, 160, 88), metadata[0].rect);
            Assert.AreEqual(new Vector2(0.5f, 0.5f), metadata[0].pivot);
            Assert.AreEqual("Icon00_6_0win_00_1", metadata[1].name);
            Assert.AreEqual(new Vector2(0f, 0f), metadata[1].pivot);
        }
    }
}
```

- [ ] **步驟 2：在 Unity Test Runner 確認測試失敗**

開啟 `Window > General > Test Runner > EditMode`，執行 SpriteBatchProcessorTests。  
預期：全部紅色（SpriteBatchProcessor 未存在）。

- [ ] **步驟 3：建立 SpriteBatchProcessor.cs 實作驗證與建構邏輯**

建立 `Assets/Editor/SpriteBatchProcessor.cs`：

```csharp
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SpriteBatch
{
    public static class SpriteBatchProcessor
    {
        public struct ValidationError { public string message; }

        public static List<ValidationError> ValidatePreflight(List<string> folderPaths, List<SpriteRectDef> spriteRects)
        {
            var errors = new List<ValidationError>();
            if (folderPaths == null || folderPaths.Count == 0)
                errors.Add(new ValidationError { message = "至少需要選取一個目標資料夾。" });
            if (spriteRects == null || spriteRects.Count == 0)
                errors.Add(new ValidationError { message = "至少需要定義一個 Sprite 切割區域。" });
            return errors;
        }

        public static bool ValidateRectBounds(SpriteRectDef rectDef, int imageWidth, int imageHeight, out string error)
        {
            error = null;
            if (rectDef.rect.x + rectDef.rect.width > imageWidth)
            {
                error = $"切割區域 '{rectDef.nameSuffix}' 超出圖片寬度（{imageWidth}px）。";
                return false;
            }
            if (rectDef.rect.y + rectDef.rect.height > imageHeight)
            {
                error = $"切割區域 '{rectDef.nameSuffix}' 超出圖片高度（{imageHeight}px）。";
                return false;
            }
            return true;
        }

        public static SpriteMetaData[] BuildSpriteMetaData(string assetFileName, List<SpriteRectDef> spriteRects)
        {
            var metadata = new SpriteMetaData[spriteRects.Count];
            for (int i = 0; i < spriteRects.Count; i++)
            {
                var def = spriteRects[i];
                metadata[i] = new SpriteMetaData
                {
                    name      = assetFileName + def.nameSuffix,
                    rect      = def.rect,
                    pivot     = def.pivot,
                    alignment = (int)def.alignment
                };
            }
            return metadata;
        }
    }
}
```

- [ ] **步驟 4：在 Test Runner 確認測試通過**

重新執行 SpriteBatchProcessorTests，預期全部綠色。

- [ ] **步驟 5：Commit**

```bash
git add "Assets/Tests/Editor/SpriteBatchProcessorTests.cs" "Assets/Editor/SpriteBatchProcessor.cs"
git commit -m "feat: add SpriteBatchProcessor validation and BuildSpriteMetaData with tests"
```

---

### Task 4：SpriteBatchProcessor — Apply 邏輯

**Files:**
- Modify: `Assets/Editor/SpriteBatchProcessor.cs`（新增 ApplyResult struct 與 ApplyToFolders）

- [ ] **步驟 1：在 SpriteBatchProcessor.cs 末尾（namespace 大括號內）加入 Apply 邏輯**

在 `BuildSpriteMetaData` 後面、namespace 結尾 `}` 前插入：

```csharp
        public struct ApplyResult
        {
            public int successCount;
            public List<string> skippedPaths;  // 尺寸不符
            public List<string> failedPaths;   // 其他錯誤
        }

        public static ApplyResult ApplyToFolders(
            BatchSettings settings,
            System.Action<float, string> onProgress = null,
            System.Func<bool> isCancelled = null)
        {
            var result = new ApplyResult
            {
                skippedPaths = new List<string>(),
                failedPaths  = new List<string>()
            };

            var allPaths = new List<string>();
            foreach (var folder in settings.targetFolders)
            {
                if (folder == null) continue;
                var folderPath = AssetDatabase.GetAssetPath(folder);
                var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { folderPath });
                foreach (var guid in guids)
                    allPaths.Add(AssetDatabase.GUIDToAssetPath(guid));
            }

            for (int i = 0; i < allPaths.Count; i++)
            {
                if (isCancelled != null && isCancelled()) break;

                var path = allPaths[i];
                onProgress?.Invoke((float)i / allPaths.Count, path);

                try
                {
                    var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                    if (importer == null) continue;

                    importer.GetSourceTextureWidthAndHeight(out int width, out int height);

                    bool boundsOk = true;
                    foreach (var rectDef in settings.spriteRects)
                    {
                        if (!ValidateRectBounds(rectDef, width, height, out string boundsError))
                        {
                            Debug.LogError($"[Sprite 批次設定] {Path.GetFileName(path)}: {boundsError}");
                            boundsOk = false;
                            break;
                        }
                    }
                    if (!boundsOk) { result.skippedPaths.Add(path); continue; }

                    importer.textureType         = TextureImporterType.Sprite;
                    importer.spriteImportMode    = SpriteImportMode.Multiple;
                    importer.filterMode          = settings.filterMode;
                    importer.alphaIsTransparency = settings.alphaIsTransparency;
                    importer.maxTextureSize      = settings.maxTextureSize;
                    importer.textureCompression  = settings.compression;

                    importer.spritesheet = BuildSpriteMetaData(
                        Path.GetFileNameWithoutExtension(path), settings.spriteRects);

                    importer.SaveAndReimport();
                    result.successCount++;
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[Sprite 批次設定] 處理失敗 {path}: {ex.Message}");
                    result.failedPaths.Add(path);
                }
            }

            return result;
        }
```

- [ ] **步驟 2：驗證編譯**

切換回 Unity，確認 Console 無錯誤，Test Runner 內現有測試仍為綠色。

- [ ] **步驟 3：Commit**

```bash
git add "Assets/Editor/SpriteBatchProcessor.cs"
git commit -m "feat: add ApplyToFolders with progress and error handling"
```

---

### Task 5：SpriteBatchWindow — 骨架與選單項目

**Files:**
- Create: `Assets/Editor/SpriteBatchWindow.cs`

- [ ] **步驟 1：建立 SpriteBatchWindow.cs 骨架**

```csharp
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace SpriteBatch
{
    public class SpriteBatchWindow : EditorWindow
    {
        private BatchSettings _settings = new BatchSettings();
        private ReorderableList _folderList;
        private ReorderableList _rectList;

        private List<string> _allTexturePaths = new List<string>();
        private int          _previewIndex    = 0;
        private Texture2D    _previewTexture;
        private Vector2      _scrollPos;

        [MenuItem("工具 (Tools)/Sprite 批次設定")]
        public static void ShowWindow()
        {
            var window = GetWindow<SpriteBatchWindow>("Sprite 批次設定");
            window.minSize = new Vector2(440, 620);
            window.Show();
        }

        private void OnEnable()
        {
            InitFolderList();
            InitRectList();
        }

        private void InitFolderList()
        {
            _folderList = new ReorderableList(
                _settings.targetFolders, typeof(DefaultAsset), true, true, true, true);
            _folderList.drawHeaderCallback = rect =>
                EditorGUI.LabelField(rect, "目標資料夾");
            _folderList.drawElementCallback = (rect, index, isActive, isFocused) =>
            {
                var r = new Rect(rect.x, rect.y + 2, rect.width, EditorGUIUtility.singleLineHeight);
                _settings.targetFolders[index] = (DefaultAsset)EditorGUI.ObjectField(
                    r, _settings.targetFolders[index], typeof(DefaultAsset), false);
            };
            _folderList.onAddCallback    = _ => { _settings.targetFolders.Add(null); RefreshTexturePaths(); };
            _folderList.onRemoveCallback = list => { _settings.targetFolders.RemoveAt(list.index); RefreshTexturePaths(); };
            _folderList.onChangedCallback = _ => RefreshTexturePaths();
        }

        private void InitRectList()
        {
            _rectList = new ReorderableList(
                _settings.spriteRects, typeof(SpriteRectDef), true, true, true, true);
            _rectList.drawHeaderCallback = rect =>
                EditorGUI.LabelField(rect, "Sprite 切割 (後綴 | x | y | 寬 | 高 | 錨點 X | 錨點 Y)");
            _rectList.elementHeight = EditorGUIUtility.singleLineHeight + 4;
            _rectList.drawElementCallback = DrawRectElement;
            _rectList.onAddCallback = _ => _settings.spriteRects.Add(new SpriteRectDef());
        }

        private void DrawRectElement(Rect rect, int index, bool isActive, bool isFocused)
        {
            var def = _settings.spriteRects[index];
            float x = rect.x, y = rect.y + 2, h = EditorGUIUtility.singleLineHeight;

            def.nameSuffix    = EditorGUI.TextField(    new Rect(x,  y, 38, h), def.nameSuffix);    x += 42;
            def.rect.x        = EditorGUI.FloatField(   new Rect(x,  y, 48, h), def.rect.x);        x += 52;
            def.rect.y        = EditorGUI.FloatField(   new Rect(x,  y, 48, h), def.rect.y);        x += 52;
            def.rect.width    = EditorGUI.FloatField(   new Rect(x,  y, 48, h), def.rect.width);    x += 52;
            def.rect.height   = EditorGUI.FloatField(   new Rect(x,  y, 48, h), def.rect.height);   x += 52;
            def.pivot.x       = EditorGUI.FloatField(   new Rect(x,  y, 38, h), def.pivot.x);       x += 42;
            def.pivot.y       = EditorGUI.FloatField(   new Rect(x,  y, 38, h), def.pivot.y);
        }

        private void OnGUI()
        {
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);
            EditorGUILayout.Space(4);

            DrawFolderSection();
            EditorGUILayout.Space(6);
            DrawTextureSettingsSection();
            EditorGUILayout.Space(6);
            DrawRectSection();
            EditorGUILayout.Space(6);
            DrawPreviewSection();
            EditorGUILayout.Space(6);
            DrawApplySection();

            EditorGUILayout.EndScrollView();
        }

        private void DrawFolderSection()   { _folderList.DoLayoutList(); }
        private void DrawRectSection()     { _rectList.DoLayoutList(); }

        private void DrawTextureSettingsSection()
        {
            EditorGUILayout.LabelField("貼圖設定 (Texture Settings)", EditorStyles.boldLabel);
            _settings.maxTextureSize      = EditorGUILayout.IntField(    "最大尺寸 (Max Size)",          _settings.maxTextureSize);
            _settings.filterMode          = (FilterMode)EditorGUILayout.EnumPopup("過濾模式 (Filter Mode)", _settings.filterMode);
            _settings.alphaIsTransparency = EditorGUILayout.Toggle(      "Alpha 透明度",                  _settings.alphaIsTransparency);
            _settings.compression         = (TextureImporterCompression)EditorGUILayout.EnumPopup(
                                                "壓縮 (Compression)", _settings.compression);
        }

        private void DrawPreviewSection()
        {
            EditorGUILayout.LabelField("預覽圖片 (Preview)", EditorStyles.boldLabel);
            if (_allTexturePaths.Count == 0)
            {
                EditorGUILayout.HelpBox("請先新增目標資料夾。", MessageType.Info);
                return;
            }

            var names    = _allTexturePaths.Select(p => Path.GetFileNameWithoutExtension(p)).ToArray();
            int newIndex = EditorGUILayout.Popup(_previewIndex, names);
            if (newIndex != _previewIndex || _previewTexture == null)
            {
                _previewIndex   = newIndex;
                _previewTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(_allTexturePaths[_previewIndex]);
            }

            if (_previewTexture == null) return;

            float maxW  = position.width - 24;
            float scale = Mathf.Min(1f, maxW / _previewTexture.width);
            float dispW = _previewTexture.width  * scale;
            float dispH = _previewTexture.height * scale;

            Rect previewRect = GUILayoutUtility.GetRect(dispW, dispH);
            previewRect.width = dispW;
            GUI.DrawTexture(previewRect, _previewTexture, ScaleMode.StretchToFill);

            foreach (var def in _settings.spriteRects)
            {
                float rx = previewRect.x + def.rect.x * scale;
                float ry = previewRect.y + (_previewTexture.height - def.rect.y - def.rect.height) * scale;
                float rw = def.rect.width  * scale;
                float rh = def.rect.height * scale;

                var overlay = new Rect(rx, ry, rw, rh);
                EditorGUI.DrawRect(overlay, new Color(1f, 1f, 0f, 0.15f));
                var border = new Color(1f, 1f, 0f, 0.9f);
                EditorGUI.DrawRect(new Rect(rx,          ry,          rw, 1),  border);
                EditorGUI.DrawRect(new Rect(rx,          ry + rh - 1, rw, 1),  border);
                EditorGUI.DrawRect(new Rect(rx,          ry,          1,  rh), border);
                EditorGUI.DrawRect(new Rect(rx + rw - 1, ry,          1,  rh), border);
            }
        }

        private void DrawApplySection()
        {
            int total       = _allTexturePaths.Count;
            int folderCount = _settings.targetFolders.Count(f => f != null);
            EditorGUILayout.LabelField($"將處理 {total} 張圖片（{folderCount} 個資料夾）");

            if (GUILayout.Button("套用全部 (Apply All)", GUILayout.Height(32)))
                ApplyAll();
        }

        private void RefreshTexturePaths()
        {
            _allTexturePaths.Clear();
            foreach (var folder in _settings.targetFolders)
            {
                if (folder == null) continue;
                var folderPath = AssetDatabase.GetAssetPath(folder);
                var guids      = AssetDatabase.FindAssets("t:Texture2D", new[] { folderPath });
                foreach (var guid in guids)
                    _allTexturePaths.Add(AssetDatabase.GUIDToAssetPath(guid));
            }
            _previewIndex   = 0;
            _previewTexture = null;
            Repaint();
        }

        private void ApplyAll()
        {
            var folderPaths = _settings.targetFolders
                .Where(f => f != null)
                .Select(f => AssetDatabase.GetAssetPath(f))
                .ToList();

            var errors = SpriteBatchProcessor.ValidatePreflight(folderPaths, _settings.spriteRects);
            if (errors.Count > 0)
            {
                EditorUtility.DisplayDialog("驗證失敗",
                    string.Join("\n", errors.Select(e => e.message)), "確認");
                return;
            }

            bool cancelled = false;
            var result = SpriteBatchProcessor.ApplyToFolders(
                _settings,
                (progress, path) =>
                {
                    cancelled = EditorUtility.DisplayCancelableProgressBar(
                        "Sprite 批次設定", Path.GetFileName(path), progress);
                },
                () => cancelled);

            EditorUtility.ClearProgressBar();

            string report = $"成功：{result.successCount} 張\n" +
                            $"跳過（尺寸不符）：{result.skippedPaths.Count} 張\n" +
                            $"失敗（其他錯誤）：{result.failedPaths.Count} 張";

            if (result.skippedPaths.Count > 0 || result.failedPaths.Count > 0)
            {
                report += "\n\n問題檔案：\n" +
                          string.Join("\n", result.skippedPaths.Concat(result.failedPaths));
            }

            EditorUtility.DisplayDialog("套用完成", report, "確認");
            RefreshTexturePaths();
        }
    }
}
```

- [ ] **步驟 2：驗證編譯並開啟視窗**

切換回 Unity，確認 Console 無錯誤。  
點選選單 `工具 (Tools) > Sprite 批次設定`，確認視窗開啟，看到資料夾列表、貼圖設定、切割列表、預覽區塊、套用按鈕。

- [ ] **步驟 3：手動測試 — 新增資料夾**

1. 在視窗中點 `+` 新增一筆資料夾
2. 將 `Assets/Sprites/Icon00_6_0win` 拖入
3. 確認底部顯示「將處理 60 張圖片（1 個資料夾）」
4. 確認預覽下拉選單列出圖片名稱，選取後圖片顯示正確

- [ ] **步驟 4：手動測試 — 新增切割並觀察預覽**

1. 在「Sprite 切割」區塊點 `+` 新增一筆
2. 填入：後綴 `_0`、x=0、y=463、寬=160、高=88
3. 確認預覽圖片上出現黃色框線，位置對應圖片頂部區域

- [ ] **步驟 5：手動測試 — Apply 驗證攔截**

1. 清空「目標資料夾」後點「套用全部」
2. 確認彈出「驗證失敗」對話框，訊息包含「資料夾」

- [ ] **步驟 6：手動測試 — 完整 Apply**

1. 恢復資料夾設定（Icon00_6_0win）
2. 加入切割：`_0` x=0 y=463 w=160 h=88 pivot=0.5,0.5
3. 點「套用全部」
4. 等待進度條完成
5. 確認對話框顯示「成功：60 張」
6. 在 Project 視窗選取任一圖片，確認 Inspector 中 Sprite Mode = Multiple，spritesheet 有 `_0` 切割

- [ ] **步驟 7：手動測試 — 尺寸不符攔截**

1. 新增切割：`_bad` x=0 y=0 w=9999 h=9999
2. 點「套用全部」
3. 確認結果對話框顯示「跳過（尺寸不符）：60 張」
4. 移除 `_bad` 切割

- [ ] **步驟 8：Commit**

```bash
git add "Assets/Editor/SpriteBatchWindow.cs"
git commit -m "feat: complete SpriteBatchWindow with folder list, preview, and apply flow"
```

---

## 完成確認清單

- [ ] `工具 (Tools) > Sprite 批次設定` 選單可開啟視窗
- [ ] 可新增/移除多個資料夾，底部即時顯示圖片數量
- [ ] 貼圖設定欄位（最大尺寸、過濾模式、Alpha 透明度、壓縮）正確顯示與編輯
- [ ] Sprite 切割列表可新增/移除/拖排，所有欄位可編輯
- [ ] 預覽圖片下拉選單列出所有目標圖片，選取後顯示圖片
- [ ] 黃色切割框線疊加在預覽圖片上，位置正確
- [ ] 空資料夾或空切割時 Apply 被攔截並顯示中文錯誤訊息
- [ ] 切割超出圖片邊界時，該圖片被跳過並記錄錯誤
- [ ] Apply 完成後顯示成功/跳過/失敗數量報告
- [ ] 所有 EditMode 測試（SpriteBatchProcessorTests）通過
