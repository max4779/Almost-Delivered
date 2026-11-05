using System.Linq;
using AlmostDelivered.Combat;
using AlmostDelivered.Gameplay;
using AlmostDelivered.Gameplay.Payload;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace AlmostDelivered.Tests.PlayMode
{
    public sealed class MissionMinimapTests
    {
        private GameObject root;
        private Transform player;
        private Transform payload;
        private PayloadMissionController mission;
        private PayloadWaveSpawner waves;
        private MissionMinimap minimap;
        private PayloadRoutePlan route;
        private readonly Rect bounds = new Rect(-28f, -50f, 56f, 100f);

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Minimap test root");
            player = Actor("Player", CombatTeamId.Player).transform;
            player.SetParent(root.transform, false);
            payload = new GameObject("Payload").transform;
            payload.SetParent(root.transform, false);

            var missionObject = new GameObject("Mission");
            missionObject.transform.SetParent(root.transform, false);
            mission = missionObject.AddComponent<PayloadMissionController>();
            mission.Configure(false, 77, bounds, player, payload, null, null, 10f, 0f, 4.5f);
            route = mission.RoutePlan;

            var projectile = new GameObject("Projectile", typeof(CombatProjectile));
            projectile.transform.SetParent(root.transform, false);
            projectile.SetActive(false);
            var waveObject = new GameObject("Waves");
            waveObject.transform.SetParent(root.transform, false);
            waves = waveObject.AddComponent<PayloadWaveSpawner>();
            waves.Configure(player, projectile, null, null, null, null, null, new[] { root.transform }, 77, 18);
            waves.SpawnWave(1, 0, false);

            var panel = new GameObject("Minimap", typeof(RectTransform), typeof(Image), typeof(MissionMinimap));
            panel.transform.SetParent(root.transform, false);
            panel.GetComponent<RectTransform>().sizeDelta = new Vector2(220f, 180f);
            minimap = panel.GetComponent<MissionMinimap>();
            minimap.Configure(bounds, route, player, payload, mission, waves);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(root);
        }

        [Test]
        public void ConfigureCreatesStaticRouteAndCheckpointViewsOnce()
        {
            Assert.That(minimap.RouteSegmentCount, Is.EqualTo(route.Points.Count - 1));
            Assert.That(minimap.CheckpointMarkerCount, Is.EqualTo(4));

            minimap.Refresh();
            minimap.Refresh();

            Assert.That(minimap.RouteSegmentCount, Is.EqualTo(route.Points.Count - 1));
            Assert.That(minimap.CheckpointMarkerCount, Is.EqualTo(4));
        }

        [Test]
        public void PlayerPayloadAndEnemyIconsTrackProjectedPositions()
        {
            player.position = new Vector3(10f, 0f, 20f);
            payload.position = new Vector3(-10f, 0f, -20f);
            var enemy = waves.LivingEnemies.First();
            enemy.transform.position = new Vector3(5f, 0f, 10f);

            minimap.Refresh();
            var panelRect = minimap.GetComponent<RectTransform>().rect;

            Assert.That(minimap.PlayerIcon.anchoredPosition, Is.EqualTo(MissionMinimap.ProjectWorld(player.position, bounds, panelRect)));
            Assert.That(minimap.PayloadIcon.anchoredPosition, Is.EqualTo(MissionMinimap.ProjectWorld(payload.position, bounds, panelRect)));
            Assert.That(minimap.GetEnemyIcon(enemy.EnemyId).anchoredPosition, Is.EqualTo(MissionMinimap.ProjectWorld(enemy.transform.position, bounds, panelRect)));
        }

        [Test]
        public void DeadEnemyIconDisappearsOnRefresh()
        {
            var enemy = waves.LivingEnemies.First();
            Assert.That(minimap.EnemyIconCount, Is.EqualTo(10));

            enemy.GetComponent<ActorHealth>().ApplyDamage(10000f);
            minimap.Refresh();

            Assert.That(minimap.EnemyIconCount, Is.EqualTo(9));
            Assert.That(minimap.GetEnemyIcon(enemy.EnemyId), Is.Null);
        }

        [Test]
        public void PayloadHudShowsPhaseProgressCheckpointEscortAndTime()
        {
            var hudObject = new GameObject("HUD", typeof(MissionHud));
            hudObject.transform.SetParent(root.transform, false);
            var hud = hudObject.GetComponent<MissionHud>();
            var phase = Text("Phase", hudObject.transform);
            var progress = Text("Progress", hudObject.transform);
            var checkpoint = Text("Checkpoint", hudObject.transform);
            var escort = Text("Escort", hudObject.transform);
            var elapsed = Text("Elapsed", hudObject.transform);
            hud.ConfigurePayload(mission, phase, progress, checkpoint, escort, elapsed);
            mission.ApplySnapshot(new PayloadMissionSnapshot(
                PayloadMissionPhase.CheckpointDefense,
                125f,
                route.TotalLength * .5f,
                .5f,
                1,
                2,
                4,
                1,
                2,
                false,
                true));

            hud.Refresh();

            Assert.That(phase.text, Does.Contain("CHECKPOINT DEFENSE"));
            Assert.That(progress.text, Does.Contain("50%"));
            Assert.That(checkpoint.text, Does.Contain("2 / 3"));
            Assert.That(escort.text, Does.Contain("CONTESTED"));
            Assert.That(elapsed.text, Does.Contain("02:05"));
        }

        private static GameObject Actor(string name, CombatTeamId team)
        {
            var actor = new GameObject(name);
            actor.AddComponent<CapsuleCollider>();
            actor.AddComponent<ActorHealth>();
            actor.AddComponent<CombatTeam>().Team = team;
            return actor;
        }

        private static Text Text(string name, Transform parent)
        {
            var textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            textObject.transform.SetParent(parent, false);
            return textObject.GetComponent<Text>();
        }
    }
}
