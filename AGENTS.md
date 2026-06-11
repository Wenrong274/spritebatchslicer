# AGENTS.md

本文件提供給在此 repo 工作的 AI agent 或自動化工程代理。請先讀完再修改程式碼或文件。

## 專案概要

這是 Unity Editor 工具專案，核心套件位於 `Packages/com.wenrong.spritebatchslicer`。工具入口是 Unity 選單 `Tools > Sprite 批次設定`，用途是將一組 Sprite slicing 與 TextureImporter 設定批次套用到多個資料夾的 Texture2D。

建議 Unity 版本是 `2022.3.62f3`。`package.json` 的最低 Unity 版本是 `2022.3`。

## 重要路徑

- `Packages/com.wenrong.spritebatchslicer/package.json`: UPM package metadata。
- `Packages/com.wenrong.spritebatchslicer/Editor/SpriteBatchWindow.cs`: EditorWindow UI 與使用者流程。
- `Packages/com.wenrong.spritebatchslicer/Editor/SpriteBatchProcessor.cs`: 掃描、驗證與批次套用 importer 設定。
- `Packages/com.wenrong.spritebatchslicer/Editor/SpriteBatchData.cs`: 可序列化資料模型。
- `Packages/com.wenrong.spritebatchslicer/Editor/SpriteBatchEditorUtils.cs`: Editor 輔助方法。
- `Packages/com.wenrong.spritebatchslicer/Tests/Editor`: NUnit EditMode tests。
- `Assets/Sprites`: 測試與範例貼圖資產。
- `docs/superpowers`: 既有設計規格與實作計畫，可作為歷史脈絡參考。

## 工作規則

- 優先保持 UPM package 結構，不要把套件程式搬回 `Assets/`。
- 此套件目前是 Editor-only。使用 `UnityEditor`、`AssetDatabase`、`TextureImporter`、`ReorderableList` 等 API 時，維持在 Editor assembly 內。
- 命名空間維持 `SpriteBatch`；測試命名空間維持 `SpriteBatch.Tests`。
- UI 目前使用繁體中文標籤，新增介面文字時保持同一語氣與語言。
- Unity asset path 使用 forward slash `/`，不要使用 Windows backslash。
- 修改腳本或 Unity 資產時，保留並提交相關 `.meta` 檔。
- 不要手動編輯或提交 `Library/`、`Temp/`、`Logs/`、`UserSettings/`。
- 不要在未確認需求時改動 `Assets/Sprites` 測試素材；很多 EditMode tests 依賴其中的範例資產。
- 若工作樹已有與任務無關的變更，不要回復它們。

## 功能開發規範

寫新功能、修 bug、重構或改變既有行為時，必須遵守以下規則：

- 遵守 TDD。先寫能描述目標行為的最小測試，確認測試因預期原因失敗，再寫最少量 production code 讓測試通過，最後才重構。不要先寫實作再補測試。
- 每個新增或改動的可測試行為都要有 EditMode 測試。若真的無法自動化測試，必須在回報中說明原因與手動驗證步驟。
- 遵守 SOLID。類別與方法維持單一責任；UI、資料模型、AssetDatabase 存取、TextureImporter 套用邏輯要保持清楚分層；新增擴充點時優先擴充既有抽象，不要把條件分支塞進大型方法。
- 偏好 Functional Programming 風格。把可獨立測試的邏輯寫成純函式；用明確輸入與回傳值表達資料轉換；避免隱藏狀態、副作用與共享 mutable state。Unity Editor API 的副作用應集中在邊界層。
- 使用 modern C#，但以 Unity 2022 支援的 C# 版本為界。可使用 pattern matching、switch expression、target-typed `new`、range/index operator、null coalescing、object/collection initializer 等既有 `.editorconfig` 允許的語法；不要使用 Unity 目前不支援的 C# 10+ / C# 12 語法，例如 file-scoped namespace、primary constructor、collection expression。
- 嚴格遵守 `.editorconfig`。包含明確存取修飾詞、Allman braces、block-scoped namespace、`System.*` using 優先排序、命名規則、null/pattern matching 偏好與行長限制。
- 新增 C# 檔案時，先確認放在正確 asmdef 範圍內，並讓 Unity 產生或保留對應 `.meta`。
- 重構時保持可觀察行為不變，除非任務明確要求行為變更；每次重構後都要重新跑相關測試。

## 測試與驗證

主要驗證方式是 Unity Test Runner 的 EditMode tests：

1. 在 Unity 開啟 `Window > General > Test Runner`。
2. 切到 `EditMode`。
3. 執行全部測試。

CI 設定在 `.github/workflows/ci.yml`，使用 `game-ci/unity-test-runner@v4` 與 Unity `2022.3.62f3` 跑 `editMode`。

若無法在目前環境啟動 Unity，至少要做靜態檢查：

- 確認 C# 檔案沒有殘留已移除欄位或方法名稱。
- 確認 asmdef references 仍正確。
- 確認新增檔案有對應 `.meta`，或在 Unity 開啟後補齊。

## 功能行為約束

- `ValidatePreflight` 應阻擋空資料夾、空切割清單與重複後綴。
- `ValidateRectBounds` 應阻擋負座標、零面積與超出圖片尺寸的 rect。
- `BuildSpriteRects` 應依 `assetFileName + NameSuffix` 命名，並盡可能保留既有同名 sprite 的 GUID。
- `CollectTexturePaths` 應接受資料夾 path 清單，回傳排序後、不重複的 Texture2D asset paths。
- `ApplyToFolders` 應以 `AssetDatabase.StartAssetEditing()` / `StopAssetEditing()` 包住批次 reimport，並在 `finally` 中停止 asset editing。

## 文件更新

修改使用者流程、選單位置、支援 Unity 版本、測試方式或套件結構時，請同步更新 `README.md` 和本文件。
