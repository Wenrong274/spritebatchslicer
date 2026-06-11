# Sprite Batch UX 狀態持久化實作計畫

> **給代理工作者（agentic workers）：** 必要子技能：使用 superpowers:subagent-driven-development（建議）或 superpowers:executing-plans 來逐項實作本計畫。步驟使用核取方塊（`- [ ]`）語法來追蹤。

**目標：** 透過在 Unity Domain Reload 之間保留 Sprite Batch 視窗設定，來完成剩餘的 UI/UX 規格缺口，同時修正過時的 UX 規格，使未來的工作能對準目前的架構。

**架構：** 請在 `codex/agents-compliance-fixes` 之上執行此計畫，或在該分支合併到 `main` 之後執行；本計畫假設 `BatchSettings.Compression` 使用 `BatchTextureCompression`。僅將 UI 階段狀態（session state）持久化到 `ScriptableSingleton` 中，將資料夾資產物件保留在視窗邊界內，並使用可測試的對應（mapping）方法，確保 Domain Reload 行為不依賴隱藏的可變狀態。

**技術堆疊：** Unity 2022.3.62f3、Unity Editor IMGUI、ScriptableSingleton、AssetDatabase、NUnit EditMode 測試、PowerShell、Git。

---

## 執行前置條件

- 在獨立的 worktree 中執行。
- 使用包含 AGENTS 合規性（compliance）工作的分支。預期的程式碼具有：
  - `Packages/com.wenrong.spritebatchslicer/Editor/SpriteBatchData.cs` 中有 `BatchTextureCompression`
  - `Packages/com.wenrong.spritebatchslicer/Editor/SpriteBatchImporterOptions.cs` 中有 `SpriteBatchImporterOptions`
  - `SpriteBatchProcessor.cs` 中沒有直接呼叫 `EditorUtility.DisplayProgressBar`

開始前請先驗證：

```powershell
rg -n "enum BatchTextureCompression|public BatchTextureCompression Compression" Packages\com.wenrong.spritebatchslicer\Editor\SpriteBatchData.cs
rg -n "class SpriteBatchImporterOptions" Packages\com.wenrong.spritebatchslicer\Editor\SpriteBatchImporterOptions.cs
rg -n "DisplayProgressBar" Packages\com.wenrong.spritebatchslicer\Editor\SpriteBatchProcessor.cs
```

預期結果：

- 第一個指令會印出 enum 以及 `BatchSettings.Compression` 這幾行。
- 第二個指令會印出 `SpriteBatchImporterOptions` 類別那一行。
- 第三個指令不輸出任何內容。

如果第一或第二個指令沒有輸出內容，請停止並 merge 或 rebase 到 `codex/agents-compliance-fixes` 之後再繼續。

在整個計畫中使用此 Unity EditMode 指令。不要加上 `-quit`；此專案使用 Unity Test Framework 1.1.33，其命令列執行器會警告若傳入 `-quit` 則不會執行測試。

```powershell
$projectPath = (Get-Location).Path
& "C:\Program Files\Unity\Hub\Editor\2022.3.62f3\Editor\Unity.exe" -batchmode -projectPath $projectPath -runTests -testPlatform EditMode -testResults "$projectPath\ux-state-results.xml" -logFile "$projectPath\ux-state-unity.log"
```

預期成功結果：

- 處理程序結束代碼（Process exits）為 `0`。
- `ux-state-results.xml` 包含 `result="Passed"` 且 `failed="0"`。
- 每次驗證後刪除 `ux-state-results.xml`，以免將其提交（commit）。

---

## 檔案結構

| 檔案 | 職責 |
|------|----------------|
| `docs/superpowers/specs/2026-06-11-ux-improvements-design.md` | 將原始 UX 規格標記為部分實作，並定義剩餘可執行的範圍。 |
| `Packages/com.wenrong.spritebatchslicer/Editor/SpriteBatchWindowState.cs` | 透過 `ScriptableSingleton` 將視窗狀態持久化至 Unity preferences；提供可測試的 `Capture` 與 `ApplyTo` 對應方法。 |
| `Packages/com.wenrong.spritebatchslicer/Editor/SpriteBatchEditorUtils.cs` | 在 AssetDatabase 邊界處新增資料夾資產/路徑轉換的輔助方法。 |
| `Packages/com.wenrong.spritebatchslicer/Editor/SpriteBatchWindow.cs` | 在 enable 時載入持久化狀態，在 disable 時儲存，並保持 `_folderAssets` 與 `BatchSettings.FolderPaths` 同步。 |
| `Packages/com.wenrong.spritebatchslicer/Tests/Editor/SpriteBatchWindowStateTests.cs` | 涵蓋狀態 capture/apply 行為與深拷貝（deep-copy）語意。 |
| `Packages/com.wenrong.spritebatchslicer/Tests/Editor/SpriteBatchWindowTests.cs` | 涵蓋資料夾路徑轉換的輔助方法。 |

