using System;
using System.Collections.Generic;
using AlmostDelivered.Combat;
using AlmostDelivered.CommanderAI.Commander;
using AlmostDelivered.CommanderAI.Domain;
using AlmostDelivered.Frontend;
using AlmostDelivered.MapGeneration;
using UnityEngine;

namespace AlmostDelivered.Gameplay.Payload
{
    public sealed class MissionCommanderDirector : MonoBehaviour
    {
        [SerializeField] private PayloadMissionController mission;
        [SerializeField] private PayloadWaveSpawner waves;
        [SerializeField] private MissionMapGenerator map;
        [SerializeField] private ObservableActor player;
        [SerializeField] private Rect bounds;
        [SerializeField] private int seed;
        [SerializeField] private Color groundColor = new Color(.25f, .35f, .2f);
        [SerializeField] private Color foliageColor = new Color(.2f, .4f, .18f);
        [SerializeField] private CommanderWaveBudget waveBudget = new CommanderWaveBudget();
        private SquadController squad;
        private ActorHealth commanderHealth;
        private bool initialized;
        private readonly Dictionary<int, MissionMobController> trackedMobs =
            new Dictionary<int, MissionMobController>();
        private readonly List<MissionScoutController> scouts = new List<MissionScoutController>();
        private readonly Dictionary<int, MissionScoutCombatProfile> combatProfiles =
            new Dictionary<int, MissionScoutCombatProfile>();
        private RangedAttackController observedRanged;
        private MeleeAttackController observedMelee;
        private float nextTacticalOrderAt = float.NegativeInfinity;
        private int nextScoutId = 1;

        public int ReportCount { get; private set; }
        public int TrackedMobCount => trackedMobs.Count;
        public IReadOnlyList<MissionScoutController> Scouts => scouts;
        public float LastReportTime { get; private set; } = float.NegativeInfinity;
        public float LastScoutObservationTime { get; private set; } = float.NegativeInfinity;
        public int LastScoutReporterId { get; private set; }
        public int LastScoutObservedActorId { get; private set; }
        public Vector3 LastScoutObservedPosition { get; private set; }
        public MissionScoutCombatProfile LatestScoutProfile =>
            combatProfiles.TryGetValue(LastScoutObservedActorId, out var profile) ? profile : null;
        public bool HasLiveScoutSight
        {
            get
            {
                if (commanderHealth == null || commanderHealth.IsDead ||
                    Time.time - LastScoutObservationTime > .65f) return false;
                foreach (var scout in scouts)
                    if (scout != null && !scout.IsDefeated && scout.IsWatchingPlayer &&
                        scout.ReporterId == LastScoutReporterId)
                        return true;
                return false;
            }
        }
        public bool IsCoordinating => squad != null && squad.State == SquadAlertState.Alerted &&
                                      commanderHealth != null && !commanderHealth.IsDead;
        public bool CanCommand => initialized && commanderHealth != null && !commanderHealth.IsDead;
        public CommanderWaveBudget WaveBudget => waveBudget ?? (waveBudget = new CommanderWaveBudget());
        public int LastReconDeployedCount { get; private set; }

        public bool TryGetCombatProfile(int actorId, out MissionScoutCombatProfile profile) =>
            combatProfiles.TryGetValue(actorId, out profile);

        public bool TryPlanTravelGroup(PayloadEscortEncounter encounter, bool includeCargoSaboteur,
            out MissionMobArchetype[] archetypes)
            => TryPlanTravelGroup(encounter, includeCargoSaboteur, encounter.GroupSize, 0, out archetypes);

        public bool TryPlanTravelGroup(PayloadEscortEncounter encounter, bool includeCargoSaboteur,
            int maximumUnits, int bonusTokens, out MissionMobArchetype[] archetypes)
        {
            archetypes = null;
            if (!CanCommand) return false;
            archetypes = MissionCommanderAssaultPlanner.PlanBudget(LatestProfile(), WaveBudget,
                Mathf.Max(1, WaveBudget.tokensPerWave) + Mathf.Max(0, bonusTokens),
                maximumUnits, includeCargoSaboteur,
                unchecked(seed * 486187739 + encounter.SequenceId * 397));
            return archetypes.Length > 0;
        }

