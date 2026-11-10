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
            var muzzle = FindMuzzle(attack.gameObject, weapon);
            if (muzzle != null) attack.SetMuzzle(muzzle);
            if (weapon == PlayerWeaponType.Revolver)
            {
                var stats = attack.GetComponentInChildren<RevolverStats>(true);
                attack.ConfigureMagazine(stats != null ? stats.MagazineSize : 6,
                    stats != null ? stats.ReloadSeconds : 2.4f,
                    stats != null ? stats.Damage : RevolverStats.DefaultDamage,
                    stats != null ? stats.FireCooldown : .58f,
                    stats == null || stats.PierceEnemies);
                attack.SetRangeAndNoise(stats != null ? stats.Range : RevolverStats.DefaultRange,
                    stats != null ? stats.NoiseRadius : RevolverStats.DefaultNoiseRadius);
            }
            else
            {
                var stats = attack.GetComponentInChildren<AutomaticRifleStats>(true);
                attack.ConfigureMagazine(stats != null ? stats.MagazineSize : 20,
                    stats != null ? stats.ReloadSeconds : 1.8f,
                    stats != null ? stats.Damage : AutomaticRifleStats.DefaultDamage,
                    stats != null ? stats.FireCooldown : .12f);
                attack.SetRangeAndNoise(stats != null ? stats.Range : AutomaticRifleStats.DefaultRange,
                    stats != null ? stats.NoiseRadius : AutomaticRifleStats.DefaultNoiseRadius);
            }
        }

        public static void ConfigureMelee(MeleeAttackController attack)
        {
            if (attack == null) return;
            var stats = attack.GetComponentInChildren<MeleeWeaponStats>(true);
            if (stats == null) return;
            attack.Configure(stats.Damage, stats.Reach, stats.ArcDegrees,
                stats.AttackCooldown, stats.NoiseRadius);
        }

        public static Transform FindMuzzle(GameObject actor, PlayerWeaponType weapon)
        {
            if (actor == null || !IsFirearm(weapon)) return null;
            var socketName = weapon == PlayerWeaponType.Revolver ? "Revolver Muzzle" : "Rifle Muzzle";
            foreach (var part in actor.GetComponentsInChildren<Transform>(true))
                if (part.name == socketName) return part;
            return null;
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
