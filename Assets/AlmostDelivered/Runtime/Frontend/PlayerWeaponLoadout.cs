using UnityEngine;

namespace AlmostDelivered.Frontend
{
    public enum PlayerWeaponType
    {
        Ranged,
        Melee
    }

    public static class PlayerWeaponLoadout
    {
        public static PlayerWeaponType Selected { get; private set; } = PlayerWeaponType.Ranged;

        public static void Select(PlayerWeaponType weapon)
        {
            Selected = weapon;
        }

        public static void ApplyVisual(GameObject actor, PlayerWeaponType weapon)
        {
            if (actor == null) return;
            foreach (var part in actor.GetComponentsInChildren<Transform>(true))
            {
                if (part.name == "RangedWeapon") part.gameObject.SetActive(weapon == PlayerWeaponType.Ranged);
                if (part.name == "MeleeWeapon") part.gameObject.SetActive(weapon == PlayerWeaponType.Melee);
            }
        }
    }
}
