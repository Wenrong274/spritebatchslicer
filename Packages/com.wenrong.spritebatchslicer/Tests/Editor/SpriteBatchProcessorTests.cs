using NUnit.Framework;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SpriteBatch.Tests
{
    public class SpriteBatchProcessorTests
    {
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
    }

    public class SpriteBatchWindowTests
    {
        [Test]
        public void FilterNewFolders_重複資料夾_不加入()
        {
            var folder = AssetDatabase.LoadAssetAtPath<DefaultAsset>("Assets/Sprites/Icon00_6_0win");
            Assume.That(folder, Is.Not.Null, "測試素材 Icon00_6_0win 不存在");
            var existing = new List<DefaultAsset> { folder };

            var result = SpriteBatchWindow.FilterNewFolders(existing, new Object[] { folder });

            Assert.AreEqual(0, result.Count);
        }

        [Test]
        public void FilterNewFolders_新資料夾_加入()
        {
            var folder = AssetDatabase.LoadAssetAtPath<DefaultAsset>("Assets/Sprites/Icon00_6_0win");
            Assume.That(folder, Is.Not.Null, "測試素材 Icon00_6_0win 不存在");

            var result = SpriteBatchWindow.FilterNewFolders(new List<DefaultAsset>(), new Object[] { folder });

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(folder, result[0]);
        }

        [Test]
        public void FilterNewFolders_非資料夾物件_略過()
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(
                "Assets/Sprites/Icon00_6_0win/Icon00_6_0win_00.png");
            Assume.That(texture, Is.Not.Null, "測試素材 Icon00_6_0win_00.png 不存在");

            var result = SpriteBatchWindow.FilterNewFolders(new List<DefaultAsset>(), new Object[] { texture });

            Assert.AreEqual(0, result.Count);
        }

        [Test]
        public void FilterNewFolders_批次內重複資料夾_只加入一次()
        {
            var folder = AssetDatabase.LoadAssetAtPath<DefaultAsset>("Assets/Sprites/Icon00_6_0win");
            Assume.That(folder, Is.Not.Null, "測試素材 Icon00_6_0win 不存在");

            var result = SpriteBatchWindow.FilterNewFolders(
                new List<DefaultAsset>(), new Object[] { folder, folder });

            Assert.AreEqual(1, result.Count);
        }

        // --- AlignmentToPivot ---

        [Test]
        public void AlignmentToPivot_TopLeft_回傳_0_1()
        {
            var result = SpriteBatchWindow.AlignmentToPivot(SpriteAlignment.TopLeft, Vector2.zero);
            Assert.AreEqual(new Vector2(0f, 1f), result);
        }

        [Test]
        public void AlignmentToPivot_BottomRight_回傳_1_0()
        {
            var result = SpriteBatchWindow.AlignmentToPivot(SpriteAlignment.BottomRight, Vector2.zero);
            Assert.AreEqual(new Vector2(1f, 0f), result);
        }

        [Test]
        public void AlignmentToPivot_Center_回傳_05_05()
        {
            var result = SpriteBatchWindow.AlignmentToPivot(SpriteAlignment.Center, Vector2.zero);
            Assert.AreEqual(new Vector2(0.5f, 0.5f), result);
        }

        [Test]
        public void AlignmentToPivot_Custom_不改變Pivot()
        {
            var original = new Vector2(0.3f, 0.7f);
            var result = SpriteBatchWindow.AlignmentToPivot(SpriteAlignment.Custom, original);
            Assert.AreEqual(original, result);
        }
    }
}
