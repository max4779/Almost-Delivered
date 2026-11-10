using UnityEngine;

namespace AlmostDelivered.Frontend
{
    public sealed class RevolverStats : MonoBehaviour
    {
        public const float DefaultDamage = 55f;
        public const float DefaultRange = 38f;
        public const float DefaultNoiseRadius = 28f;

        [SerializeField, Min(0f)] private float damage = DefaultDamage;
        [SerializeField, Min(0f)] private float fireCooldown = .58f;
        [SerializeField, Min(1)] private int magazineSize = 6;
        [SerializeField, Min(.01f)] private float reloadSeconds = 2.4f;
        [SerializeField, Min(.1f)] private float range = DefaultRange;
        [SerializeField, Min(0f)] private float noiseRadius = DefaultNoiseRadius;
        [SerializeField] private bool pierceEnemies = true;

        public float Damage => damage;
        public float FireCooldown => fireCooldown;
        public int MagazineSize => magazineSize;
        public float ReloadSeconds => reloadSeconds;
        public float Range => range;
        public float NoiseRadius => noiseRadius;
        public bool PierceEnemies => pierceEnemies;
    }
}
