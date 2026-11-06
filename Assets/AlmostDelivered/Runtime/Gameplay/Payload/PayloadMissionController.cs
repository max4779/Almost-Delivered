using System;
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
        private PayloadMissionStateMachine stateMachine;
        private PayloadEscortDirector escortDirector;
        [SerializeField] private float escortRadius;
        private PayloadMissionSnapshot snapshot;

        public bool IsAuthority => authority;
        public PayloadRoutePlan RoutePlan { get; private set; }
        public PayloadMissionSnapshot Snapshot => snapshot;

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
            float corridorWidth = 3f)
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
                configuredDeploymentDuration);
            stateMachine.WaveRequested += HandleWaveRequested;
            escortDirector = authority && waveSpawner != null
                ? new PayloadEscortDirector(configuredSeed)
                : null;
            snapshot = stateMachine.Snapshot;

            if (payloadTransform != null)
            {
                payloadTransform.position = RoutePlan.Points[0];
            }

            if (configuredMapGenerator != null)
            {
                configuredMapGenerator.Generate(configuredSeed, RoutePlan.CorridorExclusions);
            }

            BindWaveSpawner();
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
            payloadTransform.position = RoutePlan.EvaluateDistance(stateMachine.Snapshot.RouteDistance);
            PublishSnapshot(previousPhase);
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

        private void TickEscortDirector(float deltaTime)
        {
            if (escortDirector == null || waveSpawner == null || RoutePlan == null || payloadTransform == null)
            {
                return;
            }

            if (!escortDirector.Tick(deltaTime, snapshot.Phase, waveSpawner.ActiveCount, out var encounter))
            {
                return;
            }

            var distance = snapshot.RouteDistance;
            var before = RoutePlan.EvaluateDistance(Mathf.Max(0f, distance - 1f));
            var after = RoutePlan.EvaluateDistance(Mathf.Min(RoutePlan.TotalLength, distance + 1f));
            waveSpawner.SpawnTravelGroup(
                encounter,
                payloadTransform.position,
                after - before,
                configuredBounds,
                escortRadius,
                Camera.main);
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
            stateMachine.ReportEnemyRemoved(waveId, enemyId);
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
    }
}