        public bool TryPlanCheckpointGroup(int waveId, bool finalWave,
            out MissionMobArchetype[] archetypes)
        {
            archetypes = null;
            if (!CanCommand) return false;
            var budget = Mathf.Max(1, WaveBudget.tokensPerWave);
            if (finalWave) budget -= WaveBudget.CostFor(MissionMobArchetype.ChargerBoss);
            var escorts = MissionCommanderAssaultPlanner.PlanBudget(LatestProfile(), WaveBudget,
                budget, 12, true, unchecked(seed * 486187739 + waveId * 397));
            if (escorts.Length == 0) return false;
            if (!finalWave)
            {
                archetypes = escorts;
                return true;
            }
            archetypes = new MissionMobArchetype[escorts.Length + 1];
            archetypes[0] = MissionMobArchetype.ChargerBoss;
            Array.Copy(escorts, 0, archetypes, 1, escorts.Length);
            return true;
        }

        public bool TryDeployRecon(int sequenceId, Vector3 cargoPosition, bool forcedAssault)
        {
            LastReconDeployedCount = 0;
            if (!CanCommand || forcedAssault ||
                Time.time - LastScoutObservationTime <= WaveBudget.freshIntelSeconds) return false;
            var livingScouts = 0;
            foreach (var scout in scouts)
                if (scout != null && !scout.IsDefeated) livingScouts++;
            var count = Mathf.Min(Mathf.Max(1, WaveBudget.reconPatrolSize),
                Mathf.Max(0, WaveBudget.maximumLivingScouts - livingScouts),
                Mathf.Max(1, WaveBudget.tokensPerWave) / Mathf.Max(1, WaveBudget.scoutCost));
            if (count <= 0) return false;
            LastReconDeployedCount = count;

            for (var index = 0; index < count; index++)
            {
                var id = nextScoutId++;
                var routeDistance = mission != null ? mission.Snapshot.RouteDistance : 0f;
                var anchor = mission?.RoutePlan != null
                    ? mission.RoutePlan.EvaluateDistance(Mathf.Min(mission.RoutePlan.TotalLength,
                        routeDistance + 20f + index * 14f))
                    : cargoPosition;
                var position = FindCover(anchor, id + sequenceId);
                var destination = FindCover(anchor + new Vector3(index == 0 ? -16f : 16f, 0f, 18f), id + 101);
                var scout = CreateScout(position, id);
                scout.ConfigurePatrol(destination);
                scouts.Add(scout);
                squad.RegisterMember(id, scout.GetComponent<ActorHealth>());
            }
            return true;
        }

        private MissionScoutCombatProfile LatestProfile()
        {
            MissionScoutCombatProfile latest = null;
            foreach (var profile in combatProfiles.Values)
                if (profile.HasObservedWeapon &&
                    (latest == null || profile.LastObservedAt > latest.LastObservedAt))
                    latest = profile;
            return latest;
        }

        public void Configure(PayloadMissionController missionController, PayloadWaveSpawner spawner,
            MissionMapGenerator generator, ObservableActor playerActor, Rect missionBounds,
            int missionSeed, Color ground, Color foliage)
        {
            mission = missionController;
            waves = spawner;
            map = generator;
            player = playerActor;
            bounds = missionBounds;
            seed = missionSeed;
            groundColor = ground;
            foliageColor = foliage;
        }

        private void Update()
        {
            if (!initialized && mission != null && mission.IsAuthority && mission.RoutePlan != null &&
                waves != null && map != null && player != null)
                Initialize();
            if (initialized) RefreshTrackedMobs();
            if (commanderHealth != null && commanderHealth.IsDead &&
                squad != null && squad.State != SquadAlertState.Confused)
                squad.EnterConfused();
        }

