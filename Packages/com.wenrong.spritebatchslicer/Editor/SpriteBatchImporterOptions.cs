using UnityEditor;

namespace SpriteBatch
{
    public static class SpriteBatchImporterOptions
    {
        public static TextureImporterCompression ToUnityCompression(BatchTextureCompression compression) =>
            compression switch
            {
                BatchTextureCompression.Uncompressed => TextureImporterCompression.Uncompressed,
                BatchTextureCompression.Compressed => TextureImporterCompression.Compressed,
                BatchTextureCompression.CompressedHQ => TextureImporterCompression.CompressedHQ,
                BatchTextureCompression.CompressedLQ => TextureImporterCompression.CompressedLQ,
                _ => TextureImporterCompression.Compressed,
            };
    }
}
