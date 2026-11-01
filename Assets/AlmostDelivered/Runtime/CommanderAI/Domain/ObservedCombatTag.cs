using System;

namespace AlmostDelivered.CommanderAI.Domain
{
    [Flags]
    public enum ObservedCombatTag
    {
        None = 0,
        RangedWeapon = 1 << 0,
        Shield = 1 << 1,
        Healing = 1 << 2,
        Mobility = 1 << 3
    }
}
