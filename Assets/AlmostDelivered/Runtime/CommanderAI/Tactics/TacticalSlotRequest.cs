using UnityEngine;

namespace AlmostDelivered.CommanderAI.Tactics
{
    public readonly struct TacticalSlotRequest
    {
        public Vector3 Position { get; }
        public TacticalAnchorTag Tag { get; }
        public TacticalSlotRequest(Vector3 position, TacticalAnchorTag tag) { Position = position; Tag = tag; }
    }
}
