# Sprite 批次設定 UI/UX 改善實作計畫

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 修正 SpriteBatchWindow 的 10 項 UX 問題（防呆輸入、彩色預覽框、相對路徑、Pivot 自動同步、Domain Reload 持久化等）。

**Architecture:** 所有改動集中在 `SpriteBatchWindow.cs`；新增 `SpriteBatchWindowState.cs`（ScriptableSingleton）負責 Domain Reload 持久化。`SpriteBatchProcessor.cs` 與 `SpriteBatchData.cs` 不動。

**Tech Stack:** Unity 2021.3+, IMGUI (EditorGUI / GUILayout), ReorderableList, ScriptableSingleton, NUnit EditMode Tests

---

## 涉及檔案

| 動作 | 路徑 |
|------|------|
| 修改 | `Packages/com.wenrong.spritebatchslicer/Editor/SpriteBatchWindow.cs` |
| 新增 | `Packages/com.wenrong.spritebatchslicer/Editor/SpriteBatchWindowState.cs` |
| 修改 | `Packages/com.wenrong.spritebatchslicer/Tests/Editor/SpriteBatchProcessorTests.cs` |

規格文件：`docs/superpowers/specs/2026-06-11-ux-improvements-design.md`

---

### Task 1: 三項高優先 UI 修改（MaxTextureSize、NameSuffix、預覽高度）

**Files:**

- Modify: `Packages/com.wenrong.spritebatchslicer/Editor/SpriteBatchWindow.cs`

這三項改動範圍小，無相依性，合為一個 commit。

- [ ] **Step 1: 在 `SpriteBatchWindow` 類別頂部加入兩組靜態常數**

在 `SpriteBatchWindow.cs` 的 `private BatchSettings _settings = new();` **上方**加入：

```csharp
private const float PreviewMaxHeight = 240f;
private static readonly int[]    MaxTextureSizeValues = { 32, 64, 128, 256, 512, 1024, 2048, 4096, 8192 };
private static readonly string[] MaxTextureSizeNames  = { "32", "64", "128", "256", "512", "1024", "2048", "4096", "8192" };
```

- [ ] **Step 2: 將 `DrawTextureSettingsSection` 的 IntField 改為 IntPopup**

找到 `DrawTextureSettingsSection` 方法，把：

```csharp
_settings.MaxTextureSize = EditorGUILayout.IntField("最大尺寸 (Max Size)", _settings.MaxTextureSize);
```

改為：

```csharp
_settings.MaxTextureSize = EditorGUILayout.IntPopup(
    "最大尺寸 (Max Size)", _settings.MaxTextureSize, MaxTextureSizeNames, MaxTextureSizeValues);
```

- [ ] **Step 3: 修改 `InitRectList` 的 `onAddCallback` 為自動遞增後綴**

找到 `InitRectList` 方法，把：

```csharp
onAddCallback = _ => _settings.SpriteRects.Add(new SpriteRectDef())
```

改為：

```csharp
onAddCallback = _ => _settings.SpriteRects.Add(new SpriteRectDef
{
    NameSuffix = $"_{_settings.SpriteRects.Count}"
})
```

- [ ] **Step 4: 修改 `DrawPreviewSection` 的縮放計算加入高度限制**

找到 `DrawPreviewSection` 方法，把：

```csharp
float scale = Mathf.Min(1f, maxW / _previewTexture.width);
```

改為：

```csharp
float scale = Mathf.Min(1f, maxW / _previewTexture.width, PreviewMaxHeight / _previewTexture.height);
```

- [ ] **Step 5: 確認 Unity 可以編譯（開啟 Unity Editor，確認 Console 無紅字）**

- [ ] **Step 6: Commit**

```bash
git add Packages/com.wenrong.spritebatchslicer/Editor/SpriteBatchWindow.cs
git commit -m "fix: MaxTextureSize 改 IntPopup，NameSuffix 自動遞增，預覽高度限制 240px"
```

---

### Task 2: Alignment 變更自動同步 Pivot（TDD）

**Files:**

- Modify: `Packages/com.wenrong.spritebatchslicer/Editor/SpriteBatchWindow.cs`
- Modify: `Packages/com.wenrong.spritebatchslicer/Tests/Editor/SpriteBatchProcessorTests.cs`

`AlignmentToPivot` 是純函式，先寫測試再實作。

- [ ] **Step 1: 在 `SpriteBatchWindowTests` 加入失敗的測試**

在 `SpriteBatchProcessorTests.cs` 的 `SpriteBatchWindowTests` 類別（現有類別，接著已有的測試往下加）加入：

