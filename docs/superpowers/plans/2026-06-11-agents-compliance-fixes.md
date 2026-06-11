# AGENTS Compliance Fixes Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Bring Sprite Batch Slicer closer to `AGENTS.md` requirements by adding EditMode coverage for core batch behavior, separating processor logic from UI side effects, reducing data-model coupling to UnityEditor importer enums, and fixing obvious `.editorconfig` violations.

**Architecture:** Keep the package Editor-only and avoid a broad UI rewrite. Add test-only asset creation helpers under `Tests/Editor`, introduce a package-level `BatchTextureCompression` enum in the data model, isolate Unity `TextureImporterCompression` conversion in a small Editor helper, and keep progress bar ownership in `SpriteBatchWindow`.

**Tech Stack:** Unity 2022.3.62f3, Unity Editor C#, Unity Test Framework EditMode/NUnit, AssetDatabase, TextureImporter, UnityEditor.U2D.Sprites, `.editorconfig`.

**Spec:** `docs/superpowers/specs/2026-06-11-agents-compliance-fixes-design.md`

---

## File Structure

| File | Responsibility |
|------|----------------|
| `Packages/com.wenrong.spritebatchslicer/Editor/SpriteBatchData.cs` | Batch settings and sprite rect data in the Editor assembly. Will own `BatchTextureCompression` so `BatchSettings` no longer stores `TextureImporterCompression`; `SpriteRectDef.Alignment` still uses Unity `SpriteAlignment`. |
| `Packages/com.wenrong.spritebatchslicer/Editor/SpriteBatchImporterOptions.cs` | New Editor-only adapter from package data enums to Unity importer enums. |
| `Packages/com.wenrong.spritebatchslicer/Editor/SpriteBatchProcessor.cs` | Batch import logic. Will use adapter and stop owning progress bar UI. |
| `Packages/com.wenrong.spritebatchslicer/Editor/SpriteBatchWindow.cs` | EditorWindow UI and progress bar ownership. Will keep progress UI in `ApplyAll`. |
| `Packages/com.wenrong.spritebatchslicer/Editor/SpriteBatchEditorUtils.cs` | Existing stateless editor helpers. Minor formatting only if touched. |
| `Packages/com.wenrong.spritebatchslicer/Tests/Editor/TestAssetFactory.cs` | New test-only helper to create and clean temporary PNG assets. |
| `Packages/com.wenrong.spritebatchslicer/Tests/Editor/SpriteBatchProcessorTests.cs` | Core processor, importer option, and texture path tests. |
| `Packages/com.wenrong.spritebatchslicer/Tests/Editor/SpriteBatchWindowTests.cs` | Utility tests and using-order cleanup. |

Temporary test assets must live under `Assets/Temp/SpriteBatchSlicerTests/`. Do not modify or rely on changed `Assets/Sprites/**/*.png.meta` files for this work.

---

## Verification Commands

Use the Unity Test Runner UI when working interactively:

```text
Window > General > Test Runner > EditMode > Run All
```

For batch verification on a standard Unity Hub install:

```powershell
& "C:\Program Files\Unity\Hub\Editor\2022.3.62f3\Editor\Unity.exe" -batchmode -projectPath "D:\Dev\SpriteBatchSlicer" -runTests -testPlatform EditMode -testResults "Temp\editmode-results.xml" -quit
```

Expected success: process exits `0`, and `Temp/editmode-results.xml` reports `failed="0"`.

If the Unity executable is installed elsewhere, use the Unity Test Runner UI and record the EditMode pass/fail result before committing.

---

### Task 1: Add Test Asset Factory

**Files:**

- Create: `Packages/com.wenrong.spritebatchslicer/Tests/Editor/TestAssetFactory.cs`
- Modify: `Packages/com.wenrong.spritebatchslicer/Tests/Editor/SpriteBatchProcessorTests.cs`

- [ ] **Step 1: Create test asset helper**

Create `Packages/com.wenrong.spritebatchslicer/Tests/Editor/TestAssetFactory.cs`:

