# Sprite Batch Slicer

Sprite Batch Slicer 是一個 Unity Editor 工具，用來把同一組 Sprite 切割設定批次套用到多個資料夾中的貼圖。它適合處理大量尺寸一致、命名規則一致的 PNG 或 Texture2D 資產，例如圖示集、角色幀圖、UI 圖塊等。

目前工具以 UPM embedded package 形式放在 `Packages/com.wenrong.spritebatchslicer`。

## 功能

- 從多個 Unity 專案資料夾收集 `Texture2D`。
- 批次設定貼圖為 Sprite / Multiple。
- 為每張貼圖套用多組切割矩形、名稱後綴、Pivot 和 Sprite Alignment。
- 可設定 Texture Importer 的 Max Size、Filter Mode、Alpha Is Transparency 和 Compression。
- 在套用前預覽切割框。
- 批次匯入時顯示可取消的進度條。
- 依既有 Sprite 名稱保留 `spriteID`，降低重新切割造成引用斷裂的風險。
- 跳過切割範圍超出圖片尺寸的貼圖，並在結果對話框列出問題檔案。

## 環境需求

- Unity `2022.3.62f3` 建議版本。
- Package metadata 最低 Unity 版本為 `2022.3`。
- 需要 Unity 2D Sprite 相關 Editor API。

## 使用方式

1. 在 Unity 開啟此專案。
2. 從選單開啟 `Tools > Sprite 批次設定`。
3. 將 Project 視窗中的目標資料夾拖曳到「目標資料夾」清單，或用清單的 `+` 按鈕新增。
4. 設定貼圖匯入選項：
   - `最大尺寸 (Max Size)`
   - `過濾模式 (Filter Mode)`
   - `Alpha 透明度`
   - `壓縮 (Compression)`
5. 在「Sprite 切割」清單新增一或多個切割區域。
6. 從預覽下拉選擇貼圖，確認切割框位置。
7. 按下 `套用全部 (Apply All)`。

## 切割設定

每個切割區域包含以下欄位：

- `後綴`: 會加在原始貼圖檔名後面，例如 `Icon_00` 搭配 `_0` 會產生 `Icon_00_0`。
- `X`, `Y`, `W`, `H`: Sprite rect 的像素座標與尺寸。
- `Pivot X`, `Pivot Y`: 自訂 Pivot。
- `對齊`: Unity 的 `SpriteAlignment`。選擇非 Custom 對齊時，工具會同步更新 Pivot。

套用前會檢查：

- 至少選取一個資料夾。
- 至少定義一個切割區域。
- 切割後綴不可重複。
- 每個切割區域必須有正數寬高，且不可超出圖片尺寸。

## 專案結構

```text
Packages/com.wenrong.spritebatchslicer/
  package.json
  Editor/
    SpriteBatchWindow.cs
    SpriteBatchProcessor.cs
    SpriteBatchData.cs
    SpriteBatchEditorUtils.cs
    com.wenrong.spritebatchslicer.Editor.asmdef
  Tests/Editor/
    SpriteBatchProcessorTests.cs
    SpriteBatchWindowTests.cs
    com.wenrong.spritebatchslicer.Tests.asmdef
Assets/Sprites/
  測試與範例貼圖資產
docs/superpowers/
  既有設計規格與實作計畫
```

## 測試

使用 Unity Test Runner 執行 EditMode 測試：

1. 開啟 `Window > General > Test Runner`。
2. 切到 `EditMode`。
3. 執行全部測試。

CI 也會在 GitHub Actions 透過 GameCI 執行 Unity EditMode 測試，設定檔位於 `.github/workflows/ci.yml`。

## 開發注意事項

- 主要功能都在 Editor assembly，請避免把 Editor-only API 移到 Runtime assembly。
- Unity asset path 請使用 `/` 作為分隔符。
- 修改或新增 Unity 資產與腳本時，請一併保留對應 `.meta` 檔。
- 不要提交 `Library/`、`Temp/`、`Logs/`、`UserSettings/` 等本機產物。
- 大量套用會修改目標貼圖的 import settings；操作前建議確認版本控制狀態。
