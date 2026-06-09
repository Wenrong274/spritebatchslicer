# UPM 轉換設計文件

## 目標

將 `Assets/Editor` 的工具程式碼轉換為本地 UPM Package，
讓工具可以獨立發布、跨專案共用。

---

## 命名規格

| 項目 | 值 |
|------|-----|
| Package name | `com.wenrong.spritebatchslicer` |
| displayName | `Sprite Batch Slicer` |
| version | `1.0.0` |
| unity 最低版本 | `2022.3` |
| asmdef (Editor) | `com.wenrong.spritebatchslicer.Editor` |
| asmdef (Tests) | `com.wenrong.spritebatchslicer.Tests` |
| MenuItem | `GameTools/Sprite 批次設定` |

---

## 目標目錄結構

```
Packages/
└── com.wenrong.spritebatchslicer/
    ├── package.json
    ├── Editor/
    │   ├── SpriteBatchData.cs
    │   ├── SpriteBatchData.cs.meta
    │   ├── SpriteBatchProcessor.cs
    │   ├── SpriteBatchProcessor.cs.meta
    │   ├── SpriteBatchWindow.cs
    │   ├── SpriteBatchWindow.cs.meta
    │   ├── com.wenrong.spritebatchslicer.Editor.asmdef
    │   └── com.wenrong.spritebatchslicer.Editor.asmdef.meta
    └── Tests/
        └── Editor/
            ├── SpriteBatchProcessorTests.cs
            ├── SpriteBatchProcessorTests.cs.meta
            ├── com.wenrong.spritebatchslicer.Tests.asmdef
            └── com.wenrong.spritebatchslicer.Tests.asmdef.meta
```

`Assets/` 保留：`Scenes/`、`Sprites/`（範例素材留在 Assets，不移入 Samples~）。

---

## 需要異動的檔案

### 新增
- `Packages/com.wenrong.spritebatchslicer/package.json`
- `Packages/com.wenrong.spritebatchslicer/Editor/com.wenrong.spritebatchslicer.Editor.asmdef`
- `Packages/com.wenrong.spritebatchslicer/Tests/Editor/com.wenrong.spritebatchslicer.Tests.asmdef`

### 移動（git mv）
- `Assets/Editor/*.cs` → `Packages/com.wenrong.spritebatchslicer/Editor/`
- `Assets/Tests/Editor/*.cs` → `Packages/com.wenrong.spritebatchslicer/Tests/Editor/`
- `Assets/Sprites/` → `Packages/com.wenrong.spritebatchslicer/Samples~/SampleSprites/`

### 修改
- `Packages/manifest.json` — 加入 `"com.wenrong.spritebatchslicer": "file:com.wenrong.spritebatchslicer"`
- `Assets/Editor/SpriteBatchWindow.cs` — MenuItem 改為 `GameTools/Sprite 批次設定`

### 刪除
- `Assets/Editor/SpriteBatchEditor.asmdef` + `.meta`（以新 asmdef 取代）
- `Assets/Tests/Editor/SpriteBatchTests.asmdef` + `.meta`（以新 asmdef 取代）
- `Assets/Editor.meta`、`Assets/Tests.meta`（資料夾移除後）

---

## package.json 內容

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

---

## asmdef 變更

### Editor asmdef（新檔案）
`com.wenrong.spritebatchslicer.Editor.asmdef`
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

### Tests asmdef（新檔案）
`com.wenrong.spritebatchslicer.Tests.asmdef`
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

---

## 範疇外（不在此次改動）

- 發布至 OpenUPM 或私有 Registry
- CHANGELOG.md、LICENSE.md
- CI/CD 自動化發布
