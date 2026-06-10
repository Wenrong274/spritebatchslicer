# Sprite 批次設定工具 — 設計規格
日期：2026-06-08

## 概述

一個 Unity Editor 工具，讓使用者定義一組 Sprite 切割設定，並一鍵批次套用到多個資料夾下的所有 PNG 貼圖。

## 目標

- 在一處設定 `SpriteMode = Multiple` 及各個 Sprite Rect（切割區域）
- 將相同設定套用到 N 個資料夾下的所有 PNG，只需點一次 Apply
- 套用前提供視覺預覽，套用後顯示完整結果報告

---

## 元件架構

```
Assets/Editor/
└── SpriteBatchWindow.cs    ← EditorWindow，UI 介面、資料與執行邏輯
```

所有設定儲存在 EditorWindow 的欄位中（session 期間有效）。視窗關閉後設定不保留，日後可升級為 ScriptableObject 持久化（見「不在本次範圍」）。

**視窗內部資料結構：**

| 欄位 | 型別 | 說明 |
|---|---|---|
| `targetFolders` | `List<DefaultAsset>` | 要處理的資料夾清單 |
| `maxTextureSize` | `int` | 最大貼圖尺寸，預設 2048 |
| `filterMode` | `FilterMode` | 過濾模式，預設 Bilinear |
| `alphaIsTransparency` | `bool` | Alpha 作為透明度，預設 true |
| `compression` | `TextureImporterCompression` | 壓縮設定，預設 Compressed |
| `spriteRects` | `List<SpriteRectDef>` | Sprite 切割定義清單 |

**SpriteRectDef**（巢狀資料類別）：

| 欄位 | 型別 | 說明 |
|---|---|---|
| `nameSuffix` | `string` | 附加在檔名後的後綴，必填，例如 `_0`、`_1` |
| `rect` | `Rect` | 切割區域（x, y, width, height，單位：像素） |
| `pivot` | `Vector2` | 正規化錨點（0–1） |
| `alignment` | `SpriteAlignment` | 錨點對齊方式 |

### SpriteBatchWindow（EditorWindow）

開啟路徑：`工具（Tools）> Sprite 批次設定`

UI 佈局：

```
┌─────────────────────────────────────────────┐
│  目標資料夾                                  │
│  [Icon00_6_0win]  [Icon01_6_0win]  [+新增]  │
├─────────────────────────────────────────────┤
│  貼圖設定 (Texture Settings)                 │
│  最大尺寸 (Max Size)：[2048]                 │
│  過濾模式 (Filter Mode)：[Bilinear]          │
│  Alpha 透明度：☑                             │
│  壓縮 (Compression)：[Compressed]            │
├─────────────────────────────────────────────┤
│  Sprite 切割 (Sprite Rects)                  │
│  後綴   x    y    寬    高    錨點           │
│  _0    0   463  160   88   0.5, 0.5         │
│  _1    0   409  160  170   0.0, 0.0         │
│  [+ 新增切割]                                │
├─────────────────────────────────────────────┤
│  預覽圖片：[Icon00_6_0win_00 ▼]             │
│  ┌─────────────────────────┐               │
│  │  圖片 + 切割框線疊加      │               │
│  └─────────────────────────┘               │
├─────────────────────────────────────────────┤
│  將處理 420 張圖片（7 個資料夾）              │
│  [套用全部 (Apply All)]                      │
└─────────────────────────────────────────────┘
```

- **目標資料夾**：可拖拉資料夾至此，或點 `+新增` 使用 Object Picker 選取
- 所有欄位的編輯直接寫入 ScriptableObject asset（透過 `SerializedObject`）
- **預覽圖片**：下拉選單列出所有目標資料夾中的 PNG；選取後，以 GUI 繪圖方式在圖片上疊加切割框線
- **圖片數量**標籤隨資料夾增減即時更新

---

## Apply 執行流程

1. **預飛驗證（Pre-flight）**
   - 至少選取一個資料夾
   - 至少定義一筆 SpriteRectDef
   - 若驗證失敗：顯示錯誤對話框並中止

2. **收集目標檔案**
   - 對每個資料夾執行 `AssetDatabase.FindAssets("t:Texture2D", folderPath)`

3. **顯示進度條**（`EditorUtility.DisplayProgressBar`，支援中途取消）

4. **逐圖處理**

   ```
   a. 取得該 PNG 的 TextureImporter
   b. 讀取圖片實際寬高（GetSourceTextureWidthAndHeight）
   c. 尺寸驗證（所有 SpriteRectDef）：
        若 rect.x + rect.width  > 圖片寬度 → 記錄錯誤，跳過
        若 rect.y + rect.height > 圖片高度 → 記錄錯誤，跳過
   d. 設定 TextureImporter 欄位：
        textureType         = Sprite
        spriteImportMode    = Multiple
        filterMode          = 設定檔.filterMode
        alphaIsTransparency = 設定檔.alphaIsTransparency
        maxTextureSize      = 設定檔.maxTextureSize
        textureCompression  = 設定檔.compression
   e. 組裝 SpriteMetaData[]：
        name = 檔名（不含副檔名）+ rectDef.nameSuffix
        rect、pivot、alignment 來自 rectDef
   f. importer.spritesheet = metadata
   g. importer.SaveAndReimport()
   ```

5. **完成報告對話框**
   ```
   成功：N 張
   跳過（尺寸不符）：M 張
   失敗（其他錯誤）：K 張
   [問題檔案路徑清單]
   ```

---

## 錯誤處理總表

| 情況 | 處理方式 |
|---|---|
| 資料夾內有非 Texture2D 檔案 | 靜默跳過 |
| Sprite Rect 超出圖片邊界 | `Debug.LogError` + 跳過該圖片 |
| `SaveAndReimport` 例外 | `Debug.LogWarning` + 跳過該圖片 |
| 使用者中途取消 | 立即停止，報告目前進度 |

---

## 不在本次範圍

- **ScriptableObject 持久化**：將設定存成 `.asset` 供跨 session 重用（日後升級）
- 從「範本圖片」讀取切割設定（可日後擴充）
- 個別圖片的自訂覆寫設定
- 遞迴處理子資料夾
- 批次套用的 Undo/Redo（Unity TextureImporter 變更不支援）
