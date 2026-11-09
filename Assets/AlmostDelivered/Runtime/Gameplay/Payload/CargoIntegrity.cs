using System;
using UnityEngine;

namespace AlmostDelivered.Gameplay.Payload
{
    public sealed class CargoIntegrity : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float maximumHealth = 200f;
        [SerializeField, Range(.01f, 1f)] private float restartThreshold = .25f;

        public float CurrentHealth { get; private set; }
        public float MaximumHealth => maximumHealth;
        public bool IsBroken { get; private set; }
        public event Action<float, float, bool> Changed;

        private void Awake()
        {
            CurrentHealth = maximumHealth;
        }

        public void ApplyDamage(float amount)
        {
            if (!IsValid(amount))
                throw new ArgumentOutOfRangeException(nameof(amount));
            if (amount <= 0f || CurrentHealth <= 0f)
                return;

            CurrentHealth = Mathf.Max(0f, CurrentHealth - amount);
            if (CurrentHealth <= 0f)
                IsBroken = true;
            Changed?.Invoke(CurrentHealth, maximumHealth, IsBroken);
        }

        public void Repair(float amount)
        {
            if (!IsValid(amount))
                throw new ArgumentOutOfRangeException(nameof(amount));
            if (amount <= 0f || CurrentHealth >= maximumHealth)
                return;

            CurrentHealth = Mathf.Min(maximumHealth, CurrentHealth + amount);
            if (IsBroken && CurrentHealth >= maximumHealth * restartThreshold)
                IsBroken = false;
            Changed?.Invoke(CurrentHealth, maximumHealth, IsBroken);
        }

        public void ApplySnapshot(float current, float maximum, bool broken)
        {
            if (!IsValid(current) || !IsValid(maximum) || maximum <= 0f)
                throw new ArgumentOutOfRangeException(nameof(maximum));

            maximumHealth = maximum;
            CurrentHealth = Mathf.Clamp(current, 0f, maximum);
            IsBroken = broken;
            Changed?.Invoke(CurrentHealth, maximumHealth, IsBroken);
        }

        private static bool IsValid(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;
    }
}
