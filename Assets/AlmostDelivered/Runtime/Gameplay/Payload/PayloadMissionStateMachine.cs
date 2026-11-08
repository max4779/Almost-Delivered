using System;
using System.Collections.Generic;
using UnityEngine;

namespace AlmostDelivered.Gameplay.Payload
{
    public sealed class PayloadMissionStateMachine
    {
        private readonly float routeLength;
        private readonly float[] checkpointDistances;
        private readonly float movementSpeed;
        private readonly float deploymentDuration;
        private readonly bool requiresRecovery;
        private readonly float activationDuration;
        private readonly HashSet<int> remainingEnemies = new HashSet<int>();

        private PayloadMissionPhase phase;
        private float elapsedTime;
        private float routeDistance;
        private int nextCheckpointIndex;
        private int nextWaveId = 1;
        private int activeWaveId = -1;
        private int playersInRange;
        private int enemiesInRange;
        private bool isEscortActive;
        private bool isWaveRegistered;
        private float activationElapsed;

        public PayloadMissionStateMachine(
            float routeLength,
            IReadOnlyList<float> checkpointDistances,
            float movementSpeed,
            float deploymentDuration,
            bool requiresCargoRecovery = false,
            float cargoActivationDuration = 4f)
        {
            this.routeLength = IsFinite(routeLength) ? Mathf.Max(0f, routeLength) : 0f;
            this.movementSpeed = IsFinite(movementSpeed) ? Mathf.Max(0f, movementSpeed) : 0f;
            this.deploymentDuration = IsFinite(deploymentDuration) ? Mathf.Max(0f, deploymentDuration) : 0f;
            requiresRecovery = requiresCargoRecovery;
            activationDuration = IsFinite(cargoActivationDuration) ? Mathf.Max(0f, cargoActivationDuration) : 0f;
            this.checkpointDistances = SanitizeCheckpoints(checkpointDistances, this.routeLength);
            phase = this.deploymentDuration > 0f
                ? PayloadMissionPhase.Deploying
                : requiresRecovery
                    ? PayloadMissionPhase.Recovering
                    : PayloadMissionPhase.Escorting;
        }

        public event Action<PayloadWaveRequest> WaveRequested;

        public PayloadMissionSnapshot Snapshot => new PayloadMissionSnapshot(
            phase,
            elapsedTime,
            routeDistance,
            routeLength > Mathf.Epsilon ? Mathf.Clamp01(routeDistance / routeLength) : 0f,
            nextCheckpointIndex,
            activeWaveId,
            remainingEnemies.Count,
            playersInRange,
            enemiesInRange,
            isEscortActive,
            isWaveRegistered,
            requiresRecovery && (phase == PayloadMissionPhase.Activating || phase == PayloadMissionPhase.Escorting ||
                                 phase == PayloadMissionPhase.CheckpointDefense || phase == PayloadMissionPhase.FinalDefense ||
                                 phase == PayloadMissionPhase.Completed)
                ? activationDuration <= Mathf.Epsilon ? 1f : Mathf.Clamp01(activationElapsed / activationDuration)
                : 0f);

        public void Tick(float deltaTime, int playersInRange, int enemiesInRange)
        {
            if (!IsFinite(deltaTime) || deltaTime < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(deltaTime), "Delta time must be finite and non-negative.");
            }

            if (phase == PayloadMissionPhase.Completed || phase == PayloadMissionPhase.Failed)
            {
                return;
            }

            this.playersInRange = Mathf.Max(0, playersInRange);
            this.enemiesInRange = Mathf.Max(0, enemiesInRange);
            elapsedTime += deltaTime;
            isEscortActive = false;

            if (phase == PayloadMissionPhase.Deploying)
            {
                if (elapsedTime >= deploymentDuration)
                {
                    phase = requiresRecovery
                        ? PayloadMissionPhase.Recovering
                        : PayloadMissionPhase.Escorting;
                }

                return;
            }

            if (phase == PayloadMissionPhase.Recovering)
            {
                if (isWaveRegistered && remainingEnemies.Count == 0 && this.playersInRange > 0)
                {
                    remainingEnemies.Clear();
                    isWaveRegistered = false;
                    activeWaveId = -1;
                    activationElapsed = 0f;
                    phase = PayloadMissionPhase.Activating;
                }

                return;
            }

            if (phase == PayloadMissionPhase.Activating)
            {
                if (this.playersInRange <= 0)
                {
                    return;
                }

                activationElapsed = Mathf.Min(activationDuration, activationElapsed + deltaTime);
                if (activationDuration <= Mathf.Epsilon || activationElapsed + .0001f >= activationDuration)
                {
                    activationElapsed = activationDuration;
                    phase = PayloadMissionPhase.Escorting;
                }

                return;
            }

