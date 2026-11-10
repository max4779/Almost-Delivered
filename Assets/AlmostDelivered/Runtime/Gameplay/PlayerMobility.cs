using UnityEngine;

namespace AlmostDelivered.Gameplay
{
    /// <summary>Shared sprint, roll and stamina rules for the hub and mission player.</summary>
    public sealed class PlayerMobility : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float maxStamina = 100f;
        [SerializeField, Min(0f)] private float sprintDrainPerSecond = 18f;
        [SerializeField, Min(0f)] private float staminaRecoveryPerSecond = 25f;
        [SerializeField, Min(0f)] private float recoveryDelay = 2f;
        [SerializeField, Min(1f)] private float sprintSpeedMultiplier = 1.6f;
        [SerializeField, Min(0f)] private float rollStaminaCost = 28f;
        [SerializeField, Min(.01f)] private float rollDuration = .38f;
        [SerializeField, Min(0f)] private float rollSpeed = 13f;

        private float stamina = 100f;
        private float stillTime;
        private float rollRemaining;
        private Vector3 rollDirection;

        public float Stamina => stamina;
        public float MaxStamina => maxStamina;
        public float RecoveryDelay => recoveryDelay;
        public bool IsSprinting { get; private set; }
        public bool IsRolling => rollRemaining > 0f;
        public float RollProgress => IsRolling ? 1f - rollRemaining / rollDuration : 0f;

        private void Awake() => stamina = maxStamina;

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
                stillTime = 0f;
                rollRemaining = Mathf.Max(0f, rollRemaining - deltaTime);
                return rollDirection * rollSpeed;
            }

            var moving = direction.sqrMagnitude > .0001f;
            IsSprinting = moving && sprintHeld && stamina > 0f;
            if (IsSprinting)
                stamina = Mathf.Max(0f, stamina - sprintDrainPerSecond * deltaTime);

            if (moving)
                stillTime = 0f;
            else
            {
                stillTime += deltaTime;
                if (stillTime >= recoveryDelay)
                    stamina = Mathf.Min(maxStamina, stamina + staminaRecoveryPerSecond * deltaTime);
            }

            return direction * baseSpeed * (IsSprinting ? sprintSpeedMultiplier : 1f);
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