        private void Initialize()
        {
            var boss = waves.FindEnemy(PayloadWaveSpawner.SideCommanderEnemyId);
            if (boss == null) return;
            commanderHealth = boss.GetComponent<ActorHealth>();
            waves.SetCommander(this);
            squad = GetComponent<SquadController>() ?? gameObject.AddComponent<SquadController>();
            observedRanged = player.GetComponent<RangedAttackController>();
            observedMelee = player.GetComponent<MeleeAttackController>();
            if (observedRanged != null)
            {
                observedRanged.ShotFired += HandleObservedShot;
                observedRanged.ReloadStarted += HandleObservedReload;
            }
            if (observedMelee != null)
                observedMelee.AttackPerformed += HandleObservedMelee;
            nextScoutId = 1;
            for (var index = 0; index < 3; index++)
            {
                var anchor = mission.RoutePlan.EvaluateDistance(
                    mission.RoutePlan.TotalLength * (index + 1f) / 4f);
                var position = FindCover(anchor, index);
                var id = nextScoutId++;
                var scout = CreateScout(position, id);
                scouts.Add(scout);
                squad.RegisterMember(id, scout.GetComponent<ActorHealth>());
            }
            waves.EnemyRemoved += HandleEnemyRemoved;
            RefreshTrackedMobs();
            initialized = true;
        }

        private void OnDestroy()
        {
            if (observedRanged != null)
            {
                observedRanged.ShotFired -= HandleObservedShot;
                observedRanged.ReloadStarted -= HandleObservedReload;
            }
            if (observedMelee != null)
                observedMelee.AttackPerformed -= HandleObservedMelee;
            if (waves != null) waves.EnemyRemoved -= HandleEnemyRemoved;
            if (waves != null) waves.SetCommander(null);
            foreach (var mob in trackedMobs.Values)
                if (mob != null) mob.VisualContactObserved -= HandleVisualContact;
            trackedMobs.Clear();
            scouts.Clear();
            combatProfiles.Clear();
        }

        private void RefreshTrackedMobs()
        {
            if (waves == null || squad == null) return;
            foreach (var mob in waves.LivingEnemies)
            {
                if (mob == null || mob.IsDefeated || trackedMobs.ContainsKey(mob.EnemyId)) continue;
                var health = mob.GetComponent<ActorHealth>();
                if (!squad.RegisterMember(ReporterIdFor(mob.EnemyId), health)) continue;
                trackedMobs.Add(mob.EnemyId, mob);
                mob.VisualContactObserved += HandleVisualContact;
            }
        }

        private void HandleEnemyRemoved(int waveId, int enemyId)
        {
            if (!trackedMobs.TryGetValue(enemyId, out var mob)) return;
            if (mob != null) mob.VisualContactObserved -= HandleVisualContact;
            trackedMobs.Remove(enemyId);
            squad?.UnregisterMember(ReporterIdFor(enemyId));
        }

        private void HandleVisualContact(MissionMobController mob, Vector3 position)
        {
            if (mob == null || player == null || !trackedMobs.ContainsKey(mob.EnemyId)) return;
            ReportObservation(ReporterIdFor(mob.EnemyId), player, position, player.Velocity);
        }

        private static int ReporterIdFor(int enemyId) =>
            unchecked(100000 + Mathf.Abs(enemyId));

        private Vector3 FindCover(Vector3 anchor, int index)
        {
            var best = Vector3.zero;
            var bestScore = float.PositiveInfinity;
            var pieces = map.GeneratedRoot != null
                ? map.GeneratedRoot.GetComponentsInChildren<MissionMapPieceInstance>(true)
                : Array.Empty<MissionMapPieceInstance>();
            foreach (var piece in pieces)
            {
                if (piece == null || piece.Category != MissionMapPieceCategory.Foliage ||
                    !piece.name.Contains("TallGrass")) continue;
                var candidate = new Vector3(piece.Position.x, 0f, piece.Position.y);
                var distance = Vector3.Distance(candidate, anchor);
                if (distance < 12f || distance > 42f ||
                    Vector3.Distance(candidate, player.transform.position) < 48f) continue;
                var occupied = false;
                foreach (var scout in scouts)
                    if (scout != null && !scout.IsDefeated &&
                        Vector3.Distance(candidate, scout.transform.position) < 8f)
                    { occupied = true; break; }
                if (occupied) continue;
                var score = Mathf.Abs(distance - 21f) +
                            Mathf.Abs(candidate.z - anchor.z) * .2f +
                            Mathf.Abs(candidate.x) * .001f;
                foreach (var other in pieces)
                {
                    if (other == null || other == piece) continue;
                    var separation = Vector2.Distance(piece.Position, other.Position);
                    if (other.Category == MissionMapPieceCategory.Foliage && separation < 5f)
                        score -= 2f;
                    else if (other.Category == MissionMapPieceCategory.BlockingObstacle &&
                             separation >= 3f && separation < 9f)
                        score -= 3f;
                }
                if (score >= bestScore) continue;
                bestScore = score;
                best = candidate;
            }
            if (bestScore < float.PositiveInfinity) return best;
            foreach (var piece in pieces)
            {
                if (piece == null || piece.Category != MissionMapPieceCategory.Foliage ||
                    !piece.name.Contains("TallGrass")) continue;
                var candidate = new Vector3(piece.Position.x, 0f, piece.Position.y);
                if (Vector3.Distance(candidate, player.transform.position) < 48f) continue;
                var score = Vector3.Distance(candidate, anchor);
                if (score >= bestScore) continue;
                bestScore = score;
                best = candidate;
            }
            if (bestScore < float.PositiveInfinity) return best;
            return new Vector3(Mathf.Clamp(anchor.x + (index % 2 == 0 ? -19f : 19f),
                    bounds.xMin + 4f, bounds.xMax - 4f), 0f, anchor.z);
        }

