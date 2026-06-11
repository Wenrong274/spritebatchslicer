using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace SpriteBatch.Tests
{
    public class SpriteBatchProcessorTests
    {
        [SetUp]
        public void SetUp()
        {
            TestAssetFactory.DeleteTestRoot();
        }

        [TearDown]
        public void TearDown()
        {
            TestAssetFactory.DeleteTestRoot();
        }

        // --- ValidatePreflight ---

        [Test]
        public void ValidatePreflight_空資料夾清單_回傳資料夾錯誤()
        {
            var rects = new List<SpriteRectDef> { new() };
            var errors = SpriteBatchProcessor.ValidatePreflight(new List<string>(), rects);
            Assert.AreEqual(1, errors.Count);
            StringAssert.Contains("資料夾", errors[0].Message);
        }

        [Test]
        public void ValidatePreflight_空切割清單_回傳切割錯誤()
        {
            var folders = new List<string> { "Assets/Sprites/Icon00_6_0win" };
            var errors = SpriteBatchProcessor.ValidatePreflight(folders, new List<SpriteRectDef>());
            Assert.AreEqual(1, errors.Count);
            StringAssert.Contains("切割", errors[0].Message);
        }

        [Test]
        public void ValidatePreflight_兩者皆空_回傳兩個錯誤()
        {
            var errors = SpriteBatchProcessor.ValidatePreflight(new List<string>(), new List<SpriteRectDef>());
            Assert.AreEqual(2, errors.Count);
        }

        [Test]
        public void ValidatePreflight_兩者皆有_回傳無錯誤()
        {
            var folders = new List<string> { "Assets/Sprites/Icon00_6_0win" };
            var rects = new List<SpriteRectDef> { new() };
            var errors = SpriteBatchProcessor.ValidatePreflight(folders, rects);
            Assert.AreEqual(0, errors.Count);
        }

        // --- ValidateRectBounds ---

        [Test]
        public void ValidateRectBounds_範圍內_回傳true()
        {
            var def = new SpriteRectDef { Rect = new Rect(0, 0, 160, 88) };
            bool valid = SpriteBatchProcessor.ValidateRectBounds(def, 160, 551, out string error);
            Assert.IsTrue(valid);
            Assert.IsNull(error);
        }

        [Test]
        public void ValidateRectBounds_超出寬度_回傳false()
        {
            var def = new SpriteRectDef { NameSuffix = "_0", Rect = new Rect(0, 0, 200, 88) };
            bool valid = SpriteBatchProcessor.ValidateRectBounds(def, 160, 551, out string error);
            Assert.IsFalse(valid);
            Assert.IsNotNull(error);
        }

        [Test]
        public void ValidateRectBounds_超出高度_回傳false()
        {
            var def = new SpriteRectDef { NameSuffix = "_1", Rect = new Rect(0, 0, 100, 600) };
            bool valid = SpriteBatchProcessor.ValidateRectBounds(def, 160, 551, out string error);
            Assert.IsFalse(valid);
            Assert.IsNotNull(error);
        }

        [Test]
        public void ValidateRectBounds_負數座標_回傳false()
        {
            var def = new SpriteRectDef { NameSuffix = "_0", Rect = new Rect(-10, 0, 50, 50) };
            bool valid = SpriteBatchProcessor.ValidateRectBounds(def, 160, 551, out string error);
            Assert.IsFalse(valid);
            Assert.IsNotNull(error);
        }

        [Test]
        public void ValidateRectBounds_零面積_回傳false()
        {
            var def = new SpriteRectDef { NameSuffix = "_0", Rect = new Rect(0, 0, 0, 88) };
            bool valid = SpriteBatchProcessor.ValidateRectBounds(def, 160, 551, out string error);
            Assert.IsFalse(valid);
            Assert.IsNotNull(error);
        }

        [Test]
        public void ValidatePreflight_重複後綴_回傳後綴錯誤()
        {
            var folders = new List<string> { "Assets/Sprites/Icon00_6_0win" };
            var rects = new List<SpriteRectDef>
            {
                new() { NameSuffix = "_0" },
                new() { NameSuffix = "_0" }
            };
            var errors = SpriteBatchProcessor.ValidatePreflight(folders, rects);
            Assert.AreEqual(1, errors.Count);
            StringAssert.Contains("後綴", errors[0].Message);
        }

        // --- BuildSpriteRects ---

        [Test]
        public void BuildSpriteRects_兩個切割_回傳正確名稱與Rect()
        {
            var rects = new List<SpriteRectDef>
            {
                new() { NameSuffix = "_0", Rect = new Rect(0, 463, 160, 88),  Pivot = new Vector2(0.5f, 0.5f), Alignment = SpriteAlignment.Center   },
                new() { NameSuffix = "_1", Rect = new Rect(0, 409, 160, 170), Pivot = new Vector2(0f,   0f),   Alignment = SpriteAlignment.BottomLeft }
            };

            var result = SpriteBatchProcessor.BuildSpriteRects("Icon00_6_0win_00", rects);

            Assert.AreEqual(2, result.Length);
            Assert.AreEqual("Icon00_6_0win_00_0", result[0].name);
            Assert.AreEqual(new Rect(0, 463, 160, 88), result[0].rect);
            Assert.AreEqual(new Vector2(0.5f, 0.5f), result[0].pivot);
            Assert.AreEqual(SpriteAlignment.Center, result[0].alignment);
            Assert.AreEqual("Icon00_6_0win_00_1", result[1].name);
            Assert.AreEqual(new Rect(0, 409, 160, 170), result[1].rect);
            Assert.AreEqual(new Vector2(0f, 0f), result[1].pivot);
            Assert.AreEqual(SpriteAlignment.BottomLeft, result[1].alignment);
        }

        [Test]
        public void BuildSpriteRects_有既有GUID_保留既有GUID()
        {
            var existingId = GUID.Generate();
            var existingIds = new Dictionary<string, GUID> { { "Icon_0", existingId } };
            var rects = new List<SpriteRectDef> { new() { NameSuffix = "_0" } };

            var result = SpriteBatchProcessor.BuildSpriteRects("Icon", rects, existingIds);

            Assert.AreEqual(existingId, result[0].spriteID);
        }

        [Test]
        public void BuildSpriteRects_無既有GUID_產生非零GUID()
        {
            var rects = new List<SpriteRectDef> { new() { NameSuffix = "_0" } };

            var result = SpriteBatchProcessor.BuildSpriteRects("Icon", rects, null);

            Assert.AreNotEqual(new GUID(), result[0].spriteID);
        }

        [Test]
        public void BuildSpriteRects_兩筆切割_GUID各不相同()
        {
            var rects = new List<SpriteRectDef>
            {
                new() { NameSuffix = "_0" },
                new() { NameSuffix = "_1" }
            };

            var result = SpriteBatchProcessor.BuildSpriteRects("Icon", rects, null);

            Assert.AreNotEqual(result[0].spriteID, result[1].spriteID);
        }

        // --- CollectTexturePaths ---

        [Test]
        public void CollectTexturePaths_空清單_回傳空清單()
        {
            var result = SpriteBatchProcessor.CollectTexturePaths(new List<string>());
            Assert.AreEqual(0, result.Count);
        }

        [Test]
        public void CollectTexturePaths_包含不存在路徑_略過並回傳空清單()
        {
            var result = SpriteBatchProcessor.CollectTexturePaths(
                new List<string> { "Assets/NonExistent/Folder" });
            Assert.AreEqual(0, result.Count);
        }

        // --- SpriteBatchImporterOptions ---

        [Test]
        public void ToUnityCompression_CompressedHQ_回傳UnityCompressedHQ()
        {
            var result = SpriteBatchImporterOptions.ToUnityCompression(BatchTextureCompression.CompressedHQ);

            Assert.AreEqual(TextureImporterCompression.CompressedHQ, result);
        }

        [Test]
        public void ToUnityCompression_Uncompressed_回傳UnityUncompressed()
        {
            var result = SpriteBatchImporterOptions.ToUnityCompression(BatchTextureCompression.Uncompressed);

            Assert.AreEqual(TextureImporterCompression.Uncompressed, result);
        }

        // --- ApplyToFolders ---

        [Test]
        public void ApplyToFolders_有效設定_套用TextureImporter與SpriteRects()
        {
            string path = TestAssetFactory.CreatePng(
                $"{TestAssetFactory.TestRoot}/apply_success.png", 64, 64, Color.white);
            var settings = new BatchSettings
            {
                FolderPaths = new List<string> { TestAssetFactory.TestRoot },
                MaxTextureSize = 512,
                FilterMode = FilterMode.Point,
                AlphaIsTransparency = false,
                Compression = BatchTextureCompression.Uncompressed,
                SpriteRects = new List<SpriteRectDef>
                {
                    new()
                    {
                        NameSuffix = "_idle",
                        Rect = new Rect(0, 0, 32, 32),
                        Pivot = new Vector2(0.5f, 0.5f),
                        Alignment = SpriteAlignment.Center
                    }
                }
            };

            var result = SpriteBatchProcessor.ApplyToFolders(settings);

            Assert.AreEqual(1, result.SuccessCount);
            Assert.AreEqual(0, result.SkippedPaths.Count);
            Assert.AreEqual(0, result.FailedPaths.Count);
            Assert.IsFalse(result.WasCancelled);

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            Assert.AreEqual(TextureImporterType.Sprite, importer.textureType);
            Assert.AreEqual(SpriteImportMode.Multiple, importer.spriteImportMode);
            Assert.AreEqual(FilterMode.Point, importer.filterMode);
            Assert.IsFalse(importer.alphaIsTransparency);
            Assert.AreEqual(512, importer.maxTextureSize);
            Assert.AreEqual(TextureImporterCompression.Uncompressed, importer.textureCompression);

            var sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToArray();
            Assert.IsTrue(sprites.Any(sprite => sprite.name == "apply_success_idle"));
        }

        [Test]
        public void ApplyToFolders_既有同名Sprite_保留SpriteGUID()
        {
            string path = TestAssetFactory.CreatePng(
                $"{TestAssetFactory.TestRoot}/guid_preserved.png", 64, 64, Color.white);
            var settings = new BatchSettings
            {
                FolderPaths = new List<string> { TestAssetFactory.TestRoot },
                SpriteRects = new List<SpriteRectDef>
                {
                    new()
                    {
                        NameSuffix = "_main",
                        Rect = new Rect(0, 0, 32, 32),
                        Pivot = new Vector2(0.5f, 0.5f),
                        Alignment = SpriteAlignment.Center
                    }
                }
            };

            var firstResult = SpriteBatchProcessor.ApplyToFolders(settings);
            var firstIds = ReadSpriteIds(path);

            var secondResult = SpriteBatchProcessor.ApplyToFolders(settings);
            var secondIds = ReadSpriteIds(path);

            Assert.AreEqual(1, firstResult.SuccessCount);
            Assert.AreEqual(1, secondResult.SuccessCount);
            Assert.IsTrue(firstIds.ContainsKey("guid_preserved_main"));
            Assert.AreEqual(firstIds["guid_preserved_main"], secondIds["guid_preserved_main"]);
        }

        private static Dictionary<string, GUID> ReadSpriteIds(string path)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            var factory = new SpriteDataProviderFactories();
            factory.Init();
            var dataProvider = factory.GetSpriteEditorDataProviderFromObject(importer);
            dataProvider.InitSpriteEditorDataProvider();

            return dataProvider.GetSpriteRects()
                .ToDictionary(rect => rect.name, rect => rect.spriteID);
        }
    }

}