```csharp
using System.IO;
using UnityEditor;
using UnityEngine;

namespace SpriteBatch.Tests
{
    internal static class TestAssetFactory
    {
        public const string TestRoot = "Assets/Temp/SpriteBatchSlicerTests";

        public static string CreatePng(string assetPath, int width, int height, Color color)
        {
            string fullPath = Path.GetFullPath(assetPath);
            string directory = Path.GetDirectoryName(fullPath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var pixels = new Color[width * height];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = color;
            }

            texture.SetPixels(pixels);
            texture.Apply();
            File.WriteAllBytes(fullPath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            return assetPath;
        }

        public static void DeleteTestRoot()
        {
            if (AssetDatabase.IsValidFolder(TestRoot))
            {
                AssetDatabase.DeleteAsset(TestRoot);
            }

            AssetDatabase.Refresh();
        }
    }
}
```

- [ ] **Step 2: Add cleanup hooks to processor tests**

In `Packages/com.wenrong.spritebatchslicer/Tests/Editor/SpriteBatchProcessorTests.cs`, inside `public class SpriteBatchProcessorTests`, add this near the top of the class:

```csharp
        [SetUp]
        public void SetUp()
        {
            TestAssetFactory.DeleteTestRoot();
        }

        [TearDown]
        public void TearDown()
        {
            TestAssetFactory.DeleteTestRoot();
        }
```

- [ ] **Step 3: Run existing EditMode tests**

Run:

```powershell
& "C:\Program Files\Unity\Hub\Editor\2022.3.62f3\Editor\Unity.exe" -batchmode -projectPath "D:\Dev\SpriteBatchSlicer" -runTests -testPlatform EditMode -testResults "Temp\task1-editmode-results.xml" -quit
```

Expected: existing tests pass. If the command cannot run locally, use Test Runner UI and confirm all existing EditMode tests pass.

- [ ] **Step 4: Commit**

```powershell
git add -- Packages/com.wenrong.spritebatchslicer/Tests/Editor/TestAssetFactory.cs Packages/com.wenrong.spritebatchslicer/Tests/Editor/TestAssetFactory.cs.meta Packages/com.wenrong.spritebatchslicer/Tests/Editor/SpriteBatchProcessorTests.cs
git commit -m "test: add temporary asset factory for editor tests"
```

If Unity has not generated `TestAssetFactory.cs.meta` yet, open Unity once and let it generate the meta before committing.

---

### Task 2: Decouple Compression Setting From Unity Importer Enum

**Files:**

- Modify: `Packages/com.wenrong.spritebatchslicer/Editor/SpriteBatchData.cs`
- Create: `Packages/com.wenrong.spritebatchslicer/Editor/SpriteBatchImporterOptions.cs`
- Modify: `Packages/com.wenrong.spritebatchslicer/Editor/SpriteBatchProcessor.cs`
- Modify: `Packages/com.wenrong.spritebatchslicer/Editor/SpriteBatchWindow.cs`
- Modify: `Packages/com.wenrong.spritebatchslicer/Tests/Editor/SpriteBatchProcessorTests.cs`

- [ ] **Step 1: Write failing importer option tests**

In `SpriteBatchProcessorTests.cs`, add this section after the `CollectTexturePaths` tests:

```csharp
        // --- SpriteBatchImporterOptions ---

        [Test]
        public void ToUnityCompression_CompressedHQ_回傳UnityCompressedHQ()
        {
            var result = SpriteBatchImporterOptions.ToUnityCompression(BatchTextureCompression.CompressedHQ);

            Assert.AreEqual(TextureImporterCompression.CompressedHQ, result);
        }

        [Test]
        public void ToUnityCompression_Uncompressed_回傳UnityUncompressed()
        {
            var result = SpriteBatchImporterOptions.ToUnityCompression(BatchTextureCompression.Uncompressed);

            Assert.AreEqual(TextureImporterCompression.Uncompressed, result);
        }
```

- [ ] **Step 2: Run tests and verify RED**

Run EditMode tests. Expected: compile fails because `SpriteBatchImporterOptions` and `BatchTextureCompression` do not exist.

- [ ] **Step 3: Update data model**

Replace `Packages/com.wenrong.spritebatchslicer/Editor/SpriteBatchData.cs` with. Keep `UnityEditor` because `SpriteRectDef.Alignment` still uses `SpriteAlignment`; this task only removes the `TextureImporterCompression` field from `BatchSettings`.

```csharp
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SpriteBatch
{
    public enum BatchTextureCompression
    {
        Uncompressed,
        Compressed,
        CompressedHQ,
        CompressedLQ,
    }

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
        public BatchTextureCompression Compression = BatchTextureCompression.Compressed;
        public List<SpriteRectDef> SpriteRects = new();
    }
}
```