        private MissionScoutController CreateScout(Vector3 position, int reporterId)
        {
            var actor = new GameObject($"Hidden Scout {reporterId}");
            actor.transform.SetParent(transform, false);
            actor.transform.position = position;
            var health = actor.AddComponent<ActorHealth>();
            health.ConfigureMaximum(36f);
            actor.AddComponent<CombatTeam>().Team = CombatTeamId.Enemy;
            var movement = actor.AddComponent<CharacterController>();
            movement.height = 1.45f;
            movement.radius = .38f;
            movement.center = Vector3.up * .72f;
            movement.stepOffset = .24f;
            var hitbox = new GameObject("Scout Combat Hurtbox", typeof(CapsuleCollider), typeof(CombatHurtbox));
            hitbox.transform.SetParent(actor.transform, false);
            var capsule = hitbox.GetComponent<CapsuleCollider>();
            capsule.isTrigger = true;
            capsule.radius = .58f;
            capsule.height = 1.6f;
            capsule.center = Vector3.up * .8f;
            var scout = actor.AddComponent<MissionScoutController>();
            scout.Configure(player, this, reporterId, bounds, groundColor, foliageColor,
                unchecked(seed * 486187739 + reporterId * 397));
            return scout;
        }

        public bool ReportScout(MissionScoutController scout, ObservableActor observed,
            Vector3 position, Vector3 velocity)
        {
            if (!initialized || squad == null || commanderHealth == null || commanderHealth.IsDead ||
                scout == null || observed == null || scout.GetComponent<ActorHealth>().IsDead)
                return false;
            return ReportObservation(scout.ReporterId, observed, position, velocity);
        }

        public bool ObserveScoutVision(MissionScoutController scout, ObservableActor observed,
            Vector3 position, Vector3 velocity)
        {
            if (scout == null || observed == null || !scout.IsWatchingPlayer ||
                !scouts.Contains(scout))
                return false;
            ObserveCombatStatus(observed);
            if (!ReportScout(scout, observed, position, velocity)) return false;
            LastScoutReporterId = scout.ReporterId;
            LastScoutObservedActorId = observed.ActorId;
            LastScoutObservedPosition = position;
            LastScoutObservationTime = Time.time;
            return true;
        }

        private MissionScoutCombatProfile GetOrCreateProfile(int actorId)
        {
            if (!combatProfiles.TryGetValue(actorId, out var profile))
            {
                profile = new MissionScoutCombatProfile(actorId);
                combatProfiles.Add(actorId, profile);
            }
            return profile;
        }

        private void ObserveCombatStatus(ObservableActor observed)
        {
            var controller = observed.GetComponent<MissionPlayerController>();
            var mobility = observed.GetComponent<PlayerMobility>();
            GetOrCreateProfile(observed.ActorId).ObserveStatus(
                controller != null ? controller.WeaponType : PlayerWeaponType.Melee,
                mobility != null && mobility.IsSprinting,
                mobility != null && mobility.IsRolling, Time.time);
        }

        private MissionScoutController ViewingScout()
        {
            if (commanderHealth == null || commanderHealth.IsDead) return null;
            foreach (var scout in scouts)
                if (scout != null && scout.CanSeePlayerNow()) return scout;
            return null;
        }

