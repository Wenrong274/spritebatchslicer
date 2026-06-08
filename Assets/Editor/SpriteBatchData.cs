using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace SpriteBatch
{
    [Serializable]
    public class SpriteRectDef
    {
        public string nameSuffix = "_0";
        public Rect rect = new Rect(0, 0, 100, 100);
        public Vector2 pivot = new Vector2(0.5f, 0.5f);
        public SpriteAlignment alignment = SpriteAlignment.Center;
    }

    [Serializable]
    public class BatchSettings
    {
        public List<DefaultAsset> targetFolders = new List<DefaultAsset>();
        public int maxTextureSize = 2048;
        public FilterMode filterMode = FilterMode.Bilinear;
        public bool alphaIsTransparency = true;
        public TextureImporterCompression compression = TextureImporterCompression.Compressed;
        public List<SpriteRectDef> spriteRects = new List<SpriteRectDef>();
    }
}
