using System;
using UnityEngine;

namespace AlmostDelivered.Combat
{
    public readonly struct CombatNoise
    {
        public CombatNoise(Vector3 position, float radius, CombatTeamId sourceTeam)
        {
            Position = position;
            Radius = Mathf.Max(0f, radius);
            SourceTeam = sourceTeam;
        }

        public Vector3 Position { get; }
        public float Radius { get; }
        public CombatTeamId SourceTeam { get; }
    }

    public static class CombatNoiseSystem
    {
        public static event Action<CombatNoise> NoiseEmitted;

        public static bool Emit(Vector3 position, float radius, CombatTeamId sourceTeam)
        {
            if (!IsFinite(position) || !IsFinite(radius) || radius <= 0f)
            {
                return false;
            }

            NoiseEmitted?.Invoke(new CombatNoise(position, radius, sourceTeam));
            return true;
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private static bool IsFinite(Vector3 value) =>
            IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
    }
}