- [ ] **Step 4: Add importer option adapter**

Create `Packages/com.wenrong.spritebatchslicer/Editor/SpriteBatchImporterOptions.cs`:

```csharp
using UnityEditor;

namespace SpriteBatch
{
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
}
```

- [ ] **Step 5: Update processor compression assignment**

In `SpriteBatchProcessor.ApplyToFolders`, replace:

```csharp
                        importer.textureCompression = settings.Compression;
```

with:

```csharp
                        importer.textureCompression = SpriteBatchImporterOptions.ToUnityCompression(settings.Compression);
```

- [ ] **Step 6: Update window compression popup**

In `SpriteBatchWindow.DrawTextureSettingsSection`, replace:

```csharp
            _settings.Compression = (TextureImporterCompression)EditorGUILayout.EnumPopup(
                                                "壓縮 (Compression)", _settings.Compression);
```

with:

```csharp
            _settings.Compression = (BatchTextureCompression)EditorGUILayout.EnumPopup(
                "壓縮 (Compression)", _settings.Compression);
```

- [ ] **Step 7: Run tests and verify GREEN**

Run EditMode tests. Expected: importer option tests pass and compile errors are gone.

- [ ] **Step 8: Commit**

```powershell
git add -- Packages/com.wenrong.spritebatchslicer/Editor/SpriteBatchData.cs Packages/com.wenrong.spritebatchslicer/Editor/SpriteBatchImporterOptions.cs Packages/com.wenrong.spritebatchslicer/Editor/SpriteBatchImporterOptions.cs.meta Packages/com.wenrong.spritebatchslicer/Editor/SpriteBatchProcessor.cs Packages/com.wenrong.spritebatchslicer/Editor/SpriteBatchWindow.cs Packages/com.wenrong.spritebatchslicer/Tests/Editor/SpriteBatchProcessorTests.cs
git commit -m "refactor: decouple batch compression from texture importer enum"
```

---

### Task 3: Cover Successful Apply And GUID Preservation

**Files:**

- Modify: `Packages/com.wenrong.spritebatchslicer/Tests/Editor/SpriteBatchProcessorTests.cs`

- [ ] **Step 1: Add required usings**

At the top of `SpriteBatchProcessorTests.cs`, ensure these using directives exist in this order:

```csharp
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;
```

- [ ] **Step 2: Add helper to read sprite IDs**

Inside `SpriteBatchProcessorTests`, near the bottom of the class before the closing brace, add:

```csharp
        private static Dictionary<string, GUID> ReadSpriteIds(string path)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            var factory = new SpriteDataProviderFactories();
            factory.Init();
            var dataProvider = factory.GetSpriteEditorDataProviderFromObject(importer);
            dataProvider.InitSpriteEditorDataProvider();

            return dataProvider.GetSpriteRects()
                .ToDictionary(rect => rect.name, rect => rect.spriteID);
        }
```

- [ ] **Step 3: Add successful apply characterization test**

Add this test in `SpriteBatchProcessorTests.cs`:

```csharp
        // --- ApplyToFolders ---

        [Test]
        public void ApplyToFolders_有效設定_套用TextureImporter與SpriteRects()
        {
            string path = TestAssetFactory.CreatePng(
                $"{TestAssetFactory.TestRoot}/apply_success.png", 64, 64, Color.white);
            var settings = new BatchSettings
            {
                FolderPaths = new List<string> { TestAssetFactory.TestRoot },
                MaxTextureSize = 512,
                FilterMode = FilterMode.Point,
                AlphaIsTransparency = false,
                Compression = BatchTextureCompression.Uncompressed,
                SpriteRects = new List<SpriteRectDef>
                {
                    new()
                    {
                        NameSuffix = "_idle",
                        Rect = new Rect(0, 0, 32, 32),
                        Pivot = new Vector2(0.5f, 0.5f),
                        Alignment = SpriteAlignment.Center
                    }
                }
            };

            var result = SpriteBatchProcessor.ApplyToFolders(settings);

            Assert.AreEqual(1, result.SuccessCount);
            Assert.AreEqual(0, result.SkippedPaths.Count);
            Assert.AreEqual(0, result.FailedPaths.Count);
            Assert.IsFalse(result.WasCancelled);

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            Assert.AreEqual(TextureImporterType.Sprite, importer.textureType);
            Assert.AreEqual(SpriteImportMode.Multiple, importer.spriteImportMode);
            Assert.AreEqual(FilterMode.Point, importer.filterMode);
            Assert.IsFalse(importer.alphaIsTransparency);
            Assert.AreEqual(512, importer.maxTextureSize);
            Assert.AreEqual(TextureImporterCompression.Uncompressed, importer.textureCompression);

            var sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToArray();
            Assert.IsTrue(sprites.Any(sprite => sprite.name == "apply_success_idle"));
        }
```

