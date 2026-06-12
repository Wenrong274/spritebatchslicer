using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SpriteBatch
{
    public static class SpriteBatchEditorUtils
    {
        public static List<DefaultAsset> FilterNewFolders(
            List<DefaultAsset> existing, IEnumerable<Object> dragged)
        {
            var result = new List<DefaultAsset>();
            foreach (var obj in dragged)
            {
                if (obj is not DefaultAsset asset)
                {
                    continue;
                }
                if (!AssetDatabase.IsValidFolder(AssetDatabase.GetAssetPath(obj)))
                {
                    continue;
                }
                if (existing.Contains(asset) || result.Contains(asset))
                {
                    continue;
                }
                result.Add(asset);
            }
            return result;
        }
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

        public static int NormalizeMaxTextureSize(
            int value, IReadOnlyList<int> allowedValues, int defaultValue)
        {
            return allowedValues.Contains(value) ? value : defaultValue;
        }

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
    }
}