```csharp
[Test]
public void AlignmentToPivot_TopLeft_回傳_0_1()
{
    var result = SpriteBatchWindow.AlignmentToPivot(SpriteAlignment.TopLeft, Vector2.zero);
    Assert.AreEqual(new Vector2(0f, 1f), result);
}

[Test]
public void AlignmentToPivot_BottomRight_回傳_1_0()
{
    var result = SpriteBatchWindow.AlignmentToPivot(SpriteAlignment.BottomRight, Vector2.zero);
    Assert.AreEqual(new Vector2(1f, 0f), result);
}

[Test]
public void AlignmentToPivot_Center_回傳_05_05()
{
    var result = SpriteBatchWindow.AlignmentToPivot(SpriteAlignment.Center, Vector2.zero);
    Assert.AreEqual(new Vector2(0.5f, 0.5f), result);
}

[Test]
public void AlignmentToPivot_Custom_不改變Pivot()
{
    var original = new Vector2(0.3f, 0.7f);
    var result = SpriteBatchWindow.AlignmentToPivot(SpriteAlignment.Custom, original);
    Assert.AreEqual(original, result);
}
```

- [ ] **Step 2: 在 Unity Test Runner 執行這 4 個測試，確認編譯失敗（AlignmentToPivot 不存在）**

開啟 Window > General > Test Runner，執行 `AlignmentToPivot_*` 測試。
預期：編譯錯誤 `'SpriteBatchWindow' does not contain a definition for 'AlignmentToPivot'`

- [ ] **Step 3: 在 `SpriteBatchWindow` 加入 `AlignmentToPivot` 公開靜態方法**

在 `SpriteBatchWindow` 類別中加入（位置：`FilterNewFolders` 方法旁邊）：

```csharp
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
```

- [ ] **Step 4: 執行 `AlignmentToPivot_*` 4 個測試，確認全部 PASS**

- [ ] **Step 5: 修改 `DrawRectElement`，在 Alignment 變更時呼叫 `AlignmentToPivot`**

找到 `DrawRectElement` 方法，把最後一行：

```csharp
def.Alignment = (SpriteAlignment)EditorGUI.EnumPopup(new Rect(x, y, 84, h), def.Alignment);
```

改為：

```csharp
var newAlignment = (SpriteAlignment)EditorGUI.EnumPopup(new Rect(x, y, 84, h), def.Alignment);
if (newAlignment != def.Alignment)
{
    def.Alignment = newAlignment;
    def.Pivot = AlignmentToPivot(newAlignment, def.Pivot);
}
```

- [ ] **Step 6: 確認 Unity 可以編譯（Console 無紅字）**

- [ ] **Step 7: Commit**

```bash
git add Packages/com.wenrong.spritebatchslicer/Editor/SpriteBatchWindow.cs
git add Packages/com.wenrong.spritebatchslicer/Tests/Editor/SpriteBatchProcessorTests.cs
git commit -m "feat: Alignment 變更自動同步 Pivot，新增 AlignmentToPivot 單元測試"
```

---

### Task 3: 切割框各自不同顏色 + 後綴標籤

**Files:**

- Modify: `Packages/com.wenrong.spritebatchslicer/Editor/SpriteBatchWindow.cs`

- [ ] **Step 1: 在 `SpriteBatchWindow` 類別頂部加入顏色調色盤常數**

在 Task 1 加入的 `PreviewMaxHeight` **下方**加入：

```csharp
private static readonly Color[] RectOverlayColors =
{
    new Color(0.298f, 0.686f, 0.314f), // Material Green 500
    new Color(0.129f, 0.588f, 0.953f), // Material Blue 500
    new Color(1.000f, 0.596f, 0.000f), // Material Orange 700
    new Color(0.957f, 0.263f, 0.212f), // Material Red 500
};
```

- [ ] **Step 2: 在 `DrawPreviewSection` 將 `foreach` 切割框迴圈替換為索引式 `for` 迴圈**

找到以下程式碼（`foreach` 開始到結尾 `}`）：

```csharp
foreach (var def in _settings.SpriteRects)
{
    float rx = previewRect.x + def.Rect.x * scale;
    float ry = previewRect.y + (_previewTexture.height - def.Rect.y - def.Rect.height) * scale;
    float rw = def.Rect.width * scale;
    float rh = def.Rect.height * scale;

    var overlay = new Rect(rx, ry, rw, rh);
    EditorGUI.DrawRect(overlay, new Color(1f, 1f, 0f, 0.15f));
    var border = new Color(1f, 1f, 0f, 0.9f);
    EditorGUI.DrawRect(new Rect(rx, ry, rw, 1), border);
    EditorGUI.DrawRect(new Rect(rx, ry + rh - 1, rw, 1), border);
    EditorGUI.DrawRect(new Rect(rx, ry, 1, rh), border);
    EditorGUI.DrawRect(new Rect(rx + rw - 1, ry, 1, rh), border);
}
```

