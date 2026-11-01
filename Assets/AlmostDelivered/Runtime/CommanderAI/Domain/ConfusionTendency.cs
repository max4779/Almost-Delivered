using System;

namespace AlmostDelivered.CommanderAI.Domain
{
    public enum ConfusionTendency { NearestVisibleTarget, HoldArea, RetreatWhenWounded, SearchLastKnownPosition }

    public static class ConfusionTendencies
    {
        public static ConfusionTendency Choose(MonsterRole role, int memberId, int seed = 0)
        {
            if (memberId <= 0) throw new ArgumentOutOfRangeException(nameof(memberId));
            int preferred;
            switch (role)
            {
                case MonsterRole.Shield: preferred = 1; break;
                case MonsterRole.Ranged: preferred = 2; break;
                case MonsterRole.Charger: preferred = 0; break;
                case MonsterRole.Support: preferred = 3; break;
                default: throw new ArgumentOutOfRangeException(nameof(role));
            }
            return (ConfusionTendency)((preferred + unchecked((uint)(memberId - 1) + (uint)seed)) % 4);
        }
    }
}
