using System.Collections;
using System.Linq;
using AlmostDelivered.Combat;
using AlmostDelivered.Frontend;
using AlmostDelivered.Gameplay;
using AlmostDelivered.Gameplay.Payload;
using AlmostDelivered.MapGeneration;
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
        private static readonly Rect SafeSpawnBounds = Rect.MinMaxRect(-125f, -250f, 125f, 250f);

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
        public IEnumerator CargoBlueprintLosesRightLeftCenterThenShowsGameOver()
        {
            yield return LoadMission();
            var mission = Object.FindObjectOfType<PayloadMissionController>();
            var integrity = GameObject.Find("Payload").GetComponent<CargoIntegrity>();
            var card = GameObject.Find("Cargo Stock Blueprint").transform;
            var stocks = card.Find("Cargo Stocks").GetComponent<Text>();
            var left = card.Find("Blueprint Module 0 Frame/Blueprint Module 0").GetComponent<Image>();
            var center = card.Find("Blueprint Module 1 Frame/Blueprint Module 1").GetComponent<Image>();
            var right = card.Find("Blueprint Module 2 Frame/Blueprint Module 2").GetComponent<Image>();
            var gameOver = card.parent.Find("Cargo Game Over").gameObject;

            Assert.That(stocks.text, Does.Contain("3 / 3"));
            Assert.That(mission.IsAuthority, Is.True);
            Assert.That(mission.CargoHealth, Is.SameAs(integrity));
            for (var loss = 1; loss <= 3; loss++)
            {
                integrity.ApplyDamage(integrity.MaximumHealth);
                Assert.That(integrity.CurrentHealth, Is.Zero);
                Assert.That(mission.Snapshot.CargoDestructions, Is.EqualTo(loss));
                Assert.That(stocks.text, Does.Contain($"{3 - loss} / 3"));
                Assert.That(right.color.r > right.color.b, Is.True);
                Assert.That(left.color.r > left.color.b, Is.EqualTo(loss >= 2));
                Assert.That(center.color.r > center.color.b, Is.EqualTo(loss >= 3));
                if (loss < 3) integrity.Repair(integrity.MaximumHealth);
            }

            Assert.That(mission.Snapshot.Phase, Is.EqualTo(PayloadMissionPhase.Failed));
            Assert.That(gameOver.activeSelf, Is.True);
        }

        [UnityTest]
        public IEnumerator SpawnIsSafeAndCommanderTracksScatteredMobs()
        {
            yield return LoadMission();
            var player = GameObject.Find("Player").transform;
            var cargo = GameObject.Find("Payload").transform;
            var waves = Object.FindObjectOfType<PayloadWaveSpawner>();
            var commander = Object.FindObjectOfType<MissionCommanderDirector>();
            var scouts = Object.FindObjectsOfType<MissionScoutController>();
            var foliage = Object.FindObjectsOfType<MissionMapPieceInstance>()
                .Where(piece => piece.Category == MissionMapPieceCategory.Foliage).ToArray();

            Assert.That(Vector3.Distance(player.position, cargo.position), Is.GreaterThan(45f));
            Assert.That(waves.LivingEnemies.All(enemy =>
                Vector3.Distance(enemy.transform.position, player.position) >= 35f), Is.True);
            Assert.That(waves.LivingEnemies.Count(enemy =>
                enemy.WaveId == PayloadWaveSpawner.PreplacedSentryWaveId), Is.EqualTo(8));
            Assert.That(commander.TrackedMobCount, Is.EqualTo(waves.ActiveCount));
            Assert.That(scouts, Has.Length.EqualTo(3));
            foreach (var scout in scouts)
            {
                Assert.That(scout.FleeSpeed,
                    Is.InRange(MissionMobDefinition.For(MissionMobArchetype.Melee).MoveSpeed - 1f,
                        MissionMobDefinition.For(MissionMobArchetype.Melee).MoveSpeed));
                Assert.That(Vector3.Distance(scout.transform.position, player.position), Is.GreaterThan(48f));
                Assert.That(foliage.Any(piece => piece.name.Contains("TallGrass") &&
                    Vector2.Distance(piece.Position,
                        new Vector2(scout.transform.position.x, scout.transform.position.z)) < 1f), Is.True);
            }
        }

        [UnityTest]
        public IEnumerator RangedEnemiesFireFromCarbineAndUseDistinctPulseTemplate()
        {
            yield return LoadMission();
            var waves = Object.FindObjectOfType<PayloadWaveSpawner>();
            var ranged = waves.LivingEnemies.First(enemy =>
                enemy.Definition.Archetype == MissionMobArchetype.Ranged);
            var attack = ranged.GetComponent<RangedAttackController>();
            var pulse = Object.FindObjectsOfType<CombatProjectile>(true).First(projectile =>
                projectile.name == "Enemy Pulse Template");

            Assert.That(attack.Muzzle.parent.name, Is.EqualTo("Carbine Muzzle"));
            Assert.That(ranged.transform.Find("VisualRoot/BodyPivot/RightArmPivot/Weapon/Carbine Cell"),
                Is.Not.Null);
            Assert.That(pulse.GetComponent<Renderer>().sharedMaterial.color.r,
                Is.GreaterThan(pulse.GetComponent<Renderer>().sharedMaterial.color.b));
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
                mission.AddFuel(240f);
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
            mission.AddFuel(240f);
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
            Assert.That(minimap.EnemyIconCount, Is.GreaterThanOrEqualTo(14));
            Assert.That(Object.FindObjectsOfType<MissionScoutController>(), Has.Length.EqualTo(3));
            Assert.That(minimap.EnemyIconCount, Is.EqualTo(waves.ActiveCount),
                "Scout markers must remain separate from regular enemy markers.");
            Assert.That(minimap.ShowScoutsForDevelopment, Is.True);
            Assert.That(minimap.ScoutIconCount, Is.EqualTo(3));
            Assert.That(minimap.transform.Find("Scout 1"), Is.Not.Null);
            minimap.SetScoutDebugVisibility(false);
            Assert.That(minimap.ScoutIconCount, Is.Zero);
            minimap.SetScoutDebugVisibility(true);
            Assert.That(minimap.ScoutIconCount, Is.EqualTo(3));
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
            mission.AddFuel(240f);
            mission.Tick(1000f);

            Assert.That(mission.Snapshot.Phase, Is.EqualTo(PayloadMissionPhase.CheckpointDefense));
            Assert.That(firstRelay.VisualState, Is.EqualTo(PayloadRelayVisualState.Loading));
            Assert.That(firstCargo.activeSelf, Is.False);

            ClearWave(waves);

            Assert.That(mission.Snapshot.NextCheckpointIndex, Is.EqualTo(1));
            Assert.That(firstRelay.VisualState, Is.EqualTo(PayloadRelayVisualState.Completed));
            Assert.That(firstCargo.activeSelf, Is.True);
        }

        [UnityTest]
        public IEnumerator ScoutReportAlertsCommanderAndSendsLastKnownPositionOrder()
        {
            yield return LoadMission();
            var network = Object.FindObjectOfType<MissionCommanderDirector>();
            var scout = Object.FindObjectOfType<MissionScoutController>();
            var player = GameObject.Find("Player").GetComponent<ObservableActor>();
            var waves = Object.FindObjectOfType<PayloadWaveSpawner>();
            var boss = waves.FindEnemy(PayloadWaveSpawner.SideCommanderEnemyId);
            var roamer = waves.LivingEnemies.First(enemy =>
                enemy.WaveId == PayloadWaveSpawner.PreplacedSentryWaveId);

            Assert.That(network, Is.Not.Null);
            Assert.That(scout, Is.Not.Null);
            Assert.That(boss, Is.Not.Null);
            Assert.That(network.ReportScout(scout, player,
                roamer.transform.position + Vector3.right * 5f, Vector3.right), Is.True);
            Assert.That(network.IsCoordinating, Is.True);
            Assert.That(network.ReportCount, Is.EqualTo(1));
            Assert.That(roamer.AwarenessState, Is.EqualTo(MissionMobAwarenessState.Vigilant));
            Assert.That(boss.AwarenessState, Is.EqualTo(MissionMobAwarenessState.Unaware));
        }

        [UnityTest]
        public IEnumerator ScoutSightImmediatelyReachesCommanderWithoutDelayedReport()
        {
            yield return LoadMission();
            var network = Object.FindObjectOfType<MissionCommanderDirector>();
            var scout = Object.FindObjectOfType<MissionScoutController>();
            var player = GameObject.Find("Player");
            player.GetComponent<MissionPlayerController>().enabled = false;
            var character = player.GetComponent<CharacterController>();
            character.enabled = false;
            player.transform.position = scout.transform.position + Vector3.forward * 2f;
            character.enabled = true;
            Physics.SyncTransforms();

            var deadline = Time.time + .6f;
            while (!network.HasLiveScoutSight && Time.time < deadline)
                yield return null;

            Assert.That(scout.IsWatchingPlayer, Is.True);
            Assert.That(network.TryGetLiveScoutSight(out var reporterId, out var observedPosition), Is.True);
            Assert.That(reporterId, Is.EqualTo(scout.ReporterId));
            Assert.That(Vector3.Distance(observedPosition, player.transform.position), Is.LessThan(.5f));
            Assert.That(network.ReportCount, Is.GreaterThan(0));

            scout.GetComponent<ActorHealth>().ApplyDamage(1000f);
            Assert.That(network.HasLiveScoutSight, Is.False);
        }

        [UnityTest]
        public IEnumerator CommanderLearnsOnlyWeaponActionsSeenByAScout()
        {
            yield return LoadMission();
            var network = Object.FindObjectOfType<MissionCommanderDirector>();
            var scout = Object.FindObjectOfType<MissionScoutController>();
            var player = GameObject.Find("Player");
            var controller = player.GetComponent<MissionPlayerController>();
            var character = player.GetComponent<CharacterController>();
            character.enabled = false;
            player.transform.position = scout.transform.position + Vector3.forward * 2f;
            character.enabled = true;
            Physics.SyncTransforms();
            var deadline = Time.time + .6f;
            while (!network.HasLiveScoutSight && Time.time < deadline)
                yield return null;
            Assert.That(network.HasLiveScoutSight, Is.True);

            controller.SetWeaponType(PlayerWeaponType.Revolver);
            var ranged = player.GetComponent<RangedAttackController>();
            Assert.That(ranged.TryFire(player.transform.position + Vector3.forward * 15f, Time.time), Is.True);
            Assert.That(ranged.TryReload(Time.time), Is.True);
            controller.SetWeaponType(PlayerWeaponType.Melee);
            Assert.That(player.GetComponent<MeleeAttackController>().TryAttack(Time.time, Vector3.forward), Is.True);

            var actor = player.GetComponent<ObservableActor>();
            Assert.That(network.TryGetCombatProfile(actor.ActorId, out var profile), Is.True);
            Assert.That(profile.ShotsObserved, Is.EqualTo(1));
            Assert.That(profile.ReloadsObserved, Is.EqualTo(1));
            Assert.That(profile.MeleeAttacksObserved, Is.EqualTo(1));
            Assert.That(profile.LastObservedWeapon, Is.EqualTo(PlayerWeaponType.Melee));

            character.enabled = false;
            player.transform.position = scout.transform.position + Vector3.right * 35f;
            character.enabled = true;
            Physics.SyncTransforms();
            Assert.That(player.GetComponent<MeleeAttackController>().TryAttack(Time.time + 10f, Vector3.forward), Is.True);
            Assert.That(profile.MeleeAttacksObserved, Is.EqualTo(1),
                "Actions outside every scout's line of sight must remain unknown to the commander.");
        }

        [UnityTest]
        public IEnumerator CommanderPlansRifleCounterOnlyAfterScoutSightAndStopsAfterDeath()
        {
            yield return LoadMission();
            var network = Object.FindObjectOfType<MissionCommanderDirector>();
            var scout = Object.FindObjectOfType<MissionScoutController>();
            var player = GameObject.Find("Player");
            var waves = Object.FindObjectOfType<PayloadWaveSpawner>();
            var encounter = new PayloadEscortEncounter(7, 5, PayloadAssaultPattern.LeftAmbush);

            Assert.That(network.TryPlanTravelGroup(encounter, false, out _), Is.False);
            player.GetComponent<MissionPlayerController>().SetWeaponType(PlayerWeaponType.Ranged);
            player.GetComponent<MissionPlayerController>().enabled = false;
            var character = player.GetComponent<CharacterController>();
            character.enabled = false;
            player.transform.position = scout.transform.position + Vector3.forward * 2f;
            character.enabled = true;
            Physics.SyncTransforms();
            var deadline = Time.time + .6f;
            while (!network.HasLiveScoutSight && Time.time < deadline)
                yield return null;

            Assert.That(network.HasLiveScoutSight, Is.True);
            Assert.That(network.TryPlanTravelGroup(encounter, false, out var plan), Is.True);
            Assert.That(plan.Count(type => type == MissionMobArchetype.Shield), Is.EqualTo(3));

            waves.FindEnemy(PayloadWaveSpawner.SideCommanderEnemyId)
                .GetComponent<ActorHealth>().ApplyDamage(1000f);
            Assert.That(network.TryPlanTravelGroup(encounter, false, out _), Is.False);
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
            Assert.That(mission.HasActivatedCargo, Is.False);
            Assert.That(waves.LivingEnemies.Count(enemy => enemy.WaveId == PayloadWaveSpawner.ActivationWaveId(0)), Is.Zero);

            mission.Tick(1.5f, true);
            Assert.That(mission.Snapshot.ActivationProgress, Is.EqualTo(.5f).Within(.001f));
            Assert.That(activationView.Label.text, Does.Contain("50%"));
            Assert.That(waves.LivingEnemies.Count(enemy => enemy.WaveId == PayloadWaveSpawner.ActivationWaveId(1)), Is.Zero);

            mission.Tick(1.5f, true);
            Assert.That(mission.Snapshot.Phase, Is.EqualTo(PayloadMissionPhase.Escorting));
            Assert.That(mission.HasActivatedCargo, Is.True);
            Assert.That(waves.LivingEnemies.Count(enemy => enemy.WaveId == PayloadWaveSpawner.ActivationWaveId(0)), Is.EqualTo(3));
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
