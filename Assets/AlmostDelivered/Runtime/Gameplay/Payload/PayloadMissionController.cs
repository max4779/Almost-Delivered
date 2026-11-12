using System;
using System.Collections.Generic;
using AlmostDelivered.Combat;
using AlmostDelivered.Frontend;
using AlmostDelivered.MapGeneration;
using UnityEngine;

namespace AlmostDelivered.Gameplay.Payload
{
    public sealed class PayloadMissionController : MonoBehaviour
    {
        private readonly PayloadPresenceSensor presenceSensor = new PayloadPresenceSensor();
        private readonly HashSet<PlayerTraitProfile> escortProfiles = new HashSet<PlayerTraitProfile>();

        [SerializeField] private bool initializeOnStart;
        [SerializeField] private bool authority;
        [SerializeField] private int configuredSeed;
        [SerializeField] private Rect configuredBounds;
        [SerializeField] private Transform playerTransform;
        [SerializeField] private Transform payloadTransform;
        [SerializeField] private CargoIntegrity cargoIntegrity;
        [SerializeField, Min(0f)] private float cargoRepairPerSecond = 40f;
        [SerializeField] private MissionMapGenerator configuredMapGenerator;
        [SerializeField] private PayloadWaveSpawner waveSpawner;
        [SerializeField] private float configuredMovementSpeed;
        [SerializeField] private float configuredDeploymentDuration;
        [SerializeField] private float configuredCorridorWidth = 3f;
        [SerializeField] private int configuredInteriorPointCount = 5;
        [SerializeField] private bool requiresCargoRecovery;
        [SerializeField, Min(0)] private int configuredRecoveryGuardCount = 6;
        [SerializeField, Min(0f)] private float configuredCargoActivationDuration = 3f;
        [SerializeField, Min(0)] private int configuredPreplacedSentryCount = 8;
        [SerializeField, Min(0f)] private float configuredCargoNoiseRadius = 26f;
        [SerializeField, Min(.1f)] private float configuredCargoNoiseInterval = .65f;
        [SerializeField] private bool relayChoicesEnabled;
        [SerializeField] private RelayRouteOptionDefinition[] routeOptions = RelayRouteOptionDefinition.CreateDefaults();
        private static readonly RelayRouteOptionDefinition[] DefaultRouteOptions = RelayRouteOptionDefinition.CreateDefaults();
        private PayloadMissionStateMachine stateMachine;
        private PayloadEscortDirector escortDirector;
        private PayloadEscortEncounter? pendingEscortEncounter;
        [SerializeField] private float escortRadius;
        private PayloadMissionSnapshot snapshot;
        private float activationNoiseCooldown;
        private int activationWaveMask;
        private bool relayChoicePending;
        private string activeRouteOptionId;
        private float shockPulseCooldown;

        public bool IsAuthority => authority;
        public PayloadRoutePlan RoutePlan { get; private set; }
        public PayloadMissionSnapshot Snapshot => snapshot;
        public bool HasPendingEscortEncounter => pendingEscortEncounter.HasValue;
        public bool IsRelayChoicePending => relayChoicePending;
        public string ActiveRouteOptionId => activeRouteOptionId;
        public RelayRouteOptionDefinition ActiveRouteOption => FindRouteOption(activeRouteOptionId);
        public IReadOnlyList<RelayRouteOptionDefinition> RouteOptions =>
            routeOptions != null && routeOptions.Length > 0 ? routeOptions : DefaultRouteOptions;
        public event Action<string> RouteOptionRequested;
        public bool RequiresCargoRecovery => requiresCargoRecovery;
        public int RecoveryGuardCount => configuredRecoveryGuardCount;
        public float CargoActivationDuration => configuredCargoActivationDuration;
        public int PreplacedSentryCount => configuredPreplacedSentryCount;
        public CargoIntegrity CargoHealth => cargoIntegrity;
        public float CargoInteractionRadius => escortRadius;
        public bool CanInteractWithCargo
        {
            get
            {
                if (playerTransform == null || payloadTransform == null) return false;
                var playerHealth = playerTransform.GetComponent<ActorHealth>();
                if (playerHealth != null && playerHealth.IsDead) return false;
                var offset = playerTransform.position - payloadTransform.position;
                offset.y = 0f;
                return offset.sqrMagnitude <= escortRadius * escortRadius;
            }
        }

