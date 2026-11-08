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
        public void TopRightPivotKeepsEntireRouteAndMarkersInsideMaskedPanel()
        {
            var panel = minimap.GetComponent<RectTransform>();
            panel.pivot = Vector2.one;
            minimap.Configure(bounds, route, player, payload, mission, waves);

            Assert.That(minimap.GetComponent<RectMask2D>(), Is.Not.Null);
            var routeViews = minimap.GetComponentsInChildren<RectTransform>()
                .Where(view => view.name.StartsWith("Route Segment") ||
                               view.name.StartsWith("Checkpoint") ||
                               view.name == "Destination");
            foreach (var view in routeViews)
            {
                var corners = new Vector3[4];
                view.GetWorldCorners(corners);
                foreach (var corner in corners)
                {
                    var local = panel.InverseTransformPoint(corner);
                    Assert.That(panel.rect.Contains(local), Is.True,
                        $"{view.name} corner {local} must remain inside the minimap rectangle {panel.rect}.");
                }
            }
        }

        [Test]
        public void CurrentLegAndCheckpointAreBrighterThanFutureRoute()
        {
            mission.ApplySnapshot(Snapshot(0f, 0));

            minimap.Refresh();

            var currentSegment = minimap.transform.Find("Route Segment 1").GetComponent<Image>();
            var futureSegment = minimap.transform.Find($"Route Segment {route.Points.Count - 1}").GetComponent<Image>();
            var activeCheckpoint = minimap.transform.Find("Checkpoint 1").GetComponent<Image>();
            var futureCheckpoint = minimap.transform.Find("Checkpoint 3").GetComponent<Image>();
            Assert.That(currentSegment.color.a, Is.GreaterThan(futureSegment.color.a));
            Assert.That(activeCheckpoint.color.grayscale, Is.GreaterThan(futureCheckpoint.color.grayscale));
        }

        [Test]
        public void CompletedCheckpointUsesCompletedColor()
        {
            mission.ApplySnapshot(Snapshot(route.CheckpointDistances[0], 1));

            minimap.Refresh();

            var completed = minimap.transform.Find("Checkpoint 1").GetComponent<Image>().color;
            Assert.That(completed.g, Is.GreaterThan(completed.r));
            Assert.That(completed.g, Is.GreaterThan(completed.b));
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

            Assert.That(minimap.PlayerIcon.anchoredPosition, Is.EqualTo(MissionMinimap.ProjectWorldInsidePanel(player.position, bounds, panelRect)));
            Assert.That(minimap.PayloadIcon.anchoredPosition, Is.EqualTo(MissionMinimap.ProjectWorldInsidePanel(payload.position, bounds, panelRect)));
            Assert.That(minimap.GetEnemyIcon(enemy.EnemyId).anchoredPosition, Is.EqualTo(MissionMinimap.ProjectWorldInsidePanel(enemy.transform.position, bounds, panelRect)));
        }

        [Test]
        public void RecoveryShowsCargoAndGuardsWhileHidingTheDeliveryRoute()
        {
            payload.position = new Vector3(-12f, 0f, 24f);
            mission.ApplySnapshot(new PayloadMissionSnapshot(
                PayloadMissionPhase.Recovering,
                15f,
                0f,
                0f,
                0,
                PayloadWaveSpawner.RecoveryWaveId,
                10,
                0,
                10,
                false,
                true));

            minimap.Refresh();

            var panelRect = minimap.GetComponent<RectTransform>().rect;
            Assert.That(minimap.PayloadIcon.gameObject.activeSelf, Is.True);
            Assert.That(minimap.PayloadIcon.anchoredPosition,
                Is.EqualTo(MissionMinimap.ProjectWorldInsidePanel(payload.position, bounds, panelRect)));
            Assert.That(minimap.EnemyIconCount, Is.EqualTo(10));
            Assert.That(minimap.transform.Find("Route Segment 1").gameObject.activeSelf, Is.False);
            Assert.That(minimap.transform.Find("Checkpoint 1").gameObject.activeSelf, Is.False);

            var hudObject = new GameObject("Recovery HUD", typeof(MissionHud));
            hudObject.transform.SetParent(root.transform, false);
            var phase = Text("Recovery Phase", hudObject.transform);
            var objective = Text("Recovery Objective", hudObject.transform);
            hudObject.GetComponent<MissionHud>().ConfigurePayload(mission, phase, null, null, objective, null);
            hudObject.GetComponent<MissionHud>().Refresh();
            Assert.That(phase.text, Does.Contain("RECOVER LOST CARGO"));
            Assert.That(objective.text, Does.Contain("CLEAR CARGO GUARDS"));

            mission.ApplySnapshot(new PayloadMissionSnapshot(
                PayloadMissionPhase.Activating,
                17f,
                0f,
                0f,
                0,
                -1,
                0,
                1,
                0,
                false,
                false,
                .5f));
            minimap.Refresh();
            hudObject.GetComponent<MissionHud>().Refresh();
            Assert.That(minimap.transform.Find("Route Segment 1").gameObject.activeSelf, Is.False);
            Assert.That(minimap.transform.Find("Checkpoint 1").gameObject.activeSelf, Is.False);
            Assert.That(phase.text, Does.Contain("STARTING CARGO"));
            Assert.That(objective.text, Does.Contain("50%"));

            mission.ApplySnapshot(Snapshot(0f, 0));
            minimap.Refresh();

            Assert.That(minimap.transform.Find("Route Segment 1").gameObject.activeSelf, Is.True);
            Assert.That(minimap.transform.Find("Checkpoint 1").gameObject.activeSelf, Is.True);
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
            var targets = Text("Targets", hudObject.transform);
            hud.BindViews(null, null, targets);
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

            Assert.That(phase.text, Does.Contain("LOADING SUPPLIES"));
            Assert.That(progress.text, Does.Contain("50%"));
            Assert.That(checkpoint.text, Does.Contain("2 / 3"));
            Assert.That(escort.text, Does.Contain("LOADING SUPPLIES"));
            Assert.That(elapsed.text, Does.Contain("02:05"));
            Assert.That(targets.text, Does.Contain("4"));
        }

        [Test]
        public void ActivationCountdownShowsRemainingSecondsAndPausesWithInput()
        {
            var hudObject = new GameObject("Countdown HUD", typeof(MissionHud));
            hudObject.transform.SetParent(root.transform, false);
            var hud = hudObject.GetComponent<MissionHud>();
            var countdown = Text("Activation Countdown", hudObject.transform);
            hud.ConfigurePayload(mission, null, null, null, null, null);
            hud.BindActivationCountdown(countdown);

            mission.ApplySnapshot(new PayloadMissionSnapshot(
                PayloadMissionPhase.Activating,
                2f,
                0f,
                0f,
                0,
                -1,
                0,
                1,
                0,
                false,
                false,
                .25f,
                true));
            hud.Refresh();

            Assert.That(countdown.gameObject.activeSelf, Is.True);
            Assert.That(countdown.text, Does.Contain("CARGO STARTUP"));
            Assert.That(countdown.text, Does.Contain("2.3"));

            mission.ApplySnapshot(new PayloadMissionSnapshot(
                PayloadMissionPhase.Activating,
                3f,
                0f,
                0f,
                0,
                -1,
                0,
                1,
                0,
                false,
                false,
                .5f,
                false));
            hud.Refresh();

            Assert.That(countdown.text, Does.Contain("HOLD E"));
            Assert.That(countdown.text, Does.Contain("1.5"));

            mission.ApplySnapshot(Snapshot(0f, 0));
            hud.Refresh();
            Assert.That(countdown.gameObject.activeSelf, Is.False);
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

        private PayloadMissionSnapshot Snapshot(float distance, int nextCheckpointIndex)
        {
            return new PayloadMissionSnapshot(
                PayloadMissionPhase.Escorting,
                20f,
                distance,
                route.TotalLength > 0f ? distance / route.TotalLength : 0f,
                nextCheckpointIndex,
                -1,
                0,
                1,
                0,
                true,
                false);
        }
    }
}
