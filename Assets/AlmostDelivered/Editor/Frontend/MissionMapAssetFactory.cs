using System;
using System.Collections.Generic;
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
        public const string MeshRoot = "Assets/AlmostDelivered/Art/Meshes/MapThemes/IndustrialDock";
        public const string PrefabRoot = "Assets/AlmostDelivered/Prefabs/MapThemes/IndustrialDock";

        public static void CreateOrUpdateAll()
        {
            var shader = Shader.Find("Standard");
            if (shader == null)
            {
                throw new InvalidOperationException("The Standard shader is required for Industrial Dock assets.");
            }

            Directory.CreateDirectory(MaterialRoot);
            Directory.CreateDirectory(MeshRoot);
            Directory.CreateDirectory(PrefabRoot + "/Terrain");
            Directory.CreateDirectory(PrefabRoot + "/Landscape");
            Directory.CreateDirectory(PrefabRoot + "/Obstacles");
            Directory.CreateDirectory(PrefabRoot + "/Foliage");
            Directory.CreateDirectory(Path.GetDirectoryName(ThemePath));
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            var floor = Material("DockFloor", shader, new Color(.12f, .16f, .19f), .35f);
            var hazard = Material("HazardYellow", shader, new Color(.95f, .63f, .06f), .3f);
            var rust = Material("DockRust", shader, new Color(.42f, .19f, .1f), .25f);
            var darkMetal = Material("DarkMetal", shader, new Color(.055f, .075f, .09f), .42f);
            var cargoBlue = Material("CargoBlue", shader, new Color(.07f, .35f, .45f), .3f);
            var cargoOrange = Material("CargoOrange", shader, new Color(.72f, .25f, .06f), .28f);
            var earth = Material("Earth", shader, new Color(.23f, .18f, .11f), .12f, 0f);
            var moss = Material("Moss", shader, new Color(.19f, .32f, .12f), .08f, 0f);
            var grass = Material("GrassGreen", shader, new Color(.24f, .48f, .13f), .05f, 0f);
            var dryGrass = Material("DryGrass", shader, new Color(.5f, .46f, .19f), .04f, 0f);

            BuildDockPlate(floor, darkMetal);
            BuildHazardStripe(hazard, darkMetal);
            BuildOilSpill(darkMetal);
            BuildDrainGrate(darkMetal, rust);
            BuildCargoCrate(cargoBlue, darkMetal, hazard);
            BuildBarricade(cargoOrange, darkMetal, hazard);
            BuildGenerator(darkMetal, cargoOrange, hazard);
            BuildPipeStack(rust, darkMetal);
            BuildHill("Landscape/GrassHill", SaveMesh("GrassHill", RadialHillMesh("GrassHill", 2.75f, 2.25f, 1.6f)), moss);
            BuildHill("Landscape/RockyMound", SaveMesh("RockyMound", RadialHillMesh("RockyMound", 2.1f, 1.8f, 1.1f)), earth);
            BuildFoliage("Foliage/GrassPatch", SaveMesh("GrassPatch", GrassClusterMesh("GrassPatch", 9, .65f, .18f, .8f, 1801)), grass);
            BuildFoliage("Foliage/TallGrass", SaveMesh("TallGrass", GrassClusterMesh("TallGrass", 11, 1f, .16f, .7f, 2903)), dryGrass);

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
                    Piece("Landscape/GrassHill", new Vector2(5.8f, 4.8f)),
                    Piece("Landscape/RockyMound", new Vector2(4.4f, 3.8f))
                },
                new[]
                {
                    Piece("Obstacles/CargoCrate", new Vector2(2.8f, 2.4f)),
                    Piece("Obstacles/Barricade", new Vector2(3.5f, 1.2f)),
                    Piece("Obstacles/Generator", new Vector2(2.8f, 2.2f)),
                    Piece("Obstacles/PipeStack", new Vector2(3.3f, 2.1f))
                },
                new[]
                {
                    Piece("Foliage/GrassPatch", new Vector2(1.8f, 1.8f)),
                    Piece("Foliage/TallGrass", new Vector2(1.6f, 1.6f))
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

        private static Material Material(string name, Shader shader, Color color, float smoothness, float metallic = .15f)
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
            material.SetFloat("_Metallic", metallic);
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

        private static void BuildHill(string relativePath, Mesh mesh, Material material)
        {
            BuildPrefab(relativePath, root =>
            {
                root.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
                root.gameObject.AddComponent<MeshRenderer>().sharedMaterial = material;
                root.gameObject.AddComponent<MeshCollider>().sharedMesh = mesh;
            });
        }

        private static void BuildFoliage(string relativePath, Mesh mesh, Material material)
        {
            BuildPrefab(relativePath, root =>
            {
                root.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
                root.gameObject.AddComponent<MeshRenderer>().sharedMaterial = material;
            });
        }

        private static Mesh RadialHillMesh(string name, float radiusX, float radiusZ, float height)
        {
            const int rings = 4;
            const int segments = 12;
            var vertices = new List<Vector3>(1 + rings * segments) { new Vector3(0f, height, 0f) };
            var uv = new List<Vector2>(1 + rings * segments) { new Vector2(.5f, .5f) };
            for (var ring = 1; ring <= rings; ring++)
            {
                var radial = ring / (float)rings;
                var elevation = height * (1f - radial);
                for (var segment = 0; segment < segments; segment++)
                {
                    var angle = segment * Mathf.PI * 2f / segments;
                    var cosine = Mathf.Cos(angle);
                    var sine = Mathf.Sin(angle);
                    vertices.Add(new Vector3(cosine * radiusX * radial, elevation, sine * radiusZ * radial));
                    uv.Add(new Vector2(cosine * radial * .5f + .5f, sine * radial * .5f + .5f));
                }
            }

            var triangles = new List<int>(segments * (1 + (rings - 1) * 2) * 3);
            for (var segment = 0; segment < segments; segment++)
            {
                var next = (segment + 1) % segments;
                triangles.Add(0);
                triangles.Add(1 + next);
                triangles.Add(1 + segment);
            }

            for (var ring = 1; ring < rings; ring++)
            {
                var innerStart = 1 + (ring - 1) * segments;
                var outerStart = 1 + ring * segments;
                for (var segment = 0; segment < segments; segment++)
                {
                    var next = (segment + 1) % segments;
                    var innerCurrent = innerStart + segment;
                    var innerNext = innerStart + next;
                    var outerCurrent = outerStart + segment;
                    var outerNext = outerStart + next;
                    triangles.Add(innerCurrent);
                    triangles.Add(innerNext);
                    triangles.Add(outerCurrent);
                    triangles.Add(outerCurrent);
                    triangles.Add(innerNext);
                    triangles.Add(outerNext);
                }
            }

            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh GrassClusterMesh(
            string name,
            int bladeCount,
            float height,
            float width,
            float radius,
            int seed)
        {
            var vertices = new List<Vector3>(bladeCount * 8);
            var uv = new List<Vector2>(bladeCount * 8);
            var triangles = new List<int>(bladeCount * 12);
            var random = new System.Random(seed);
            for (var blade = 0; blade < bladeCount; blade++)
            {
                var angle = (float)random.NextDouble() * Mathf.PI * 2f;
                var distance = Mathf.Sqrt((float)random.NextDouble()) * radius;
                var center = new Vector3(Mathf.Cos(angle) * distance, 0f, Mathf.Sin(angle) * distance);
                var orientation = (float)random.NextDouble() * Mathf.PI * 2f;
                var right = new Vector3(Mathf.Cos(orientation), 0f, Mathf.Sin(orientation)) * (width * .5f);
                var bladeHeight = height * Mathf.Lerp(.72f, 1f, (float)random.NextDouble());
                var frontStart = vertices.Count;
                AddBladeSide(vertices, uv, center, right, bladeHeight, false);
                triangles.Add(frontStart);
                triangles.Add(frontStart + 2);
                triangles.Add(frontStart + 1);
                triangles.Add(frontStart);
                triangles.Add(frontStart + 3);
                triangles.Add(frontStart + 2);

                var backStart = vertices.Count;
                AddBladeSide(vertices, uv, center, right, bladeHeight, true);
                triangles.Add(backStart);
                triangles.Add(backStart + 1);
                triangles.Add(backStart + 2);
                triangles.Add(backStart);
                triangles.Add(backStart + 2);
                triangles.Add(backStart + 3);
            }

            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void AddBladeSide(
            ICollection<Vector3> vertices,
            ICollection<Vector2> uv,
            Vector3 center,
            Vector3 right,
            float height,
            bool reverse)
        {
            var leftBottom = center - right;
            var rightBottom = center + right;
            var rightTop = center + right * .2f + Vector3.up * height;
            var leftTop = center - right * .2f + Vector3.up * height;
            if (reverse)
            {
                vertices.Add(rightBottom);
                vertices.Add(leftBottom);
                vertices.Add(leftTop);
                vertices.Add(rightTop);
            }
            else
            {
                vertices.Add(leftBottom);
                vertices.Add(rightBottom);
                vertices.Add(rightTop);
                vertices.Add(leftTop);
            }

            uv.Add(new Vector2(0f, 0f));
            uv.Add(new Vector2(1f, 0f));
            uv.Add(new Vector2(1f, 1f));
            uv.Add(new Vector2(0f, 1f));
        }

        private static Mesh SaveMesh(string name, Mesh generated)
        {
            var path = $"{MeshRoot}/{name}.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null)
            {
                AssetDatabase.CreateAsset(generated, path);
                return generated;
            }

            EditorUtility.CopySerialized(generated, mesh);
            UnityEngine.Object.DestroyImmediate(generated);
            EditorUtility.SetDirty(mesh);
            return mesh;
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
