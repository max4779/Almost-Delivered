using System;
using UnityEngine;

namespace AlmostDelivered.Combat
{
    public sealed class StunState : MonoBehaviour
    {
        public bool IsStunned { get; private set; }
        public float StunnedUntil { get; private set; }
        public event Action<bool> StunChanged;

        public void Apply(float duration, float now)
        {
            ValidateTime(duration, nameof(duration));
            ValidateTime(now, nameof(now));
            var until = now + duration;
            if (float.IsInfinity(until))
                throw new ArgumentOutOfRangeException(nameof(duration));

            Tick(now);
            if (duration == 0f)
                return;

            StunnedUntil = Mathf.Max(StunnedUntil, until);
            if (!IsStunned)
            {
                IsStunned = true;
                StunChanged?.Invoke(true);
            }
        }

        public void Tick(float now)
        {
            ValidateTime(now, nameof(now));
            if (IsStunned && now >= StunnedUntil)
            {
                IsStunned = false;
                StunChanged?.Invoke(false);
            }
        }

        private void Update()
        {
            Tick(Time.unscaledTime);
        }

        private static void ValidateTime(float value, string parameterName)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f)
                throw new ArgumentOutOfRangeException(parameterName);
        }
    }
}
