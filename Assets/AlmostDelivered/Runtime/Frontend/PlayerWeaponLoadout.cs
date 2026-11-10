using AlmostDelivered.Combat;
using UnityEngine;

namespace AlmostDelivered.Frontend
{
    public enum PlayerWeaponType
    {
        Ranged = 0,
        AutomaticRifle = 0,
        Melee = 1,
        Revolver = 2
    }

    public static class PlayerWeaponLoadout
    {
        public static PlayerWeaponType Selected { get; private set; } = PlayerWeaponType.Ranged;

        public static void Select(PlayerWeaponType weapon)
        {
            Selected = weapon;
        }

        public static bool IsFirearm(PlayerWeaponType weapon) => weapon != PlayerWeaponType.Melee;

        public static string DisplayName(PlayerWeaponType weapon)
        {
            if (weapon == PlayerWeaponType.Melee) return "근접 무기";
            return weapon == PlayerWeaponType.Revolver ? "리볼버" : "자동소총";
        }

        public static void ConfigureFirearm(RangedAttackController attack, PlayerWeaponType weapon)
        {
            if (attack == null || !IsFirearm(weapon)) return;
            if (weapon == PlayerWeaponType.Revolver)
                attack.ConfigureMagazine(6, 2.4f, 55f, .58f);
            else
                attack.ConfigureMagazine(30, 1.8f, 12f, .12f);
        }

        public static void ApplyVisual(GameObject actor, PlayerWeaponType weapon)
        {
            if (actor == null) return;
            foreach (var part in actor.GetComponentsInChildren<Transform>(true))
            {
                if (part.name == "RangedWeapon") part.gameObject.SetActive(weapon == PlayerWeaponType.AutomaticRifle);
                if (part.name == "RevolverWeapon") part.gameObject.SetActive(weapon == PlayerWeaponType.Revolver);
                if (part.name == "MeleeWeapon") part.gameObject.SetActive(weapon == PlayerWeaponType.Melee);
            }
        }
    }
}
