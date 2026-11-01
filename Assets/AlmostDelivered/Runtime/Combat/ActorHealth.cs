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
        public event Action Died;

        private void Awake()
        {
            CurrentHealth = maxHealth;
        }

        public void ApplyDamage(float damage)
        {
            if (float.IsNaN(damage) || float.IsInfinity(damage) || damage < 0f)
                throw new ArgumentOutOfRangeException(nameof(damage));
            if (IsDead)
                return;

            CurrentHealth = Mathf.Max(0f, CurrentHealth - damage);
            if (IsDead)
                Died?.Invoke();
        }
    }
}