        public event Action<PayloadMissionSnapshot> SnapshotChanged;
        public event Action<PayloadMissionPhase, PayloadMissionPhase> PhaseChanged;

        public void RegisterEscortPlayer(PlayerTraitProfile profile)
        {
            if (profile != null) escortProfiles.Add(profile);
        }

        public void UnregisterEscortPlayer(PlayerTraitProfile profile)
        {
            if (profile != null) escortProfiles.Remove(profile);
        }

        private float HighestNearbyEscortMultiplier()
        {
            if (payloadTransform == null) return 1f;
            var highest = 1f;
            foreach (var profile in escortProfiles)
            {
                if (profile == null || profile.Progress == null) continue;
                var health = profile.GetComponent<ActorHealth>();
                if (health != null && health.IsDead) continue;
                var delta = profile.transform.position - payloadTransform.position;
                delta.y = 0f;
                if (delta.sqrMagnitude > escortRadius * escortRadius) continue;
                highest = Mathf.Max(highest, profile.EscortSpeedMultiplier);
            }
            return highest;
        }

        public void Configure(
            bool isAuthority,
            int missionSeed,
            Rect missionBounds,
            Transform player,
            Transform payload,
            MissionMapGenerator mapGenerator,
            PayloadWaveSpawner waves,
            float movementSpeed,
            float deploymentDuration,
            float radius,
            int interiorPointCount = 5,
            float corridorWidth = 3f,
            bool requireCargoRecovery = false,
            int recoveryGuardCount = 6,
            float cargoActivationDuration = 3f,
            int preplacedSentryCount = 8,
            bool enableRelayChoices = false)
        {
            if (stateMachine != null)
            {
                stateMachine.WaveRequested -= HandleWaveRequested;
            }

            UnbindWaveSpawner(false);
            initializeOnStart = true;
            authority = isAuthority;
            configuredSeed = missionSeed;
            configuredBounds = missionBounds;
            playerTransform = player;
            payloadTransform = payload;
            cargoIntegrity = payload != null ? payload.GetComponent<CargoIntegrity>() : null;
            configuredMapGenerator = mapGenerator;
            waveSpawner = waves;
            if (waveSpawner != null)
                waveSpawner.ConfigureCargoTarget(payload);
            escortRadius = Mathf.Max(0f, radius);
            configuredMovementSpeed = movementSpeed;
            configuredDeploymentDuration = deploymentDuration;
            configuredInteriorPointCount = interiorPointCount;
            configuredCorridorWidth = corridorWidth;
            requiresCargoRecovery = requireCargoRecovery;
            configuredRecoveryGuardCount = Mathf.Max(0, recoveryGuardCount);
            configuredCargoActivationDuration = IsFinite(cargoActivationDuration)
                ? Mathf.Max(0f, cargoActivationDuration)
                : 0f;
            configuredPreplacedSentryCount = Mathf.Max(0, preplacedSentryCount);
            relayChoicesEnabled = enableRelayChoices;
            Initialize();
        }

        public void SetRouteOptions(RelayRouteOptionDefinition[] options)
        {
            if (options == null || options.Length == 0)
                throw new ArgumentException("At least one route option is required.", nameof(options));
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var option in options)
                if (option == null || string.IsNullOrWhiteSpace(option.id) || !ids.Add(option.id))
                    throw new ArgumentException("Route option IDs must be unique and nonempty.", nameof(options));
            routeOptions = (RelayRouteOptionDefinition[])options.Clone();
        }

        private void Start()
        {
            if (initializeOnStart && stateMachine == null)
            {
                Initialize();
            }
            if (relayChoicesEnabled && Application.isPlaying &&
                GetComponent<RelayRouteChoiceView>() == null)
                gameObject.AddComponent<RelayRouteChoiceView>().Configure(this);
        }

