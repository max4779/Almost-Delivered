using System;
using System.IO;
using AlmostDelivered.MapGeneration;
using UnityEditor;
using UnityEngine;

namespace AlmostDelivered.Editor.Frontend
{
    public static class MissionMapAssetFactory
    {
        public const string ThemePath = "Assets/AlmostDelivered/Configs/MapThemes/IndustrialDock.asset";
        public const string MaterialRoot = "Assets/AlmostDelivered/Art/Materials/MapThemes/IndustrialDock";
        public const string PrefabRoot = "Assets/AlmostDelivered/Prefabs/MapThemes/IndustrialDock";

        public static void CreateOrUpdateAll()
        {
            var shader = Shader.Find("Standard");
            if (shader == null)
            {
                throw new InvalidOperationException("The Standard shader is required for Industrial Dock assets.");
            }

            Directory.CreateDirectory(MaterialRoot);
            Directory.CreateDirectory(PrefabRoot + "/Terrain");
            Directory.CreateDirectory(PrefabRoot + "/Obstacles");
            Directory.CreateDirectory(Path.GetDirectoryName(ThemePath));
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            var floor = Material("DockFloor", shader, new Color(.12f, .16f, .19f), .35f);
            var hazard = Material("HazardYellow", shader, new Color(.95f, .63f, .06f), .3f);
            var rust = Material("DockRust", shader, new Color(.42f, .19f, .1f), .25f);
            var darkMetal = Material("DarkMetal", shader, new Color(.055f, .075f, .09f), .42f);
            var cargoBlue = Material("CargoBlue", shader, new Color(.07f, .35f, .45f), .3f);
            var cargoOrange = Material("CargoOrange", shader, new Color(.72f, .25f, .06f), .28f);

            BuildDockPlate(floor, darkMetal);
            BuildHazardStripe(hazard, darkMetal);
            BuildOilSpill(darkMetal);
            BuildDrainGrate(darkMetal, rust);
            BuildCargoCrate(cargoBlue, darkMetal, hazard);
            BuildBarricade(cargoOrange, darkMetal, hazard);
            BuildGenerator(darkMetal, cargoOrange, hazard);
            BuildPipeStack(rust, darkMetal);

            var theme = AssetDatabase.LoadAssetAtPath<MissionMapTheme>(ThemePath);
            if (theme == null)
            {
                theme = ScriptableObject.CreateInstance<MissionMapTheme>();
                AssetDatabase.CreateAsset(theme, ThemePath);
            }

            theme.Configure(
                new[]
                {
                    Piece("Terrain/DockPlate", new Vector2(3.6f, 3.6f)),
                    Piece("Terrain/HazardStripe", new Vector2(3.2f, 2.6f)),
                    Piece("Terrain/OilSpill", new Vector2(2.4f, 1.8f)),
                    Piece("Terrain/DrainGrate", new Vector2(2.2f, 2.2f))
                },
                new[]
                {
                    Piece("Obstacles/CargoCrate", new Vector2(2.8f, 2.4f)),
                    Piece("Obstacles/Barricade", new Vector2(3.5f, 1.2f)),
                    Piece("Obstacles/Generator", new Vector2(2.8f, 2.2f)),
                    Piece("Obstacles/PipeStack", new Vector2(3.3f, 2.1f))
                });
            EditorUtility.SetDirty(theme);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        private static MissionMapPiece Piece(string relativePath, Vector2 footprint)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabRoot}/{relativePath}.prefab");
            return new MissionMapPiece(prefab, footprint);
        }