在此計畫中，請勿修改 `Assets/Sprites/**/*.png.meta`。

---

### 任務 1：修正過時的 UX 規格

**檔案：**

- 修改：`docs/superpowers/specs/2026-06-11-ux-improvements-design.md`

- [ ] **步驟 1：在 `## 概要` 後方插入狀態備註**

在 `## 概要` 標題之後及現有概觀段落之前，加入此區塊：

```markdown
> **狀態更新 (2026-06-11)：** 項目 1-8 與 10 在經歷架構與 AGENTS 合規性工作後，已於目前的編輯器視窗中實作。保留此規格是為了提供歷史脈絡。剩餘可執行的 UX 範圍是項目 9 的 Domain Reload 持久化，以及修正舊有的目標資料夾欄位，將其參照更新為目前的 `_folderAssets` + `_settings.FolderPaths` 架構。
>
> **目前架構注意事項：** `SpriteBatchWindow` 擁有用於 UI 綁定的 `_folderAssets: List<DefaultAsset>`。`BatchSettings.FolderPaths` 儲存供 `SpriteBatchProcessor` 消耗的資產路徑。`AlignmentToPivot` 位於 `SpriteBatchEditorUtils` 而不是 `SpriteBatchWindow`。當此工作在 `codex/agents-compliance-fixes` 之上執行時，壓縮狀態會使用 `BatchTextureCompression`。
```

- [ ] **步驟 2：替換檔案職責表**

將 `**涉及檔案：**` 下方的現有表格替換為：

```markdown
| 動作 | 檔案 |
|------|------|
| 修改 | `Packages/com.wenrong.spritebatchslicer/Editor/SpriteBatchWindow.cs` |
| 新增 | `Packages/com.wenrong.spritebatchslicer/Editor/SpriteBatchWindowState.cs` |
| 修改 | `Packages/com.wenrong.spritebatchslicer/Editor/SpriteBatchEditorUtils.cs` |
| 新增 | `Packages/com.wenrong.spritebatchslicer/Tests/Editor/SpriteBatchWindowStateTests.cs` |
| 修改 | `Packages/com.wenrong.spritebatchslicer/Tests/Editor/SpriteBatchWindowTests.cs` |
| 不動 | `SpriteBatchProcessor.cs`、`SpriteBatchData.cs` |
```

- [ ] **步驟 3：替換 Domain Reload 區段中過時的術語**

在 `### 🟢 9. Domain Reload 持久化（ScriptableSingleton）` 區段中，替換所有出現的：

```text
_settings.TargetFolders
```

替換為：

```text
_folderAssets
```

接著將範例 `SpriteBatchWindowState` 程式碼中的這一行：

```csharp
public TextureImporterCompression Compression = TextureImporterCompression.Compressed;
```

替換為：

```csharp
public BatchTextureCompression Compression = BatchTextureCompression.Compressed;
```

- [ ] **步驟 4：提交文件修正**

```powershell
git add -- docs/superpowers/specs/2026-06-11-ux-improvements-design.md
git commit -m "docs: clarify remaining UX state persistence scope"
```

---

### 任務 2：新增狀態對應測試與 ScriptableSingleton

**檔案：**

- 新增：`Packages/com.wenrong.spritebatchslicer/Tests/Editor/SpriteBatchWindowStateTests.cs`
- 新增：`Packages/com.wenrong.spritebatchslicer/Editor/SpriteBatchWindowState.cs`

- [ ] **步驟 1：撰寫會失敗的狀態測試**

建立 `Packages/com.wenrong.spritebatchslicer/Tests/Editor/SpriteBatchWindowStateTests.cs`：