        private void Initialize()
        {
            if (cargoIntegrity == null && payloadTransform != null)
                cargoIntegrity = payloadTransform.GetComponent<CargoIntegrity>();
            RoutePlan = PayloadRoutePlanner.Generate(configuredSeed, configuredBounds, configuredInteriorPointCount, configuredCorridorWidth);
            stateMachine = new PayloadMissionStateMachine(
                RoutePlan.TotalLength,
                RoutePlan.CheckpointDistances,
                configuredMovementSpeed,
                configuredDeploymentDuration,
                requiresCargoRecovery,
                configuredCargoActivationDuration);
            stateMachine.WaveRequested += HandleWaveRequested;
            escortDirector = authority && waveSpawner != null
                ? new PayloadEscortDirector(configuredSeed)
                : null;
            pendingEscortEncounter = null;
            activationNoiseCooldown = 0f;
            activationWaveMask = 0;
            relayChoicePending = false;
            activeRouteOptionId = null;
            shockPulseCooldown = 0f;
            if (payloadTransform != null)
            {
                payloadTransform.position = RoutePlan.Points[0];
                payloadTransform.rotation = Quaternion.LookRotation(RoutePlan.EvaluateDirection(0f), Vector3.up);
            }

            if (configuredMapGenerator != null)
            {
                configuredMapGenerator.Generate(configuredSeed, RoutePlan.CorridorExclusions);
            }

            BindWaveSpawner();
            var recoveryGuardIds = Array.Empty<int>();
            if (requiresCargoRecovery && authority && waveSpawner != null && payloadTransform != null && Application.isPlaying)
            {
                var spawned = waveSpawner.SpawnRecoveryGuardGroup(
                    payloadTransform.position,
                    configuredRecoveryGuardCount,
                    configuredBounds);
                recoveryGuardIds = new int[spawned.Count];
                var guardIndex = 0;
                foreach (var guardId in spawned)
                {
                    recoveryGuardIds[guardIndex++] = guardId;
                }

                BuildPreplacedSentryPoints(
                    configuredPreplacedSentryCount,
                    out var sentryPositions,
                    out var sentryFacings);
                waveSpawner.SpawnPreplacedSentries(sentryPositions, sentryFacings);
            }

            if (requiresCargoRecovery)
            {
                stateMachine.RegisterRecoveryGuards(recoveryGuardIds);
            }

            snapshot = ComposeSnapshot();
            SnapshotChanged?.Invoke(snapshot);
        }

        private void Update()
        {
            Tick(Time.deltaTime, Input.GetKey(KeyCode.E));
        }

        private void OnDestroy()
        {
            if (stateMachine != null)
            {
                stateMachine.WaveRequested -= HandleWaveRequested;
            }

            UnbindWaveSpawner(false);
        }

        public void Tick(float deltaTime)
        {
            Tick(deltaTime, false);
        }

        public void Tick(float deltaTime, bool activationInputHeld)
        {
            if (!authority || stateMachine == null || playerTransform == null || payloadTransform == null)
            {
                return;
            }

            presenceSensor.Sample(payloadTransform.position, escortRadius, out var players, out var enemies);
            var canInteract = CanInteractWithCargo;
            if (canInteract) players = Mathf.Max(players, 1);
            var previousPhase = snapshot.Phase;
            if (relayChoicePending)
            {
                stateMachine.Tick(0f, players, enemies);
                PublishSnapshot(previousPhase);
                return;
            }
            if (cargoIntegrity != null && activationInputHeld && canInteract &&
                previousPhase != PayloadMissionPhase.Recovering && previousPhase != PayloadMissionPhase.Activating &&
                previousPhase != PayloadMissionPhase.Deploying && previousPhase != PayloadMissionPhase.Completed)
                cargoIntegrity.Repair(cargoRepairPerSecond * deltaTime *
                    (playerTransform.GetComponent<PlayerTraitProfile>()?.RepairMultiplier ?? 1f));

            stateMachine.SetEscortSpeedMultiplier(
                Mathf.Max(.01f, ActiveRouteOption != null ? ActiveRouteOption.escortSpeedMultiplier : 1f) *
                HighestNearbyEscortMultiplier());

            stateMachine.Tick(deltaTime, cargoIntegrity != null && cargoIntegrity.IsBroken ? 0 : players,
                enemies, activationInputHeld && canInteract);
            var currentState = stateMachine.Snapshot;
            BeginRelayChoiceIfNeeded(previousPhase, currentState.Phase);
            HandleActivationTransition(previousPhase, currentState.Phase);
            TickActivationWaves(previousPhase, currentState);
            payloadTransform.position = RoutePlan.EvaluateDistance(stateMachine.Snapshot.RouteDistance);
            payloadTransform.rotation = Quaternion.LookRotation(
                RoutePlan.EvaluateDirection(stateMachine.Snapshot.RouteDistance), Vector3.up);
            TickRoutePattern(deltaTime);
            PublishSnapshot(previousPhase);
            TickActivationNoise(deltaTime);
            TickEscortDirector(deltaTime);
        }

