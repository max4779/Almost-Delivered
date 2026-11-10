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
        const string Prefabs = "Assets/AlmostDelivered/Prefabs/Generated";
        public static string GetPrefabPath(VisualArchetype type) => Prefabs + (type == VisualArchetype.Payload || type == VisualArchetype.ObjectiveBeacon ? "/Objectives/" : "/Characters/") + type + "/VisualRoot.prefab";
        public static GameObject LoadPrefab(VisualArchetype type) => AssetDatabase.LoadAssetAtPath<GameObject>(GetPrefabPath(type));

        public static void CreateOrUpdateAll(Func<string, Shader> shaderResolver = null)
        {
            var resolve = shaderResolver ?? Shader.Find;
            var lit = resolve("Standard");
            var unlit = resolve("Unlit/Color");
            if (lit == null || unlit == null) throw new InvalidOperationException("Both Standard and Unlit/Color shaders are required.");
            Directory.CreateDirectory(Materials);
            Directory.CreateDirectory(Prefabs + "/Characters");
            Directory.CreateDirectory(Prefabs + "/Objectives");
            foreach (VisualArchetype type in Enum.GetValues(typeof(VisualArchetype)))
                Directory.CreateDirectory(Path.GetDirectoryName(GetPrefabPath(type)));
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            AssetDatabase.StartAssetEditing();
            try {
                var palette = new[] { Mat("Player", lit, new Color(.05f,.55f,.62f)), Mat("Dummy",lit,Color.gray), Mat("Shield",lit,new Color(.22f,.48f,.23f)), Mat("Ranged",lit,new Color(.95f,.43f,.12f)), Mat("Charger",lit,new Color(.68f,.12f,.1f)), Mat("Support",lit,new Color(.56f,.78f,.18f)), Mat("Commander",lit,new Color(.38f,.2f,.52f)), Mat("Payload",lit,new Color(.18f,.28f,.38f)), Mat("Beacon",lit,new Color(.04f,.52f,.49f)) };
                var rust = Mat("EnemyRust",lit,new Color(.45f,.23f,.14f));
                var gold = Mat("Gold",lit,new Color(.94f,.69f,.2f));
                var glow = Mat("Rune",unlit,new Color(.15f,.9f,.8f));
                foreach (VisualArchetype type in Enum.GetValues(typeof(VisualArchetype))) Build(type, palette[(int)type], rust, gold, glow);
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
        static void Build(VisualArchetype type, Material color, Material rust, Material gold, Material glow)
        {
            var root = new GameObject("VisualRoot");
            try {
                var body = Child(root.transform,"BodyPivot");
                Transform left = null, right = null, leftLeg = null, rightLeg = null;
                if (type == VisualArchetype.Payload) {
                    Part(root.transform,"HoverShadow",PrimitiveType.Cylinder,new Vector3(0,.035f,0),new Vector3(1.35f,.015f,1.65f),rust);
                    Part(body,"CargoHull",PrimitiveType.Cube,new Vector3(0,1.1f,0),new Vector3(1.9f,.55f,2.4f),color);
                    Part(body,"CargoCanister",PrimitiveType.Cube,new Vector3(0,1.55f,-.1f),new Vector3(1.5f,.42f,1.7f),color);
                    Part(body,"ForwardSensor",PrimitiveType.Cube,new Vector3(0,1.28f,1.25f),new Vector3(.65f,.22f,.16f),glow);
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
                        Part(weapon,"RangedWeapon",PrimitiveType.Cube,new Vector3(.14f,-.45f,.32f),new Vector3(.12f,.12f,.85f),gold);
                        var blade=Child(right,"MeleeWeapon"); blade.gameObject.SetActive(false);
                        Part(blade,"Grip",PrimitiveType.Cube,new Vector3(.14f,-.4f,.16f),new Vector3(.16f,.16f,.35f),rust);
                        Part(blade,"Blade",PrimitiveType.Cube,new Vector3(.14f,-.4f,.72f),new Vector3(.2f,.08f,.9f),glow);
                    } else {
                        Part(right,"Weapon",PrimitiveType.Cube,new Vector3(.14f,-.45f,.32f),new Vector3(.12f,.12f,.85f),gold);
                    }
                    switch(type) {
                        case VisualArchetype.Shield: Part(left,"Shield",PrimitiveType.Cube,new Vector3(-.42f,-.3f,.15f),new Vector3(.7f,1,.15f),color); break;
                        case VisualArchetype.Ranged: Part(right,"RangedWeapon",PrimitiveType.Cube,new Vector3(.17f,-.3f,.2f),new Vector3(.12f,.8f,.12f),gold); break;
                        case VisualArchetype.Charger: Part(body,"Horns",PrimitiveType.Cube,new Vector3(0,1.65f,.62f),new Vector3(.7f,.16f,.9f),gold); break;
                        case VisualArchetype.Support: Part(left,"Totem",PrimitiveType.Cylinder,new Vector3(-.22f,-.2f,0),new Vector3(.12f,.75f,.12f),gold); break;
                        case VisualArchetype.Commander:
                            Part(body,"Cape",PrimitiveType.Cube,new Vector3(0,1.1f,-.32f),new Vector3(.8f,1.1f,.08f),color);
                            Part(body,"Banner",PrimitiveType.Cube,new Vector3(.38f,2.35f,-.25f),new Vector3(.6f,.6f,.08f),gold);
                            Part(body,"WarHorn",PrimitiveType.Cylinder,new Vector3(-.65f,1.4f,.2f),new Vector3(.14f,.35f,.14f),gold,Quaternion.Euler(90,0,0)); break;
                    }
                }
                var animator = root.AddComponent<ProceduralPoseAnimator>(); animator.Configure(body,left,right,type == VisualArchetype.Payload,leftLeg,rightLeg);
                var visual = root.AddComponent<LowPolyVisual>(); visual.Configure(type,root.transform,root.GetComponentsInChildren<Renderer>(true),left == null ? new[]{body} : new[]{body,left,right,leftLeg,rightLeg});
                if (!visual.IsGameplayFree(out var reason)) throw new InvalidOperationException(reason);
                PrefabUtility.SaveAsPrefabAsset(root,GetPrefabPath(type));
            } finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        static Transform Child(Transform parent,string name) { var t = new GameObject(name).transform; t.SetParent(parent,false); return t; }
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
