using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AlmostDelivered.Combat
{
    public sealed class CombatProjectile : MonoBehaviour
    {
        private GameObject owner;
        private CombatTeamId team;
        private Vector3 direction;
        private float speed;
        private float damage;
        private float remainingLifetime;
        private float collisionRadius;
        private bool configured;
        private bool resolved;
        private bool pierceEnemies;
        private readonly HashSet<ActorHealth> piercedActors = new HashSet<ActorHealth>();

        public GameObject Owner => owner;
        public CombatTeamId Team => team;
        public Vector3 Direction => direction;
        public float Damage => damage;
        public bool PiercesEnemies => pierceEnemies;

        public void Configure(
            GameObject projectileOwner,
            CombatTeamId projectileTeam,
            Vector3 travelDirection,
            float travelSpeed,
            float hitDamage,
            float lifetime,
            bool piercesEnemies = false)
        {
            if (projectileOwner == null)
                throw new ArgumentNullException(nameof(projectileOwner));
            if (!IsFinite(travelDirection) || travelDirection.sqrMagnitude < 0.0001f)
                throw new ArgumentOutOfRangeException(nameof(travelDirection));
            if (!IsFinite(travelSpeed) || travelSpeed <= 0f)
                throw new ArgumentOutOfRangeException(nameof(travelSpeed));
            if (!IsFinite(hitDamage) || hitDamage < 0f)
                throw new ArgumentOutOfRangeException(nameof(hitDamage));
            if (!IsFinite(lifetime) || lifetime <= 0f)
                throw new ArgumentOutOfRangeException(nameof(lifetime));

            owner = projectileOwner;
            team = projectileTeam;
            direction = Vector3.ProjectOnPlane(travelDirection, Vector3.up).normalized;
            speed = travelSpeed;
            damage = hitDamage;
            remainingLifetime = lifetime;
            pierceEnemies = piercesEnemies;
            piercedActors.Clear();
            collisionRadius = ResolveCollisionRadius();
            configured = true;
            resolved = false;
            transform.forward = direction;
        }

        private void FixedUpdate()
        {
            if (!configured || resolved)
                return;

            var step = speed * Time.fixedDeltaTime;
            var origin = transform.position;
            var overlaps = Physics.OverlapSphere(origin, collisionRadius, ~0, QueryTriggerInteraction.Ignore)
                .Where(candidate => !ShouldIgnore(candidate))
                .OrderBy(candidate => (candidate.ClosestPoint(origin) - origin).sqrMagnitude);
            foreach (var overlap in overlaps)
            {
                if (HandleImpact(overlap)) return;
            }

            var hits = collisionRadius > Mathf.Epsilon
                ? Physics.SphereCastAll(origin, collisionRadius, direction, step, ~0, QueryTriggerInteraction.Ignore)
                : Physics.RaycastAll(origin, direction, step, ~0, QueryTriggerInteraction.Ignore);

            foreach (var hit in hits.OrderBy(hit => hit.distance))
            {
                if (ShouldIgnore(hit.collider))
                    continue;

                if (HandleImpact(hit.collider)) return;
            }

            transform.position = origin + direction * step;
            remainingLifetime -= Time.fixedDeltaTime;
            if (remainingLifetime <= 0f)
                Finish();
        }

        private bool ShouldIgnore(Collider candidate)
        {
            if (candidate == null)
                return true;

            var candidateTransform = candidate.transform;
            if (candidateTransform == transform || candidateTransform.IsChildOf(transform))
                return true;
            if (owner != null && (candidateTransform == owner.transform || candidateTransform.IsChildOf(owner.transform)))
                return true;

            if (pierceEnemies && piercedActors.Contains(candidate.GetComponentInParent<ActorHealth>()))
                return true;

            var candidateTeam = candidate.GetComponentInParent<CombatTeam>();
            return candidateTeam != null && candidateTeam.Team == team;
        }

        private bool HandleImpact(Collider candidate)
        {
            if (resolved)
                return true;

            if (candidate != null)
            {
                var health = candidate.GetComponentInParent<ActorHealth>();
                var candidateTeam = candidate.GetComponentInParent<CombatTeam>();
                if (health != null && candidateTeam != null && candidateTeam.Team != team)
                {
                    if (pierceEnemies)
                    {
                        if (piercedActors.Add(health)) health.ApplyDamage(damage);
                        return false;
                    }
                    health.ApplyDamage(damage);
                }
            }

            Finish();
            return true;
        }

        private void Finish()
        {
            if (resolved) return;
            resolved = true;
            Destroy(gameObject);
        }

        private float ResolveCollisionRadius()
        {
            var sphere = GetComponent<SphereCollider>();
            if (sphere == null || !sphere.enabled)
            {
                return 0f;
            }

            var scale = transform.lossyScale;
            var largestScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
            return Mathf.Max(0f, sphere.radius * largestScale);
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private static bool IsFinite(Vector3 value) =>
            IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
    }
}
