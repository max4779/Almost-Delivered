using System;
using System.Collections.Generic;
using AlmostDelivered.Presentation;
using UnityEngine;

namespace AlmostDelivered.Combat
{
    [RequireComponent(typeof(CombatTeam))]
    public sealed class MeleeAttackController : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float damage = 34f;
        [SerializeField, Min(.1f)] private float reach = 2.8f;
        [SerializeField, Range(1f, 360f)] private float arc = 140f;
        [SerializeField, Min(0f)] private float cooldown = .55f;
        [SerializeField, Min(0f)] private float noiseRadius = 5f;
        private Collider[] hits = new Collider[32];
        private readonly HashSet<ActorHealth> damaged = new HashSet<ActorHealth>();
        private float nextAttackTime;

        public float Damage => damage;

        public void Configure(float attackDamage, float attackReach, float attackArc, float attackCooldown,
            float attackNoiseRadius = 5f)
        {
            var nextDamage = Mathf.Max(0f, attackDamage);
            var nextReach = Mathf.Max(.1f, attackReach);
            var nextArc = Mathf.Clamp(attackArc, 1f, 360f);
            var nextCooldown = Mathf.Max(0f, attackCooldown);
            var nextNoiseRadius = Mathf.Max(0f, attackNoiseRadius);
            if (damage == nextDamage && reach == nextReach && arc == nextArc &&
                cooldown == nextCooldown && noiseRadius == nextNoiseRadius) return;
            damage = nextDamage;
            reach = nextReach;
            arc = nextArc;
            cooldown = nextCooldown;
            noiseRadius = nextNoiseRadius;
            nextAttackTime = 0f;
        }

        public bool TryAttack(float now)
        {
            return TryAttack(now, transform.forward);
        }

        public bool TryAttack(float now, Vector3 aimDirection)
        {
            if (!isActiveAndEnabled || float.IsNaN(now) || float.IsInfinity(now) || now < nextAttackTime)
                return false;
            var attackDirection = Vector3.ProjectOnPlane(aimDirection, Vector3.up);
            if (float.IsNaN(attackDirection.x) || float.IsNaN(attackDirection.z) ||
                float.IsInfinity(attackDirection.x) || float.IsInfinity(attackDirection.z) ||
                attackDirection.sqrMagnitude <= .0001f) return false;
            attackDirection.Normalize();
            var ownerHealth = GetComponent<ActorHealth>();
            if (ownerHealth != null && ownerHealth.IsDead) return false;
            var ownTeam = GetComponent<CombatTeam>();
            if (ownTeam == null) return false;

            nextAttackTime = now + cooldown;
            transform.rotation = Quaternion.LookRotation(attackDirection, Vector3.up);
            var animator = GetComponentInChildren<ProceduralPoseAnimator>();
            if (animator != null) animator.PlayPlayerSlash();
            var wind = GetComponent<MeleeSlashVfx>();
            wind?.Play(reach, arc, attackDirection);
            CombatNoiseSystem.Emit(transform.position, noiseRadius, ownTeam.Team);

            var center = transform.position + Vector3.up * .65f;
            int count;
            while (true)
            {
                count = Physics.OverlapSphereNonAlloc(center, reach, hits,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                if (count < hits.Length) break;
                Array.Resize(ref hits, hits.Length * 2);
            }
            damaged.Clear();
            for (var i = 0; i < count; i++)
            {
                var hit = hits[i];
                if (hit == null) continue;
                var targetHealth = hit.GetComponentInParent<ActorHealth>();
                if (targetHealth == null || targetHealth == ownerHealth || targetHealth.IsDead ||
                    damaged.Contains(targetHealth)) continue;
                var targetTeam = targetHealth.GetComponent<CombatTeam>();
                if (targetTeam == null || targetTeam.Team == ownTeam.Team) continue;
                var direction = Vector3.ProjectOnPlane(hit.bounds.center - transform.position, Vector3.up);
                if (direction.sqrMagnitude > .0001f &&
                    Vector3.Angle(attackDirection, direction) <= arc * .5f)
                {
                    damaged.Add(targetHealth);
                    targetHealth.ApplyDamage(damage);
                    targetHealth.GetComponentInChildren<ProceduralPoseAnimator>()?.PlayHit();
                }
            }
            if (damaged.Count > 0)
            {
                wind?.ConfirmHit();
            }
            return true;
        }
    }
}
