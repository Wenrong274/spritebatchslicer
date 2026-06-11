using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace SpriteBatch.Tests
{
    public class SpriteBatchWindowStateTests
    {
        [Test]
        public void Capture_設定與資料夾路徑_保存所有欄位()
        {
            var state = ScriptableObject.CreateInstance<SpriteBatchWindowState>();
            try
            {
                var settings = new BatchSettings
                {
                    MaxTextureSize = 512,
                    FilterMode = FilterMode.Point,
                    AlphaIsTransparency = false,
                    Compression = TextureImporterCompression.Uncompressed,
                    SpriteRects = new List<SpriteRectDef>
                    {
                        new()
                        {
                            NameSuffix = "_idle",
                            Rect = new Rect(1, 2, 16, 32),
                            Pivot = new Vector2(0.25f, 0.75f),
                            Alignment = SpriteAlignment.Custom
                        }
                    }
                };

                state.Capture(settings, new[]
                {
                    "Assets/Sprites/Icon00_6_0win",
                    "Assets/Sprites/Icon01_6_0win"
                });

                Assert.AreEqual(512, state.MaxTextureSize);
                Assert.AreEqual(FilterMode.Point, state.FilterMode);
                Assert.IsFalse(state.AlphaIsTransparency);
                Assert.AreEqual(TextureImporterCompression.Uncompressed, state.Compression);
                Assert.AreEqual(2, state.FolderPaths.Count);
                Assert.AreEqual("Assets/Sprites/Icon00_6_0win", state.FolderPaths[0]);
                Assert.AreEqual("Assets/Sprites/Icon01_6_0win", state.FolderPaths[1]);
                Assert.AreEqual(1, state.SpriteRects.Count);
                Assert.AreEqual("_idle", state.SpriteRects[0].NameSuffix);
                Assert.AreEqual(new Rect(1, 2, 16, 32), state.SpriteRects[0].Rect);
                Assert.AreEqual(new Vector2(0.25f, 0.75f), state.SpriteRects[0].Pivot);
                Assert.AreEqual(SpriteAlignment.Custom, state.SpriteRects[0].Alignment);
            }
            finally
            {
                Object.DestroyImmediate(state);
            }
        }

        [Test]
        public void ApplyTo_還原設定_不共用SpriteRect參考()
        {
            var sourceRect = new SpriteRectDef
            {
                NameSuffix = "_main",
                Rect = new Rect(0, 0, 32, 32),
                Pivot = new Vector2(0.5f, 0.5f),
                Alignment = SpriteAlignment.Center
            };
            var state = ScriptableObject.CreateInstance<SpriteBatchWindowState>();
            try
            {
                state.FolderPaths = new List<string> { "Assets/Sprites/Icon00_6_0win" };
                state.SpriteRects = new List<SpriteRectDef> { sourceRect };
                state.MaxTextureSize = 1024;
                state.FilterMode = FilterMode.Trilinear;
                state.AlphaIsTransparency = true;
                state.Compression = TextureImporterCompression.CompressedHQ;
                var settings = new BatchSettings();
                var folderPaths = new List<string>();

                state.ApplyTo(settings, folderPaths);

                Assert.AreEqual(1024, settings.MaxTextureSize);
                Assert.AreEqual(FilterMode.Trilinear, settings.FilterMode);
                Assert.IsTrue(settings.AlphaIsTransparency);
                Assert.AreEqual(TextureImporterCompression.CompressedHQ, settings.Compression);
                Assert.AreEqual(1, folderPaths.Count);
                Assert.AreEqual("Assets/Sprites/Icon00_6_0win", folderPaths[0]);
                Assert.AreEqual(1, settings.FolderPaths.Count);
                Assert.AreEqual("Assets/Sprites/Icon00_6_0win", settings.FolderPaths[0]);
                Assert.AreEqual(1, settings.SpriteRects.Count);
                Assert.AreNotSame(sourceRect, settings.SpriteRects[0]);
                settings.SpriteRects[0].NameSuffix = "_changed";
                Assert.AreEqual("_main", state.SpriteRects[0].NameSuffix);
            }
            finally
            {
                Object.DestroyImmediate(state);
            }
        }
    }
}
