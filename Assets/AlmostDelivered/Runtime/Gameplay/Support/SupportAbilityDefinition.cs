using System;
using UnityEngine;

namespace AlmostDelivered.Gameplay.Support
{
    public enum SupportEffectKind { AutoTurret, PersonalShield }

    [Serializable]
    public sealed class SupportAbilityDefinition
    {
        public string id;
        public string displayName;
        public SupportEffectKind effect;
        [Min(1)] public int cost = 10;
        [Min(0f)] public float cooldown = 15f;
        [Min(.1f)] public float duration = 20f;
        [Min(.1f)] public float power = 30f;
        public KeyCode key = KeyCode.Alpha1;

        public static SupportAbilityDefinition[] CreateDefaults() => new[]
        {
            new SupportAbilityDefinition { id = "support.turret", displayName = "자동포탑",
                effect = SupportEffectKind.AutoTurret, cost = 15, cooldown = 20f,
                duration = 25f, power = 9f, key = KeyCode.Alpha1 },
            new SupportAbilityDefinition { id = "support.shield", displayName = "개인 실드",
                effect = SupportEffectKind.PersonalShield, cost = 10, cooldown = 18f,
                duration = 12f, power = 45f, key = KeyCode.Alpha2 }
        };
    }
}
