using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace SpriteBatch
{
    [Serializable]
    public class SpriteRectDef
    {
        public string NameSuffix = "_0";
        public Rect Rect = new(0, 0, 100, 100);
        public Vector2 Pivot = new(0.5f, 0.5f);
        public SpriteAlignment Alignment = SpriteAlignment.Center;
    }

    [Serializable]
    public class BatchSettings
    {
        public List<DefaultAsset> TargetFolders = new();
        public int MaxTextureSize = 2048;
        public FilterMode FilterMode = FilterMode.Bilinear;
        public bool AlphaIsTransparency = true;
        public TextureImporterCompression Compression = TextureImporterCompression.Compressed;
        public List<SpriteRectDef> SpriteRects = new();
    }
}