- [ ] **Step 4: Run test**

Run the single test via Test Runner UI or batch EditMode. Expected: PASS if current behavior is correct. If it fails, fix only the production behavior needed for this assertion.

- [ ] **Step 5: Add GUID preservation test**

Add this test:

```csharp
        [Test]
        public void ApplyToFolders_既有同名Sprite_保留SpriteGUID()
        {
            string path = TestAssetFactory.CreatePng(
                $"{TestAssetFactory.TestRoot}/guid_preserved.png", 64, 64, Color.white);
            var settings = new BatchSettings
            {
                FolderPaths = new List<string> { TestAssetFactory.TestRoot },
                SpriteRects = new List<SpriteRectDef>
                {
                    new()
                    {
                        NameSuffix = "_main",
                        Rect = new Rect(0, 0, 32, 32),
                        Pivot = new Vector2(0.5f, 0.5f),
                        Alignment = SpriteAlignment.Center
                    }
                }
            };

            var firstResult = SpriteBatchProcessor.ApplyToFolders(settings);
            var firstIds = ReadSpriteIds(path);

            var secondResult = SpriteBatchProcessor.ApplyToFolders(settings);
            var secondIds = ReadSpriteIds(path);

            Assert.AreEqual(1, firstResult.SuccessCount);
            Assert.AreEqual(1, secondResult.SuccessCount);
            Assert.IsTrue(firstIds.ContainsKey("guid_preserved_main"));
            Assert.AreEqual(firstIds["guid_preserved_main"], secondIds["guid_preserved_main"]);
        }
```

- [ ] **Step 6: Run test**

Run EditMode tests. Expected: GUID preservation test passes.

- [ ] **Step 7: Commit**

```powershell
git add -- Packages/com.wenrong.spritebatchslicer/Tests/Editor/SpriteBatchProcessorTests.cs
git commit -m "test: cover successful sprite batch apply and guid preservation"
```

---

### Task 4: Cover Skip And Cancel Paths, Then Remove Processor Progress UI

**Files:**

- Modify: `Packages/com.wenrong.spritebatchslicer/Tests/Editor/SpriteBatchProcessorTests.cs`
- Modify: `Packages/com.wenrong.spritebatchslicer/Editor/SpriteBatchProcessor.cs`

- [ ] **Step 1: Add bounds skip test**

Add this test:

```csharp
        [Test]
        public void ApplyToFolders_Rect超出圖片_跳過且不計成功()
        {
            string path = TestAssetFactory.CreatePng(
                $"{TestAssetFactory.TestRoot}/bounds_skip.png", 32, 32, Color.white);
            var settings = new BatchSettings
            {
                FolderPaths = new List<string> { TestAssetFactory.TestRoot },
                SpriteRects = new List<SpriteRectDef>
                {
                    new()
                    {
                        NameSuffix = "_too_large",
                        Rect = new Rect(0, 0, 64, 64)
                    }
                }
            };

            var result = SpriteBatchProcessor.ApplyToFolders(settings);

            Assert.AreEqual(0, result.SuccessCount);
            Assert.AreEqual(1, result.SkippedPaths.Count);
            Assert.AreEqual(path, result.SkippedPaths[0]);
            Assert.AreEqual(0, result.FailedPaths.Count);
        }
```

- [ ] **Step 2: Add cancel test**

Add this test:

