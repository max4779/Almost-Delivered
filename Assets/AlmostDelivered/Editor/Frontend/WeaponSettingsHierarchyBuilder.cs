using System.IO;
using AlmostDelivered.Frontend;
using UnityEditor;
using UnityEngine;

namespace AlmostDelivered.Editor.Frontend
{
    internal static class WeaponSettingsHierarchyBuilder
    {
        private const string PrefabPath =
            "Assets/AlmostDelivered/Prefabs/Generated/Characters/Player/Weapon Settings.prefab";

        public static void Attach(Transform player)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
                var root = new GameObject("Weapon Settings");
                Add<AutomaticRifleStats>(root.transform, "Automatic Rifle");
                Add<RevolverStats>(root.transform, "Revolver");
                Add<MeleeWeaponStats>(root.transform, "Melee Weapon");
                prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Object.DestroyImmediate(root);
            }

            PrefabUtility.InstantiatePrefab(prefab, player);
        }

        private static void Add<T>(Transform parent, string name) where T : Component
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent, false);
            child.AddComponent<T>();
        }
    }
}
