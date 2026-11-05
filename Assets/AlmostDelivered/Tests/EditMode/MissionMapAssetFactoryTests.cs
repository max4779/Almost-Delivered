using AlmostDelivered.Editor.Frontend;
using AlmostDelivered.MapGeneration;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AlmostDelivered.Tests.EditMode
{
    public sealed class MissionMapAssetFactoryTests
    {
        [OneTimeSetUp]
        public void BuildAssets()
        {
            MissionMapAssetFactory.CreateOrUpdateAll();
        }

        [Test]
        public void IndustrialDockThemeContainsValidCatalogs()
        {
            var theme = AssetDatabase.LoadAssetAtPath<MissionMapTheme>(MissionMapAssetFactory.ThemePath);

            Assert.That(theme, Is.Not.Null);
            Assert.That(theme.TerrainDetails.Count, Is.GreaterThanOrEqualTo(3));
            Assert.That(theme.BlockingObstacles.Count, Is.GreaterThanOrEqualTo(4));
            AssertCatalog(theme.TerrainDetails);
            AssertCatalog(theme.BlockingObstacles);
        }

        [Test]
        public void TerrainDetailsHaveNoColliders()
        {
            var theme = AssetDatabase.LoadAssetAtPath<MissionMapTheme>(MissionMapAssetFactory.ThemePath);

            foreach (var piece in theme.TerrainDetails)
            {
                Assert.That(piece.Prefab.GetComponentsInChildren<Collider>(true), Is.Empty, piece.Prefab.name);
            }
        }

        [Test]
        public void BlockingObstaclesHaveColliders()
        {
            var theme = AssetDatabase.LoadAssetAtPath<MissionMapTheme>(MissionMapAssetFactory.ThemePath);

            foreach (var piece in theme.BlockingObstacles)
            {
                Assert.That(piece.Prefab.GetComponentsInChildren<Collider>(true), Is.Not.Empty, piece.Prefab.name);
            }
        }

        private static void AssertCatalog(System.Collections.Generic.IReadOnlyList<MissionMapPiece> catalog)
        {
            foreach (var piece in catalog)
            {
                Assert.That(piece, Is.Not.Null);
                Assert.That(piece.Prefab, Is.Not.Null);
                Assert.That(piece.Footprint.x, Is.GreaterThan(0f));
                Assert.That(piece.Footprint.y, Is.GreaterThan(0f));
            }
        }
    }
}