        public bool ChooseRelayOption(string optionId)
        {
            var option = FindRouteOption(optionId);
            if (!relayChoicePending || option == null)
                return false;
            if (!authority)
            {
                RouteOptionRequested?.Invoke(option.id);
                return false;
            }
            activeRouteOptionId = option.id;
            relayChoicePending = false;
            shockPulseCooldown = Mathf.Max(.1f, option.pulseInterval);
            stateMachine.SetEscortSpeedMultiplier(Mathf.Max(.01f, option.escortSpeedMultiplier));
            waveSpawner?.SetEnemyHealthMultiplier(Mathf.Max(.01f, option.enemyHealthMultiplier));
            PublishSnapshot(snapshot.Phase);
            return true;
        }

        public bool ApplySnapshot(PayloadMissionSnapshot value)
        {
            if (authority || RoutePlan == null)
            {
                return false;
            }

            var previousPhase = snapshot.Phase;
            snapshot = value;
            relayChoicePending = value.IsRelayChoicePending;
            activeRouteOptionId = value.ActiveRouteOptionId;
            if (cargoIntegrity != null && value.CargoMaximumHealth > 0f && value.CargoHealth >= 0f)
                cargoIntegrity.ApplySnapshot(value.CargoHealth, value.CargoMaximumHealth, value.IsCargoBroken);
            if (payloadTransform != null)
            {
                payloadTransform.position = RoutePlan.EvaluateDistance(value.RouteDistance);
                payloadTransform.rotation = Quaternion.LookRotation(
                    RoutePlan.EvaluateDirection(value.RouteDistance), Vector3.up);
            }

            SnapshotChanged?.Invoke(snapshot);
            if (previousPhase != snapshot.Phase)
            {
                PhaseChanged?.Invoke(previousPhase, snapshot.Phase);
            }

            return true;
        }

        private void BindWaveSpawner()
        {
            if (waveSpawner == null)
            {
                return;
            }

            waveSpawner.WaveStarted += HandleWaveStarted;
            waveSpawner.EnemyRemoved += HandleEnemyRemoved;
        }

        private void UnbindWaveSpawner(bool clearReference)
        {
            if (waveSpawner == null)
            {
                return;
            }

            waveSpawner.WaveStarted -= HandleWaveStarted;
            waveSpawner.EnemyRemoved -= HandleEnemyRemoved;
            if (clearReference)
            {
                waveSpawner = null;
            }
        }

        private void HandleWaveRequested(PayloadWaveRequest request)
        {
            if (authority && waveSpawner != null)
            {
                waveSpawner.SpawnWave(request.WaveId, request.CheckpointIndex, request.IsFinal);
            }
        }

        private void HandleActivationTransition(PayloadMissionPhase previousPhase, PayloadMissionPhase currentPhase)
        {
            if (previousPhase == PayloadMissionPhase.Activating || currentPhase != PayloadMissionPhase.Activating)
            {
                return;
            }

            activationNoiseCooldown = configuredCargoNoiseInterval;
            TrySpawnActivationWave(0);
            EmitCargoNoise();
        }

        private void TickActivationWaves(PayloadMissionPhase previousPhase, PayloadMissionSnapshot currentState)
        {
            if (previousPhase != PayloadMissionPhase.Activating && currentState.Phase != PayloadMissionPhase.Activating)
            {
                return;
            }

            if (currentState.ActivationProgress >= .5f)
            {
                TrySpawnActivationWave(1);
            }
        }

        private void TrySpawnActivationWave(int waveIndex)
        {
            var mask = 1 << Mathf.Clamp(waveIndex, 0, 30);
            if ((activationWaveMask & mask) != 0 || waveSpawner == null || payloadTransform == null || RoutePlan == null)
            {
                return;
            }

            var routeDirection = RoutePlan.Points.Count > 1
                ? RoutePlan.Points[1] - RoutePlan.Points[0]
                : Vector3.forward;
            if (waveSpawner.SpawnActivationResponseWave(
                    waveIndex,
                    payloadTransform.position,
                    routeDirection,
                    configuredBounds,
                    escortRadius))
            {
                activationWaveMask |= mask;
            }
        }

