using System;
using System.Collections.Generic;
using AlmostDelivered.Combat;
using UnityEngine;

namespace AlmostDelivered.Gameplay.Payload
{
    public enum MissionMobArchetype
    {
        Melee,
        Ranged,
        Shield,
        CommanderBoss,
        ChargerBoss
    }

    public readonly struct MissionMobDefinition
    {
        public MissionMobDefinition(
            MissionMobArchetype archetype,
            float maxHealth,
            float moveSpeed,
            float contactRange,
            float contactDamage,
            float contactCooldown,
            float standOffRange)
        {
            Archetype = archetype;
            MaxHealth = maxHealth;
            MoveSpeed = moveSpeed;
            ContactRange = contactRange;
            ContactDamage = contactDamage;
            ContactCooldown = contactCooldown;
            StandOffRange = standOffRange;
        }

        public MissionMobArchetype Archetype { get; }
        public float MaxHealth { get; }
        public float MoveSpeed { get; }
        public float ContactRange { get; }
        public float ContactDamage { get; }
        public float ContactCooldown { get; }
        public float StandOffRange { get; }

        public static MissionMobDefinition For(MissionMobArchetype archetype)
        {
            switch (archetype)
            {
                case MissionMobArchetype.Ranged:
                    return new MissionMobDefinition(archetype, 45f, 3f, 1.2f, 8f, 1.2f, 6f);
                case MissionMobArchetype.Shield:
                    return new MissionMobDefinition(archetype, 90f, 2.2f, 1.6f, 12f, 1.2f, 0f);
                case MissionMobArchetype.CommanderBoss:
                    return new MissionMobDefinition(archetype, 220f, 2.5f, 1.5f, 12f, 1f, 7f);
                case MissionMobArchetype.ChargerBoss:
                    return new MissionMobDefinition(archetype, 300f, 3.8f, 1.7f, 16f, 1.1f, 0f);
                default:
                    return new MissionMobDefinition(MissionMobArchetype.Melee, 60f, 4f, 1.4f, 10f, 1f, 0f);
            }
        }
    }

    [RequireComponent(typeof(ActorHealth), typeof(CombatTeam))]
    public sealed class MissionMobController : MonoBehaviour
    {
        private static readonly HashSet<MissionMobController> ActiveMobs = new HashSet<MissionMobController>();

        [SerializeField] private Transform target;
        [SerializeField] private Transform cargoTarget;
        [SerializeField] private RangedAttackController rangedAttack;
        [SerializeField, Min(1f)] private float turnSpeed = 360f;
        [SerializeField] private float gravity = -24f;
        [SerializeField] private float groundedVerticalSpeed = -2f;
        [SerializeField, Min(.1f)] private float deathDuration = 1f;
        [SerializeField] private float deathFallAngle = -82f;
        [SerializeField, Min(0f)] private float deathSinkDepth = 1.35f;
        [SerializeField, Min(0f)] private float alertRadius = 7f;
        [SerializeField, Min(.1f)] private float alertIndicatorDuration = 1.25f;
        [SerializeField, Range(1f, 180f)] private float sightFieldOfView = 110f;
        [SerializeField, Min(0f)] private float closeAwarenessRadius = 2.5f;

        private ActorHealth health;
        private CharacterController characterController;
        private float nextContactTime;
        private float verticalVelocity;
        private bool removalReported;
        private bool deathAnimating;
        private float deathElapsed;
        private Transform deathVisual;
        private Vector3 deathStartPosition;
        private Quaternion deathStartRotation;
        private bool guardPostEnabled;
        private bool guardPostEngaged;
        private Vector3 guardPost;
        private float guardAwarenessRadius;
        private float lastKnownHealth;
        private float alertTimeRemaining;
        private bool suppressDamageAlert;
        private GameObject alertIndicator;
        private bool assaultingCargo;
        private readonly RaycastHit[] navigationHits = new RaycastHit[24];
        private Collider detourObstacle;
        private Vector3 detourNear;
        private Vector3 detourFar;
        private bool headingToFar;

