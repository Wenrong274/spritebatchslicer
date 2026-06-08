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
            if (rectDef.rect.x < 0 || rectDef.rect.y < 0)
            {
                error = $"切割區域 '{rectDef.nameSuffix}' 的起點座標不可為負值。";
                return false;
            }
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
    }
}
