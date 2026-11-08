using System.Linq;
using AlmostDelivered.Combat;
using AlmostDelivered.Editor.Frontend;
using AlmostDelivered.Frontend;
using AlmostDelivered.Gameplay;
using AlmostDelivered.Gameplay.Payload;
using AlmostDelivered.MapGeneration;
using AlmostDelivered.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AlmostDelivered.Tests.EditMode
{
    public sealed class MissionPrototypeSceneBuilderTests
    {
        private static readonly Rect MissionBounds = new Rect(-28f, -50f, 56f, 100f);

        [Test]
        public void BuildCreatesPayloadMissionAuthorityPlayerCameraHudAndMinimap()
        {
            FrontendSceneBuilder.BuildOrUpdateAll();
            var scene = EditorSceneManager.OpenScene(MissionPrototypeSceneBuilder.ScenePath, OpenSceneMode.Single);
            var player = Find(scene, "Player");
            var payload = Find(scene, "Payload");
            var camera = Find(scene, "Main Camera");

            Assert.That(player.GetComponent<MissionPlayerController>(), Is.Not.Null);
            Assert.That(player.GetComponent<RangedAttackController>(), Is.Not.Null);
            Assert.That(player.GetComponent<CombatTeam>().Team, Is.EqualTo(CombatTeamId.Player));
            Assert.That(Object.FindObjectsOfType<PayloadMissionController>(true), Has.Length.EqualTo(1));
            Assert.That(Object.FindObjectOfType<PayloadMissionController>(true).IsAuthority, Is.True);
            Assert.That(Object.FindObjectsOfType<PayloadWaveSpawner>(true), Has.Length.EqualTo(1));
            Assert.That(Object.FindObjectsOfType<PayloadEscortRadiusView>(true), Has.Length.EqualTo(1));
            Assert.That(Object.FindObjectOfType<PayloadEscortRadiusView>(true).Radius, Is.EqualTo(4.5f));
            Assert.That(Object.FindObjectsOfType<MissionMinimap>(true), Has.Length.EqualTo(1));
            Assert.That(Object.FindObjectsOfType<MissionHud>(true), Has.Length.EqualTo(1));
            Assert.That(Object.FindObjectOfType<MissionHud>(true).HasPayloadViews, Is.True);
            Assert.That(Object.FindObjectsOfType<PrototypeBossController>(true), Is.Empty);
            Assert.That(Object.FindObjectsOfType<CombatProjectile>(true).Count(item => item.name == "Projectile Template"), Is.EqualTo(1));
            AssertVisual(player, VisualArchetype.Player);
            AssertVisual(payload, VisualArchetype.Payload);

            var follow = camera.GetComponent<QuarterViewCamera>();
            Assert.That(camera.GetComponent<Camera>().orthographicSize, Is.EqualTo(9f));
            Assert.That(follow.Target, Is.EqualTo(player.transform));

            var buildPaths = EditorBuildSettings.scenes.Select(item => item.path).ToArray();
            Assert.That(buildPaths.Take(3), Is.EqualTo(new[]
            {
                FrontendSceneBuilder.FrontendScenePath,
                FrontendSceneBuilder.DepartureHubScenePath,
                MissionPrototypeSceneBuilder.ScenePath
            }));
        }

        [Test]
        public void MissionUsesRouteAwareMapAndCappedWaveConfiguration()
        {
            FrontendSceneBuilder.BuildOrUpdateAll();
            EditorSceneManager.OpenScene(MissionPrototypeSceneBuilder.ScenePath, OpenSceneMode.Single);
            var generator = Object.FindObjectOfType<MissionMapGenerator>(true);
            var spawner = Object.FindObjectOfType<PayloadWaveSpawner>(true);

            Assert.That(generator.Theme, Is.Not.Null);
            Assert.That(generator.RandomizeOnStart, Is.False);
            Assert.That(generator.TerrainDetailCount, Is.Zero,
                "Large rectangular floor-detail prefabs read as stray squares in the outdoor mission.");
            Assert.That(generator.TraversableTerrainCount, Is.EqualTo(6));
            Assert.That(generator.BlockingObstacleCount, Is.EqualTo(28));
            Assert.That(generator.FoliageCount, Is.EqualTo(42));
            Assert.That(spawner.ActiveCap, Is.EqualTo(18));
            Assert.That(PayloadWaveSpawner.WaveCountFor(0, false), Is.EqualTo(10));
            Assert.That(PayloadWaveSpawner.WaveCountFor(1, false), Is.EqualTo(14));
            Assert.That(PayloadWaveSpawner.WaveCountFor(2, false), Is.EqualTo(18));
            Assert.That(PayloadWaveSpawner.WaveCountFor(3, true), Is.EqualTo(22));

            var route = PayloadRoutePlanner.Generate(MissionPrototypeSceneBuilder.MissionSeed, MissionBounds, 5, 8f);
            var generatedPieces = generator.GeneratedRoot.GetComponentsInChildren<MissionMapPieceInstance>(true);
            Assert.That(generatedPieces, Has.Length.EqualTo(76), "Generated map pieces must survive a saved-scene reload.");
            Assert.That(generatedPieces.Count(piece => piece.Category == MissionMapPieceCategory.TerrainDetail), Is.Zero);
            Assert.That(generatedPieces.Count(piece => piece.Category == MissionMapPieceCategory.TraversableTerrain), Is.EqualTo(6));
            Assert.That(generatedPieces.Count(piece => piece.Category == MissionMapPieceCategory.BlockingObstacle), Is.EqualTo(28));
            Assert.That(generatedPieces.Count(piece => piece.Category == MissionMapPieceCategory.Foliage), Is.EqualTo(42));
            var routeSensitivePieces = generatedPieces
                .Where(piece => piece.Category != MissionMapPieceCategory.TerrainDetail)
                .ToArray();
            foreach (var piece in routeSensitivePieces)
            foreach (var zone in route.CorridorExclusions)
                Assert.That(Overlaps(piece.Position, piece.Footprint, zone.Center, zone.Size), Is.False, piece.name);
        }

        [Test]
        public void BuildUsesVisibleSupplyRelaysWithoutDrawingWorldRoute()
        {
            FrontendSceneBuilder.BuildOrUpdateAll();
            var scene = EditorSceneManager.OpenScene(MissionPrototypeSceneBuilder.ScenePath, OpenSceneMode.Single);
            var payload = Find(scene, "Payload");
            var relays = Object.FindObjectsOfType<PayloadRelayBeaconView>(true);

            Assert.That(Object.FindObjectsOfType<PayloadRouteView>(true), Is.Empty);
            Assert.That(relays, Has.Length.EqualTo(4));
            Assert.That(relays.Count(relay => !relay.IsFinalDestination), Is.EqualTo(3));
            Assert.That(relays.Count(relay => relay.IsFinalDestination), Is.EqualTo(1));
            Assert.That(Object.FindObjectsOfType<PayloadNavigationIndicator>(true), Has.Length.EqualTo(1));
            Assert.That(relays.Count(relay => relay.transform.Find("Supply Cache") != null), Is.EqualTo(3));
            Assert.That(relays.Single(relay => relay.IsFinalDestination).name, Is.EqualTo("Final Destination Relay"));
            Assert.That(payload.transform.Cast<Transform>().Count(child => child.name.StartsWith("Collected Cargo ")), Is.EqualTo(3));
        }

        [Test]
        public void BuildCreatesNaturalRoadWithoutRestoringWorldRouteLine()
        {
            FrontendSceneBuilder.BuildOrUpdateAll();
            EditorSceneManager.OpenScene(MissionPrototypeSceneBuilder.ScenePath, OpenSceneMode.Single);
            var roads = Object.FindObjectsOfType<PayloadRoadView>(true);

            Assert.That(roads, Has.Length.EqualTo(1));
            Assert.That(roads[0].RoadWidth, Is.EqualTo(5.5f));
            Assert.That(roads[0].RoadMesh, Is.Not.Null);
            Assert.That(roads[0].RoadMesh.vertexCount, Is.GreaterThan(0));
            Assert.That(roads[0].RoadMesh.subMeshCount, Is.EqualTo(2));
            Assert.That(roads[0].GetComponent<MeshRenderer>().sharedMaterials.Select(item => item.name),
                Is.EqualTo(new[] { "Earth", "Moss" }));
            Assert.That(roads[0].RoadsideFoliageCount, Is.InRange(8, 30));
            Assert.That(roads[0].GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(Object.FindObjectsOfType<PayloadRouteView>(true), Is.Empty);
        }

        [Test]
        public void RepeatedBuildKeepsOneMissionRootAndOneBuildEntry()
        {
            FrontendSceneBuilder.BuildOrUpdateAll();
            FrontendSceneBuilder.BuildOrUpdateAll();
            var scene = EditorSceneManager.OpenScene(MissionPrototypeSceneBuilder.ScenePath, OpenSceneMode.Single);

            Assert.That(scene.GetRootGameObjects().Count(root => root.name == "Mission Prototype"), Is.EqualTo(1));
            Assert.That(scene.GetRootGameObjects().Count(root => root.name == "Player"), Is.EqualTo(0));
            Assert.That(EditorBuildSettings.scenes.Count(item => item.path == MissionPrototypeSceneBuilder.ScenePath), Is.EqualTo(1));
        }

        private static GameObject Find(Scene scene, string name) => scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Transform>(true)).Select(item => item.gameObject)
            .Single(candidate => candidate.name == name);

        private static void AssertVisual(GameObject actor, VisualArchetype expected)
        {
            var visuals = actor.GetComponentsInChildren<LowPolyVisual>(true);
            Assert.That(actor.GetComponent<Renderer>(), Is.Null);
            Assert.That(visuals, Has.Length.EqualTo(1));
            Assert.That(visuals[0].Archetype, Is.EqualTo(expected));
        }

        private static bool Overlaps(Vector2 firstCenter, Vector2 firstSize, Vector2 secondCenter, Vector2 secondSize) =>
            Mathf.Abs(firstCenter.x - secondCenter.x) < (firstSize.x + secondSize.x) * .5f &&
            Mathf.Abs(firstCenter.y - secondCenter.y) < (firstSize.y + secondSize.y) * .5f;
    }
}
