using System.Collections;
using System.Linq;
using AlmostDelivered.Frontend;
using AlmostDelivered.Gameplay;
using AlmostDelivered.Gameplay.Payload;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace AlmostDelivered.Tests.PlayMode
{
    public sealed class MissionPrototypeIntegrationTests
    {
        private const string MissionPath = "Assets/AlmostDelivered/Scenes/MissionPrototype.unity";
        private static readonly Rect SafeSpawnBounds = Rect.MinMaxRect(-27.5f, -49.5f, 27.5f, 49.5f);

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            var loaded = SceneManager.GetActiveScene();
            var empty = SceneManager.CreateScene("Mission prototype cleanup");
            SceneManager.SetActiveScene(empty);
            if (loaded.IsValid() && loaded.isLoaded)
                yield return SceneManager.UnloadSceneAsync(loaded);
        }

        [UnityTest]
        public IEnumerator PlayerMovementUpdatesTrackingCamera()
        {
            yield return LoadMission();
            var player = GameObject.Find("Player");
            var mission = Object.FindObjectOfType<PayloadMissionController>();
            mission.enabled = false;
            var controller = player.GetComponent<MissionPlayerController>();
            var follow = GameObject.Find("Main Camera").GetComponent<QuarterViewCamera>();
            var playerStart = player.transform.position;
            var cameraStart = follow.transform.position;

            Assert.That(controller.Move(Vector2.right, .25f), Is.True);
            yield return null;

            var playerDelta = player.transform.position - playerStart;
            var cameraDelta = follow.transform.position - cameraStart;
            Assert.That(playerDelta.x, Is.GreaterThan(1f));
            Assert.That((cameraDelta - playerDelta).sqrMagnitude, Is.LessThan(.0001f));
        }

        [UnityTest]
        public IEnumerator AcceleratedEscortClearsAllCheckpointsAndFinalDefense()
        {
            yield return LoadMission();
            var mission = Object.FindObjectOfType<PayloadMissionController>();
            var waves = Object.FindObjectOfType<PayloadWaveSpawner>();
            var player = GameObject.Find("Player").transform;
            var payload = GameObject.Find("Payload").transform;
            mission.enabled = false;

            RecoverCargo(mission, waves, player, payload);
            for (var checkpoint = 0; checkpoint < 3; checkpoint++)
            {
                MovePlayerIntoEscortRadius(player, payload);
                mission.Tick(1000f);
                Assert.That(mission.Snapshot.PlayersInRange, Is.EqualTo(1));
                Assert.That(mission.Snapshot.EnemiesInRange, Is.Zero);
                Assert.That(mission.Snapshot.Phase, Is.EqualTo(PayloadMissionPhase.CheckpointDefense));
                ClearWave(waves);
                Assert.That(mission.Snapshot.Phase, Is.EqualTo(PayloadMissionPhase.Escorting));
                Assert.That(mission.Snapshot.IsRelayChoicePending, Is.True);
                Assert.That(mission.ChooseRelayOption(mission.RouteOptions[checkpoint].id), Is.True);
                Assert.That(mission.Snapshot.IsRelayChoicePending, Is.False);
            }

            MovePlayerIntoEscortRadius(player, payload);
            mission.Tick(1000f);
            Assert.That(mission.Snapshot.PlayersInRange, Is.EqualTo(1));
            Assert.That(mission.Snapshot.EnemiesInRange, Is.Zero);
            Assert.That(mission.Snapshot.Phase, Is.EqualTo(PayloadMissionPhase.FinalDefense));
            ClearWave(waves);
            Assert.That(mission.Snapshot.Phase, Is.EqualTo(PayloadMissionPhase.Completed));
            Assert.That(payload.position, Is.EqualTo(mission.RoutePlan.Destination));
        }

        [UnityTest]
        public IEnumerator SceneHudAndMinimapReflectPayloadMission()
        {
            yield return LoadMission();
            var mission = Object.FindObjectOfType<PayloadMissionController>();
            var waves = Object.FindObjectOfType<PayloadWaveSpawner>();
            var player = GameObject.Find("Player").transform;
            var payload = GameObject.Find("Payload").transform;
            mission.enabled = false;
            var minimap = Object.FindObjectOfType<MissionMinimap>();
            var hud = Object.FindObjectOfType<MissionHud>();
            hud.Refresh();
            minimap.Refresh();

            Assert.That(hud.HasPayloadViews, Is.True);
            Assert.That(mission.Snapshot.Phase, Is.EqualTo(PayloadMissionPhase.Recovering));
            Assert.That(mission.Snapshot.RemainingEnemyCount, Is.EqualTo(6));
            Assert.That(minimap.RouteSegmentCount, Is.GreaterThan(0));
            Assert.That(minimap.CheckpointMarkerCount, Is.EqualTo(4));
            Assert.That(minimap.PlayerIcon, Is.Not.Null);
            Assert.That(minimap.PayloadIcon, Is.Not.Null);
            Assert.That(minimap.PayloadIcon.gameObject.activeSelf, Is.True);
            Assert.That(minimap.EnemyIconCount, Is.EqualTo(14));
            Assert.That(minimap.transform.Find("Route Segment 1").gameObject.activeSelf, Is.False);
            Assert.That(minimap.GetComponent<RectMask2D>(), Is.Not.Null);
            Assert.That(Object.FindObjectsOfType<PayloadRelayBeaconView>(true)
                .Count(relay => relay.VisualState == PayloadRelayVisualState.Active), Is.Zero);
            var minimapRect = minimap.GetComponent<RectTransform>();

            var escortRadius = Object.FindObjectOfType<PayloadEscortRadiusView>();
            Assert.That(escortRadius, Is.Not.Null);
            Assert.That(escortRadius.Radius, Is.EqualTo(4.5f));
            Assert.That(escortRadius.GetComponent<LineRenderer>().enabled, Is.True,
                "The interaction boundary must be visible while recovering the cargo.");

            RecoverCargo(mission, waves, player, payload);
            hud.Refresh();
            minimap.Refresh();

            Assert.That(minimap.transform.Find("Route Segment 1").gameObject.activeSelf, Is.True);
            foreach (var view in minimap.GetComponentsInChildren<RectTransform>()
                         .Where(item => item.name.StartsWith("Route Segment") ||
                                        item.name.StartsWith("Checkpoint") ||
                                        item.name == "Destination"))
            {
                var corners = new Vector3[4];
                view.GetWorldCorners(corners);
                Assert.That(corners.All(corner => minimapRect.rect.Contains(minimapRect.InverseTransformPoint(corner))), Is.True, view.name);
            }
            Assert.That(escortRadius.GetComponent<LineRenderer>().enabled, Is.True);
            Assert.That(Object.FindObjectsOfType<PayloadRouteView>(true), Is.Empty);
            Assert.That(Object.FindObjectsOfType<PayloadRelayBeaconView>(true).Count(relay => relay.VisualState == PayloadRelayVisualState.Active), Is.EqualTo(1));
            Assert.That(Object.FindObjectsOfType<PayloadRelayBeaconView>(true).Single(relay => relay.CheckpointIndex == 0).VisualState, Is.EqualTo(PayloadRelayVisualState.Active));
            Assert.That(Object.FindObjectsOfType<PayloadRelayBeaconView>(true).Single(relay => relay.CheckpointIndex == 0)
                .transform.Find("Relay Label").GetComponent<TextMesh>().text, Does.EndWith("m"));
        }

        [UnityTest]
        public IEnumerator EscortSpawnsTravelCombatThenLoadsSuppliesAtRelay()
        {
            yield return LoadMission();
            var mission = Object.FindObjectOfType<PayloadMissionController>();
            var waves = Object.FindObjectOfType<PayloadWaveSpawner>();
            var minimap = Object.FindObjectOfType<MissionMinimap>();
            var player = GameObject.Find("Player").transform;
            var payload = GameObject.Find("Payload").transform;
            var firstRelay = Object.FindObjectsOfType<PayloadRelayBeaconView>(true)
                .Single(relay => relay.CheckpointIndex == 0);
            var firstCargo = payload.Find("Collected Cargo 1").gameObject;
            mission.enabled = false;

            RecoverCargo(mission, waves, player, payload);
            mission.Tick(8f);
            minimap.Refresh();

            Assert.That(mission.Snapshot.Phase, Is.EqualTo(PayloadMissionPhase.Escorting));
            Assert.That(waves.LivingEnemies, Is.Not.Empty);
            Assert.That(waves.LivingEnemies.All(enemy => enemy.EnemyId < 0), Is.True);
            Assert.That(Object.FindObjectsOfType<PayloadRouteView>(true), Is.Empty);
            Assert.That(minimap.RouteSegmentCount, Is.GreaterThan(0));

            MovePlayerIntoEscortRadius(player, payload);
            mission.Tick(1000f);

            Assert.That(mission.Snapshot.Phase, Is.EqualTo(PayloadMissionPhase.CheckpointDefense));
            Assert.That(firstRelay.VisualState, Is.EqualTo(PayloadRelayVisualState.Loading));
            Assert.That(firstCargo.activeSelf, Is.False);

            ClearWave(waves);

            Assert.That(mission.Snapshot.NextCheckpointIndex, Is.EqualTo(1));
            Assert.That(firstRelay.VisualState, Is.EqualTo(PayloadRelayVisualState.Completed));
            Assert.That(firstCargo.activeSelf, Is.True);
        }

        private static void ClearWave(PayloadWaveSpawner waves)
        {
            while (waves.ActiveCount > 0)
                foreach (var enemy in waves.LivingEnemies.ToArray())
                {
                    Assert.That(
                        SafeSpawnBounds.Contains(new Vector2(enemy.transform.position.x, enemy.transform.position.z)),
                        Is.True,
                        $"{enemy.name} spawned outside the mission arena at {enemy.transform.position}.");
                    enemy.GetComponent<AlmostDelivered.Combat.ActorHealth>().ApplyDamage(10000f);
                }
            Assert.That(waves.PendingCount, Is.Zero);
        }

        private static void MovePlayerIntoEscortRadius(Transform player, Transform payload)
        {
            var character = player.GetComponent<CharacterController>();
            character.enabled = false;
            player.position = payload.position + Vector3.right * 3f;
            character.enabled = true;
            Physics.SyncTransforms();
        }

        private static void RecoverCargo(
            PayloadMissionController mission,
            PayloadWaveSpawner waves,
            Transform player,
            Transform payload)
        {
            Assert.That(mission.Snapshot.Phase, Is.EqualTo(PayloadMissionPhase.Recovering));
            Assert.That(waves.LivingEnemies.Count(enemy => enemy.WaveId == PayloadWaveSpawner.RecoveryWaveId),
                Is.EqualTo(6));
            foreach (var guard in waves.LivingEnemies
                         .Where(enemy => enemy.WaveId == PayloadWaveSpawner.RecoveryWaveId)
                         .ToArray())
            {
                guard.GetComponent<AlmostDelivered.Combat.ActorHealth>().ApplyDamage(10000f);
            }
            Assert.That(mission.Snapshot.RemainingEnemyCount, Is.Zero);
            MovePlayerIntoEscortRadius(player, payload);
            var activationView = Object.FindObjectOfType<PayloadActivationView>(true);
            Assert.That(activationView, Is.Not.Null);
            mission.Tick(0f);
            Assert.That(mission.Snapshot.Phase, Is.EqualTo(PayloadMissionPhase.Recovering));

            mission.Tick(0f, true);
            Assert.That(mission.Snapshot.Phase, Is.EqualTo(PayloadMissionPhase.Activating));
            Assert.That(activationView.IsVisible, Is.True);
            Assert.That(activationView.Label.text, Does.Contain("0%"));
            Assert.That(waves.LivingEnemies.Count(enemy => enemy.WaveId == PayloadWaveSpawner.ActivationWaveId(0)), Is.EqualTo(3));
            Assert.That(waves.ActiveCount, Is.EqualTo(11));

            mission.Tick(1.5f, true);
            Assert.That(mission.Snapshot.ActivationProgress, Is.EqualTo(.5f).Within(.001f));
            Assert.That(activationView.Label.text, Does.Contain("50%"));
            Assert.That(waves.LivingEnemies.Count(enemy => enemy.WaveId == PayloadWaveSpawner.ActivationWaveId(1)), Is.EqualTo(3));
            Assert.That(waves.ActiveCount, Is.EqualTo(14));

            mission.Tick(1.5f, true);
            Assert.That(mission.Snapshot.Phase, Is.EqualTo(PayloadMissionPhase.Escorting));
            Assert.That(activationView.IsVisible, Is.False);
        }

        private static IEnumerator LoadMission()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(MissionPath, new LoadSceneParameters(LoadSceneMode.Single));
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(GameSceneFlow.MissionPrototypeScene));
            yield return null;
        }
    }
}
