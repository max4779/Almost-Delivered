using System;
using AlmostDelivered.Combat;
using UnityEngine;

namespace AlmostDelivered.Gameplay
{
    [Serializable]
    public struct PlayerMobilitySettings
    {
        [Min(1f)] public float maxStamina;
        [Min(0f)] public float sprintDrainPerSecond;
        [Min(0f)] public float staminaRecoveryPerSecond;
        [Min(0f)] public float recoveryDelay;
        [Min(1f)] public float sprintSpeedMultiplier;
        [Min(0f)] public float rollStaminaCost;
        [Min(.01f)] public float rollDuration;
        [Min(0f)] public float rollSpeed;

        public static PlayerMobilitySettings Default => new PlayerMobilitySettings
        {
            maxStamina = 100f,
            sprintDrainPerSecond = 18f,
            staminaRecoveryPerSecond = 25f,
            recoveryDelay = 2f,
            sprintSpeedMultiplier = 1.6f,
            rollStaminaCost = 28f,
            rollDuration = .38f,
            rollSpeed = 13f
        };
    }

    /// <summary>Shared sprint, roll and stamina rules for the hub and mission player.</summary>
    public sealed class PlayerMobility : MonoBehaviour
    {
        [SerializeField, HideInInspector] private float maxStamina = 100f;
        [SerializeField, HideInInspector] private float sprintDrainPerSecond = 18f;
        [SerializeField, HideInInspector] private float staminaRecoveryPerSecond = 25f;
        [SerializeField, HideInInspector] private float recoveryDelay = 2f;
        [SerializeField, HideInInspector] private float sprintSpeedMultiplier = 1.6f;
        [SerializeField, HideInInspector] private float rollStaminaCost = 28f;
        [SerializeField, HideInInspector] private float rollDuration = .38f;
        [SerializeField, HideInInspector] private float rollSpeed = 13f;

        private float stamina = 100f;
        private float recoveryClock;
        private float rollRemaining;
        private Vector3 rollDirection;
        private ActorHealth health;

        public float Stamina => stamina;
        public float MaxStamina => maxStamina;
        public float RecoveryDelay => recoveryDelay;
        public bool IsSprinting { get; private set; }
        public bool IsRolling => rollRemaining > 0f;
        public bool IsRollInvulnerable => isActiveAndEnabled && IsRolling;
        public float RollProgress => IsRolling ? 1f - rollRemaining / rollDuration : 0f;
        public Vector3 RollDirection => rollDirection;

        private void Awake()
        {
            stamina = maxStamina;
            health = GetComponent<ActorHealth>();
        }

        private void OnDisable()
        {
            rollRemaining = 0f;
            health?.SetInvulnerable(false);
        }

        public void ApplySettings(PlayerMobilitySettings settings)
        {
            var remainingRatio = maxStamina > 0f ? Mathf.Clamp01(stamina / maxStamina) : 1f;
            maxStamina = Mathf.Max(1f, settings.maxStamina);
            sprintDrainPerSecond = Mathf.Max(0f, settings.sprintDrainPerSecond);
            staminaRecoveryPerSecond = Mathf.Max(0f, settings.staminaRecoveryPerSecond);
            recoveryDelay = Mathf.Max(0f, settings.recoveryDelay);
            sprintSpeedMultiplier = Mathf.Max(1f, settings.sprintSpeedMultiplier);
            rollStaminaCost = Mathf.Max(0f, settings.rollStaminaCost);
            rollDuration = Mathf.Max(.01f, settings.rollDuration);
            rollSpeed = Mathf.Max(0f, settings.rollSpeed);
            stamina = remainingRatio * maxStamina;
        }

        public Vector3 Step(Vector2 input, bool sprintHeld, bool rollPressed, float deltaTime, float baseSpeed)
        {
            if (!IsFinite(input.x) || !IsFinite(input.y) || !IsFinite(deltaTime) || deltaTime <= 0f ||
                !IsFinite(baseSpeed) || baseSpeed < 0f)
                return Vector3.zero;

            input = Vector2.ClampMagnitude(input, 1f);
            var direction = new Vector3(input.x, 0f, input.y);
            if (rollPressed && !IsRolling && stamina >= rollStaminaCost)
            {
                stamina -= rollStaminaCost;
                rollRemaining = rollDuration;
                rollDirection = direction.sqrMagnitude > .0001f
                    ? direction.normalized
                    : Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            }

            if (IsRolling)
            {
                IsSprinting = false;
                recoveryClock = 0f;
                rollRemaining = Mathf.Max(0f, rollRemaining - deltaTime);
                SyncInvulnerability();
                return rollDirection * rollSpeed;
            }

            SyncInvulnerability();

            var moving = direction.sqrMagnitude > .0001f;
            IsSprinting = moving && sprintHeld && stamina > 0f;
            if (IsSprinting)
                stamina = Mathf.Max(0f, stamina - sprintDrainPerSecond * deltaTime);

            if (moving && sprintHeld)
                recoveryClock = 0f;
            else
            {
                recoveryClock += deltaTime;
                if (recoveryClock >= recoveryDelay)
                    stamina = Mathf.Min(maxStamina, stamina + staminaRecoveryPerSecond * deltaTime);
            }

            return direction * baseSpeed * (IsSprinting ? sprintSpeedMultiplier : 1f);
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private void SyncInvulnerability()
        {
            if (health == null) health = GetComponent<ActorHealth>();
            health?.SetInvulnerable(IsRollInvulnerable);
        }
    }
}
