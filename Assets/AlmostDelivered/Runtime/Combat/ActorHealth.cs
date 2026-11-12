using System;
using UnityEngine;

namespace AlmostDelivered.Combat
{
    public sealed class ActorHealth : MonoBehaviour
    {
        [SerializeField, Min(0.01f)] private float maxHealth = 100f;

        public float MaxHealth => maxHealth;
        public float CurrentHealth { get; private set; } = 100f;
        public bool IsDead => CurrentHealth <= 0f;
        public bool IsInvulnerable { get; private set; }
        public event Action<float, float> HealthChanged;
        public event Action Died;

        private void Awake()
        {
            CurrentHealth = maxHealth;
        }

        public void SetInvulnerable(bool value) => IsInvulnerable = value;

        public void ConfigureMaximum(float maximum, bool refill = true, bool preserveRatio = false)
        {
            if (float.IsNaN(maximum) || float.IsInfinity(maximum) || maximum <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(maximum));
            }

            var previousRatio = maxHealth > 0f ? Mathf.Clamp01(CurrentHealth / maxHealth) : 1f;
            maxHealth = maximum;
            CurrentHealth = preserveRatio ? previousRatio * maximum :
                refill ? maximum : Mathf.Min(CurrentHealth, maximum);
            HealthChanged?.Invoke(CurrentHealth, maxHealth);
        }

        public void ApplyDamage(float damage)
        {
            if (float.IsNaN(damage) || float.IsInfinity(damage) || damage < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(damage));
            }

            if (IsDead || IsInvulnerable)
            {
                return;
            }

            CurrentHealth = Mathf.Max(0f, CurrentHealth - damage);
            HealthChanged?.Invoke(CurrentHealth, maxHealth);
            if (IsDead)
            {
                Died?.Invoke();
            }
        }
    }
}