        private void HandleObservedShot() => ObserveVisibleAction(ScoutAction.Shot);
        private void HandleObservedReload() => ObserveVisibleAction(ScoutAction.Reload);
        private void HandleObservedMelee() => ObserveVisibleAction(ScoutAction.Melee);

        private void ObserveVisibleAction(ScoutAction action)
        {
            var scout = ViewingScout();
            if (scout == null || player == null) return;
            ObserveCombatStatus(player);
            var profile = GetOrCreateProfile(player.ActorId);
            switch (action)
            {
                case ScoutAction.Shot: profile.ObserveShot(Time.time); break;
                case ScoutAction.Reload: profile.ObserveReload(Time.time); break;
                case ScoutAction.Melee: profile.ObserveMelee(Time.time); break;
            }
            ReportObservation(scout.ReporterId, player, player.transform.position, player.Velocity);
            LastScoutReporterId = scout.ReporterId;
            LastScoutObservedActorId = player.ActorId;
            LastScoutObservedPosition = player.transform.position;
            LastScoutObservationTime = Time.time;
        }

        private enum ScoutAction { Shot, Reload, Melee }

        public bool TryGetLiveScoutSight(out int reporterId, out Vector3 position)
        {
            reporterId = LastScoutReporterId;
            position = LastScoutObservedPosition;
            return HasLiveScoutSight;
        }

        private bool ReportObservation(int reporterId, ObservableActor observed,
            Vector3 position, Vector3 velocity)
        {
            if (squad == null || observed == null || commanderHealth == null || commanderHealth.IsDead)
                return false;
            if (squad.State == SquadAlertState.Unaware)
                squad.TryCompleteAlert(reporterId);
            if (squad.State != SquadAlertState.Alerted) return false;
            var tags = ObservedCombatTag.None;
            var controller = observed.GetComponent<MissionPlayerController>();
            if (controller != null && PlayerWeaponLoadout.IsFirearm(controller.WeaponType))
                tags |= ObservedCombatTag.RangedWeapon;
            combatProfiles.TryGetValue(observed.ActorId, out var combatProfile);
            if (velocity.magnitude > 6.8f || combatProfile != null && combatProfile.ObservedRolls > 0)
                tags |= ObservedCombatTag.Mobility;
            var sample = new ObservationSample(observed.ActorId, position, velocity,
                Time.unscaledTime, 1f, tags, reporterId);
            if (!squad.Report(in sample)) return false;
            ReportCount++;
            LastReportTime = Time.time;
            if (Time.time < nextTacticalOrderAt) return true;
            nextTacticalOrderAt = Time.time + 2f;
            if (!squad.Knowledge.TryGet(observed.ActorId, out var memory)) return false;
            var leadSeconds = combatProfile != null && combatProfile.ObservedRolls >= 2 ? .7f : 1.3f;
            var prediction = memory.LastKnownPosition +
                             Vector3.ClampMagnitude(memory.LastKnownVelocity * leadSeconds, 6f);
            prediction.x = Mathf.Clamp(prediction.x, bounds.xMin + 2f, bounds.xMax - 2f);
            prediction.z = Mathf.Clamp(prediction.z, bounds.yMin + 2f, bounds.yMax - 2f);
            foreach (var enemy in waves.LivingEnemies)
            {
                if (enemy == null || enemy.IsDefeated) continue;
                var roaming = enemy.WaveId == PayloadWaveSpawner.PreplacedSentryWaveId;
                var outpostGuard = enemy.WaveId == PayloadWaveSpawner.SideCommanderWaveId &&
                                   enemy.EnemyId != PayloadWaveSpawner.SideCommanderEnemyId;
                if (!roaming && !outpostGuard) continue;
                var responseRange = roaming ? 110f : 65f;
                if (combatProfile != null && combatProfile.IsRapidFire &&
                    enemy.Definition.Archetype == MissionMobArchetype.Shield)
                    responseRange += 25f;
                if (combatProfile != null && combatProfile.PrefersMelee &&
                    enemy.Definition.Archetype == MissionMobArchetype.Ranged)
                    responseRange += 25f;
                if (Vector3.Distance(enemy.transform.position, position) > responseRange)
                    continue;
                enemy.InvestigateCommanderReport(prediction);
            }
            return true;
        }
    }
}