        public MissionMobDefinition Definition { get; private set; }
        public int WaveId { get; private set; } = -1;
        public int EnemyId { get; private set; } = -1;
        public bool IsDefeated => health != null && health.IsDead;
        public bool HasGuardPost => guardPostEnabled;
        public bool IsGuardPostEngaged => guardPostEngaged;
        public bool IsAlerted => alertTimeRemaining > 0f && alertIndicator != null && alertIndicator.activeSelf;
        public bool IsPursuingPlayer => !assaultingCargo || guardPostEngaged;
        public GameObject AlertIndicator => alertIndicator;

        public event Action<MissionMobController> Removed;

        private void Awake()
        {
            health = GetComponent<ActorHealth>();
            characterController = GetComponent<CharacterController>();
            lastKnownHealth = health.CurrentHealth;
            health.HealthChanged += HandleHealthChanged;
            health.Died += HandleDeath;
        }

        private void OnEnable()
        {
            ActiveMobs.Add(this);
            CombatNoiseSystem.NoiseEmitted += HandleNoise;
        }

        private void OnDisable()
        {
            ActiveMobs.Remove(this);
            CombatNoiseSystem.NoiseEmitted -= HandleNoise;
            if (health != null && !health.IsDead)
            {
                NotifyRemoved();
            }
        }

        private void OnDestroy()
        {
            ActiveMobs.Remove(this);
            CombatNoiseSystem.NoiseEmitted -= HandleNoise;
            if (health != null)
            {
                health.HealthChanged -= HandleHealthChanged;
                health.Died -= HandleDeath;
            }

            NotifyRemoved();
        }

        private void Update()
        {
            Tick(Time.time, Time.deltaTime);
        }

        private void LateUpdate()
        {
            if (alertIndicator == null || !alertIndicator.activeSelf || Camera.main == null)
            {
                return;
            }

            var direction = alertIndicator.transform.position - Camera.main.transform.position;
            if (direction.sqrMagnitude > .0001f)
            {
                alertIndicator.transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            }
        }

        public void Configure(MissionMobDefinition definition, Transform combatTarget, RangedAttackController attack)
        {
            Definition = definition;
            target = combatTarget;
            rangedAttack = attack;
            nextContactTime = 0f;
            verticalVelocity = 0f;
            removalReported = false;
            deathAnimating = false;
            deathElapsed = 0f;
            deathVisual = null;
            guardPostEnabled = false;
            guardPostEngaged = false;
            assaultingCargo = false;
            cargoTarget = null;
            alertTimeRemaining = 0f;
            if (alertIndicator != null)
            {
                alertIndicator.SetActive(false);
            }

            suppressDamageAlert = true;
            health.ConfigureMaximum(definition.MaxHealth);
            lastKnownHealth = health.CurrentHealth;
            suppressDamageAlert = false;
            ClearDetour();
        }

        public void ConfigureGuardPost(Vector3 position, float awarenessRadius)
        {
            guardPost = position;
            guardAwarenessRadius = Mathf.Max(0f, awarenessRadius);
            guardPostEnabled = true;
            guardPostEngaged = false;
        }

        public void ConfigureCargoAssault(Transform cargo)
        {
            cargoTarget = cargo;
            assaultingCargo = cargo != null;
            guardAwarenessRadius = 10f;
        }

        public void AssignIdentity(int waveId, int enemyId)
        {
            WaveId = waveId;
            EnemyId = enemyId;
        }

