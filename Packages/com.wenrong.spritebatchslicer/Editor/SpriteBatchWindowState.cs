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

        public void Save() => Save(true);
    }
}