整段替換為：

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
    EditorGUI.DrawRect(new Rect(rx,          ry,          rw,  1), c);
    EditorGUI.DrawRect(new Rect(rx,          ry + rh - 1, rw,  1), c);
    EditorGUI.DrawRect(new Rect(rx,          ry,          1,  rh), c);
    EditorGUI.DrawRect(new Rect(rx + rw - 1, ry,          1,  rh), c);
    labelStyle.normal.textColor = c;
    GUI.Label(new Rect(rx + 2, ry + 1, rw - 4, 14), def.NameSuffix, labelStyle);
}
```

- [ ] **Step 3: 確認 Unity 可以編譯**

- [ ] **Step 4: Commit**

```bash
git add Packages/com.wenrong.spritebatchslicer/Editor/SpriteBatchWindow.cs
git commit -m "feat: 切割框依索引套用彩色（綠藍橙紅循環）並顯示後綴標籤"
```

---

### Task 4: 標題欄對齊 + RefreshTexturePaths 重寫（相對路徑 + 保持預覽選擇）

**Files:**

- Modify: `Packages/com.wenrong.spritebatchslicer/Editor/SpriteBatchWindow.cs`

項目 6（標題欄）、7（相對路徑）、8（保持預覽選擇）合為一個 commit，因為 7 和 8 都在 `RefreshTexturePaths` 同一方法中。

- [ ] **Step 1: 修改 `InitRectList` 的 `drawHeaderCallback`，向右偏移 20px**

找到 `InitRectList` 方法中：

```csharp
drawHeaderCallback = rect =>
    EditorGUI.LabelField(rect, "Sprite 切割 (後綴 | X | Y | W | H | Pivot X | Pivot Y | 對齊)"),
```

改為：

```csharp
drawHeaderCallback = rect =>
    EditorGUI.LabelField(
        new Rect(rect.x + 20, rect.y, rect.width - 20, rect.height),
        "Sprite 切割 (後綴 | X | Y | W | H | Pivot X | Pivot Y | 對齊)"),
```

- [ ] **Step 2: 完整替換 `RefreshTexturePaths` 方法**

找到現有的 `RefreshTexturePaths` 方法（整段從 `private void RefreshTexturePaths()` 到對應的 `}`），整段替換為：

```csharp
private void RefreshTexturePaths()
{
    string preservedPath = _previewIndex < _allTexturePaths.Count
        ? _allTexturePaths[_previewIndex]
        : null;

    var pathSet = new HashSet<string>();
    foreach (var folder in _settings.TargetFolders)
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

    _allTexturePaths = new List<string>(pathSet);
    _previewNames = _allTexturePaths
        .Select(p =>
        {
            string ext = Path.GetExtension(p);
            return p.StartsWith("Assets/")
                ? p["Assets/".Length..^ext.Length]
                : Path.GetFileNameWithoutExtension(p);
        })
        .ToArray();

    int restored = preservedPath != null ? _allTexturePaths.IndexOf(preservedPath) : -1;
    _previewIndex = restored >= 0 ? restored : 0;
    _previewTexture = null;
    Repaint();
}
```

- [ ] **Step 3: 確認 Unity 可以編譯**

- [ ] **Step 4: Commit**

```bash
git add Packages/com.wenrong.spritebatchslicer/Editor/SpriteBatchWindow.cs
git commit -m "fix: 標題欄對齊、預覽改顯示相對路徑、新增資料夾不重置預覽選擇"
```

---

### Task 5: Domain Reload 持久化（ScriptableSingleton）

**Files:**

- Create: `Packages/com.wenrong.spritebatchslicer/Editor/SpriteBatchWindowState.cs`
- Modify: `Packages/com.wenrong.spritebatchslicer/Editor/SpriteBatchWindow.cs`

- [ ] **Step 1: 建立 `SpriteBatchWindowState.cs`**

在 `Packages/com.wenrong.spritebatchslicer/Editor/` 目錄下建立新檔案 `SpriteBatchWindowState.cs`，內容如下：

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

- [ ] **Step 2: 確認 Unity 可以編譯（Console 無紅字）**

- [ ] **Step 3: 在 `SpriteBatchWindow` 加入 `LoadState` 方法**

在 `SpriteBatchWindow` 的 `RefreshTexturePaths` 方法**前方**加入：

```csharp
private void LoadState()
{
    var s = SpriteBatchWindowState.instance;
    _settings.MaxTextureSize      = s.MaxTextureSize;
    _settings.FilterMode          = s.FilterMode;
    _settings.AlphaIsTransparency = s.AlphaIsTransparency;
    _settings.Compression         = s.Compression;
    _settings.SpriteRects         = new List<SpriteRectDef>(s.SpriteRects);
    _settings.TargetFolders.Clear();
    foreach (var path in s.FolderPaths)
    {
        var asset = AssetDatabase.LoadAssetAtPath<DefaultAsset>(path);
        if (asset != null)
        {
            _settings.TargetFolders.Add(asset);
        }
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
        if (folder != null)
        {
            s.FolderPaths.Add(AssetDatabase.GetAssetPath(folder));
        }
    }
    s.Save();
}
```

- [ ] **Step 4: 修改 `OnEnable` 加入 `LoadState()`；新增 `OnDisable` 呼叫 `SaveState()`**

找到現有的 `OnEnable` 方法：

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
}

private void OnDisable()
{
    SaveState();
}
```

