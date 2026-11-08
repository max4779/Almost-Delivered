using System;
using UnityEngine;

namespace AlmostDelivered.Combat
{
    public sealed class RangedAttackController : MonoBehaviour
    {
        [SerializeField] private GameObject projectileTemplate;
        [SerializeField] private Transform muzzle;
        [SerializeField] private float damage = 20f;
        [SerializeField] private float cooldown = .25f;
        [SerializeField] private float projectileSpeed = 24f;
        [SerializeField] private float projectileLifetime = 2f;
        [SerializeField, Min(0f)] private float shotNoiseRadius = 14f;

        private ActorHealth ownerHealth;
        private CombatTeam ownerTeam;
        private float nextFireTime;

        private void Awake()
        {
            CacheOwnerComponents();
        }

        public void Configure(
            GameObject template,
            Transform muzzleTransform,
            float shotDamage,
            float shotCooldown,
            float shotSpeed,
            float shotLifetime,
            float noiseRadius = 14f)
        {
            if (template == null || template.GetComponent<CombatProjectile>() == null)
                throw new ArgumentException("A CombatProjectile template is required.", nameof(template));
            if (muzzleTransform == null)
                throw new ArgumentNullException(nameof(muzzleTransform));
            if (!IsFinite(shotDamage) || shotDamage < 0f)
                throw new ArgumentOutOfRangeException(nameof(shotDamage));
            if (!IsFinite(shotCooldown) || shotCooldown < 0f)
                throw new ArgumentOutOfRangeException(nameof(shotCooldown));
            if (!IsFinite(shotSpeed) || shotSpeed <= 0f)
                throw new ArgumentOutOfRangeException(nameof(shotSpeed));
            if (!IsFinite(shotLifetime) || shotLifetime <= 0f)
                throw new ArgumentOutOfRangeException(nameof(shotLifetime));
            if (!IsFinite(noiseRadius) || noiseRadius < 0f)
                throw new ArgumentOutOfRangeException(nameof(noiseRadius));

            projectileTemplate = template;
            muzzle = muzzleTransform;
            damage = shotDamage;
            cooldown = shotCooldown;
            projectileSpeed = shotSpeed;
            projectileLifetime = shotLifetime;
            shotNoiseRadius = noiseRadius;
            CacheOwnerComponents();
            nextFireTime = 0f;
        }

        public bool TryFire(Vector3 aimPoint, float now)
        {
            if (!enabled || projectileTemplate == null || muzzle == null || ownerTeam == null)
                return false;
            if (ownerHealth != null && ownerHealth.IsDead)
                return false;
            if (!IsFinite(now) || now < nextFireTime || !IsFinite(aimPoint))
                return false;

            var planarDirection = Vector3.ProjectOnPlane(aimPoint - muzzle.position, Vector3.up);
            if (planarDirection.sqrMagnitude < 0.0001f)
                return false;

            planarDirection.Normalize();

            var clone = Instantiate(projectileTemplate, muzzle.position, Quaternion.LookRotation(planarDirection));
            clone.name = projectileTemplate.name + " (Shot)";
            clone.SetActive(false);
            clone.GetComponent<CombatProjectile>().Configure(
                gameObject,
                ownerTeam.Team,
                planarDirection,
                projectileSpeed,
                damage,
                projectileLifetime);
            clone.SetActive(true);
            nextFireTime = now + cooldown;
            CombatNoiseSystem.Emit(muzzle.position, shotNoiseRadius, ownerTeam.Team);
            return true;
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private static bool IsFinite(Vector3 value) =>
            IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);

        private void CacheOwnerComponents()
        {
            ownerHealth = GetComponent<ActorHealth>();
            ownerTeam = GetComponent<CombatTeam>();
        }
    }
}