```csharp
        [Test]
        public void ApplyToFolders_取消後_停止後續處理()
        {
            _ = TestAssetFactory.CreatePng(
                $"{TestAssetFactory.TestRoot}/cancel_00.png", 32, 32, Color.white);
            _ = TestAssetFactory.CreatePng(
                $"{TestAssetFactory.TestRoot}/cancel_01.png", 32, 32, Color.white);
            int progressCalls = 0;
            var settings = new BatchSettings
            {
                FolderPaths = new List<string> { TestAssetFactory.TestRoot },
                SpriteRects = new List<SpriteRectDef>
                {
                    new()
                    {
                        NameSuffix = "_main",
                        Rect = new Rect(0, 0, 16, 16)
                    }
                }
            };

            var result = SpriteBatchProcessor.ApplyToFolders(
                settings,
                (_, _) => progressCalls++,
                () => progressCalls > 0);

            Assert.IsTrue(result.WasCancelled);
            Assert.AreEqual(1, result.SuccessCount);
            Assert.AreEqual(1, progressCalls);
        }
```

- [ ] **Step 3: Run tests**

Run EditMode tests. Expected: both tests pass or expose current behavior mismatch. If behavior mismatches, adjust production code minimally to match the spec.

- [ ] **Step 4: Remove processor progress bar side effect**

In `SpriteBatchProcessor.ApplyToFolders`, replace:

```csharp
            finally
            {
                EditorUtility.DisplayProgressBar("Sprite 批次設定", "正在完成匯入...", 1f);
                AssetDatabase.StopAssetEditing();
            }
```

with:

```csharp
            finally
            {
                AssetDatabase.StopAssetEditing();
            }
```

- [ ] **Step 5: Run tests**

Run EditMode tests. Expected: all processor tests still pass; no production behavior outside UI progress display changes.

- [ ] **Step 6: Commit**

```powershell
git add -- Packages/com.wenrong.spritebatchslicer/Tests/Editor/SpriteBatchProcessorTests.cs Packages/com.wenrong.spritebatchslicer/Editor/SpriteBatchProcessor.cs
git commit -m "test: cover sprite batch skip and cancel paths"
```

---

### Task 5: Strengthen Utility Tests

**Files:**

- Modify: `Packages/com.wenrong.spritebatchslicer/Tests/Editor/SpriteBatchProcessorTests.cs`
- Modify: `Packages/com.wenrong.spritebatchslicer/Tests/Editor/SpriteBatchWindowTests.cs`

- [ ] **Step 1: Add CollectTexturePaths sorting and dedupe test**

Add this test to `SpriteBatchProcessorTests.cs`:

```csharp
        [Test]
        public void CollectTexturePaths_多資料夾重複結果_去重且排序()
        {
            string folderA = $"{TestAssetFactory.TestRoot}/BFolder";
            string folderB = $"{TestAssetFactory.TestRoot}/AFolder";
            string pathB = TestAssetFactory.CreatePng($"{folderA}/b_texture.png", 8, 8, Color.white);
            string pathA = TestAssetFactory.CreatePng($"{folderB}/a_texture.png", 8, 8, Color.white);

            var result = SpriteBatchProcessor.CollectTexturePaths(new List<string>
            {
                TestAssetFactory.TestRoot,
                folderA,
                folderB
            });

            Assert.AreEqual(2, result.Count);
            Assert.AreEqual(pathA, result[0]);
            Assert.AreEqual(pathB, result[1]);
        }
```

- [ ] **Step 2: Add empty dragged collection test**

Add this test to `SpriteBatchWindowTests.cs`:

```csharp
        [Test]
        public void FilterNewFolders_空拖曳集合_回傳空清單()
        {
            var result = SpriteBatchEditorUtils.FilterNewFolders(
                new List<DefaultAsset>(), System.Array.Empty<Object>());

            Assert.AreEqual(0, result.Count);
        }
```

- [ ] **Step 3: Run tests**

Run EditMode tests. Expected: utility tests pass.

- [ ] **Step 4: Commit**

```powershell
git add -- Packages/com.wenrong.spritebatchslicer/Tests/Editor/SpriteBatchProcessorTests.cs Packages/com.wenrong.spritebatchslicer/Tests/Editor/SpriteBatchWindowTests.cs
git commit -m "test: strengthen sprite batch utility coverage"
```

---

### Task 6: Fix Obvious EditorConfig Violations

**Files:**

- Modify: `Packages/com.wenrong.spritebatchslicer/Editor/SpriteBatchProcessor.cs`
- Modify: `Packages/com.wenrong.spritebatchslicer/Editor/SpriteBatchWindow.cs`
- Modify: `Packages/com.wenrong.spritebatchslicer/Tests/Editor/SpriteBatchProcessorTests.cs`
- Modify: `Packages/com.wenrong.spritebatchslicer/Tests/Editor/SpriteBatchWindowTests.cs`
- Modify: `Packages/com.wenrong.spritebatchslicer/Tests/Editor/TestAssetFactory.cs`
- Modify: `Packages/com.wenrong.spritebatchslicer/Editor/SpriteBatchImporterOptions.cs`

