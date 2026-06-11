using NUnit.Framework;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SpriteBatch.Tests
{
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
