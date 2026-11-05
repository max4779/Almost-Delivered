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

            mission.Tick(8f);
            Assert.That(mission.Snapshot.Phase, Is.EqualTo(PayloadMissionPhase.Escorting));
            for (var checkpoint = 0; checkpoint < 3; checkpoint++)
            {
                MovePlayerIntoEscortRadius(player, payload);
                mission.Tick(1000f);
                Assert.That(mission.Snapshot.PlayersInRange, Is.EqualTo(1));
                Assert.That(mission.Snapshot.EnemiesInRange, Is.Zero);
                Assert.That(mission.Snapshot.Phase, Is.EqualTo(PayloadMissionPhase.CheckpointDefense));
                ClearWave(waves);
                Assert.That(mission.Snapshot.Phase, Is.EqualTo(PayloadMissionPhase.Escorting));
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
            mission.enabled = false;
            mission.Tick(8f);
            var minimap = Object.FindObjectOfType<MissionMinimap>();
            var hud = Object.FindObjectOfType<MissionHud>();
            hud.Refresh();
            minimap.Refresh();

            Assert.That(hud.HasPayloadViews, Is.True);
            Assert.That(minimap.RouteSegmentCount, Is.GreaterThan(0));
            Assert.That(minimap.CheckpointMarkerCount, Is.EqualTo(4));
            Assert.That(minimap.PlayerIcon, Is.Not.Null);
            Assert.That(minimap.PayloadIcon, Is.Not.Null);
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

        private static IEnumerator LoadMission()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(MissionPath, new LoadSceneParameters(LoadSceneMode.Single));
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(GameSceneFlow.MissionPrototypeScene));
            yield return null;
        }
    }
}
