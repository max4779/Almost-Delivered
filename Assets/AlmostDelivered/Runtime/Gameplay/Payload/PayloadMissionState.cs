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
        Failed
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
            bool isActivationInputHeld = false)
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
    }
}
