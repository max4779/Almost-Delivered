using System;
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
        [SerializeField] private Transform target;
        [SerializeField] private RangedAttackController rangedAttack;
        [SerializeField, Min(1f)] private float turnSpeed = 360f;
        [SerializeField] private float gravity = -24f;
        [SerializeField] private float groundedVerticalSpeed = -2f;

        private ActorHealth health;
        private CharacterController characterController;
        private float nextContactTime;
        private float verticalVelocity;
        private bool removalReported;

        public MissionMobDefinition Definition { get; private set; }
        public int WaveId { get; private set; } = -1;
        public int EnemyId { get; private set; } = -1;
        public bool IsDefeated => health != null && health.IsDead;

        public event Action<MissionMobController> Removed;

        private void Awake()
        {
            health = GetComponent<ActorHealth>();
            characterController = GetComponent<CharacterController>();
            health.Died += HandleDeath;
        }

        private void OnDisable()
        {
            if (health != null && !health.IsDead)
            {
                NotifyRemoved();
            }
        }

        private void OnDestroy()
        {
            if (health != null)
            {
                health.Died -= HandleDeath;
            }

            NotifyRemoved();
        }

        private void Update()
        {
            Tick(Time.time, Time.deltaTime);
        }

        public void Configure(MissionMobDefinition definition, Transform combatTarget, RangedAttackController attack)
        {
            Definition = definition;
            target = combatTarget;
            rangedAttack = attack;
            nextContactTime = 0f;
            verticalVelocity = 0f;
            removalReported = false;
            health.ConfigureMaximum(definition.MaxHealth);
        }

        public void AssignIdentity(int waveId, int enemyId)
        {
            WaveId = waveId;
            EnemyId = enemyId;
        }

        public void Tick(float now, float deltaTime)
        {
            if (!enabled || IsDefeated || target == null || !IsFinite(now) || !IsFinite(deltaTime) || deltaTime < 0f)
            {
                return;
            }

            var targetHealth = target.GetComponentInParent<ActorHealth>();
            if (targetHealth == null || targetHealth.IsDead)
            {
                Move(Vector3.zero, deltaTime);
                return;
            }

            var toTarget = Vector3.ProjectOnPlane(target.position - transform.position, Vector3.up);
            if (toTarget.sqrMagnitude <= .0001f)
            {
                Move(Vector3.zero, deltaTime);
                return;
            }

            var distance = toTarget.magnitude;
            var direction = toTarget / distance;
            RotateTowards(direction, deltaTime);

            if (IsRanged(Definition.Archetype))
            {
                if (distance > Definition.StandOffRange)
                {
                    Move(direction, deltaTime);
                }
                else if (rangedAttack != null)
                {
                    rangedAttack.TryFire(target.position, now);
                }

                if (distance <= Definition.StandOffRange)
                {
                    Move(Vector3.zero, deltaTime);
                }

                return;
            }

            if (distance > Definition.ContactRange)
            {
                Move(direction, deltaTime);
                return;
            }

            if (now >= nextContactTime)
            {
                targetHealth.ApplyDamage(Definition.ContactDamage);
                nextContactTime = now + Definition.ContactCooldown;
            }

            Move(Vector3.zero, deltaTime);
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

            NotifyRemoved();
            enabled = false;
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
