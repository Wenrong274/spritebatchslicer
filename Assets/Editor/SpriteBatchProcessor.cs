using System.Collections.Generic;
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
    }
}