        public void Tick(float now, float deltaTime)
        {
            if (!enabled || !IsFinite(now) || !IsFinite(deltaTime) || deltaTime < 0f)
            {
                return;
            }

            TickAlertIndicator(deltaTime);

            if (deathAnimating)
            {
                TickDeath(deltaTime);
                return;
            }

            if (IsDefeated || target == null)
            {
                return;
            }

            var targetHealth = target.GetComponentInParent<ActorHealth>();
            if (targetHealth == null || targetHealth.IsDead)
            {
                Move(Vector3.zero, deltaTime);
                return;
            }

            var toPlayer = Vector3.ProjectOnPlane(target.position - transform.position, Vector3.up);
            if (assaultingCargo && !guardPostEngaged && CanDetectTarget(toPlayer))
                ReceiveAlert(false);

            var pursuingPlayer = IsPursuingPlayer || cargoTarget == null;
            var destination = pursuingPlayer ? target : cargoTarget;
            var toTarget = Vector3.ProjectOnPlane(destination.position - transform.position, Vector3.up);
            if (guardPostEnabled && !guardPostEngaged)
            {
                if (CanDetectTarget(toTarget))
                {
                    ReceiveAlert();
                }
                else
                {
                    var toGuardPost = Vector3.ProjectOnPlane(guardPost - transform.position, Vector3.up);
                    if (toGuardPost.sqrMagnitude > .25f)
                    {
                        var guardDirection = toGuardPost.normalized;
                        RotateTowards(guardDirection, deltaTime);
                        Move(guardDirection, deltaTime);
                    }
                    else
                    {
                        Move(Vector3.zero, deltaTime);
                    }

                    return;
                }
            }

            if (toTarget.sqrMagnitude <= .0001f)
            {
                Move(Vector3.zero, deltaTime);
                return;
            }

            var distance = toTarget.magnitude;
            var direction = toTarget / distance;
            if (!pursuingPlayer && distance <= 2.4f)
            {
                Move(Vector3.zero, deltaTime);
                return;
            }
            Collider obstacle = null;
            var blocked = characterController != null && FindBlockingObstacle(direction, distance, out obstacle);
            if (blocked && detourObstacle != obstacle && detourObstacle == null)
                PlanDetour(obstacle, direction);
            var navigationDirection = NavigationDirection(direction);
            RotateTowards(navigationDirection, deltaTime);

            if (IsRanged(Definition.Archetype))
            {
                if (distance > Definition.StandOffRange || blocked || detourObstacle != null)
                {
                    Move(navigationDirection, deltaTime);
                }
                else if (pursuingPlayer && rangedAttack != null && !blocked && detourObstacle == null)
                {
                    rangedAttack.TryFire(target.position, now);
                }

                if (distance <= Definition.StandOffRange && !blocked && detourObstacle == null)
                {
                    Move(Vector3.zero, deltaTime);
                }

                return;
            }

            if (distance > Definition.ContactRange || blocked || detourObstacle != null)
            {
                Move(navigationDirection, deltaTime);
                return;
            }

            if (pursuingPlayer && now >= nextContactTime)
            {
                targetHealth.ApplyDamage(Definition.ContactDamage);
                nextContactTime = now + Definition.ContactCooldown;
            }

            Move(Vector3.zero, deltaTime);
        }

