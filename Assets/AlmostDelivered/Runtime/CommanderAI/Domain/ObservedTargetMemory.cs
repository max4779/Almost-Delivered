using UnityEngine;

namespace AlmostDelivered.CommanderAI.Domain
{
    public readonly struct ObservedTargetMemory
    {
        public int ActorId { get; }
        public Vector3 LastKnownPosition { get; }
        public Vector3 LastKnownVelocity { get; }
        public float LastObservedTime { get; }
        public float Confidence { get; }
        public ObservedCombatTag Tags { get; }
        public int ReporterId { get; }
        internal float ObservedConfidence { get; }

        internal ObservedTargetMemory(in ObservationSample sample, ObservedCombatTag tags)
            : this(sample.ActorId, sample.Position, sample.Velocity, sample.Timestamp,
                sample.Confidence, sample.Confidence, tags, sample.ReporterId)
        {
        }

        internal ObservedTargetMemory(int actorId, Vector3 position, Vector3 velocity, float timestamp,
            float confidence, float observedConfidence, ObservedCombatTag tags, int reporterId)
        {
            ActorId = actorId;
            LastKnownPosition = position;
            LastKnownVelocity = velocity;
            LastObservedTime = timestamp;
            Confidence = confidence;
            ObservedConfidence = observedConfidence;
            Tags = tags;
            ReporterId = reporterId;
        }

        internal ObservedTargetMemory With(float confidence, ObservedCombatTag tags)
        {
            return new ObservedTargetMemory(ActorId, LastKnownPosition, LastKnownVelocity,
                LastObservedTime, confidence, ObservedConfidence, tags, ReporterId);
        }
    }
}
