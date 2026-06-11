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

        public void Capture(BatchSettings settings, IEnumerable<string> folderPaths)
        {
            MaxTextureSize = settings.MaxTextureSize;
            FilterMode = settings.FilterMode;
            AlphaIsTransparency = settings.AlphaIsTransparency;
            Compression = settings.Compression;
            SpriteRects = CopySpriteRects(settings.SpriteRects);

            FolderPaths = new List<string>();
            foreach (string folderPath in folderPaths)
            {
                if (!string.IsNullOrEmpty(folderPath))
                {
                    FolderPaths.Add(folderPath);
                }
            }
        }

        public void ApplyTo(BatchSettings settings, List<string> folderPaths)
        {
            settings.MaxTextureSize = MaxTextureSize;
            settings.FilterMode = FilterMode;
            settings.AlphaIsTransparency = AlphaIsTransparency;
            settings.Compression = Compression;
            settings.SpriteRects = CopySpriteRects(SpriteRects);

            settings.FolderPaths.Clear();
            folderPaths.Clear();
            foreach (string folderPath in FolderPaths)
            {
                if (!string.IsNullOrEmpty(folderPath))
                {
                    settings.FolderPaths.Add(folderPath);
                    folderPaths.Add(folderPath);
                }
            }
        }

        public void SaveState()
        {
            Save(true);
        }

        private static List<SpriteRectDef> CopySpriteRects(IEnumerable<SpriteRectDef> spriteRects)
        {
            var result = new List<SpriteRectDef>();
            foreach (var rect in spriteRects)
            {
                result.Add(new SpriteRectDef
                {
                    NameSuffix = rect.NameSuffix,
                    Rect = rect.Rect,
                    Pivot = rect.Pivot,
                    Alignment = rect.Alignment
                });
            }
            return result;
        }
    }
}