        private void TickActivationNoise(float deltaTime)
        {
            if (snapshot.Phase != PayloadMissionPhase.Activating || !snapshot.IsActivationInputHeld)
            {
                return;
            }

            activationNoiseCooldown -= Mathf.Max(0f, deltaTime);
            if (activationNoiseCooldown > 0f)
            {
                return;
            }

            EmitCargoNoise();
            activationNoiseCooldown = configuredCargoNoiseInterval;
        }

        private void EmitCargoNoise()
        {
            if (payloadTransform != null)
            {
                CombatNoiseSystem.Emit(
                    payloadTransform.position,
                    configuredCargoNoiseRadius,
                    CombatTeamId.Player);
            }
        }

        private void BuildPreplacedSentryPoints(
            int requestedCount,
            out Vector3[] positions,
            out Vector3[] facings)
        {
            var count = Mathf.Max(0, requestedCount);
            positions = new Vector3[count];
            facings = new Vector3[count];
            if (count == 0 || RoutePlan == null)
            {
                return;
            }

            var campCount = Mathf.CeilToInt(count / 2f);
            for (var index = 0; index < count; index++)
            {
                var campIndex = index / 2;
                var campT = (campIndex + 1f) / (campCount + 1f);
                var routeDistance = RoutePlan.TotalLength * Mathf.Lerp(.18f, .82f, campT);
                var routeCenter = RoutePlan.EvaluateDistance(routeDistance);
                var before = RoutePlan.EvaluateDistance(Mathf.Max(0f, routeDistance - 1f));
                var after = RoutePlan.EvaluateDistance(Mathf.Min(RoutePlan.TotalLength, routeDistance + 1f));
                var forward = HorizontalDirection(after - before, Vector3.forward);
                var right = new Vector3(forward.z, 0f, -forward.x);
                var side = campIndex % 2 == 0 ? 1f : -1f;
                var slotOffset = index % 2 == 0 ? -1.4f : 1.4f;
                var position = routeCenter + right * side * 6f + forward * slotOffset;
                position.x = Mathf.Clamp(position.x, configuredBounds.xMin + 1f, configuredBounds.xMax - 1f);
                position.z = Mathf.Clamp(position.z, configuredBounds.yMin + 1f, configuredBounds.yMax - 1f);
                position.y = 0f;
                positions[index] = position;
                facings[index] = HorizontalDirection(routeCenter - position, -right * side);
            }
        }

        private void TickEscortDirector(float deltaTime)
        {
            if (escortDirector == null || waveSpawner == null || RoutePlan == null || payloadTransform == null)
            {
                return;
            }

            if (snapshot.Phase != PayloadMissionPhase.Escorting)
            {
                escortDirector.Tick(deltaTime, snapshot.Phase, waveSpawner.ActiveCount, out _);
                return;
            }

            if (pendingEscortEncounter.HasValue)
            {
                TrySpawnEscortEncounter(pendingEscortEncounter.Value);
                return;
            }

            if (!escortDirector.Tick(deltaTime, snapshot.Phase, waveSpawner.ActiveCount, out var encounter))
            {
                return;
            }

            pendingEscortEncounter = encounter;
            TrySpawnEscortEncounter(encounter);
        }

        private void TrySpawnEscortEncounter(PayloadEscortEncounter encounter)
        {
            if (waveSpawner == null || RoutePlan == null || payloadTransform == null)
            {
                return;
            }

            var distance = snapshot.RouteDistance;
            var before = RoutePlan.EvaluateDistance(Mathf.Max(0f, distance - 1f));
            var after = RoutePlan.EvaluateDistance(Mathf.Min(RoutePlan.TotalLength, distance + 1f));
            var option = ActiveRouteOption;
            var extraEnemies = option != null ? Mathf.Max(0, option.extraTravelEnemies) : 0;
            var adjustedEncounter = new PayloadEscortEncounter(
                encounter.SequenceId, encounter.GroupSize + extraEnemies, encounter.Pattern);
            if (waveSpawner.SpawnTravelGroup(
                adjustedEncounter,
                payloadTransform.position,
                after - before,
                configuredBounds,
                escortRadius,
                Camera.main,
                option != null && option.forceCargoSaboteur))
            {
                pendingEscortEncounter = null;
            }
        }

