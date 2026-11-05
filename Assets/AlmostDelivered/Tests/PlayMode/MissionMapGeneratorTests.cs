using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AlmostDelivered.MapGeneration;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AlmostDelivered.Tests.PlayMode
{
    public sealed class MissionMapGeneratorTests
    {
        private readonly List<Object> created = new List<Object>();
        private MissionMapTheme theme;
        private MissionMapGenerator generator;

        [SetUp]
        public void SetUp()
        {
            var terrain = Primitive("Test Terrain", PrimitiveType.Quad, false);
            var blocker = Primitive("Test Blocker", PrimitiveType.Cube, true);
            theme = ScriptableObject.CreateInstance<MissionMapTheme>();
            created.Add(theme);
            theme.Configure(
                new[] { new MissionMapPiece(terrain, new Vector2(1.5f, 1.5f)) },
                new[] { new MissionMapPiece(blocker, new Vector2(2f, 2f)) });

            var root = new GameObject("Map Generator Test");
            created.Add(root);
            generator = root.AddComponent<MissionMapGenerator>();
            generator.Configure(theme, new Vector2(12f, 10f), 8, 6, ProtectedZones(), false, 600);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (var item in created.Where(item => item != null))
            {
                Object.Destroy(item);
            }

            created.Clear();
            yield return null;
        }

        [UnityTest]
        public IEnumerator ExplicitSeedReproducesLayout()
        {
            generator.Generate(1117);
            var first = Snapshot(generator.GeneratedRoot);
            generator.Generate(1117);
            yield return null;
            var second = Snapshot(generator.GeneratedRoot);

            Assert.That(generator.LastSeed, Is.EqualTo(1117));
            Assert.That(second, Is.EqualTo(first));
        }

        [UnityTest]
        public IEnumerator DifferentSeedsVaryLayout()
        {
            generator.Generate(31);
            var first = Snapshot(generator.GeneratedRoot);
            generator.Generate(47);
            yield return null;
            var second = Snapshot(generator.GeneratedRoot);

            Assert.That(second, Is.Not.EqualTo(first));
        }

        [UnityTest]
        public IEnumerator RegenerationReplacesOldGeneratedRoot()
        {
            generator.Generate(5);
            var firstRoot = generator.GeneratedRoot;

            generator.Generate(6);
            yield return null;

            Assert.That(firstRoot == null, Is.True);
            Assert.That(generator.GeneratedRoot, Is.Not.Null);
            Assert.That(generator.transform.Cast<Transform>().Count(child => child.name == MissionMapGenerator.GeneratedRootName), Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator GeneratedPiecesAvoidProtectedRegions()
        {
            var protectedZones = ProtectedZones();
            generator.Generate(90210);
            yield return null;

            var pieces = generator.GeneratedRoot.GetComponentsInChildren<MissionMapPieceInstance>();
            Assert.That(pieces.Length, Is.GreaterThan(6));
            foreach (var piece in pieces)
            {
                foreach (var zone in protectedZones)
                {
                    Assert.That(Overlaps(piece.Position, piece.Footprint, zone.Center, zone.Size), Is.False, piece.name);
                }
            }
        }

        [UnityTest]
        public IEnumerator ExtraRouteExclusionsBlockObstaclesButNotTerrainDetails()
        {
            var routeZone = new MapExclusionZone(Vector2.zero, new Vector2(24f, 20f));

            generator.Generate(444, new[] { routeZone });
            yield return null;

            var pieces = generator.GeneratedRoot.GetComponentsInChildren<MissionMapPieceInstance>();
            var terrain = pieces.Where(piece => piece.Category == MissionMapPieceCategory.TerrainDetail).ToArray();
            var blockers = pieces.Where(piece => piece.Category == MissionMapPieceCategory.BlockingObstacle).ToArray();
            Assert.That(terrain, Is.Not.Empty);
            Assert.That(blockers, Is.Empty);
        }

        [UnityTest]
        public IEnumerator ExtraExclusionsRemainDeterministicForSameSeed()
        {
            var routeZones = new[]
            {
                new MapExclusionZone(new Vector2(3f, 0f), new Vector2(4f, 12f))
            };

            generator.Generate(9191, routeZones);
            var first = Snapshot(generator.GeneratedRoot);
            generator.Generate(9191, routeZones);
            yield return null;
            var second = Snapshot(generator.GeneratedRoot);

            Assert.That(second, Is.EqualTo(first));
        }

        [UnityTest]
        public IEnumerator ExistingGenerateOverloadStillCreatesBothCategories()
        {
            generator.Generate(8181);
            yield return null;

            var pieces = generator.GeneratedRoot.GetComponentsInChildren<MissionMapPieceInstance>();
            Assert.That(pieces.Any(piece => piece.Category == MissionMapPieceCategory.TerrainDetail), Is.True);
            Assert.That(pieces.Any(piece => piece.Category == MissionMapPieceCategory.BlockingObstacle), Is.True);
        }

        private GameObject Primitive(string name, PrimitiveType type, bool keepCollider)
        {
            var primitive = GameObject.CreatePrimitive(type);
            primitive.name = name;
            primitive.SetActive(false);
            if (!keepCollider)
            {
                Object.DestroyImmediate(primitive.GetComponent<Collider>());
            }

            created.Add(primitive);
            return primitive;
        }

        private static MissionMapProtectedZone[] ProtectedZones()
        {
            return new[]
            {
                new MissionMapProtectedZone(Vector2.zero, new Vector2(5f, 5f)),
                new MissionMapProtectedZone(new Vector2(-7f, 6f), new Vector2(3f, 3f))
            };
        }

        private static string Snapshot(Transform root)
        {
            return string.Join("|", root.GetComponentsInChildren<MissionMapPieceInstance>()
                .Select(piece => $"{piece.Category}:{piece.PieceIndex}:{piece.Position.x:F4},{piece.Position.y:F4}:{piece.QuarterTurns}"));
        }

        private static bool Overlaps(Vector2 firstCenter, Vector2 firstSize, Vector2 secondCenter, Vector2 secondSize)
        {
            return Mathf.Abs(firstCenter.x - secondCenter.x) < (firstSize.x + secondSize.x) * .5f &&
                   Mathf.Abs(firstCenter.y - secondCenter.y) < (firstSize.y + secondSize.y) * .5f;
        }
    }
}