```csharp
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace SpriteBatch.Tests
{
    public class SpriteBatchWindowStateTests
    {
        [Test]
        public void Capture_設定與資料夾路徑_保存所有欄位()
        {
            var state = ScriptableObject.CreateInstance<SpriteBatchWindowState>();
            try
            {
                var settings = new BatchSettings
                {
                    MaxTextureSize = 512,
                    FilterMode = FilterMode.Point,
                    AlphaIsTransparency = false,
                    Compression = BatchTextureCompression.Uncompressed,
                    SpriteRects = new List<SpriteRectDef>
                    {
                        new()
                        {
                            NameSuffix = "_idle",
                            Rect = new Rect(1, 2, 16, 32),
                            Pivot = new Vector2(0.25f, 0.75f),
                            Alignment = SpriteAlignment.Custom
                        }
                    }
                };

                state.Capture(settings, new[]
                {
                    "Assets/Sprites/Icon00_6_0win",
                    "Assets/Sprites/Icon01_6_0win"
                });

                Assert.AreEqual(512, state.MaxTextureSize);
                Assert.AreEqual(FilterMode.Point, state.FilterMode);
                Assert.IsFalse(state.AlphaIsTransparency);
                Assert.AreEqual(BatchTextureCompression.Uncompressed, state.Compression);
                Assert.AreEqual(2, state.FolderPaths.Count);
                Assert.AreEqual("Assets/Sprites/Icon00_6_0win", state.FolderPaths[0]);
                Assert.AreEqual("Assets/Sprites/Icon01_6_0win", state.FolderPaths[1]);
                Assert.AreEqual(1, state.SpriteRects.Count);
                Assert.AreEqual("_idle", state.SpriteRects[0].NameSuffix);
                Assert.AreEqual(new Rect(1, 2, 16, 32), state.SpriteRects[0].Rect);
                Assert.AreEqual(new Vector2(0.25f, 0.75f), state.SpriteRects[0].Pivot);
                Assert.AreEqual(SpriteAlignment.Custom, state.SpriteRects[0].Alignment);
            }
            finally
            {
                Object.DestroyImmediate(state);
            }
        }

        [Test]
        public void ApplyTo_還原設定_不共用SpriteRect參考()
        {
            var sourceRect = new SpriteRectDef
            {
                NameSuffix = "_main",
                Rect = new Rect(0, 0, 32, 32),
                Pivot = new Vector2(0.5f, 0.5f),
                Alignment = SpriteAlignment.Center
            };
            var state = ScriptableObject.CreateInstance<SpriteBatchWindowState>();
            try
            {
                state.FolderPaths = new List<string> { "Assets/Sprites/Icon00_6_0win" };
                state.SpriteRects = new List<SpriteRectDef> { sourceRect };
                state.MaxTextureSize = 1024;
                state.FilterMode = FilterMode.Trilinear;
                state.AlphaIsTransparency = true;
                state.Compression = BatchTextureCompression.CompressedHQ;
                var settings = new BatchSettings();
                var folderPaths = new List<string>();

                state.ApplyTo(settings, folderPaths);

                Assert.AreEqual(1024, settings.MaxTextureSize);
                Assert.AreEqual(FilterMode.Trilinear, settings.FilterMode);
                Assert.IsTrue(settings.AlphaIsTransparency);
                Assert.AreEqual(BatchTextureCompression.CompressedHQ, settings.Compression);
                Assert.AreEqual(1, folderPaths.Count);
                Assert.AreEqual("Assets/Sprites/Icon00_6_0win", folderPaths[0]);
                Assert.AreEqual(1, settings.FolderPaths.Count);
                Assert.AreEqual("Assets/Sprites/Icon00_6_0win", settings.FolderPaths[0]);
                Assert.AreEqual(1, settings.SpriteRects.Count);
                Assert.AreNotSame(sourceRect, settings.SpriteRects[0]);
                settings.SpriteRects[0].NameSuffix = "_changed";
                Assert.AreEqual("_main", state.SpriteRects[0].NameSuffix);
            }
            finally
            {
                Object.DestroyImmediate(state);
            }
        }
    }
}
```

- [ ] **步驟 2：執行測試並驗證 RED (紅燈)**

執行：

