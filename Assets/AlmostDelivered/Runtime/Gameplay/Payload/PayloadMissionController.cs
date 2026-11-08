using System;
using AlmostDelivered.Combat;
using AlmostDelivered.MapGeneration;
using UnityEngine;

namespace AlmostDelivered.Gameplay.Payload
{
    public sealed class PayloadMissionController : MonoBehaviour
    {
        private readonly PayloadPresenceSensor presenceSensor = new PayloadPresenceSensor();

        [SerializeField] private bool initializeOnStart;
        [SerializeField] private bool authority;
        [SerializeField] private int configuredSeed;
        [SerializeField] private Rect configuredBounds;
        [SerializeField] private Transform playerTransform;
        [SerializeField] private Transform payloadTransform;
        [SerializeField] private MissionMapGenerator configuredMapGenerator;
        [SerializeField] private PayloadWaveSpawner waveSpawner;
        [SerializeField] private float configuredMovementSpeed;
        [SerializeField] private float configuredDeploymentDuration;
        [SerializeField] private float configuredCorridorWidth = 3f;
        [SerializeField] private int configuredInteriorPointCount = 5;
        [SerializeField] private bool requiresCargoRecovery;
        [SerializeField, Min(0)] private int configuredRecoveryGuardCount = 6;
        [SerializeField, Min(0f)] private float configuredCargoActivationDuration = 4f;
        [SerializeField, Min(0f)] private float configuredCargoNoiseRadius = 26f;
        [SerializeField, Min(.1f)] private float configuredCargoNoiseInterval = .65f;
        private PayloadMissionStateMachine stateMachine;
        private PayloadEscortDirector escortDirector;
        private PayloadEscortEncounter? pendingEscortEncounter;
        [SerializeField] private float escortRadius;
        private PayloadMissionSnapshot snapshot;
        private float activationNoiseCooldown;
        private bool activationResponseSpawned;

        public bool IsAuthority => authority;
        public PayloadRoutePlan RoutePlan { get; private set; }
        public PayloadMissionSnapshot Snapshot => snapshot;
        public bool HasPendingEscortEncounter => pendingEscortEncounter.HasValue;
        public bool RequiresCargoRecovery => requiresCargoRecovery;
        public int RecoveryGuardCount => configuredRecoveryGuardCount;
        public float CargoActivationDuration => configuredCargoActivationDuration;

        public event Action<PayloadMissionSnapshot> SnapshotChanged;
        public event Action<PayloadMissionPhase, PayloadMissionPhase> PhaseChanged;

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
            float cargoActivationDuration = 4f)
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
            configuredMapGenerator = mapGenerator;
            waveSpawner = waves;
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
            Initialize();
        }

        private void Start()
        {
            if (initializeOnStart && stateMachine == null)
            {
                Initialize();
            }
        }

        private void Initialize()
        {
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
            activationResponseSpawned = false;
            if (payloadTransform != null)
            {
                payloadTransform.position = RoutePlan.Points[0];
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
            }

            if (requiresCargoRecovery)
            {
                stateMachine.RegisterRecoveryGuards(recoveryGuardIds);
            }

            snapshot = stateMachine.Snapshot;
            SnapshotChanged?.Invoke(snapshot);
        }

        private void Update()
        {
            Tick(Time.deltaTime);
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
            if (!authority || stateMachine == null || playerTransform == null || payloadTransform == null)
            {
                return;
            }

            presenceSensor.Sample(payloadTransform.position, escortRadius, out var players, out var enemies);
            var previousPhase = snapshot.Phase;
            stateMachine.Tick(deltaTime, players, enemies);
            HandleActivationTransition(previousPhase, stateMachine.Snapshot.Phase);
            payloadTransform.position = RoutePlan.EvaluateDistance(stateMachine.Snapshot.RouteDistance);
            PublishSnapshot(previousPhase);
            TickActivationNoise(deltaTime);
            TickEscortDirector(deltaTime);
        }

        public bool ApplySnapshot(PayloadMissionSnapshot value)
        {
            if (authority || RoutePlan == null)
            {
                return false;
            }

            var previousPhase = snapshot.Phase;
            snapshot = value;
            if (payloadTransform != null)
            {
                payloadTransform.position = RoutePlan.EvaluateDistance(value.RouteDistance);
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
            if (!activationResponseSpawned && waveSpawner != null && payloadTransform != null && RoutePlan != null)
            {
                var routeDirection = RoutePlan.Points.Count > 1
                    ? RoutePlan.Points[1] - RoutePlan.Points[0]
                    : Vector3.forward;
                activationResponseSpawned = waveSpawner.SpawnActivationResponseGroup(
                    payloadTransform.position,
                    routeDirection,
                    configuredBounds,
                    escortRadius);
            }

            EmitCargoNoise();
        }

        private void TickActivationNoise(float deltaTime)
        {
            if (snapshot.Phase != PayloadMissionPhase.Activating)
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
            if (waveSpawner.SpawnTravelGroup(
                encounter,
                payloadTransform.position,
                after - before,
                configuredBounds,
                escortRadius,
                Camera.main))
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
            PublishSnapshot(previousPhase);
        }

        private void PublishSnapshot(PayloadMissionPhase previousPhase)
        {
            snapshot = stateMachine.Snapshot;
            SnapshotChanged?.Invoke(snapshot);
            if (previousPhase != snapshot.Phase)
            {
                PhaseChanged?.Invoke(previousPhase, snapshot.Phase);
            }
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