        private bool FindBlockingObstacle(Vector3 direction, float distance, out Collider obstacle)
        {
            obstacle = null;
            var origin = transform.position + Vector3.up;
            var count = Physics.SphereCastNonAlloc(origin, .38f, direction, navigationHits,
                Mathf.Max(0f, distance - .5f), Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            var nearest = float.PositiveInfinity;
            for (var index = 0; index < count; index++)
            {
                var hit = navigationHits[index];
                var candidate = hit.collider;
                if (candidate == null || candidate.transform.IsChildOf(transform) ||
                    candidate.transform == target || candidate.transform.IsChildOf(target) ||
                    candidate.GetComponentInParent<ActorHealth>() != null || hit.distance >= nearest)
                    continue;

                nearest = hit.distance;
                obstacle = candidate;
            }

            return obstacle != null;
        }

        private void PlanDetour(Collider obstacle, Vector3 targetDirection)
        {
            var bounds = obstacle.bounds;
            var center = Vector3.ProjectOnPlane(bounds.center, Vector3.up);
            var lateral = new Vector3(targetDirection.z, 0f, -targetDirection.x);
            var side = Vector3.Dot(transform.position - center, lateral) >= 0f ? 1f : -1f;
            var extent = bounds.extents;
            var forwardRadius = Mathf.Abs(targetDirection.x) * extent.x + Mathf.Abs(targetDirection.z) * extent.z + .9f;
            var sideRadius = Mathf.Abs(lateral.x) * extent.x + Mathf.Abs(lateral.z) * extent.z + 1f;
            detourNear = center - targetDirection * forwardRadius + lateral * side * sideRadius;
            detourFar = center + targetDirection * forwardRadius + lateral * side * sideRadius;
            detourObstacle = obstacle;
            headingToFar = false;
        }

        private Vector3 NavigationDirection(Vector3 direct)
        {
            if (detourObstacle == null)
                return direct;

            var here = Vector3.ProjectOnPlane(transform.position, Vector3.up);
            if (!headingToFar && Vector3.Distance(here, detourNear) < .65f)
                headingToFar = true;
            if (headingToFar && Vector3.Distance(here, detourFar) < .7f)
            {
                ClearDetour();
                return direct;
            }

            var waypoint = headingToFar ? detourFar : detourNear;
            return (waypoint - here).normalized;
        }

        private void ClearDetour()
        {
            detourObstacle = null;
            headingToFar = false;
        }

        private void Move(Vector3 direction, float deltaTime)
        {
            var horizontalVelocity = direction * Definition.MoveSpeed;
            if (characterController != null && characterController.enabled)
            {
                if (characterController.isGrounded && verticalVelocity < 0f)
                    verticalVelocity = groundedVerticalSpeed;
                else
                    verticalVelocity += gravity * deltaTime;

                var movement = (horizontalVelocity + Vector3.up * verticalVelocity) * deltaTime;
                var collisionFlags = characterController.Move(movement);
                if ((collisionFlags & CollisionFlags.Below) != 0)
                    verticalVelocity = groundedVerticalSpeed;
            }
            else
            {
                transform.position += horizontalVelocity * deltaTime;
            }
        }

        private void RotateTowards(Vector3 direction, float deltaTime)
        {
            if (direction.sqrMagnitude <= .0001f || deltaTime <= 0f)
                return;

            var targetRotation = Quaternion.LookRotation(direction, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                targetRotation,
                turnSpeed * deltaTime);
        }

        private void HandleDeath()
        {
            alertTimeRemaining = 0f;
            if (alertIndicator != null)
            {
                alertIndicator.SetActive(false);
            }

            if (rangedAttack != null)
            {
                rangedAttack.enabled = false;
            }

            if (characterController != null)
            {
                characterController.enabled = false;
            }

            foreach (var actorCollider in GetComponentsInChildren<Collider>())
            {
                actorCollider.enabled = false;
            }

            deathVisual = transform.Find("VisualRoot");
            if (deathVisual != null)
            {
                deathStartPosition = deathVisual.localPosition;
                deathStartRotation = deathVisual.localRotation;
            }

            deathElapsed = 0f;
            deathAnimating = true;
            NotifyRemoved();
        }

        private void HandleHealthChanged(float current, float maximum)
        {
            var tookDamage = !suppressDamageAlert && current + .0001f < lastKnownHealth;
            lastKnownHealth = current;
            if (tookDamage)
            {
                if (assaultingCargo)
                    ReceiveAlert(false);
                AlertNearbyMobs();
            }
        }

        private void HandleNoise(CombatNoise noise)
        {
            if (!isActiveAndEnabled || IsDefeated)
            {
                return;
            }

            var offset = Vector3.ProjectOnPlane(transform.position - noise.Position, Vector3.up);
            if (offset.sqrMagnitude <= noise.Radius * noise.Radius)
            {
                ReceiveAlert(false);
            }
        }

        private bool CanDetectTarget(Vector3 toTarget)
        {
            var distanceSquared = toTarget.sqrMagnitude;
            if (distanceSquared <= closeAwarenessRadius * closeAwarenessRadius)
            {
                return true;
            }

            if (distanceSquared > guardAwarenessRadius * guardAwarenessRadius || distanceSquared <= .0001f)
            {
                return false;
            }

            var direction = toTarget.normalized;
            var minimumDot = Mathf.Cos(sightFieldOfView * .5f * Mathf.Deg2Rad);
            if (Vector3.Dot(transform.forward, direction) < minimumDot)
            {
                return false;
            }

            return HasLineOfSight(direction, Mathf.Sqrt(distanceSquared));
        }

        private bool HasLineOfSight(Vector3 planarDirection, float distance)
        {
            var eye = transform.position + Vector3.up;
            if (!Physics.Raycast(eye, planarDirection, out var hit, distance, Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Ignore))
            {
                return true;
            }

            return hit.transform == target || hit.transform.IsChildOf(target);
        }

        private void AlertNearbyMobs()
        {
            var center = transform.position;
            var radiusSquared = alertRadius * alertRadius;
            var snapshot = new List<MissionMobController>(ActiveMobs);
            foreach (var candidate in snapshot)
            {
                if (candidate == null || !candidate.isActiveAndEnabled || candidate.IsDefeated)
                {
                    continue;
                }

                var offset = Vector3.ProjectOnPlane(candidate.transform.position - center, Vector3.up);
                if (offset.sqrMagnitude <= radiusSquared)
                {
                    candidate.ReceiveAlert(!candidate.assaultingCargo);
                }
            }
        }

        private void ReceiveAlert(bool showIndicator = true)
        {
            guardPostEngaged = true;
            ClearDetour();
            if (assaultingCargo)
                showIndicator = false;
            if (!showIndicator)
            {
                return;
            }

            alertTimeRemaining = Mathf.Max(alertTimeRemaining, alertIndicatorDuration);
            EnsureAlertIndicator();
            alertIndicator.SetActive(true);
        }

        private void TickAlertIndicator(float deltaTime)
        {
            if (alertTimeRemaining <= 0f)
            {
                return;
            }

            alertTimeRemaining = Mathf.Max(0f, alertTimeRemaining - deltaTime);
            if (alertTimeRemaining <= 0f && alertIndicator != null)
            {
                alertIndicator.SetActive(false);
            }
        }

        private void EnsureAlertIndicator()
        {
            if (alertIndicator != null)
            {
                return;
            }

            alertIndicator = new GameObject("Alert Indicator", typeof(TextMesh));
            alertIndicator.transform.SetParent(transform, false);
            alertIndicator.transform.localPosition = new Vector3(0f, 2.85f, 0f);
            var text = alertIndicator.GetComponent<TextMesh>();
            text.text = "!";
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.fontSize = 64;
            text.characterSize = .16f;
            text.fontStyle = FontStyle.Bold;
            text.color = new Color(1f, .86f, .08f, 1f);
        }

        private void TickDeath(float deltaTime)
        {
            deathElapsed += deltaTime;
            var progress = Mathf.Clamp01(deathElapsed / Mathf.Max(.1f, deathDuration));
            if (deathVisual != null)
            {
                var fall = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(progress / .55f));
                var sink = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.35f, 1f, progress));
                deathVisual.localRotation = deathStartRotation * Quaternion.Euler(deathFallAngle * fall, 0f, 0f);
                deathVisual.localPosition = deathStartPosition + Vector3.down * (deathSinkDepth * sink);
            }

            if (progress < 1f)
            {
                return;
            }

            deathAnimating = false;
            enabled = false;
            Destroy(gameObject);
        }

        private void NotifyRemoved()
        {
            if (removalReported)
            {
                return;
            }

            removalReported = true;
            Removed?.Invoke(this);
        }

        private static bool IsRanged(MissionMobArchetype archetype)
        {
            return archetype == MissionMobArchetype.Ranged || archetype == MissionMobArchetype.CommanderBoss;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