```powershell
$projectPath = (Get-Location).Path
& "C:\Program Files\Unity\Hub\Editor\2022.3.62f3\Editor\Unity.exe" -batchmode -projectPath $projectPath -runTests -testPlatform EditMode -testResults "$projectPath\ux-state-red-results.xml" -logFile "$projectPath\ux-state-red-unity.log"
```

預期結果：因缺少 `SpriteBatchWindowState` 而發生編譯錯誤。

- [ ] **步驟 3：新增 `SpriteBatchWindowState`**

建立 `Packages/com.wenrong.spritebatchslicer/Editor/SpriteBatchWindowState.cs`：

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
        public BatchTextureCompression Compression = BatchTextureCompression.Compressed;

        public void Capture(BatchSettings settings, IEnumerable<string> folderPaths)
        {
            MaxTextureSize = settings.MaxTextureSize;
            FilterMode = settings.FilterMode;
            AlphaIsTransparency = settings.AlphaIsTransparency;
            Compression = settings.Compression;
            SpriteRects = CopySpriteRects(settings.SpriteRects);

            FolderPaths = new List<string>();
            foreach (string folderPath in folderPaths)
            {
                if (!string.IsNullOrEmpty(folderPath))
                {
                    FolderPaths.Add(folderPath);
                }
            }
        }

        public void ApplyTo(BatchSettings settings, List<string> folderPaths)
        {
            settings.MaxTextureSize = MaxTextureSize;
            settings.FilterMode = FilterMode;
            settings.AlphaIsTransparency = AlphaIsTransparency;
            settings.Compression = Compression;
            settings.SpriteRects = CopySpriteRects(SpriteRects);

            settings.FolderPaths.Clear();
            folderPaths.Clear();
            foreach (string folderPath in FolderPaths)
            {
                if (!string.IsNullOrEmpty(folderPath))
                {
                    settings.FolderPaths.Add(folderPath);
                    folderPaths.Add(folderPath);
                }
            }
        }

        public void SaveState()
        {
            Save(true);
        }

        private static List<SpriteRectDef> CopySpriteRects(IEnumerable<SpriteRectDef> spriteRects)
        {
            var result = new List<SpriteRectDef>();
            foreach (var rect in spriteRects)
            {
                result.Add(new SpriteRectDef
                {
                    NameSuffix = rect.NameSuffix,
                    Rect = rect.Rect,
                    Pivot = rect.Pivot,
                    Alignment = rect.Alignment
                });
            }
            return result;
        }
    }
}
```

- [ ] **步驟 4：執行測試並驗證 GREEN (綠燈)**

執行：

```powershell
$projectPath = (Get-Location).Path
& "C:\Program Files\Unity\Hub\Editor\2022.3.62f3\Editor\Unity.exe" -batchmode -projectPath $projectPath -runTests -testPlatform EditMode -testResults "$projectPath\ux-state-green-results.xml" -logFile "$projectPath\ux-state-green-unity.log"
```

預期結果：`ux-state-green-results.xml` 包含 `result="Passed"` 且 `failed="0"`。

- [ ] **步驟 5：提交 (Commit)**

```powershell
Remove-Item -LiteralPath ux-state-green-results.xml,ux-state-red-results.xml -ErrorAction SilentlyContinue
git add -- Packages/com.wenrong.spritebatchslicer/Editor/SpriteBatchWindowState.cs Packages/com.wenrong.spritebatchslicer/Editor/SpriteBatchWindowState.cs.meta Packages/com.wenrong.spritebatchslicer/Tests/Editor/SpriteBatchWindowStateTests.cs Packages/com.wenrong.spritebatchslicer/Tests/Editor/SpriteBatchWindowStateTests.cs.meta
git commit -m "feat: persist sprite batch window state data"
```

如果 Unity 尚未產生 `.meta` 檔案，請在 Unity 中開啟專案一次，並在提交前重新執行 GREEN 驗證。

---

### 任務 3：新增資料夾資產路徑轉換輔助方法

**檔案：**

- 修改：`Packages/com.wenrong.spritebatchslicer/Tests/Editor/SpriteBatchWindowTests.cs`
- 修改：`Packages/com.wenrong.spritebatchslicer/Editor/SpriteBatchEditorUtils.cs`

- [ ] **步驟 1：撰寫會失敗的輔助方法測試**

在 `SpriteBatchWindowTests.cs` 中，將這些測試加在 `FilterNewFolders_空拖曳集合_回傳空清單` 之後：

```csharp
        [Test]
        public void ToFolderPaths_資料夾資產清單_回傳AssetPath()
        {
            var folder = AssetDatabase.LoadAssetAtPath<DefaultAsset>("Assets/Sprites/Icon00_6_0win");
            Assume.That(folder, Is.Not.Null, "測試素材 Icon00_6_0win 不存在");

            var result = SpriteBatchEditorUtils.ToFolderPaths(new List<DefaultAsset> { folder, null });

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("Assets/Sprites/Icon00_6_0win", result[0]);
        }

        [Test]
        public void LoadFolderAssets_路徑清單_略過不存在路徑()
        {
            var result = SpriteBatchEditorUtils.LoadFolderAssets(new[]
            {
                "Assets/Sprites/Icon00_6_0win",
                "Assets/DoesNotExist"
            });

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("Assets/Sprites/Icon00_6_0win", AssetDatabase.GetAssetPath(result[0]));
        }
