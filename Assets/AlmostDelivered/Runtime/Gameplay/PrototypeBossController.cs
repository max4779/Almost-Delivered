using AlmostDelivered.Combat;
using UnityEngine;

namespace AlmostDelivered.Gameplay
{
    public enum PrototypeBossMode
    {
        RangedWarden,
        IronCharger
    }

    [RequireComponent(typeof(ActorHealth))]
    public sealed class PrototypeBossController : MonoBehaviour
    {
        [SerializeField] private PrototypeBossMode mode;
        [SerializeField] private Transform target;
        [SerializeField] private RangedAttackController rangedAttack;
        [SerializeField, Min(0f)] private float moveSpeed = 3.5f;
        [SerializeField, Min(.1f)] private float contactRange = 1.6f;
        [SerializeField, Min(0f)] private float contactDamage = 18f;
        [SerializeField, Min(0f)] private float contactCooldown = 1f;

        private ActorHealth health;
        private CharacterController characterController;
        private float nextContactTime;

        public PrototypeBossMode Mode => mode;
        public bool IsDefeated => health != null && health.IsDead;

        public void Configure(
            PrototypeBossMode bossMode,
            Transform combatTarget,
            RangedAttackController attack,
            float speed,
            float range,
            float damage,
            float cooldown)
        {
            mode = bossMode;
            target = combatTarget;
            rangedAttack = attack;
            moveSpeed = Mathf.Max(0f, speed);
            contactRange = Mathf.Max(.1f, range);
            contactDamage = Mathf.Max(0f, damage);
            contactCooldown = Mathf.Max(0f, cooldown);
            nextContactTime = 0f;
        }

        private void Awake()
        {
            health = GetComponent<ActorHealth>();
            characterController = GetComponent<CharacterController>();
            health.Died += HandleDeath;
        }

        private void OnDestroy()
        {
            if (health != null)
                health.Died -= HandleDeath;
        }

        private void Update()
        {
            Tick(Time.time, Time.deltaTime);
        }

        public void Tick(float now, float deltaTime)
        {
            if (!enabled || IsDefeated || target == null || !IsFinite(now) || !IsFinite(deltaTime) || deltaTime < 0f)
                return;

            var targetHealth = target.GetComponentInParent<ActorHealth>();
            if (targetHealth == null || targetHealth.IsDead)
                return;

            var toTarget = Vector3.ProjectOnPlane(target.position - transform.position, Vector3.up);
            if (toTarget.sqrMagnitude < .0001f)
                return;

            var direction = toTarget.normalized;
            transform.forward = direction;

            if (mode == PrototypeBossMode.RangedWarden)
            {
                if (rangedAttack != null)
                    rangedAttack.TryFire(target.position, now);
                return;
            }

            if (toTarget.magnitude > contactRange)
            {
                var movement = direction * (moveSpeed * deltaTime);
                if (characterController != null && characterController.enabled)
                    characterController.Move(movement);
                else
                    transform.position += movement;
                return;
            }

            if (now >= nextContactTime)
            {
                targetHealth.ApplyDamage(contactDamage);
                nextContactTime = now + contactCooldown;
            }
        }

        private void HandleDeath()
        {
            if (rangedAttack != null)
                rangedAttack.enabled = false;
            if (characterController != null)
                characterController.enabled = false;
            foreach (var actorCollider in GetComponentsInChildren<Collider>())
                actorCollider.enabled = false;
            enabled = false;
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