- [ ] **Step 1: Expand single-line struct**

In `SpriteBatchProcessor.cs`, replace:

```csharp
        public struct ValidationError { public string Message; }
```

with:

```csharp
        public struct ValidationError
        {
            public string Message;
        }
```

- [ ] **Step 2: Expand single-line method**

In `SpriteBatchWindow.cs`, replace:

```csharp
        private void DrawRectSection() { _rectList.DoLayoutList(); }
```

with:

```csharp
        private void DrawRectSection()
        {
            _rectList.DoLayoutList();
        }
```

- [ ] **Step 3: Fix using order in tests**

In `SpriteBatchProcessorTests.cs`, ensure the using block is:

```csharp
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;
```

In `SpriteBatchWindowTests.cs`, ensure the using block is:

```csharp
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
```

- [ ] **Step 4: Run tests**

Run EditMode tests. Expected: all tests pass after formatting-only changes.

- [ ] **Step 5: Commit**

```powershell
git add -- Packages/com.wenrong.spritebatchslicer/Editor/SpriteBatchProcessor.cs Packages/com.wenrong.spritebatchslicer/Editor/SpriteBatchWindow.cs Packages/com.wenrong.spritebatchslicer/Tests/Editor/SpriteBatchProcessorTests.cs Packages/com.wenrong.spritebatchslicer/Tests/Editor/SpriteBatchWindowTests.cs Packages/com.wenrong.spritebatchslicer/Tests/Editor/TestAssetFactory.cs Packages/com.wenrong.spritebatchslicer/Editor/SpriteBatchImporterOptions.cs
git commit -m "style: align sprite batch editor code with editorconfig"
```

---

### Task 7: Final Verification And Scope Check

**Files:**

- No required code changes.

- [ ] **Step 1: Run all EditMode tests**

Run:

```powershell
& "C:\Program Files\Unity\Hub\Editor\2022.3.62f3\Editor\Unity.exe" -batchmode -projectPath "D:\Dev\SpriteBatchSlicer" -runTests -testPlatform EditMode -testResults "Temp\final-editmode-results.xml" -quit
```

Expected: process exits `0`; result XML reports `failed="0"`.

- [ ] **Step 2: Check AGENTS compliance search**

Run:

```powershell
rg -n "TextureImporterCompression" Packages/com.wenrong.spritebatchslicer/Editor/SpriteBatchData.cs
rg -n "DisplayProgressBar|ClearProgressBar" Packages/com.wenrong.spritebatchslicer/Editor/SpriteBatchProcessor.cs
rg -n "public struct ValidationError \\{|private void DrawRectSection\\(\\) \\{" Packages/com.wenrong.spritebatchslicer/Editor
```

Expected:

- First command: no output.
- Second command: no output.
- Third command: no output.

- [ ] **Step 3: Check touched files only**

Run:

```powershell
git status --short
```

Expected:

- Touched code/test files from this plan may be modified or staged.
- Existing unrelated `Assets/Sprites/**/*.png.meta` changes may still appear.
- No new changes under `Assets/Sprites` caused by these tasks.

- [ ] **Step 4: Manual smoke test**

In Unity:

```text
Tools > Sprite 批次設定
```

Manual checks:

- Add a temporary folder with a test texture.
- Add one valid rect and apply.
- Confirm the texture becomes Sprite Multiple.
- Add an invalid rect larger than the texture and apply.
- Confirm the result dialog reports a skipped file.
- Cancel during a multi-texture apply.
- Confirm the progress bar closes.

- [ ] **Step 5: Commit final verification metadata if needed**

If Unity generated `.meta` files for `SpriteBatchImporterOptions.cs` or `TestAssetFactory.cs` and they were not committed earlier, commit them now:

```powershell
git add -- Packages/com.wenrong.spritebatchslicer/Editor/SpriteBatchImporterOptions.cs.meta Packages/com.wenrong.spritebatchslicer/Tests/Editor/TestAssetFactory.cs.meta
git commit -m "chore: add meta files for compliance fix helpers"
```

If those `.meta` files were already committed, skip this step.
