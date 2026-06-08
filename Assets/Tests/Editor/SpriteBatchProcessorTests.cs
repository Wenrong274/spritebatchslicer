using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;
using SpriteBatch;

namespace SpriteBatch.Tests
{
    public class SpriteBatchProcessorTests
    {
        // --- ValidatePreflight ---

        [Test]
        public void ValidatePreflight_空資料夾清單_回傳資料夾錯誤()
        {
            var rects = new List<SpriteRectDef> { new SpriteRectDef() };
            var errors = SpriteBatchProcessor.ValidatePreflight(new List<string>(), rects);
            Assert.AreEqual(1, errors.Count);
            StringAssert.Contains("資料夾", errors[0].message);
        }

        [Test]
        public void ValidatePreflight_空切割清單_回傳切割錯誤()
        {
            var folders = new List<string> { "Assets/Sprites/Icon00_6_0win" };
            var errors = SpriteBatchProcessor.ValidatePreflight(folders, new List<SpriteRectDef>());
            Assert.AreEqual(1, errors.Count);
            StringAssert.Contains("切割", errors[0].message);
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
            var rects = new List<SpriteRectDef> { new SpriteRectDef() };
            var errors = SpriteBatchProcessor.ValidatePreflight(folders, rects);
            Assert.AreEqual(0, errors.Count);
        }

        // --- ValidateRectBounds ---

        [Test]
        public void ValidateRectBounds_範圍內_回傳true()
        {
            var def = new SpriteRectDef { rect = new Rect(0, 0, 160, 88) };
            bool valid = SpriteBatchProcessor.ValidateRectBounds(def, 160, 551, out string error);
            Assert.IsTrue(valid);
            Assert.IsNull(error);
        }

        [Test]
        public void ValidateRectBounds_超出寬度_回傳false()
        {
            var def = new SpriteRectDef { nameSuffix = "_0", rect = new Rect(0, 0, 200, 88) };
            bool valid = SpriteBatchProcessor.ValidateRectBounds(def, 160, 551, out string error);
            Assert.IsFalse(valid);
            Assert.IsNotNull(error);
        }

        [Test]
        public void ValidateRectBounds_超出高度_回傳false()
        {
            var def = new SpriteRectDef { nameSuffix = "_1", rect = new Rect(0, 0, 100, 600) };
            bool valid = SpriteBatchProcessor.ValidateRectBounds(def, 160, 551, out string error);
            Assert.IsFalse(valid);
            Assert.IsNotNull(error);
        }

        // --- BuildSpriteMetaData ---

        [Test]
        public void BuildSpriteMetaData_兩個切割_回傳正確名稱與Rect()
        {
            var rects = new List<SpriteRectDef>
            {
                new SpriteRectDef { nameSuffix = "_0", rect = new Rect(0, 463, 160, 88),  pivot = new Vector2(0.5f, 0.5f) },
                new SpriteRectDef { nameSuffix = "_1", rect = new Rect(0, 409, 160, 170), pivot = new Vector2(0f,   0f)   }
            };

            var metadata = SpriteBatchProcessor.BuildSpriteMetaData("Icon00_6_0win_00", rects);

            Assert.AreEqual(2, metadata.Length);
            Assert.AreEqual("Icon00_6_0win_00_0", metadata[0].name);
            Assert.AreEqual(new Rect(0, 463, 160, 88), metadata[0].rect);
            Assert.AreEqual(new Vector2(0.5f, 0.5f), metadata[0].pivot);
            Assert.AreEqual("Icon00_6_0win_00_1", metadata[1].name);
            Assert.AreEqual(new Vector2(0f, 0f), metadata[1].pivot);
        }
    }
}
