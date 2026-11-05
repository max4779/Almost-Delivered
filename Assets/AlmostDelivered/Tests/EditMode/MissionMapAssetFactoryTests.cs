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
            Assert.That(theme.TraversableTerrain.Count, Is.GreaterThanOrEqualTo(2));
            Assert.That(theme.BlockingObstacles.Count, Is.GreaterThanOrEqualTo(4));
            Assert.That(theme.Foliage.Count, Is.GreaterThanOrEqualTo(2));
            AssertCatalog(theme.TerrainDetails);
            AssertCatalog(theme.TraversableTerrain);
            AssertCatalog(theme.BlockingObstacles);
            AssertCatalog(theme.Foliage);
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

        [Test]
        public void TraversableTerrainUsesElevatedMeshColliders()
        {
            var theme = AssetDatabase.LoadAssetAtPath<MissionMapTheme>(MissionMapAssetFactory.ThemePath);

            Assert.That(theme.TraversableTerrain.Count, Is.GreaterThanOrEqualTo(2));
            foreach (var piece in theme.TraversableTerrain)
            {
                var filter = piece.Prefab.GetComponent<MeshFilter>();
                var collider = piece.Prefab.GetComponent<MeshCollider>();
                Assert.That(filter, Is.Not.Null, piece.Prefab.name);
                Assert.That(filter.sharedMesh, Is.Not.Null, piece.Prefab.name);
                Assert.That(filter.sharedMesh.bounds.size.y, Is.GreaterThanOrEqualTo(1f), piece.Prefab.name);
                Assert.That(collider, Is.Not.Null, piece.Prefab.name);
                Assert.That(collider.sharedMesh, Is.SameAs(filter.sharedMesh), piece.Prefab.name);
                Assert.That(MaxSurfaceSlope(filter.sharedMesh), Is.LessThan(45f), piece.Prefab.name);
            }
        }

        [Test]
        public void FoliageUsesSingleMeshWithoutColliders()
        {
            var theme = AssetDatabase.LoadAssetAtPath<MissionMapTheme>(MissionMapAssetFactory.ThemePath);

            Assert.That(theme.Foliage.Count, Is.GreaterThanOrEqualTo(2));
            foreach (var piece in theme.Foliage)
            {
                var filters = piece.Prefab.GetComponentsInChildren<MeshFilter>(true);
                Assert.That(filters, Has.Length.EqualTo(1), piece.Prefab.name);
                Assert.That(filters[0].sharedMesh, Is.Not.Null, piece.Prefab.name);
                Assert.That(filters[0].sharedMesh.triangles, Is.Not.Empty, piece.Prefab.name);
                Assert.That(piece.Prefab.GetComponentsInChildren<MeshRenderer>(true), Has.Length.EqualTo(1), piece.Prefab.name);
                Assert.That(piece.Prefab.GetComponentsInChildren<Collider>(true), Is.Empty, piece.Prefab.name);
            }
        }

        [Test]
        public void FoliageUsesOpposingBackFaceNormals()
        {
            var theme = AssetDatabase.LoadAssetAtPath<MissionMapTheme>(MissionMapAssetFactory.ThemePath);

            foreach (var piece in theme.Foliage)
            {
                var mesh = piece.Prefab.GetComponent<MeshFilter>().sharedMesh;
                var vertices = mesh.vertices;
                var triangles = mesh.triangles;
                Assert.That(triangles.Length % 12, Is.Zero, piece.Prefab.name);
                for (var blade = 0; blade < triangles.Length; blade += 12)
                {
                    var front = TriangleNormal(vertices, triangles, blade);
                    var back = TriangleNormal(vertices, triangles, blade + 6);
                    Assert.That(Vector3.Dot(front, back), Is.LessThan(-.99f), $"{piece.Prefab.name} blade {blade / 12}");
                }
            }
        }

        private static Vector3 TriangleNormal(Vector3[] vertices, int[] triangles, int start)
        {
            var first = vertices[triangles[start]];
            var second = vertices[triangles[start + 1]];
            var third = vertices[triangles[start + 2]];
            return Vector3.Cross(second - first, third - first).normalized;
        }

        private static float MaxSurfaceSlope(Mesh mesh)
        {
            var vertices = mesh.vertices;
            var triangles = mesh.triangles;
            var maximum = 0f;
            for (var index = 0; index < triangles.Length; index += 3)
            {
                var first = vertices[triangles[index]];
                var second = vertices[triangles[index + 1]];
                var third = vertices[triangles[index + 2]];
                var normal = Vector3.Cross(second - first, third - first).normalized;
                if (normal.y < 0f)
                {
                    normal = -normal;
                }

                maximum = Mathf.Max(maximum, Vector3.Angle(normal, Vector3.up));
            }

            return maximum;
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