```

- [ ] **步驟 2：執行測試並驗證 RED (紅燈)**

執行 Unity EditMode 指令。預期結果：發生編譯錯誤，因為 `ToFolderPaths` 與 `LoadFolderAssets` 不存在。

- [ ] **步驟 3：實作輔助方法**

在 `Packages/com.wenrong.spritebatchslicer/Editor/SpriteBatchEditorUtils.cs` 中，將這些方法加在 `FilterNewFolders` 之後，並在 `AlignmentToPivot` 之前：

```csharp
        public static List<string> ToFolderPaths(IEnumerable<DefaultAsset> folders)
        {
            var result = new List<string>();
            foreach (var folder in folders)
            {
                if (folder == null)
                {
                    continue;
                }

                string path = AssetDatabase.GetAssetPath(folder);
                if (!string.IsNullOrEmpty(path))
                {
                    result.Add(path);
                }
            }
            return result;
        }

        public static List<DefaultAsset> LoadFolderAssets(IEnumerable<string> folderPaths)
        {
            var result = new List<DefaultAsset>();
            foreach (string folderPath in folderPaths)
            {
                var asset = AssetDatabase.LoadAssetAtPath<DefaultAsset>(folderPath);
                if (asset != null)
                {
                    result.Add(asset);
                }
            }
            return result;
        }
```

- [ ] **步驟 4：執行測試並驗證 GREEN (綠燈)**

執行 Unity EditMode 指令。預期結果：所有測試通過，且 `failed="0"`。

- [ ] **步驟 5：提交 (Commit)**

```powershell
Remove-Item -LiteralPath ux-state-results.xml -ErrorAction SilentlyContinue
git add -- Packages/com.wenrong.spritebatchslicer/Editor/SpriteBatchEditorUtils.cs Packages/com.wenrong.spritebatchslicer/Tests/Editor/SpriteBatchWindowTests.cs
git commit -m "test: cover sprite batch window folder path helpers"
```

---

### 任務 4：將 SpriteBatchWindow 與持久化狀態綁定

**檔案：**

- 修改：`Packages/com.wenrong.spritebatchslicer/Editor/SpriteBatchWindow.cs`

- [ ] **步驟 1：新增生命週期方法**

替換目前的 `OnEnable` 方法：

```csharp
        private void OnEnable()
        {
            InitFolderList();
            InitRectList();
        }
```

替換為：

```csharp
        private void OnEnable()
        {
            LoadState();
            InitFolderList();
            InitRectList();
            RefreshTexturePaths();
        }

        private void OnDisable()
        {
            SaveState();
        }
```

- [ ] **步驟 2：新增狀態載入/儲存方法**

在 `RefreshTexturePaths` 之前新增這些方法：

```csharp
        private void LoadState()
        {
            var folderPaths = new List<string>();
            SpriteBatchWindowState.instance.ApplyTo(_settings, folderPaths);
            _folderAssets = SpriteBatchEditorUtils.LoadFolderAssets(folderPaths);
            SyncFolderPathsFromAssets();
        }

        private void SaveState()
        {
            SyncFolderPathsFromAssets();
            SpriteBatchWindowState.instance.Capture(_settings, _settings.FolderPaths);
            SpriteBatchWindowState.instance.SaveState();
        }

        private void SyncFolderPathsFromAssets()
        {
            _settings.FolderPaths.Clear();
            foreach (string path in SpriteBatchEditorUtils.ToFolderPaths(_folderAssets))
            {
                _settings.FolderPaths.Add(path);
            }
        }
