using AlmostDelivered.CommanderAI.Tactics;
using UnityEngine;

namespace AlmostDelivered.CommanderAI.Domain
{
    public readonly struct MonsterOrder
    {
        public MonsterOrderType Type { get; }
        public TacticalAnchor Anchor { get; }
        public Vector3 Position { get; }
        public int TargetActorId { get; }
        public Transform ProtectedUnit { get; }

        public MonsterOrder(MonsterOrderType type, TacticalAnchor anchor = null, Vector3 position = default,
            int targetActorId = 0, Transform protectedUnit = null)
        {
            Type = type; Anchor = anchor; Position = position; TargetActorId = targetActorId; ProtectedUnit = protectedUnit;
        }
    }
}
