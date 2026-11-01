using UnityEngine;

namespace AlmostDelivered.CommanderAI.Domain
{
    public readonly struct ObservationSample
    {
        public int ActorId { get; }
        public Vector3 Position { get; }
        public Vector3 Velocity { get; }
        public float Timestamp { get; }
        public float Confidence { get; }
        public ObservedCombatTag Tags { get; }
        public int ReporterId { get; }

        public ObservationSample(int actorId, Vector3 position, Vector3 velocity, float timestamp,
            float confidence, ObservedCombatTag tags, int reporterId)
        {
            ActorId = actorId;
            Position = position;
            Velocity = velocity;
            Timestamp = timestamp;
            Confidence = confidence;
            Tags = tags;
            ReporterId = reporterId;
        }
    }
}
