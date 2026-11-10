using AlmostDelivered.Frontend;
using UnityEngine;

namespace AlmostDelivered.Editor.Frontend
{
    internal static class WeaponSettingsHierarchyBuilder
    {
        public static void Attach(Transform player)
        {
            var root = new GameObject("Weapon Settings").transform;
            root.SetParent(player, false);
            Add<AutomaticRifleStats>(root, "Automatic Rifle");
            Add<RevolverStats>(root, "Revolver");
            Add<MeleeWeaponStats>(root, "Melee Weapon");
        }

        private static void Add<T>(Transform parent, string name) where T : Component
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent, false);
            child.AddComponent<T>();
        }
    }
}
