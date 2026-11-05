using System;
using AlmostDelivered.MapGeneration;
using UnityEngine;

namespace AlmostDelivered.Gameplay.Payload
{
    public sealed class PayloadMissionController : MonoBehaviour
    {
        private readonly PayloadPresenceSensor presenceSensor = new PayloadPresenceSensor();

        private Transform playerTransform;
        private Transform payloadTransform;
        private PayloadWaveSpawner waveSpawner;
        private PayloadMissionStateMachine stateMachine;
        private float escortRadius;
        private PayloadMissionSnapshot snapshot;

        public bool IsAuthority { get; private set; }
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
            UnbindWaveSpawner();
            IsAuthority = isAuthority;
            playerTransform = player;
            payloadTransform = payload;
            waveSpawner = waves;
            escortRadius = Mathf.Max(0f, radius);
            RoutePlan = PayloadRoutePlanner.Generate(missionSeed, missionBounds, interiorPointCount, corridorWidth);
            stateMachine = new PayloadMissionStateMachine(
                RoutePlan.TotalLength,
                RoutePlan.CheckpointDistances,
                movementSpeed,
                deploymentDuration);
            stateMachine.WaveRequested += HandleWaveRequested;
            snapshot = stateMachine.Snapshot;

            if (payloadTransform != null)
            {
                payloadTransform.position = RoutePlan.Points[0];
            }

            if (mapGenerator != null)
            {
                mapGenerator.Generate(missionSeed, RoutePlan.CorridorExclusions);
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

            UnbindWaveSpawner();
        }

        public void Tick(float deltaTime)
        {
            if (!IsAuthority || stateMachine == null || playerTransform == null || payloadTransform == null)
            {
                return;
            }

            presenceSensor.Sample(payloadTransform.position, escortRadius, out var players, out var enemies);
            var previousPhase = snapshot.Phase;
            stateMachine.Tick(deltaTime, players, enemies);
            payloadTransform.position = RoutePlan.EvaluateDistance(stateMachine.Snapshot.RouteDistance);
            PublishSnapshot(previousPhase);
        }

        public bool ApplySnapshot(PayloadMissionSnapshot value)
        {
            if (IsAuthority || RoutePlan == null)
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

        private void UnbindWaveSpawner()
        {
            if (waveSpawner == null)
            {
                return;
            }

            waveSpawner.WaveStarted -= HandleWaveStarted;
            waveSpawner.EnemyRemoved -= HandleEnemyRemoved;
            waveSpawner = null;
        }

        private void HandleWaveRequested(PayloadWaveRequest request)
        {
            if (IsAuthority && waveSpawner != null)
            {
                waveSpawner.SpawnWave(request.WaveId, request.CheckpointIndex, request.IsFinal);
            }
        }

        private void HandleWaveStarted(int waveId, System.Collections.Generic.IReadOnlyCollection<int> enemyIds)
        {
            if (IsAuthority && stateMachine != null)
            {
                stateMachine.RegisterWave(waveId, enemyIds);
            }
        }

        private void HandleEnemyRemoved(int waveId, int enemyId)
        {
            if (!IsAuthority || stateMachine == null)
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
