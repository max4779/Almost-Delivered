using System.Collections.Generic;
using System.IO;
using AlmostDelivered.Combat;
using AlmostDelivered.Frontend;
using AlmostDelivered.Gameplay;
using AlmostDelivered.MapGeneration;
using AlmostDelivered.Presentation;
using AlmostDelivered.Presentation.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace AlmostDelivered.Editor.Frontend
{
    public static class MissionPrototypeSceneBuilder
    {
        public const string ScenePath = "Assets/AlmostDelivered/Scenes/MissionPrototype.unity";

        private const string FontPath = "Assets/AlmostDelivered/Art/Fonts/NotoSansCJKkr-Regular.otf";
        private const string MaterialRoot = "Assets/AlmostDelivered/Art/Materials/Frontend";

        [MenuItem("Almost Delivered/Frontend/Build mission prototype")]
        public static void BuildOrUpdate()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            MissionMapAssetFactory.CreateOrUpdateAll();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("Mission Prototype");

            var floorMaterial = Material("Floor");
            var darkMaterial = Material("Dark");
            var cyanMaterial = Material("Cyan");
            var amberMaterial = Material("Amber");

            Cube("Mission Floor", root.transform, new Vector3(0f, -.5f, 0f), new Vector3(40f, 1f, 32f), floorMaterial);
            Cube("North Boundary", root.transform, new Vector3(0f, 1f, 16f), new Vector3(40f, 2f, .6f), darkMaterial);
            Cube("West Boundary", root.transform, new Vector3(-20f, 1f, 0f), new Vector3(.6f, 2f, 32f), darkMaterial);
            Cube("East Boundary", root.transform, new Vector3(20f, 1f, 0f), new Vector3(.6f, 2f, 32f), darkMaterial);

            var projectileTemplate = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            projectileTemplate.name = "Projectile Template";
            projectileTemplate.transform.SetParent(root.transform, false);
            projectileTemplate.transform.localScale = Vector3.one * .28f;
            projectileTemplate.GetComponent<Renderer>().sharedMaterial = cyanMaterial;
            projectileTemplate.GetComponent<SphereCollider>().isTrigger = true;
            projectileTemplate.AddComponent<CombatProjectile>();
            projectileTemplate.SetActive(false);

            var player = Actor("Player", root.transform, new Vector3(0f, 1f, -8f), CombatTeamId.Player, VisualArchetype.Player);
            var playerPrimitiveCollider = player.GetComponent<CapsuleCollider>();
            Object.DestroyImmediate(playerPrimitiveCollider);
            var playerController = player.AddComponent<CharacterController>();
            playerController.height = 2f;
            playerController.radius = .5f;
            var playerAttack = player.AddComponent<RangedAttackController>();
            var playerMuzzle = Child(player.transform, "Player Muzzle", new Vector3(0f, .65f, .85f));
            playerAttack.Configure(projectileTemplate, playerMuzzle, 28f, .22f, 30f, 2.2f);
            var playerInput = player.AddComponent<MissionPlayerController>();
            playerInput.Configure(playerController, playerAttack, 6.5f);

            var warden = Actor("Warden Boss", root.transform, new Vector3(-8f, 1f, 8f), CombatTeamId.Enemy, VisualArchetype.Commander);
            SetMaxHealth(warden.GetComponent<ActorHealth>(), 220f);
            var wardenAttack = warden.AddComponent<RangedAttackController>();
            var wardenMuzzle = Child(warden.transform, "Warden Muzzle", new Vector3(0f, .65f, .9f));
            wardenAttack.Configure(projectileTemplate, wardenMuzzle, 10f, .9f, 18f, 3f);
            var wardenBoss = warden.AddComponent<PrototypeBossController>();
            wardenBoss.Configure(PrototypeBossMode.RangedWarden, player.transform, wardenAttack, 0f, 2f, 10f, 1f, 4f);

            var charger = Actor("Charger Boss", root.transform, new Vector3(8f, 1f, 8f), CombatTeamId.Enemy, VisualArchetype.Charger);
            SetMaxHealth(charger.GetComponent<ActorHealth>(), 300f);
            var chargerBoss = charger.AddComponent<PrototypeBossController>();
            chargerBoss.Configure(PrototypeBossMode.IronCharger, player.transform, null, 3.8f, 1.7f, 16f, 1.1f, 5.5f);

            BuildMapGenerator(root.transform);

            BuildCamera(player.transform);
            BuildLighting(root.transform);
            BuildHud(player.GetComponent<ActorHealth>(), new[] { wardenBoss, chargerBoss });

            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log("MISSION_PROTOTYPE_BUILT " + ScenePath);
        }

        private static void BuildMapGenerator(Transform parent)
        {
            var generatorObject = new GameObject("Mission Map Generator");
            generatorObject.transform.SetParent(parent, false);
            var generator = generatorObject.AddComponent<MissionMapGenerator>();
            var theme = AssetDatabase.LoadAssetAtPath<MissionMapTheme>(MissionMapAssetFactory.ThemePath);
            generator.Configure(
                theme,
                new Vector2(18f, 14f),
                24,
                14,
                new[]
                {
                    new MissionMapProtectedZone(new Vector2(0f, -8f), new Vector2(5f, 5f)),
                    new MissionMapProtectedZone(new Vector2(-8f, 8f), new Vector2(5f, 5f)),
                    new MissionMapProtectedZone(new Vector2(8f, 8f), new Vector2(5f, 5f)),
                    new MissionMapProtectedZone(new Vector2(0f, 0f), new Vector2(4.5f, 18f))
                },
                true,
                3000);
        }

        private static GameObject Actor(string name, Transform parent, Vector3 position, CombatTeamId team, VisualArchetype archetype)
        {
            var actor = new GameObject(name);
            actor.transform.SetParent(parent, false);
            actor.transform.position = position;
            var collider = actor.AddComponent<CapsuleCollider>();
            collider.height = 2f;
            collider.radius = .5f;
            actor.AddComponent<ActorHealth>();
            actor.AddComponent<CombatTeam>().Team = team;
            var visual = LowPolyAssetFactory.InstantiateVisual(actor, archetype);
            visual.transform.localPosition = Vector3.down;
            return actor;
        }

        private static void BuildCamera(Transform target)
        {
            var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener), typeof(QuarterViewCamera));
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.GetComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 9f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.025f, .035f, .05f);
            var follow = cameraObject.GetComponent<QuarterViewCamera>();
            follow.Target = target;
            follow.Offset = new Vector3(0f, 16f, -16f);
            follow.FocusOffset = new Vector3(0f, 0f, 3f);
            follow.SnapToTarget();
        }

        private static void BuildLighting(Transform parent)
        {
            var lightObject = new GameObject("Mission Light", typeof(Light));
            lightObject.transform.SetParent(parent, false);
            lightObject.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
            var light = lightObject.GetComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.05f;
            RenderSettings.ambientLight = new Color(.3f, .34f, .4f);
        }

        private static void BuildHud(ActorHealth player, PrototypeBossController[] bosses)
        {
            var canvasObject = new GameObject("Mission HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(MissionHud));
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            var controls = HudText("Controls", canvasObject.transform, new Vector2(24f, -22f), new Vector2(950f, 54f), 26, TextAnchor.UpperLeft);
            var health = HudText("Player Health", canvasObject.transform, new Vector2(24f, -82f), new Vector2(420f, 48f), 28, TextAnchor.UpperLeft);
            var bossCount = HudText("Boss Count", canvasObject.transform, new Vector2(-24f, -22f), new Vector2(420f, 54f), 30, TextAnchor.UpperRight, true);
            var hud = canvasObject.GetComponent<MissionHud>();
            hud.BindViews(controls, health, bossCount);
            hud.Configure(player, bosses);
        }

        private static Text HudText(string name, Transform parent, Vector2 anchoredPosition, Vector2 size, int fontSize, TextAnchor alignment, bool right = false)
        {
            var textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            textObject.transform.SetParent(parent, false);
            var rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = right ? Vector2.one : new Vector2(0f, 1f);
            rect.pivot = right ? Vector2.one : new Vector2(0f, 1f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            var text = textObject.GetComponent<Text>();
            text.font = AssetDatabase.LoadAssetAtPath<Font>(FontPath) ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = Color.white;
            return text;
        }

        private static GameObject Cube(string name, Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.transform.SetParent(parent, false);
            cube.transform.position = position;
            cube.transform.localScale = scale;
            cube.GetComponent<Renderer>().sharedMaterial = material;
            return cube;
        }

        private static Transform Child(Transform parent, string name, Vector3 localPosition)
        {
            var child = new GameObject(name).transform;
            child.SetParent(parent, false);
            child.localPosition = localPosition;
            return child;
        }

        private static void SetMaxHealth(ActorHealth health, float value)
        {
            var serialized = new SerializedObject(health);
            serialized.FindProperty("maxHealth").floatValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Material Material(string name)
        {
            return AssetDatabase.LoadAssetAtPath<Material>($"{MaterialRoot}/{name}.mat");
        }
    }
}
