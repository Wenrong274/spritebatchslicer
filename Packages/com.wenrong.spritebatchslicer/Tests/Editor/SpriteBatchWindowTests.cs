using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace SpriteBatch.Tests
{
    public class SpriteBatchWindowTests
    {
        private const string TestFolderPath = TestAssetFactory.TestRoot + "/WindowTests";
        private const string TestTexturePath = TestFolderPath + "/WindowTexture.png";

        [SetUp]
        public void SetUp()
        {
            TestAssetFactory.DeleteTestRoot();
            TestAssetFactory.CreatePng(TestTexturePath, 16, 16, Color.white);
        }

        [TearDown]
        public void TearDown()
        {
            TestAssetFactory.DeleteTestRoot();
        }

        [Test]
        public void FilterNewFolders_重複資料夾_不加入()
        {
            var folder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(TestFolderPath);
            Assume.That(folder, Is.Not.Null, "測試資料夾不存在");
            var existing = new List<DefaultAsset> { folder };

            var result = SpriteBatchEditorUtils.FilterNewFolders(existing, new Object[] { folder });

            Assert.AreEqual(0, result.Count);
        }

        [Test]
        public void FilterNewFolders_新資料夾_加入()
        {
            var folder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(TestFolderPath);
            Assume.That(folder, Is.Not.Null, "測試資料夾不存在");

            var result = SpriteBatchEditorUtils.FilterNewFolders(new List<DefaultAsset>(), new Object[] { folder });

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(folder, result[0]);
        }

        [Test]
        public void FilterNewFolders_非資料夾物件_略過()
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TestTexturePath);
            Assume.That(texture, Is.Not.Null, "測試貼圖不存在");

            var result = SpriteBatchEditorUtils.FilterNewFolders(new List<DefaultAsset>(), new Object[] { texture });

            Assert.AreEqual(0, result.Count);
        }

        [Test]
        public void FilterNewFolders_批次內重複資料夾_只加入一次()
        {
            var folder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(TestFolderPath);
            Assume.That(folder, Is.Not.Null, "測試資料夾不存在");

            var result = SpriteBatchEditorUtils.FilterNewFolders(
                new List<DefaultAsset>(), new Object[] { folder, folder });

            Assert.AreEqual(1, result.Count);
        }

        [Test]
        public void ToFolderPaths_資料夾資產清單_回傳AssetPath()
        {
            var folder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(TestFolderPath);
            Assume.That(folder, Is.Not.Null, "測試資料夾不存在");

            var result = SpriteBatchEditorUtils.ToFolderPaths(new List<DefaultAsset> { folder, null });

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(TestFolderPath, result[0]);
        }

        [Test]
        public void LoadFolderAssets_路徑清單_略過不存在路徑()
        {
            var result = SpriteBatchEditorUtils.LoadFolderAssets(new[]
            {
                TestFolderPath,
                "Assets/DoesNotExist"
            });

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(TestFolderPath, AssetDatabase.GetAssetPath(result[0]));
        }

        [Test]
        public void NormalizeMaxTextureSize_合法值_保留原值()
        {
            int result = SpriteBatchEditorUtils.NormalizeMaxTextureSize(
                512, new[] { 32, 64, 128, 256, 512 }, 2048);

            Assert.AreEqual(512, result);
        }

        [Test]
        public void NormalizeMaxTextureSize_非法值_回傳預設值()
        {
            int result = SpriteBatchEditorUtils.NormalizeMaxTextureSize(
                3000, new[] { 32, 64, 128, 256, 512 }, 2048);

            Assert.AreEqual(2048, result);
        }

        [Test]
        public void FilterNewFolders_空拖曳集合_回傳空清單()
        {
            var result = SpriteBatchEditorUtils.FilterNewFolders(
                new List<DefaultAsset>(), System.Array.Empty<Object>());

            Assert.AreEqual(0, result.Count);
        }

        // --- AlignmentToPivot ---

        [Test]
        public void AlignmentToPivot_TopLeft_回傳_0_1()
        {
            var result = SpriteBatchEditorUtils.AlignmentToPivot(SpriteAlignment.TopLeft, Vector2.zero);
            Assert.AreEqual(new Vector2(0f, 1f), result);
        }

        [Test]
        public void AlignmentToPivot_BottomRight_回傳_1_0()
        {
            var result = SpriteBatchEditorUtils.AlignmentToPivot(SpriteAlignment.BottomRight, Vector2.zero);
            Assert.AreEqual(new Vector2(1f, 0f), result);
        }

        [Test]
        public void AlignmentToPivot_Center_回傳_05_05()
        {
            var result = SpriteBatchEditorUtils.AlignmentToPivot(SpriteAlignment.Center, Vector2.zero);
            Assert.AreEqual(new Vector2(0.5f, 0.5f), result);
        }

        [Test]
        public void AlignmentToPivot_Custom_不改變Pivot()
        {
            var original = new Vector2(0.3f, 0.7f);
            var result = SpriteBatchEditorUtils.AlignmentToPivot(SpriteAlignment.Custom, original);
            Assert.AreEqual(original, result);
        }
    }
}
