using UnityEngine;

namespace AlmostDelivered.Gameplay.Payload
{
    public enum PayloadMissionPhase
    {
        Deploying,
        Recovering,
        Activating,
        Escorting,
        CheckpointDefense,
        FinalDefense,
        Completed,
        Failed,
        EvacuationTravel,
        EvacuationDefense,
        Escaped
    }

    public readonly struct PayloadWaveRequest
    {
        public PayloadWaveRequest(int waveId, int checkpointIndex, bool isFinal)
        {
            WaveId = waveId;
            CheckpointIndex = checkpointIndex;
            IsFinal = isFinal;
        }

        public int WaveId { get; }

        public int CheckpointIndex { get; }

        public bool IsFinal { get; }
    }

    public readonly struct PayloadMissionSnapshot
    {
        public PayloadMissionSnapshot(
            PayloadMissionPhase phase,
            float elapsedTime,
            float routeDistance,
            float normalizedProgress,
            int nextCheckpointIndex,
            int activeWaveId,
            int remainingEnemyCount,
            int playersInRange,
            int enemiesInRange,
            bool isEscortActive,
            bool isWaveRegistered,
            float activationProgress = 0f,
            bool isActivationInputHeld = false,
            float cargoHealth = -1f,
            float cargoMaximumHealth = -1f,
            bool isCargoBroken = false,
            bool isRelayChoicePending = false,
            string activeRouteOptionId = null,
            float fuelRemaining = -1f,
            float fuelCapacity = -1f,
            int cargoDestructions = 0,
            Vector3 evacuationTarget = default,
            float evacuationSecondsRemaining = 0f)
        {
            Phase = phase;
            ElapsedTime = elapsedTime;
            RouteDistance = routeDistance;
            NormalizedProgress = normalizedProgress;
            NextCheckpointIndex = nextCheckpointIndex;
            ActiveWaveId = activeWaveId;
            RemainingEnemyCount = remainingEnemyCount;
            PlayersInRange = playersInRange;
            EnemiesInRange = enemiesInRange;
            IsEscortActive = isEscortActive;
            IsWaveRegistered = isWaveRegistered;
            ActivationProgress = activationProgress;
            IsActivationInputHeld = isActivationInputHeld;
            CargoHealth = cargoHealth;
            CargoMaximumHealth = cargoMaximumHealth;
            IsCargoBroken = isCargoBroken;
            IsRelayChoicePending = isRelayChoicePending;
            ActiveRouteOptionId = activeRouteOptionId;
            FuelRemaining = fuelRemaining;
            FuelCapacity = fuelCapacity;
            CargoDestructions = cargoDestructions;
            EvacuationTarget = evacuationTarget;
            EvacuationSecondsRemaining = evacuationSecondsRemaining;
        }

        public PayloadMissionPhase Phase { get; }

        public float ElapsedTime { get; }

        public float RouteDistance { get; }

        public float NormalizedProgress { get; }

        public int NextCheckpointIndex { get; }

        public int ActiveWaveId { get; }

        public int RemainingEnemyCount { get; }

        public int PlayersInRange { get; }

        public int EnemiesInRange { get; }

        public bool IsEscortActive { get; }

        public bool IsWaveRegistered { get; }

        public float ActivationProgress { get; }

        public bool IsActivationInputHeld { get; }

        public float CargoHealth { get; }

        public float CargoMaximumHealth { get; }

        public bool IsCargoBroken { get; }

        public bool IsRelayChoicePending { get; }

        public string ActiveRouteOptionId { get; }
        public float FuelRemaining { get; }
        public float FuelCapacity { get; }
        public int CargoDestructions { get; }
        public Vector3 EvacuationTarget { get; }
        public float EvacuationSecondsRemaining { get; }
        public int CargoStocksRemaining => System.Math.Max(0, 3 - CargoDestructions);
        public bool UsesFuel => FuelCapacity > 0f;
        public bool IsOutOfFuel => UsesFuel && FuelRemaining <= .001f;
    }
}
