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
        public IEnumerator MissionHudDoesNotBlockHeldFireUnderTheCursor()
        {
            yield return LoadMission();
            var hud = Object.FindObjectOfType<MissionHud>();
            Assert.That(hud.transform.Find("Mission Phase").GetComponent<Text>().raycastTarget, Is.False);
            Assert.That(hud.transform.Find("Mission Minimap").GetComponent<Image>().raycastTarget, Is.False);
            Assert.That(hud.transform.Find("Player Health Bar").GetComponent<Image>().raycastTarget, Is.False);
            Assert.That(hud.GetComponent<GraphicRaycaster>().enabled, Is.True,
                "Interactive relay choices still need the HUD raycaster.");
        }

        [UnityTest]
        public IEnumerator AutomaticRifleMaintainsFireAgainstAnActualCargoGuard()
        {
            yield return LoadMission();
            var waves = Object.FindObjectOfType<PayloadWaveSpawner>();
            var guard = waves.LivingEnemies.First(enemy => enemy.WaveId == PayloadWaveSpawner.RecoveryWaveId);
            guard.enabled = false;
            var health = guard.GetComponent<ActorHealth>();
            var startingHealth = health.CurrentHealth;
            var player = GameObject.Find("Player");
            var guardCharacter = guard.GetComponent<CharacterController>();
            guardCharacter.enabled = false;
            guard.transform.position = player.transform.position + Vector3.forward * 7f;
            player.transform.forward = Vector3.forward;
            guardCharacter.enabled = true;
            Physics.SyncTransforms();
            player.GetComponent<MissionPlayerController>().SetWeaponType(PlayerWeaponType.AutomaticRifle);
            var attack = player.GetComponent<RangedAttackController>();
            var start = Time.time;
            for (var shot = 0; shot < 5; shot++)
                Assert.That(attack.TryFire(guard.transform.position + Vector3.up, start + shot * .125f),
                    Is.True, $"Shot {shot + 1} stopped while aiming at a real guard.");
            Assert.That(attack.RoundsRemaining, Is.EqualTo(15));
            yield return new WaitForSeconds(.6f);
            Assert.That(health.CurrentHealth, Is.LessThan(startingHealth),
                "The rifle must damage the mission's actual guard hurtbox.");
        }

        [UnityTest]
        public IEnumerator CargoBlueprintLightsLeftRightCenterThenStartsEvacuation()
        {
            yield return LoadMission();
            var mission = Object.FindObjectOfType<PayloadMissionController>();
            var integrity = GameObject.Find("Payload").GetComponent<CargoIntegrity>();
            var card = GameObject.Find("Cargo Stock Blueprint").transform;
            Assert.That(card.parent.Find("Cargo Health"), Is.Null);
            var schematic = card.Find("Payload Top View").GetComponent<CargoBlueprintGraphic>();
            var healthText = card.Find("Cargo Stock Health").GetComponent<Text>();
            var healthFill = card.Find("Cargo Stock Health Track/Cargo Stock Health Fill").GetComponent<Image>();
            Assert.That(card.Find("Cargo Front Label"), Is.Null);
            Assert.That(card.Find("Cargo Rear Label"), Is.Null);
            var gameOver = card.parent.Find("Cargo Game Over").gameObject;

            var cardRect = card.GetComponent<RectTransform>();
            Assert.That(cardRect.anchorMin, Is.EqualTo(new Vector2(0f, 1f)));
            Assert.That(cardRect.anchoredPosition.x, Is.LessThan(100f));
            Assert.That(cardRect.anchoredPosition.y, Is.GreaterThan(-100f));
            Assert.That(card.Find("Cargo Stocks"), Is.Null);
            Assert.That(card.Find("Blueprint Module 0 Frame"), Is.Null);
            Assert.That(card.GetComponentsInChildren<Text>().Any(text =>
                text.text == "L" || text.text == "R" || text.text == "C"), Is.False);
            Assert.That(schematic.Destructions, Is.Zero);
            Assert.That(healthText.text, Is.EqualTo("100%"));
            Assert.That(healthFill.type, Is.EqualTo(Image.Type.Simple));
            Assert.That(healthFill.fillAmount, Is.EqualTo(1f).Within(.001f));
            Assert.That(healthFill.rectTransform.anchorMax.x, Is.EqualTo(1f).Within(.001f));
            Assert.That(mission.IsAuthority, Is.True);
            Assert.That(mission.CargoHealth, Is.SameAs(integrity));
            var damageVfx = integrity.GetComponent<CargoDamageVfx>();
            Assert.That(damageVfx, Is.Not.Null);
            Assert.That(damageVfx.DestructionCueCount, Is.Zero);
            Assert.That(integrity.transform.Find("Cargo Destruction Smoke Burst"), Is.Not.Null);
            Assert.That(integrity.transform.Find("Cargo Destruction Sparks"), Is.Not.Null);
            for (var loss = 1; loss <= 3; loss++)
            {
                integrity.ApplyDamage(integrity.MaximumHealth * .25f);
                Assert.That(healthText.text, Is.EqualTo("75%"));
                Assert.That(healthFill.fillAmount, Is.EqualTo(.75f).Within(.001f));
                Assert.That(healthFill.rectTransform.anchorMax.x, Is.EqualTo(.75f).Within(.001f));
                Assert.That(healthFill.color.b, Is.GreaterThan(.7f));
                integrity.ApplyDamage(integrity.MaximumHealth * .15f);
                Assert.That(healthText.text, Is.EqualTo("60%"));
                Assert.That(healthFill.rectTransform.anchorMax.x, Is.EqualTo(.6f).Within(.001f));
                Assert.That(healthFill.color.r, Is.GreaterThan(.9f));
                Assert.That(healthFill.color.g, Is.GreaterThan(.5f));
                integrity.ApplyDamage(integrity.MaximumHealth * .3f);
                Assert.That(healthText.text, Is.EqualTo("30%"));
                Assert.That(healthFill.rectTransform.anchorMax.x, Is.EqualTo(.3f).Within(.001f));
                Assert.That(healthFill.color.g, Is.LessThan(.3f));
                Assert.That(schematic.Destructions, Is.EqualTo(loss - 1));
                Assert.That(damageVfx.DestructionCueCount, Is.EqualTo(loss - 1));
                integrity.ApplyDamage(integrity.MaximumHealth);
                Assert.That(mission.Snapshot.CargoDestructions, Is.EqualTo(loss));
                Assert.That(schematic.Destructions, Is.EqualTo(loss));
                Assert.That(damageVfx.ActiveDamageStage, Is.EqualTo(loss));
                Assert.That(damageVfx.DestructionCueCount, Is.EqualTo(loss));
                Assert.That(integrity.transform.Find("Cargo Destruction Smoke Burst").GetComponent<ParticleSystem>().particleCount,
                    Is.GreaterThan(0));
                Assert.That(integrity.GetComponent<AudioSource>().isPlaying, Is.True);
                Assert.That(integrity.transform.Find("Cargo Damage Smoke Left").GetComponent<ParticleSystem>().isPlaying,
                    Is.True);
                Assert.That(integrity.transform.Find("Cargo Damage Smoke Right").GetComponent<ParticleSystem>().isPlaying,
                    Is.EqualTo(loss >= 2));
                Assert.That(integrity.transform.Find("Cargo Damage Flame Left").GetComponent<ParticleSystem>().isPlaying,
                    Is.EqualTo(loss >= 2));
                Assert.That(schematic.IsDamaged(0), Is.True);
                Assert.That(schematic.IsDamaged(2), Is.EqualTo(loss >= 2));
                Assert.That(schematic.IsDamaged(1), Is.EqualTo(loss >= 3));
                if (loss < 3)
                {
                    Assert.That(integrity.IsBroken, Is.False);
                    Assert.That(integrity.CurrentHealth, Is.EqualTo(integrity.MaximumHealth));
                    Assert.That(healthText.text, Is.EqualTo("100%"));
                    Assert.That(healthFill.fillAmount, Is.EqualTo(1f).Within(.001f));
                    Assert.That(healthFill.rectTransform.anchorMax.x, Is.EqualTo(1f).Within(.001f));
                    Assert.That(gameOver.activeSelf, Is.False);
                }
            }

            Assert.That(integrity.CurrentHealth, Is.Zero);
            Assert.That(healthText.text, Is.EqualTo("0%"));
            Assert.That(healthFill.fillAmount, Is.Zero);
            Assert.That(mission.Snapshot.Phase, Is.EqualTo(PayloadMissionPhase.EvacuationTravel));
            Assert.That(gameOver.activeSelf, Is.False);
            var map = Object.FindObjectOfType<MissionMapGenerator>();
            Assert.That(map.ExtractionSite, Is.Not.Null);
            Assert.That(mission.EvacuationTarget, Is.EqualTo(map.ExtractionPosition));
            var minimap = Object.FindObjectOfType<MissionMinimap>();
            Assert.That(minimap, Is.Not.Null);
            minimap.Refresh();
            Assert.That(minimap.transform.Find("Evacuation Site").gameObject.activeSelf, Is.True);

            var player = GameObject.Find("Player");
            var controller = player.GetComponent<CharacterController>();
            controller.enabled = false;
            player.transform.position = mission.EvacuationTarget;
            controller.enabled = true;
            mission.Tick(.1f);
            Assert.That(mission.Snapshot.Phase, Is.EqualTo(PayloadMissionPhase.EvacuationTravel));
            Assert.That(mission.CanActivateEvacuationSignal, Is.True);
            mission.Tick(.1f, true);
            Assert.That(mission.Snapshot.Phase, Is.EqualTo(PayloadMissionPhase.EvacuationDefense));
            Assert.That(mission.Snapshot.EvacuationSecondsRemaining, Is.GreaterThan(80f));
            var signal = map.ExtractionSite.GetComponent<EvacuationSignalView>();
            Assert.That(signal, Is.Not.Null);
            Assert.That(signal.IsSignalVisible, Is.True);
            Assert.That(signal.SignalIntensity, Is.GreaterThan(0f));
            var evacuationWaves = Object.FindObjectOfType<PayloadWaveSpawner>();
            Assert.That(evacuationWaves.LivingEnemies.Any(enemy => enemy.IsAssaultingExtraction), Is.True);
            var hud = Object.FindObjectOfType<MissionHud>();
            hud.Refresh();
            var evacuationTimer = hud.ActivationCountdownText;
            Assert.That(evacuationTimer.gameObject.activeSelf, Is.True);
            Assert.That(evacuationTimer.text, Is.EqualTo("EXTRACTION IN\n01:30"));
            mission.Tick(1f);
            hud.Refresh();
            Assert.That(evacuationTimer.text, Is.EqualTo("EXTRACTION IN\n01:29"));
            controller.enabled = false;
            player.transform.position = mission.EvacuationTarget + Vector3.right * 14f;
            controller.enabled = true;
            mission.Tick(1f);
            hud.Refresh();
            Assert.That(evacuationTimer.text, Is.EqualTo("EXTRACTION IN\n01:28"));
            mission.Tick(95f);
            Assert.That(mission.Snapshot.Phase, Is.EqualTo(PayloadMissionPhase.EvacuationDefense));
            Assert.That(mission.Snapshot.EvacuationSecondsRemaining, Is.Zero);
            hud.Refresh();
            Assert.That(evacuationTimer.text, Is.EqualTo("EXTRACTION READY\nRETURN TO EVAC SITE"));
            controller.enabled = false;
            player.transform.position = mission.EvacuationTarget;
            controller.enabled = true;
            mission.Tick(.1f);
            Assert.That(mission.Snapshot.Phase, Is.EqualTo(PayloadMissionPhase.Escaped));
            Assert.That(gameOver.activeSelf, Is.True);
            Assert.That(gameOver.transform.Find("Game Over Title").GetComponent<Text>().text,
                Is.EqualTo("EXTRACTION SUCCESS"));
        }

        [UnityTest]
        public IEnumerator CommanderDebugPanelShowsTheCheckpointTokenBudgetAndActualComposition()
        {
            yield return LoadMission();
            var waves = Object.FindObjectOfType<PayloadWaveSpawner>();
            var debug = Object.FindObjectOfType<CommanderWaveDebugHud>();
            var panel = GameObject.Find("Commander Wave Debug");
            var details = panel.transform.Find("Commander Debug Details").GetComponent<Text>();

            Assert.That(debug, Is.Not.Null);
            Assert.That(details.text, Does.Contain("최초 화물 경비병: 랜덤"));
            waves.SpawnWave(71, 0, false);
            yield return null;

            var plan = waves.LastCommanderWave;
            Assert.That(plan, Is.Not.Null);
            Assert.That(plan.Kind, Is.EqualTo(CommanderWaveKind.Checkpoint));
            Assert.That(plan.Budget, Is.EqualTo(10));
            Assert.That(plan.Spent, Is.GreaterThan(0).And.LessThanOrEqualTo(plan.Budget));
            Assert.That(plan.Units.Count, Is.GreaterThan(0));
            Assert.That(details.text, Does.Contain($"토큰 {plan.Spent}/{plan.Budget}"));
            Assert.That(details.text, Does.Contain("거점 웨이브"));
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
        public IEnumerator CommanderUsesBudgetBeforeSightAndChangesPlanAfterScoutReport()
        {
            yield return LoadMission();
            var network = Object.FindObjectOfType<MissionCommanderDirector>();
            var scout = Object.FindObjectOfType<MissionScoutController>();
            var player = GameObject.Find("Player");
            var waves = Object.FindObjectOfType<PayloadWaveSpawner>();
            var encounter = new PayloadEscortEncounter(7, 5, PayloadAssaultPattern.LeftAmbush);

            Assert.That(network.TryPlanTravelGroup(encounter, false, out var blindPlan), Is.True);
            Assert.That(MissionCommanderAssaultPlanner.SpentTokens(blindPlan, network.WaveBudget),
                Is.LessThanOrEqualTo(network.WaveBudget.tokensPerWave));
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
            Assert.That(plan.Count(type => type == MissionMobArchetype.Shield), Is.GreaterThanOrEqualTo(1));
            Assert.That(plan.Count(type => type == MissionMobArchetype.Shield),
                Is.LessThanOrEqualTo(plan.Count(type => type == MissionMobArchetype.Ranged) * 2));

            waves.FindEnemy(PayloadWaveSpawner.SideCommanderEnemyId)
                .GetComponent<ActorHealth>().ApplyDamage(1000f);
            Assert.That(network.TryPlanTravelGroup(encounter, false, out _), Is.False);
        }

        [UnityTest]
        public IEnumerator CommanderCanSpendAWaveOnReconAndCapsLivingScouts()
        {
            yield return LoadMission();
            var network = Object.FindObjectOfType<MissionCommanderDirector>();
            var cargo = GameObject.Find("Payload").transform;
            var minimap = Object.FindObjectOfType<MissionMinimap>();

            Assert.That(network.Scouts.Count, Is.EqualTo(3));
            Assert.That(network.TryDeployRecon(0, cargo.position, false), Is.True);
            Assert.That(network.Scouts.Count, Is.EqualTo(5));
            Assert.That(network.WaveBudget.reconPatrolSize * network.WaveBudget.scoutCost,
                Is.LessThanOrEqualTo(network.WaveBudget.tokensPerWave));
            Assert.That(network.TryDeployRecon(1, cargo.position, false), Is.False);
            minimap.Refresh();
            Assert.That(minimap.ScoutIconCount, Is.EqualTo(5));
        }

        [UnityTest]
        public IEnumerator CommanderCheckpointWaveUsesBudgetIncludingFinalBoss()
        {
            yield return LoadMission();
            var network = Object.FindObjectOfType<MissionCommanderDirector>();
            Assert.That(network.TryPlanCheckpointGroup(5, false, out var standard), Is.True);
            Assert.That(MissionCommanderAssaultPlanner.SpentTokens(standard, network.WaveBudget),
                Is.LessThanOrEqualTo(network.WaveBudget.tokensPerWave));
            Assert.That(network.TryPlanCheckpointGroup(6, true, out var final), Is.True);
            Assert.That(final[0], Is.EqualTo(MissionMobArchetype.ChargerBoss));
            Assert.That(MissionCommanderAssaultPlanner.SpentTokens(final, network.WaveBudget),
                Is.LessThanOrEqualTo(network.WaveBudget.tokensPerWave));
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