**重要：** `LoadState()` 必須在 `InitFolderList()` / `InitRectList()` 之前呼叫，這樣 `ReorderableList` 會綁定到已還原資料的清單。

- [ ] **Step 5: 確認 Unity 可以編譯**

- [ ] **Step 6: 手動驗證 Domain Reload 持久化**

1. 開啟 `Tools/Sprite 批次設定`
2. 新增一個資料夾，新增兩個 Rect（後綴應自動為 `_0`、`_1`）
3. 儲存（按 Ctrl+S 讓 Unity 強制 Domain Reload，或在任意 .cs 檔案做任何修改後存檔）
4. 等待 Unity 重新編譯完成
5. 確認視窗仍顯示剛才設定的資料夾與 Rect

- [ ] **Step 7: Commit**

```bash
git add Packages/com.wenrong.spritebatchslicer/Editor/SpriteBatchWindowState.cs
git add Packages/com.wenrong.spritebatchslicer/Editor/SpriteBatchWindow.cs
git commit -m "feat: ScriptableSingleton 持久化，Domain Reload 後設定不遺失"
```

---

### Task 6: 空資料夾清單拖曳提示

**Files:**

- Modify: `Packages/com.wenrong.spritebatchslicer/Editor/SpriteBatchWindow.cs`

- [ ] **Step 1: 在 `DrawFolderSection` 加入空清單提示**

找到 `DrawFolderSection` 方法：

```csharp
private void DrawFolderSection()
{
    float listHeight = _folderList.GetHeight();
    Rect listRect = GUILayoutUtility.GetRect(0f, listHeight, GUILayout.ExpandWidth(true));
    _folderList.DoList(listRect);
    HandleFolderDrop(listRect);
}
```

替換為：

```csharp
private void DrawFolderSection()
{
    float listHeight = _folderList.GetHeight();
    Rect listRect = GUILayoutUtility.GetRect(0f, listHeight, GUILayout.ExpandWidth(true));
    _folderList.DoList(listRect);
    HandleFolderDrop(listRect);

    if (_settings.TargetFolders.Count == 0)
    {
        var hintRect = new Rect(listRect.x + 4, listRect.y + 21, listRect.width - 8, 36);
        EditorGUI.DrawRect(hintRect, new Color(1f, 1f, 1f, 0.03f));
        var style = new GUIStyle(EditorStyles.centeredGreyMiniLabel)
        {
            wordWrap = false,
            normal   = { textColor = new Color(0.6f, 0.6f, 0.6f) }
        };
        GUI.Label(hintRect, "將資料夾從 Project 窗口拖曳至此", style);
    }
}
```

- [ ] **Step 2: 確認 Unity 可以編譯**

- [ ] **Step 3: Commit**

```bash
git add Packages/com.wenrong.spritebatchslicer/Editor/SpriteBatchWindow.cs
git commit -m "feat: 空資料夾清單顯示拖曳提示文字"
```

---

## 完成後手動驗證清單

- [ ] MaxTextureSize 下拉選單顯示 32~8192，且只能選這些值
- [ ] 連續新增 3 個 Rect，後綴分別為 `_0`、`_1`、`_2`，不出現「重複後綴」錯誤
- [ ] 縮放後的直式圖片（如 512×2048）預覽高度不超過 240px
- [ ] 切換 Alignment 為 `TopLeft` 後 Pivot 自動變為 (0, 1)
- [ ] 切換 Alignment 為 `Custom` 後 Pivot 不變
- [ ] 4 個以上的 Rect 在預覽中顯示循環配色（第 5 個回到綠色）；每框左上角有後綴標籤
- [ ] 兩個資料夾各有 `hero.png`，下拉選單顯示 `FolderA/hero` 和 `FolderB/hero`
- [ ] 已選取某圖片後新增資料夾，預覽仍停留在原圖片
- [ ] Domain Reload（修改任一 .cs 存檔）後，資料夾清單與 Rect 設定恢復
- [ ] 空清單狀態下可見拖曳提示文字，拖入資料夾後提示消失
- [ ] Rect 清單標題欄文字與欄位對齊（向右偏移 20px）
- [ ] 全部 NUnit 測試（Test Runner）通過