        private static Material Material(string name, Shader shader, Color color, float smoothness)
        {
            var path = $"{MaterialRoot}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }

            material.shader = shader;
            material.color = color;
            material.SetFloat("_Metallic", .15f);
            material.SetFloat("_Glossiness", smoothness);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void BuildDockPlate(Material floor, Material trim)
        {
            BuildPrefab("Terrain/DockPlate", root =>
            {
                Part(root, "Plate", PrimitiveType.Cube, new Vector3(0f, .015f, 0f), new Vector3(3.5f, .03f, 3.5f), floor, false);
                Part(root, "Edge North", PrimitiveType.Cube, new Vector3(0f, .035f, 1.68f), new Vector3(3.4f, .025f, .08f), trim, false);
                Part(root, "Edge South", PrimitiveType.Cube, new Vector3(0f, .035f, -1.68f), new Vector3(3.4f, .025f, .08f), trim, false);
            });
        }

        private static void BuildHazardStripe(Material hazard, Material baseMaterial)
        {
            BuildPrefab("Terrain/HazardStripe", root =>
            {
                Part(root, "Stripe Base", PrimitiveType.Cube, new Vector3(0f, .018f, 0f), new Vector3(3.1f, .03f, 2.5f), baseMaterial, false);
                for (var index = -2; index <= 2; index++)
                {
                    Part(root, $"Hazard {index + 3}", PrimitiveType.Cube, new Vector3(index * .62f, .04f, 0f), new Vector3(.28f, .025f, 2.35f), hazard, false, Quaternion.Euler(0f, -22f, 0f));
                }
            });
        }

        private static void BuildOilSpill(Material material)
        {
            BuildPrefab("Terrain/OilSpill", root =>
            {
                Part(root, "Spill", PrimitiveType.Cylinder, new Vector3(0f, .015f, 0f), new Vector3(1.2f, .015f, .9f), material, false);
                Part(root, "Drop", PrimitiveType.Sphere, new Vector3(.9f, .03f, -.35f), new Vector3(.35f, .025f, .24f), material, false);
            });
        }

        private static void BuildDrainGrate(Material grate, Material rust)
        {
            BuildPrefab("Terrain/DrainGrate", root =>
            {
                Part(root, "Recess", PrimitiveType.Cube, new Vector3(0f, .012f, 0f), new Vector3(2.1f, .025f, 2.1f), grate, false);
                for (var index = -2; index <= 2; index++)
                {
                    Part(root, $"Bar {index + 3}", PrimitiveType.Cube, new Vector3(index * .38f, .035f, 0f), new Vector3(.1f, .025f, 1.9f), rust, false);
                }
            });
        }

        private static void BuildCargoCrate(Material body, Material trim, Material hazard)
        {
            BuildPrefab("Obstacles/CargoCrate", root =>
            {
                Part(root, "Crate Body", PrimitiveType.Cube, new Vector3(0f, .75f, 0f), new Vector3(2.6f, 1.5f, 2.2f), body, true);
                Part(root, "Top Frame", PrimitiveType.Cube, new Vector3(0f, 1.52f, 0f), new Vector3(2.72f, .12f, 2.3f), trim, false);
                Part(root, "Hazard Badge", PrimitiveType.Cube, new Vector3(0f, .82f, 1.115f), new Vector3(.7f, .55f, .03f), hazard, false);
            });
        }

        private static void BuildBarricade(Material body, Material trim, Material hazard)
        {
            BuildPrefab("Obstacles/Barricade", root =>
            {
                Part(root, "Barrier", PrimitiveType.Cube, new Vector3(0f, .65f, 0f), new Vector3(3.3f, 1f, .5f), body, true);
                Part(root, "Top Rail", PrimitiveType.Cube, new Vector3(0f, 1.18f, 0f), new Vector3(3.45f, .12f, .58f), trim, false);
                Part(root, "Hazard Left", PrimitiveType.Cube, new Vector3(-.8f, .68f, -.27f), new Vector3(.4f, .2f, .025f), hazard, false);
                Part(root, "Hazard Right", PrimitiveType.Cube, new Vector3(.8f, .68f, -.27f), new Vector3(.4f, .2f, .025f), hazard, false);
            });
        }

        private static void BuildGenerator(Material body, Material panel, Material hazard)
        {
            BuildPrefab("Obstacles/Generator", root =>
            {
                Part(root, "Generator Body", PrimitiveType.Cube, new Vector3(0f, .85f, 0f), new Vector3(2.5f, 1.7f, 1.9f), body, true);
                Part(root, "Control Panel", PrimitiveType.Cube, new Vector3(0f, 1.05f, -.97f), new Vector3(1.35f, .65f, .06f), panel, false);
                Part(root, "Status", PrimitiveType.Sphere, new Vector3(.38f, 1.12f, -1.02f), Vector3.one * .16f, hazard, false);
                Part(root, "Exhaust", PrimitiveType.Cylinder, new Vector3(.75f, 2f, .2f), new Vector3(.22f, .55f, .22f), body, false);
            });
        }

        private static void BuildPipeStack(Material pipe, Material band)
        {
            BuildPrefab("Obstacles/PipeStack", root =>
            {
                for (var index = 0; index < 3; index++)
                {
                    Part(root, $"Pipe {index + 1}", PrimitiveType.Cylinder, new Vector3(0f, .45f + index * .48f, (index - 1) * .48f), new Vector3(.42f, 1.5f, .42f), pipe, true, Quaternion.Euler(0f, 0f, 90f));
                }

                Part(root, "Band Left", PrimitiveType.Cube, new Vector3(-.9f, .72f, 0f), new Vector3(.12f, 1.4f, 1.6f), band, false);
                Part(root, "Band Right", PrimitiveType.Cube, new Vector3(.9f, .72f, 0f), new Vector3(.12f, 1.4f, 1.6f), band, false);
            });
        }

        private static void BuildPrefab(string relativePath, Action<Transform> build)
        {
            var name = Path.GetFileName(relativePath);
            var root = new GameObject(name);
            try
            {
                build(root.transform);
                PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabRoot}/{relativePath}.prefab");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void Part(
            Transform parent,
            string name,
            PrimitiveType primitiveType,
            Vector3 localPosition,
            Vector3 localScale,
            Material material,
            bool keepCollider,
            Quaternion localRotation = default)
        {
            var part = GameObject.CreatePrimitive(primitiveType);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = localScale;
            part.transform.localRotation = localRotation == default ? Quaternion.identity : localRotation;
            part.GetComponent<Renderer>().sharedMaterial = material;
            if (!keepCollider)
            {
                UnityEngine.Object.DestroyImmediate(part.GetComponent<Collider>());
            }
        }
    }
}