        private void HandleWaveStarted(int waveId, System.Collections.Generic.IReadOnlyCollection<int> enemyIds)
        {
            if (authority && stateMachine != null)
            {
                stateMachine.RegisterWave(waveId, enemyIds);
            }
        }

        private void HandleEnemyRemoved(int waveId, int enemyId)
        {
            if (!authority || stateMachine == null)
            {
                return;
            }

            var previousPhase = snapshot.Phase;
            if (waveId == PayloadWaveSpawner.RecoveryWaveId)
            {
                stateMachine.ReportRecoveryGuardRemoved(enemyId);
            }
            else
            {
                stateMachine.ReportEnemyRemoved(waveId, enemyId);
            }
            BeginRelayChoiceIfNeeded(previousPhase, stateMachine.Snapshot.Phase);
            PublishSnapshot(previousPhase);
        }

        private void BeginRelayChoiceIfNeeded(PayloadMissionPhase previous, PayloadMissionPhase current)
        {
            if (!relayChoicesEnabled || previous != PayloadMissionPhase.CheckpointDefense ||
                current != PayloadMissionPhase.Escorting || relayChoicePending)
                return;
            relayChoicePending = true;
            activeRouteOptionId = null;
            stateMachine.SetEscortSpeedMultiplier(1f);
            waveSpawner?.SetEnemyHealthMultiplier(1f);
        }

        private void TickRoutePattern(float deltaTime)
        {
            if (stateMachine.Snapshot.Phase != PayloadMissionPhase.Escorting || relayChoicePending) return;
            var option = ActiveRouteOption;
            if (option == null) return;
            if (option.cargoRepairPerSecond > 0f)
                cargoIntegrity?.Repair(option.cargoRepairPerSecond * deltaTime);
            if (option.pulseDamage <= 0f || waveSpawner == null || payloadTransform == null) return;
            shockPulseCooldown -= deltaTime;
            if (shockPulseCooldown > 0f) return;
            shockPulseCooldown = Mathf.Max(.1f, option.pulseInterval);
            foreach (var enemy in new List<MissionMobController>(waveSpawner.LivingEnemies))
            {
                if (enemy == null || enemy.IsDefeated) continue;
                var offset = Vector3.ProjectOnPlane(enemy.transform.position - payloadTransform.position, Vector3.up);
                if (offset.sqrMagnitude <= option.pulseRadius * option.pulseRadius)
                    enemy.GetComponent<ActorHealth>()?.ApplyDamage(option.pulseDamage);
            }
        }

        private void PublishSnapshot(PayloadMissionPhase previousPhase)
        {
            snapshot = ComposeSnapshot();
            SnapshotChanged?.Invoke(snapshot);
            if (previousPhase != snapshot.Phase)
            {
                PhaseChanged?.Invoke(previousPhase, snapshot.Phase);
            }
        }

        private PayloadMissionSnapshot ComposeSnapshot()
        {
            var state = stateMachine.Snapshot;
            return new PayloadMissionSnapshot(
                state.Phase, state.ElapsedTime, state.RouteDistance, state.NormalizedProgress,
                state.NextCheckpointIndex, state.ActiveWaveId, state.RemainingEnemyCount,
                state.PlayersInRange, state.EnemiesInRange, state.IsEscortActive, state.IsWaveRegistered,
                state.ActivationProgress, state.IsActivationInputHeld,
                cargoIntegrity != null ? cargoIntegrity.CurrentHealth : -1f,
                cargoIntegrity != null ? cargoIntegrity.MaximumHealth : -1f,
                cargoIntegrity != null && cargoIntegrity.IsBroken,
                relayChoicePending, activeRouteOptionId);
        }

        private RelayRouteOptionDefinition FindRouteOption(string optionId)
        {
            if (string.IsNullOrEmpty(optionId)) return null;
            foreach (var option in RouteOptions)
                if (option != null && option.id == optionId) return option;
            return null;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static Vector3 HorizontalDirection(Vector3 value, Vector3 fallback)
        {
            value = Vector3.ProjectOnPlane(value, Vector3.up);
            if (!IsFinite(value.x) || !IsFinite(value.y) || !IsFinite(value.z) || value.sqrMagnitude <= .0001f)
            {
                value = Vector3.ProjectOnPlane(fallback, Vector3.up);
            }

            return value.sqrMagnitude <= .0001f ? Vector3.forward : value.normalized;
        }
    }
}