            if (phase == PayloadMissionPhase.CheckpointDefense || phase == PayloadMissionPhase.FinalDefense)
            {
                if (isWaveRegistered && remainingEnemies.Count == 0)
                {
                    CompleteDefense();
                }

                return;
            }

            if (phase != PayloadMissionPhase.Escorting ||
                this.playersInRange == 0 ||
                this.enemiesInRange > 0 ||
                routeLength <= Mathf.Epsilon)
            {
                return;
            }

            isEscortActive = true;
            var targetDistance = nextCheckpointIndex < checkpointDistances.Length
                ? checkpointDistances[nextCheckpointIndex]
                : routeLength;
            routeDistance = Mathf.Min(targetDistance, routeDistance + movementSpeed * deltaTime);

            if (routeDistance + .0001f < targetDistance)
            {
                return;
            }

            routeDistance = targetDistance;
            if (nextCheckpointIndex < checkpointDistances.Length)
            {
                BeginDefense(false, nextCheckpointIndex);
            }
            else
            {
                BeginDefense(true, checkpointDistances.Length);
            }
        }

        public bool RegisterWave(int waveId, IReadOnlyCollection<int> enemyIds)
        {
            if (!IsDefensePhase(phase) || waveId != activeWaveId || isWaveRegistered)
            {
                return false;
            }

            remainingEnemies.Clear();
            if (enemyIds != null)
            {
                foreach (var enemyId in enemyIds)
                {
                    remainingEnemies.Add(enemyId);
                }
            }

            isWaveRegistered = true;
            return true;
        }

        public bool RegisterRecoveryGuards(IReadOnlyCollection<int> enemyIds)
        {
            if (!requiresRecovery ||
                (phase != PayloadMissionPhase.Deploying && phase != PayloadMissionPhase.Recovering) ||
                isWaveRegistered)
            {
                return false;
            }

            remainingEnemies.Clear();
            if (enemyIds != null)
            {
                foreach (var enemyId in enemyIds)
                {
                    remainingEnemies.Add(enemyId);
                }
            }

            activeWaveId = PayloadWaveSpawner.RecoveryWaveId;
            isWaveRegistered = true;
            return true;
        }

        public bool ReportRecoveryGuardRemoved(int enemyId)
        {
            if (!requiresRecovery ||
                (phase != PayloadMissionPhase.Deploying && phase != PayloadMissionPhase.Recovering) ||
                !isWaveRegistered)
            {
                return false;
            }

            return remainingEnemies.Remove(enemyId);
        }

        public bool ReportEnemyRemoved(int waveId, int enemyId)
        {
            if (!IsDefensePhase(phase) || !isWaveRegistered || waveId != activeWaveId)
            {
                return false;
            }

            if (!remainingEnemies.Remove(enemyId))
            {
                return false;
            }

            if (remainingEnemies.Count == 0)
            {
                CompleteDefense();
            }

            return true;
        }

        private void BeginDefense(bool isFinal, int checkpointIndex)
        {
            phase = isFinal
                ? PayloadMissionPhase.FinalDefense
                : PayloadMissionPhase.CheckpointDefense;
            isEscortActive = false;
            isWaveRegistered = false;
            remainingEnemies.Clear();
            activeWaveId = nextWaveId++;
            WaveRequested?.Invoke(new PayloadWaveRequest(activeWaveId, checkpointIndex, isFinal));
        }

        private void CompleteDefense()
        {
            var completedFinalDefense = phase == PayloadMissionPhase.FinalDefense;
            remainingEnemies.Clear();
            isWaveRegistered = false;
            activeWaveId = -1;
            isEscortActive = false;

            if (completedFinalDefense)
            {
                phase = PayloadMissionPhase.Completed;
                return;
            }

            nextCheckpointIndex++;
            phase = PayloadMissionPhase.Escorting;
        }

        private static float[] SanitizeCheckpoints(IReadOnlyList<float> source, float routeLength)
        {
            if (source == null || source.Count == 0 || routeLength <= Mathf.Epsilon)
            {
                return Array.Empty<float>();
            }

            var values = new List<float>(source.Count);
            for (var index = 0; index < source.Count; index++)
            {
                var value = source[index];
                if (IsFinite(value) && value > 0f && value < routeLength)
                {
                    values.Add(value);
                }
            }

            values.Sort();
            for (var index = values.Count - 1; index > 0; index--)
            {
                if (Mathf.Approximately(values[index], values[index - 1]))
                {
                    values.RemoveAt(index);
                }
            }

            return values.ToArray();
        }

        private static bool IsDefensePhase(PayloadMissionPhase value)
        {
            return value == PayloadMissionPhase.CheckpointDefense || value == PayloadMissionPhase.FinalDefense;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
