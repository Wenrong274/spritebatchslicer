# UPM 轉換實作計畫

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 將 `Assets/Editor` 的工具程式碼轉換為本地 UPM 嵌入式套件 `com.wenrong.spritebatchslicer`，讓工具可獨立發布、跨專案共用。

**Architecture:** 在 `Packages/com.wenrong.spritebatchslicer/` 建立 UPM 嵌入式套件結構；以 `git mv` 保留歷史記錄搬移現有腳本；以新命名的 asmdef 取代舊的。Unity 會自動偵測 `Packages/` 子目錄中有 `package.json` 的套件，無需修改 `manifest.json`。

**Tech Stack:** Unity Package Manager (UPM), Assembly Definition (.asmdef), git mv

---

### Task 1: 建立 package.json 與目錄骨架

**Files:**
- Create: `Packages/com.wenrong.spritebatchslicer/package.json`

- [ ] **Step 1: 建立目錄結構**

```bash
mkdir -p Packages/com.wenrong.spritebatchslicer/Editor
mkdir -p Packages/com.wenrong.spritebatchslicer/Tests/Editor
```

- [ ] **Step 2: 建立 package.json**

建立 `Packages/com.wenrong.spritebatchslicer/package.json`，內容如下：

```json
{
  "name": "com.wenrong.spritebatchslicer",
  "version": "1.0.0",
  "displayName": "Sprite Batch Slicer",
  "description": "Unity Editor tool for batch-applying sprite slice settings across multiple folders.",
  "unity": "2022.3",
  "keywords": ["sprite", "batch", "slicer", "editor", "tool"]
}
```

- [ ] **Step 3: Commit**

```bash
git add Packages/com.wenrong.spritebatchslicer/package.json
git commit -m "chore: 建立 UPM Package 骨架與 package.json"
```

---

### Task 2: 移動 Editor 腳本、更新 asmdef 與 MenuItem

**Files:**
- Move: `Assets/Editor/SpriteBatchData.cs` + `.meta` → `Packages/com.wenrong.spritebatchslicer/Editor/`
- Move: `Assets/Editor/SpriteBatchProcessor.cs` + `.meta` → `Packages/com.wenrong.spritebatchslicer/Editor/`
- Move: `Assets/Editor/SpriteBatchWindow.cs` + `.meta` → `Packages/com.wenrong.spritebatchslicer/Editor/`
- Create: `Packages/com.wenrong.spritebatchslicer/Editor/com.wenrong.spritebatchslicer.Editor.asmdef`
- Delete: `Assets/Editor/SpriteBatchEditor.asmdef` + `.meta`
- Delete: `Assets/Editor.meta`

- [ ] **Step 1: git mv 三個 .cs 及其 .meta（保留 GUID）**

```bash
PKG="Packages/com.wenrong.spritebatchslicer/Editor"
for f in SpriteBatchData SpriteBatchProcessor SpriteBatchWindow; do
  git mv "Assets/Editor/${f}.cs"      "${PKG}/${f}.cs"
  git mv "Assets/Editor/${f}.cs.meta" "${PKG}/${f}.cs.meta"
done
```

- [ ] **Step 2: 建立新 asmdef**

建立 `Packages/com.wenrong.spritebatchslicer/Editor/com.wenrong.spritebatchslicer.Editor.asmdef`：

