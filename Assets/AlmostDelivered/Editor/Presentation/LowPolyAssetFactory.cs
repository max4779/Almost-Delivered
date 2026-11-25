using System;
using System.IO;
using AlmostDelivered.Presentation;
using UnityEditor;
using UnityEngine;

namespace AlmostDelivered.Presentation.Editor
{
    public static class LowPolyAssetFactory
    {
        const string Materials = "Assets/AlmostDelivered/Art/Materials/Generated";
        const string PayloadProwPath = "Assets/AlmostDelivered/Art/Meshes/Generated/PayloadProw.asset";
        const string Prefabs = "Assets/AlmostDelivered/Prefabs/Generated";
        public static string GetPrefabPath(VisualArchetype type) => Prefabs + (type == VisualArchetype.Payload || type == VisualArchetype.ObjectiveBeacon ? "/Objectives/" : "/Characters/") + type + "/VisualRoot.prefab";
        public static GameObject LoadPrefab(VisualArchetype type) => AssetDatabase.LoadAssetAtPath<GameObject>(GetPrefabPath(type));

        [MenuItem("Almost Delivered/Art/Refresh payload visual")]
        public static void RefreshPayloadOnly()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(PayloadProwPath));
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var color = AssetDatabase.LoadAssetAtPath<Material>(Materials + "/Payload.mat");
            var rust = AssetDatabase.LoadAssetAtPath<Material>(Materials + "/EnemyRust.mat");
            var gold = AssetDatabase.LoadAssetAtPath<Material>(Materials + "/Gold.mat");
            var glow = AssetDatabase.LoadAssetAtPath<Material>(Materials + "/Rune.mat");
            if (color == null || rust == null || gold == null || glow == null)
                throw new InvalidOperationException("Payload materials must exist before refreshing its visual.");
            AssetDatabase.StartAssetEditing();
            try {
                Build(VisualArchetype.Payload, color, rust, gold, glow, CreateOrUpdatePayloadProw());
                AssetDatabase.SaveAssets();
            } finally {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            }
        }

        public static void CreateOrUpdateAll(Func<string, Shader> shaderResolver = null)
        {
            var resolve = shaderResolver ?? Shader.Find;
            var lit = resolve("Standard");
            var unlit = resolve("Unlit/Color");
            if (lit == null || unlit == null) throw new InvalidOperationException("Both Standard and Unlit/Color shaders are required.");
            Directory.CreateDirectory(Materials);
            Directory.CreateDirectory(Path.GetDirectoryName(PayloadProwPath));
            Directory.CreateDirectory(Prefabs + "/Characters");
            Directory.CreateDirectory(Prefabs + "/Objectives");
            foreach (VisualArchetype type in Enum.GetValues(typeof(VisualArchetype)))
                Directory.CreateDirectory(Path.GetDirectoryName(GetPrefabPath(type)));
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            AssetDatabase.StartAssetEditing();
            try {
                var payloadProw = CreateOrUpdatePayloadProw();
                var palette = new[] { Mat("Player", lit, new Color(.05f,.55f,.62f)), Mat("Dummy",lit,Color.gray), Mat("Shield",lit,new Color(.22f,.48f,.23f)), Mat("Ranged",lit,new Color(.95f,.43f,.12f)), Mat("Charger",lit,new Color(.68f,.12f,.1f)), Mat("Support",lit,new Color(.56f,.78f,.18f)), Mat("Commander",lit,new Color(.38f,.2f,.52f)), Mat("Payload",lit,new Color(.18f,.28f,.38f)), Mat("Beacon",lit,new Color(.04f,.52f,.49f)) };
                var rust = Mat("EnemyRust",lit,new Color(.45f,.23f,.14f));
                var gold = Mat("Gold",lit,new Color(.94f,.69f,.2f));
                var glow = Mat("Rune",unlit,new Color(.15f,.9f,.8f));
                foreach (VisualArchetype type in Enum.GetValues(typeof(VisualArchetype))) Build(type, palette[(int)type], rust, gold, glow, payloadProw);
                AssetDatabase.SaveAssets();
            } finally {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            }
        }
        static Material Mat(string name, Shader shader, Color color)
        {
            var path = Materials + "/" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) { mat = new Material(shader); AssetDatabase.CreateAsset(mat,path); }
            mat.shader = shader; mat.color = color; EditorUtility.SetDirty(mat); return mat;
        }
        static Mesh CreateOrUpdatePayloadProw()
        {
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(PayloadProwPath);
            if (mesh == null) {
                mesh = new Mesh { name = "PayloadProw" };
                AssetDatabase.CreateAsset(mesh, PayloadProwPath);
            }
            var outline = new[] {
                new Vector2(-.95f, .72f), new Vector2(.95f, .72f),
                new Vector2(.48f, 1.25f), new Vector2(.15f, 1.65f),
                new Vector2(0f, 1.73f), new Vector2(-.15f, 1.65f),
                new Vector2(-.48f, 1.25f)
            };
            var count = outline.Length;
            var vertices = new Vector3[count * 2];
            for (var i = 0; i < count; i++) {
                vertices[i] = new Vector3(outline[i].x, 1.42f, outline[i].y);
                vertices[i + count] = new Vector3(outline[i].x, .93f, outline[i].y);
            }
            var triangles = new System.Collections.Generic.List<int>();
            for (var i = 1; i < count - 1; i++) {
                triangles.Add(0); triangles.Add(i + 1); triangles.Add(i);
                triangles.Add(count); triangles.Add(count + i); triangles.Add(count + i + 1);
            }
            for (var i = 0; i < count; i++) {
                var next = (i + 1) % count;
                triangles.Add(i); triangles.Add(next); triangles.Add(next + count);
                triangles.Add(i); triangles.Add(next + count); triangles.Add(i + count);
            }
            mesh.Clear();
            mesh.vertices = vertices;
            mesh.triangles = triangles.ToArray();
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            return mesh;
        }
        static void Build(VisualArchetype type, Material color, Material rust, Material gold, Material glow, Mesh payloadProw)
        {
            var root = new GameObject("VisualRoot");
            try {
                var body = Child(root.transform,"BodyPivot");
                Transform left = null, right = null, leftLeg = null, rightLeg = null;
                if (type == VisualArchetype.Payload) {
                    Part(root.transform,"HoverShadow",PrimitiveType.Cylinder,new Vector3(0,.035f,0),new Vector3(1.35f,.015f,1.65f),rust);
                    Part(body,"CargoHull",PrimitiveType.Cube,new Vector3(0,1.1f,-.18f),new Vector3(1.9f,.55f,2.1f),color);
                    MeshPart(body,"ProwArmor",payloadProw,color);
                    Part(body,"CargoCanister",PrimitiveType.Cube,new Vector3(0,1.55f,-.2f),new Vector3(1.5f,.42f,1.45f),color);
                    Part(body,"ProwRailLeft",PrimitiveType.Cube,new Vector3(-.38f,1.46f,1.13f),new Vector3(.08f,.05f,.65f),gold,Quaternion.Euler(0f,-35f,0f));
                    Part(body,"ProwRailRight",PrimitiveType.Cube,new Vector3(.38f,1.46f,1.13f),new Vector3(.08f,.05f,.65f),gold,Quaternion.Euler(0f,35f,0f));
                    Part(body,"ForwardSensor",PrimitiveType.Cube,new Vector3(0,1.48f,1.43f),new Vector3(.62f,.08f,.16f),glow);
                    for (var exhaust = -1; exhaust <= 1; exhaust++)
                        Part(body,"RearExhaust"+exhaust,PrimitiveType.Cube,new Vector3(exhaust*.44f,1.16f,-1.28f),new Vector3(.29f,.13f,.1f),rust);
                    Part(body,"EnergyCore",PrimitiveType.Sphere,new Vector3(0,1.83f,-.1f),new Vector3(.58f,.16f,.58f),glow);
                    foreach (var side in new[]{-1f,1f}) foreach (var end in new[]{-1f,1f}) {
                        var suffix=(end<0f?"Rear":"Front")+(side<0f?"Left":"Right");
                        Part(body,"Thruster"+suffix,PrimitiveType.Cylinder,new Vector3(side*.78f,.76f,end*.88f),new Vector3(.43f,.19f,.43f),color);
                        Part(body,"Glow"+suffix,PrimitiveType.Cylinder,new Vector3(side*.78f,.54f,end*.88f),new Vector3(.42f,.035f,.42f),glow);
                    }
                } else if (type == VisualArchetype.ObjectiveBeacon) {
                    Part(body,"Beacon",PrimitiveType.Cylinder,new Vector3(0,.8f,0),new Vector3(.55f,.8f,.55f),color);
                    Part(body,"RuneRing",PrimitiveType.Cylinder,new Vector3(0,.1f,0),new Vector3(1.5f,.06f,1.5f),glow);
                } else {
                    Part(body,"Body",PrimitiveType.Cube,new Vector3(0,.95f,0),new Vector3(.7f,.9f,.45f),color);
                    Part(body,"Head",PrimitiveType.Sphere,new Vector3(0,1.65f,0),Vector3.one*.52f,color);
                    leftLeg=Child(body,"LeftLegPivot"); rightLeg=Child(body,"RightLegPivot");
                    leftLeg.localPosition=new Vector3(-.2f,.58f,0); rightLeg.localPosition=new Vector3(.2f,.58f,0);
                    Part(leftLeg,"LegLeft",PrimitiveType.Cube,new Vector3(0,-.28f,0),new Vector3(.23f,.6f,.3f),rust);
                    Part(rightLeg,"LegRight",PrimitiveType.Cube,new Vector3(0,-.28f,0),new Vector3(.23f,.6f,.3f),rust);
                    left=Child(body,"LeftArmPivot"); right=Child(body,"RightArmPivot");
                    left.localPosition = new Vector3(-.48f,1.3f,0); right.localPosition = new Vector3(.48f,1.3f,0);
                    Part(left,"LeftArm",PrimitiveType.Cube,new Vector3(0,-.3f,0),new Vector3(.22f,.7f,.24f),color);
                    Part(right,"RightArm",PrimitiveType.Cube,new Vector3(0,-.3f,0),new Vector3(.22f,.7f,.24f),color);
                    if (type == VisualArchetype.Player) {
                        var weapon=Child(right,"Weapon");
                        var rifle=Child(weapon,"RangedWeapon");
                        rifle.localPosition = new Vector3(0f,.12f,0f);
                        Part(rifle,"Rifle Receiver",PrimitiveType.Cube,new Vector3(.15f,-.43f,.31f),new Vector3(.18f,.2f,.55f),rust);
                        Part(rifle,"Rifle Barrel",PrimitiveType.Cylinder,new Vector3(.15f,-.39f,.59f),new Vector3(.06f,.16f,.06f),gold,Quaternion.Euler(90f,0f,0f));
                        Child(rifle,"Rifle Muzzle").localPosition = new Vector3(.15f,-.39f,.69f);
                        Part(rifle,"Rifle Stock",PrimitiveType.Cube,new Vector3(.15f,-.48f,0f),new Vector3(.14f,.17f,.18f),rust);
                        Part(rifle,"Rifle Magazine",PrimitiveType.Cube,new Vector3(.15f,-.58f,.29f),new Vector3(.13f,.2f,.15f),gold);
                        Part(rifle,"Rifle Sight",PrimitiveType.Cube,new Vector3(.15f,-.28f,.36f),new Vector3(.08f,.08f,.19f),glow);
                        var revolver=Child(weapon,"RevolverWeapon"); revolver.gameObject.SetActive(false);
                        Part(revolver,"Revolver Frame",PrimitiveType.Cube,new Vector3(.14f,-.44f,.27f),new Vector3(.2f,.17f,.4f),rust);
                        Part(revolver,"Revolver Cylinder",PrimitiveType.Cylinder,new Vector3(.14f,-.43f,.32f),new Vector3(.17f,.13f,.17f),gold,Quaternion.Euler(90f,0f,0f));
                        Part(revolver,"Revolver Barrel",PrimitiveType.Cylinder,new Vector3(.14f,-.42f,.62f),new Vector3(.08f,.22f,.08f),gold,Quaternion.Euler(90f,0f,0f));
                        Child(revolver,"Revolver Muzzle").localPosition = new Vector3(.14f,-.42f,.76f);
                        Part(revolver,"Revolver Grip",PrimitiveType.Cube,new Vector3(.14f,-.62f,.12f),new Vector3(.15f,.3f,.14f),rust);
                        var blade=Child(right,"MeleeWeapon"); blade.gameObject.SetActive(false);
                        Part(blade,"Grip",PrimitiveType.Cube,new Vector3(.14f,-.4f,.16f),new Vector3(.16f,.16f,.35f),rust);
                        Part(blade,"Blade",PrimitiveType.Cube,new Vector3(.14f,-.4f,.72f),new Vector3(.2f,.08f,.9f),glow);
                    } else if (type != VisualArchetype.Ranged) {
                        Part(right,"Weapon",PrimitiveType.Cube,new Vector3(.14f,-.45f,.32f),new Vector3(.12f,.12f,.85f),gold);
                    }
                    switch(type) {
                        case VisualArchetype.Shield:
                            var shieldGrip = Child(left,"Shield Grip");
                            shieldGrip.localPosition = new Vector3(.38f,-.32f,.45f);
                            Part(shieldGrip,"Shield Handle",PrimitiveType.Cube,new Vector3(0f,0f,-.15f),new Vector3(.16f,.55f,.26f),rust);
                            Part(shieldGrip,"Shield",PrimitiveType.Cube,new Vector3(0f,0f,.07f),new Vector3(1.7f,1.28f,.18f),rust);
                            Part(shieldGrip,"Shield Upper Rim",PrimitiveType.Cube,new Vector3(0f,.57f,.18f),new Vector3(1.74f,.13f,.09f),gold);
                            Part(shieldGrip,"Shield Lower Rim",PrimitiveType.Cube,new Vector3(0f,-.57f,.18f),new Vector3(1.74f,.13f,.09f),gold);
                            Part(shieldGrip,"Shield Left Rim",PrimitiveType.Cube,new Vector3(-.81f,0f,.18f),new Vector3(.12f,1.22f,.09f),gold);
                            Part(shieldGrip,"Shield Right Rim",PrimitiveType.Cube,new Vector3(.81f,0f,.18f),new Vector3(.12f,1.22f,.09f),gold);
                            Part(shieldGrip,"Shield Core",PrimitiveType.Cube,new Vector3(0f,.05f,.2f),new Vector3(.3f,.38f,.06f),glow);
                            Part(shieldGrip,"Shield Sight",PrimitiveType.Cube,new Vector3(0f,.39f,.2f),new Vector3(.6f,.08f,.06f),gold);
                            break;
                        case VisualArchetype.Ranged:
                            var carbine = Child(right,"Weapon");
                            Part(carbine,"RangedWeapon",PrimitiveType.Cube,new Vector3(.14f,-.36f,.4f),new Vector3(.26f,.18f,.66f),rust);
                            Part(carbine,"Carbine Stock",PrimitiveType.Cube,new Vector3(.14f,-.39f,-.06f),new Vector3(.22f,.17f,.34f),rust);
                            Part(carbine,"Carbine Barrel",PrimitiveType.Cylinder,new Vector3(.14f,-.36f,.91f),new Vector3(.09f,.41f,.09f),gold,Quaternion.Euler(90f,0f,0f));
                            Part(carbine,"Carbine Emitter",PrimitiveType.Sphere,new Vector3(.14f,-.36f,1.17f),new Vector3(.2f,.17f,.2f),glow);
                            Part(carbine,"Carbine Cell",PrimitiveType.Cube,new Vector3(.14f,-.53f,.38f),new Vector3(.2f,.28f,.2f),glow);
                            Part(carbine,"Carbine Rail",PrimitiveType.Cube,new Vector3(.14f,-.24f,.54f),new Vector3(.14f,.07f,.73f),gold);
                            Child(carbine,"Carbine Muzzle").localPosition = new Vector3(.14f,-.36f,1.29f);
                            break;
                        case VisualArchetype.Charger: Part(body,"Horns",PrimitiveType.Cube,new Vector3(0,1.65f,.62f),new Vector3(.7f,.16f,.9f),gold); break;
                        case VisualArchetype.Support: Part(left,"Totem",PrimitiveType.Cylinder,new Vector3(-.22f,-.2f,0),new Vector3(.12f,.75f,.12f),gold); break;
                        case VisualArchetype.Commander:
                            Part(body,"Cape",PrimitiveType.Cube,new Vector3(0,1.1f,-.32f),new Vector3(.8f,1.1f,.08f),color);
                            Part(body,"Banner",PrimitiveType.Cube,new Vector3(.38f,2.35f,-.25f),new Vector3(.6f,.6f,.08f),gold);
                            Part(body,"WarHorn",PrimitiveType.Cylinder,new Vector3(-.65f,1.4f,.2f),new Vector3(.14f,.35f,.14f),gold,Quaternion.Euler(90,0,0)); break;
                    }
                }
                var animator = root.AddComponent<ProceduralPoseAnimator>(); animator.Configure(body,left,right,type == VisualArchetype.Payload,leftLeg,rightLeg);
                animator.ConfigureFirearmReady(type == VisualArchetype.Ranged);
                animator.ConfigureShieldReady(type == VisualArchetype.Shield);
                var visual = root.AddComponent<LowPolyVisual>(); visual.Configure(type,root.transform,root.GetComponentsInChildren<Renderer>(true),left == null ? new[]{body} : new[]{body,left,right,leftLeg,rightLeg});
                if (!visual.IsGameplayFree(out var reason)) throw new InvalidOperationException(reason);
                PrefabUtility.SaveAsPrefabAsset(root,GetPrefabPath(type));
            } finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        static Transform Child(Transform parent,string name) { var t = new GameObject(name).transform; t.SetParent(parent,false); return t; }
        static void MeshPart(Transform parent, string name, Mesh mesh, Material material)
        {
            var part = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            part.transform.SetParent(parent, false);
            part.GetComponent<MeshFilter>().sharedMesh = mesh;
            part.GetComponent<MeshRenderer>().sharedMaterial = material;
        }
        static void Part(Transform parent,string name,PrimitiveType primitive,Vector3 position,Vector3 scale,Material material,Quaternion rotation = default)
        {
            var part = GameObject.CreatePrimitive(primitive); UnityEngine.Object.DestroyImmediate(part.GetComponent<Collider>()); part.name=name; var t=part.transform; t.SetParent(parent,false); t.localPosition=position; t.localScale=scale; t.localRotation=rotation == default ? Quaternion.identity : rotation; part.GetComponent<Renderer>().sharedMaterial=material;
        }
        public static LowPolyVisual InstantiateVisual(GameObject root, VisualArchetype type)
        {
            if (root == null) throw new ArgumentNullException(nameof(root));
            var prefab = LoadPrefab(type); if (prefab == null) throw new InvalidOperationException("Missing generated prefab: " + type);
            for (var i=root.transform.childCount-1;i>=0;i--) if (root.transform.GetChild(i).name == "VisualRoot") UnityEngine.Object.DestroyImmediate(root.transform.GetChild(i).gameObject);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab,root.transform); instance.name="VisualRoot"; var t=instance.transform; t.localPosition=Vector3.zero; t.localRotation=Quaternion.identity; t.localScale=Vector3.one;
            var visual=instance.GetComponent<LowPolyVisual>(); if (visual == null || !visual.IsGameplayFree(out var reason)) { UnityEngine.Object.DestroyImmediate(instance); throw new InvalidOperationException("Invalid visual prefab"); }
            instance.GetComponent<ProceduralPoseAnimator>().BindMotionSource(root.transform); return visual;
        }
    }
}
