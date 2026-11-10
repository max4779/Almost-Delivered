using AlmostDelivered.Gameplay;
using UnityEngine;

namespace AlmostDelivered.Frontend
{
    public sealed class MeleeWeaponStats : MonoBehaviour
    {
        public const float DefaultDamage = 34f;

        [SerializeField, Min(0f)] private float damage = DefaultDamage;
        [SerializeField, Min(.1f)] private float reach = 2.8f;
        [SerializeField, Range(1f, 360f)] private float arcDegrees = 140f;
        [SerializeField, Min(0f)] private float attackCooldown = .55f;
        [SerializeField, Min(0f)] private float noiseRadius = 5f;
        [Header("이 무기를 들었을 때 이동·스테미나")]
        [SerializeField] private PlayerMobilitySettings mobilitySettings = PlayerMobilitySettings.Default;

        public float Damage => damage;
        public float Reach => reach;
        public float ArcDegrees => arcDegrees;
        public float AttackCooldown => attackCooldown;
        public float NoiseRadius => noiseRadius;
        public PlayerMobilitySettings MobilitySettings => mobilitySettings;
    }
}
