using AlmostDelivered.Gameplay;
using UnityEngine;

namespace AlmostDelivered.Frontend
{
    public sealed class AutomaticRifleStats : MonoBehaviour
    {
        public const float DefaultDamage = 8f;
        public const float DefaultRange = 32f;
        public const float DefaultNoiseRadius = 24f;

        [SerializeField, Min(0f)] private float damage = DefaultDamage;
        [SerializeField, Min(0f)] private float fireCooldown = .12f;
        [SerializeField, Min(1)] private int magazineSize = 20;
        [SerializeField, Min(.01f)] private float reloadSeconds = 1.8f;
        [SerializeField, Min(.1f)] private float range = DefaultRange;
        [SerializeField, Min(0f)] private float noiseRadius = DefaultNoiseRadius;
        [Header("이 무기를 들었을 때 이동·스테미나")]
        [SerializeField] private PlayerMobilitySettings mobilitySettings = PlayerMobilitySettings.Default;

        public float Damage => damage;
        public float FireCooldown => fireCooldown;
        public int MagazineSize => magazineSize;
        public float ReloadSeconds => reloadSeconds;
        public float Range => range;
        public float NoiseRadius => noiseRadius;
        public PlayerMobilitySettings MobilitySettings => mobilitySettings;
    }
}
