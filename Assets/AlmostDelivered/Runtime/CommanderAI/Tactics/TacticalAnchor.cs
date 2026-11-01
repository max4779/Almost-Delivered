using AlmostDelivered.CommanderAI.Domain;
using UnityEngine;

namespace AlmostDelivered.CommanderAI.Tactics
{
    public sealed class TacticalAnchor : MonoBehaviour
    {
        [SerializeField] private TacticalAnchorTag anchorTag;
        [SerializeField] private MonsterRole[] allowedRoles = { MonsterRole.Shield, MonsterRole.Ranged, MonsterRole.Charger, MonsterRole.Support };
        [SerializeField, Min(1)] private int capacity = 1;

        public TacticalAnchorTag Tag { get => anchorTag; set => anchorTag = value; }
        public MonsterRole[] AllowedRoles { get => (MonsterRole[])allowedRoles.Clone(); set => allowedRoles = value == null ? new MonsterRole[0] : (MonsterRole[])value.Clone(); }
        public int Capacity { get => Mathf.Max(1, capacity); set => capacity = Mathf.Max(1, value); }
        public bool Allows(MonsterRole role) => System.Array.IndexOf(allowedRoles, role) >= 0;
    }
}