```json
{
    "name": "com.wenrong.spritebatchslicer.Editor",
    "rootNamespace": "SpriteBatch",
    "references": [
        "GUID:2c573e6b271651846a79655161004c5b"
    ],
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

（`GUID:2c573e6b271651846a79655161004c5b` 是 `com.unity.2d.sprite` 套件，與原 asmdef 相同。）

- [ ] **Step 3: 更新 MenuItem**

在 `Packages/com.wenrong.spritebatchslicer/Editor/SpriteBatchWindow.cs` 找到：

```csharp
[MenuItem("工具 (Tools)/Sprite 批次設定")]
```

改為：

```csharp
[MenuItem("GameTools/Sprite 批次設定")]
```

- [ ] **Step 4: 刪除舊 asmdef 及 Assets/Editor.meta**

```bash
git rm Assets/Editor/SpriteBatchEditor.asmdef
git rm Assets/Editor/SpriteBatchEditor.asmdef.meta
git rm Assets/Editor.meta
```

- [ ] **Step 5: Commit**

```bash
git add Packages/com.wenrong.spritebatchslicer/Editor/
git commit -m "feat: 移動 Editor 腳本至 UPM Package，更新 asmdef 與 MenuItem"
```

---

### Task 3: 移動 Tests 並更新 asmdef

**Files:**
- Move: `Assets/Tests/Editor/SpriteBatchProcessorTests.cs` + `.meta` → `Packages/com.wenrong.spritebatchslicer/Tests/Editor/`
- Create: `Packages/com.wenrong.spritebatchslicer/Tests/Editor/com.wenrong.spritebatchslicer.Tests.asmdef`
- Delete: `Assets/Tests/Editor/SpriteBatchTests.asmdef` + `.meta`
- Delete: `Assets/Tests/Editor.meta`, `Assets/Tests.meta`

- [ ] **Step 1: git mv 測試腳本及其 .meta**

```bash
PKG="Packages/com.wenrong.spritebatchslicer/Tests/Editor"
git mv Assets/Tests/Editor/SpriteBatchProcessorTests.cs      "${PKG}/SpriteBatchProcessorTests.cs"
git mv Assets/Tests/Editor/SpriteBatchProcessorTests.cs.meta "${PKG}/SpriteBatchProcessorTests.cs.meta"
```

- [ ] **Step 2: 建立新 Tests asmdef**

建立 `Packages/com.wenrong.spritebatchslicer/Tests/Editor/com.wenrong.spritebatchslicer.Tests.asmdef`：

```json
{
    "name": "com.wenrong.spritebatchslicer.Tests",
    "rootNamespace": "SpriteBatch.Tests",
    "references": [
        "com.wenrong.spritebatchslicer.Editor",
        "UnityEngine.TestRunner",
        "UnityEditor.TestRunner",
        "GUID:2c573e6b271651846a79655161004c5b"
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

（`references` 第一項改為名稱字串 `"com.wenrong.spritebatchslicer.Editor"`，無需 GUID。）

- [ ] **Step 3: 刪除舊 asmdef 及資料夾 meta**

```bash
git rm Assets/Tests/Editor/SpriteBatchTests.asmdef
git rm Assets/Tests/Editor/SpriteBatchTests.asmdef.meta
git rm Assets/Tests/Editor.meta
git rm Assets/Tests.meta
```

- [ ] **Step 4: Commit**

```bash
git add Packages/com.wenrong.spritebatchslicer/Tests/
git commit -m "feat: 移動 Tests 至 UPM Package，更新 asmdef"
```

---

### Task 4: 在 Unity Editor 驗證

- [ ] **Step 1: 開啟 Unity 確認無編譯錯誤**

開啟 Unity Editor，Console 視窗不應出現任何 CS 編譯錯誤。

- [ ] **Step 2: 確認 GameTools 選單出現**

Unity 選單列應有 `GameTools > Sprite 批次設定`。

- [ ] **Step 3: 確認 Test Runner 測試通過**

`Window > General > Test Runner > EditMode`，所有測試全部綠燈。

- [ ] **Step 4: 確認 Package Manager 顯示套件**

`Window > Package Manager > In Project`，應看到 `Sprite Batch Slicer 1.0.0`。

- [ ] **Step 5: Commit Unity 產生的 meta 檔案（若有）**

Unity 在處理套件時可能為 `Packages/com.wenrong.spritebatchslicer/` 下的檔案產生 `.meta`。

```bash
git status
# 確認只有 Packages/ 底下新增的 .meta，無其他意外變更
git add Packages/
git commit -m "chore: 加入 Unity 自動產生的 Package meta 檔案"
```