```

- [ ] **步驟 3：簡化 `RefreshTexturePaths` 的資料夾同步**

在 `RefreshTexturePaths` 中，將此區塊：

```csharp
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
```

替換為：

```csharp
            SyncFolderPathsFromAssets();
```

- [ ] **步驟 4：執行測試**

執行 Unity EditMode 指令。預期結果：所有測試通過，且 `failed="0"`。

- [ ] **步驟 5：手動測試 Domain Reload 冒煙測試**

在 Unity 中：

```text
Tools > Sprite 批次設定
```

手動檢查事項：

- 新增 `Assets/Sprites/Icon00_6_0win` 至資料夾清單中。
- 新增兩個 sprite rects。其後綴（suffixes）應為 `_0` 與 `_1`。
- 將 Max Size 變更為 `512`，Filter Mode 變更為 `Point`，Compression 變更為 `Uncompressed`。
- 編輯並儲存任何 C# 檔案來觸發腳本 Domain Reload，然後等待編譯完成。
- 重新開啟 `Tools > Sprite 批次設定`。
- 確認資料夾清單、兩個 rects 及紋理（texture）設定都已還原。
- 確認重新載入後預覽清單已填入資料。

- [ ] **步驟 6：提交 (Commit)**

```powershell
Remove-Item -LiteralPath ux-state-results.xml -ErrorAction SilentlyContinue
git add -- Packages/com.wenrong.spritebatchslicer/Editor/SpriteBatchWindow.cs
git commit -m "feat: restore sprite batch window after domain reload"
```

---

### 任務 5：最終驗證與範圍檢查

**檔案：**

- 無需修改程式碼。

- [ ] **步驟 1：執行所有 EditMode 測試**

執行：

```powershell
$projectPath = (Get-Location).Path
& "C:\Program Files\Unity\Hub\Editor\2022.3.62f3\Editor\Unity.exe" -batchmode -projectPath $projectPath -runTests -testPlatform EditMode -testResults "$projectPath\ux-state-final-results.xml" -logFile "$projectPath\ux-state-final-unity.log"
```

預期結果：`ux-state-final-results.xml` 包含 `result="Passed"` 且 `failed="0"`。

- [ ] **步驟 2：檢查過時的 UX 規格參照**

執行：

```powershell
rg -n "TargetFolders|TextureImporterCompression|SpriteBatchWindow\.AlignmentToPivot" docs\superpowers\specs\2026-06-11-ux-improvements-design.md docs\superpowers\plans\2026-06-11-ux-improvements.md
```

預期結果：

- `docs/superpowers/specs/2026-06-11-ux-improvements-design.md` 中沒有舊的 `TargetFolders`，沒有 `TextureImporterCompression`，也沒有 `SpriteBatchWindow.AlignmentToPivot`。
- 若舊有歷史計畫被刻意保留，裡面可能還留有過時的參照。若舊計畫中仍有過時的參照，請在 `docs/superpowers/plans/2026-06-11-ux-improvements.md` 頂部標題後方加入這行並提交：

```markdown
> **歷史計畫：** 此計畫早於架構與 AGENTS 合規性變更。請使用 `docs/superpowers/plans/2026-06-11-ux-state-persistence.md` 處理剩餘的可執行 UX 工作。
```

- [ ] **步驟 3：檢查 worktree 範圍**

執行：

```powershell
git status --short
git diff --stat
```

預期結果：

- 只有在計畫範圍內的檔案被修改或提交。
- `Assets/Sprites/**/*.png.meta` 檔案並未在此工作中被更動。
- 產生的結果 XML 與 Unity logs 均未被加入暫存區（staged）。

- [ ] **步驟 4：若有需要，提交最終的歷史計畫備註**

若步驟 2 中有新增歷史計畫備註，請執行：

```powershell
git add -- docs/superpowers/plans/2026-06-11-ux-improvements.md
git commit -m "docs: mark old UX plan as historical"
```

若步驟 2 未修改舊計畫，請略過此提交。
