using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace SpriteBatch
{
    public static class SpriteBatchProcessor
    {
        public struct ValidationError { public string Message; }

        public static List<ValidationError> ValidatePreflight(List<string> folderPaths, List<SpriteRectDef> spriteRects)
        {
            List<ValidationError> errors = new();
            if (folderPaths is null || folderPaths.Count == 0)
            {
                errors.Add(new ValidationError { Message = "至少需要選取一個目標資料夾。" });
            }

            if (spriteRects is null || spriteRects.Count == 0)
            {
                errors.Add(new ValidationError { Message = "至少需要定義一個 Sprite 切割區域。" });
            }
            else
            {
                HashSet<string> seen = new();
                foreach (var rect in spriteRects)
                {
                    if (!seen.Add(rect.NameSuffix))
                    {
                        errors.Add(new ValidationError { Message = $"Sprite 切割後綴 '{rect.NameSuffix}' 重複，每個後綴必須唯一。" });
                        break;
                    }
                }
            }
            return errors;
        }

        public static bool ValidateRectBounds(SpriteRectDef rectDef, int imageWidth, int imageHeight, out string error)
        {
            error = null;
            if (rectDef.Rect.width <= 0 || rectDef.Rect.height <= 0)
            {
                error = $"切割區域 '{rectDef.NameSuffix}' 的寬度和高度必須大於零。";
                return false;
            }
            if (rectDef.Rect.x < 0 || rectDef.Rect.y < 0)
            {
                error = $"切割區域 '{rectDef.NameSuffix}' 的起點座標不可為負值。";
                return false;
            }
            if (rectDef.Rect.x + rectDef.Rect.width > imageWidth)
            {
                error = $"切割區域 '{rectDef.NameSuffix}' 超出圖片寬度（{imageWidth}px）。";
                return false;
            }
            if (rectDef.Rect.y + rectDef.Rect.height > imageHeight)
            {
                error = $"切割區域 '{rectDef.NameSuffix}' 超出圖片高度（{imageHeight}px）。";
                return false;
            }
            return true;
        }

        public struct ApplyResult
        {
            public int SuccessCount;
            public List<string> SkippedPaths;  // 尺寸不符
            public List<string> FailedPaths;   // 其他錯誤
            public bool WasCancelled;
        }

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

        public static ApplyResult ApplyToFolders(
            BatchSettings settings,
            Action<float, string> onProgress = null,
            Func<bool> isCancelled = null)
        {
            var result = new ApplyResult
            {
                SkippedPaths = new List<string>(),
                FailedPaths = new List<string>()
            };

            var allPaths = CollectTexturePaths(settings.FolderPaths);

            // Batch all SaveAndReimport calls; StopAssetEditing triggers a single unified import pass.
            AssetDatabase.SaveAssets();
            AssetDatabase.StartAssetEditing();
            try
            {
                for (int i = 0; i < allPaths.Count; i++)
                {
                    if (isCancelled is not null && isCancelled())
                    {
                        result.WasCancelled = true;
                        break;
                    }

                    string path = allPaths[i];
                    onProgress?.Invoke((i + 1f) / allPaths.Count, path);

                    try
                    {
                        if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
                        {
                            Debug.LogWarning($"[Sprite 批次設定] 無法取得 TextureImporter：{path}");
                            result.FailedPaths.Add(path);
                            continue;
                        }

                        importer.GetSourceTextureWidthAndHeight(out int width, out int height);

                        bool boundsOk = true;
                        foreach (var rectDef in settings.SpriteRects)
                        {
                            if (!ValidateRectBounds(rectDef, width, height, out string boundsError))
                            {
                                Debug.LogError($"[Sprite 批次設定] {Path.GetFileName(path)}: {boundsError}");
                                boundsOk = false;
                                break;
                            }
                        }
                        if (!boundsOk)
                        {
                            result.SkippedPaths.Add(path);
                            continue;
                        }

                        importer.textureType = TextureImporterType.Sprite;
                        importer.spriteImportMode = SpriteImportMode.Multiple;
                        importer.filterMode = settings.FilterMode;
                        importer.alphaIsTransparency = settings.AlphaIsTransparency;
                        importer.maxTextureSize = settings.MaxTextureSize;
                        importer.textureCompression = settings.Compression;

                        var factory = new SpriteDataProviderFactories();
                        factory.Init();
                        var dataProvider = factory.GetSpriteEditorDataProviderFromObject(importer);
                        dataProvider.InitSpriteEditorDataProvider();

                        var existingRects = dataProvider.GetSpriteRects();
                        var existingIds = new Dictionary<string, GUID>();
                        foreach (var r in existingRects)
                        {
                            existingIds[r.name] = r.spriteID;
                        }

                        dataProvider.SetSpriteRects(BuildSpriteRects(
                            Path.GetFileNameWithoutExtension(path), settings.SpriteRects, existingIds));

                        dataProvider.Apply();

                        importer.SaveAndReimport();
                        result.SuccessCount++;
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[Sprite 批次設定] 處理失敗 {path}: {ex.Message}");
                        result.FailedPaths.Add(path);
                    }
                }
            }
            finally
            {
                EditorUtility.DisplayProgressBar("Sprite 批次設定", "正在完成匯入...", 1f);
                AssetDatabase.StopAssetEditing();
            }

            return result;
        }

        public static SpriteRect[] BuildSpriteRects(
            string assetFileName,
            List<SpriteRectDef> spriteRects,
            Dictionary<string, GUID> existingIds = null)
        {
            var rects = new SpriteRect[spriteRects.Count];
            for (int i = 0; i < spriteRects.Count; i++)
            {
                var def = spriteRects[i];
                string name = assetFileName + def.NameSuffix;
                GUID id = (existingIds is not null && existingIds.TryGetValue(name, out GUID found))
                    ? found
                    : GUID.Generate();
                rects[i] = new SpriteRect
                {
                    name = name,
                    rect = def.Rect,
                    pivot = def.Pivot,
                    alignment = def.Alignment,
                    spriteID = id
                };
            }
            return rects;
        }

    }
}
