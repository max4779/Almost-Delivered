using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AlmostDelivered.Frontend;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace AlmostDelivered.Editor.Frontend
{
    public static class FrontendSceneBuilder
    {
        public const string FrontendScenePath = "Assets/AlmostDelivered/Scenes/Frontend.unity";
        public const string DepartureHubScenePath = "Assets/AlmostDelivered/Scenes/DepartureHub.unity";

        private const string CommanderScenePath = "Assets/AlmostDelivered/Scenes/CommanderAITest.unity";
        private const string FrontendFontPath = "Assets/AlmostDelivered/Art/Fonts/NotoSansCJKkr-Regular.otf";
        private const string MaterialRoot = "Assets/AlmostDelivered/Art/Materials/Frontend";

        private static readonly Color StructureColor = new Color(.075f, .105f, .14f);
        private static readonly Color FloorColor = new Color(.11f, .14f, .17f);
        private static readonly Color PanelColor = new Color(.07f, .13f, .17f);
        private static readonly Color CyanColor = new Color(.08f, .8f, .9f);
        private static readonly Color AmberColor = new Color(1f, .55f, .08f);
        private static readonly Color HazardColor = new Color(.95f, .72f, .08f);

        [MenuItem("Almost Delivered/Frontend/Build frontend and departure hub")]
        public static void BuildOrUpdateAll()
        {
            EnsureProjectFolders();
            var materials = CreateOrUpdateMaterials();
            BuildFrontend(materials);
            BuildDepartureHub(materials);
            RegisterBuildSettings();
            AssetDatabase.SaveAssets();
            EditorSceneManager.OpenScene(DepartureHubScenePath, OpenSceneMode.Single);
            Debug.Log("FRONTEND_HUB_BUILT " + FrontendScenePath + " " + DepartureHubScenePath);
        }

        public static void BuildFromCommandLine()
        {
            try
            {
                BuildOrUpdateAll();
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        private static void EnsureProjectFolders()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FrontendScenePath));
            Directory.CreateDirectory(MaterialRoot);
            AssetDatabase.Refresh();
        }

        private static Dictionary<string, Material> CreateOrUpdateMaterials()
        {
            return new Dictionary<string, Material>
            {
                { "Structure", CreateOrUpdateMaterial("Structure", StructureColor, 0f, 0.28f) },
                { "Floor", CreateOrUpdateMaterial("Floor", FloorColor, 0f, 0.42f) },
                { "Panel", CreateOrUpdateMaterial("Panel", PanelColor, .1f, .36f) },
                { "Cyan", CreateOrUpdateMaterial("Cyan", CyanColor, .05f, .5f) },
                { "Amber", CreateOrUpdateMaterial("Amber", AmberColor, .05f, .48f) },
                { "Hazard", CreateOrUpdateMaterial("Hazard", HazardColor, 0f, .34f) },
                { "Dark", CreateOrUpdateMaterial("Dark", new Color(.025f, .035f, .05f), .1f, .3f) },
                { "Player", CreateOrUpdateMaterial("Player", new Color(.2f, .78f, .82f), .15f, .55f) }
            };
        }

        private static Material CreateOrUpdateMaterial(string name, Color color, float metallic, float smoothness)
        {
            var path = MaterialRoot + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Standard")) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }

            material.color = color;
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Glossiness", smoothness);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void BuildFrontend(IReadOnlyDictionary<string, Material> materials)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("Frontend");
            var controller = root.AddComponent<FrontendMenuController>();

            var cameraObject = new GameObject("Frontend Camera", typeof(Camera));
            cameraObject.transform.SetParent(root.transform, false);
            cameraObject.tag = "MainCamera";
            var backgroundCamera = cameraObject.GetComponent<Camera>();
            backgroundCamera.clearFlags = CameraClearFlags.SolidColor;
            backgroundCamera.backgroundColor = new Color(.018f, .03f, .045f);
            backgroundCamera.cullingMask = 0;
            backgroundCamera.depth = -10f;

            var canvasObject = new GameObject("Frontend Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(root.transform, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = .5f;

            var background = UiImage("Background", canvasObject.transform, new Color(.018f, .03f, .045f, 1f));
            Stretch(background.rectTransform);

            var leftAccent = UiImage("Cyan Accent", background.transform, new Color(CyanColor.r, CyanColor.g, CyanColor.b, .88f));
            Anchor(leftAccent.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, .5f), new Vector2(10f, 0f), new Vector2(10f, 0f));
            var topBand = UiImage("Top Band", background.transform, new Color(.04f, .08f, .11f, .96f));
            Anchor(topBand.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(.5f, 1f), new Vector2(0f, 0f), new Vector2(0f, 94f));

            var title = UiText("Title", background.transform, "ALMOST DELIVERED", 76, Color.white, TextAnchor.MiddleLeft);
            Anchor(title.rectTransform, new Vector2(.08f, .68f), new Vector2(.72f, .88f), new Vector2(0f, .5f), Vector2.zero, Vector2.zero);
            title.fontStyle = FontStyle.Bold;
            title.resizeTextForBestFit = true;
            title.resizeTextMinSize = 34;
            title.resizeTextMaxSize = 76;

            var subtitle = UiText("Subtitle", background.transform, "화물을 준비하고, 승무원과 함께 출발하십시오.", 25, new Color(.68f, .78f, .84f), TextAnchor.UpperLeft);
            Anchor(subtitle.rectTransform, new Vector2(.083f, .62f), new Vector2(.66f, .7f), new Vector2(0f, 1f), Vector2.zero, Vector2.zero);

            var status = UiText("Status", background.transform, "OPERATIONS // STANDBY", 18, AmberColor, TextAnchor.MiddleLeft);
            Anchor(status.rectTransform, new Vector2(.083f, .9f), new Vector2(.5f, .96f), new Vector2(0f, .5f), Vector2.zero, Vector2.zero);

            var menuPanel = new GameObject("Menu Panel", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            menuPanel.transform.SetParent(background.transform, false);
            var panelRect = menuPanel.GetComponent<RectTransform>();
            Anchor(panelRect, new Vector2(.08f, .17f), new Vector2(.42f, .55f), new Vector2(0f, .5f), Vector2.zero, Vector2.zero);
            menuPanel.GetComponent<Image>().color = new Color(.03f, .055f, .075f, .94f);
            var layout = menuPanel.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(30, 30, 30, 30);
            layout.spacing = 16f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            menuPanel.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var startButton = UiButton("Start Operation", menuPanel.transform, "작전 준비", CyanColor, true);
            UnityEventTools.AddPersistentListener(startButton.onClick, controller.StartGame);
            UiButton("Settings Placeholder", menuPanel.transform, "설정  ·  준비 중", new Color(.24f, .29f, .32f), false);
            var quitButton = UiButton("Quit Game", menuPanel.transform, "종료", new Color(.24f, .29f, .32f), true);
            UnityEventTools.AddPersistentListener(quitButton.onClick, controller.QuitGame);

            var footer = UiText("Footer", background.transform, "QUARTER-VIEW CO-OP DEFENSE PROTOTYPE", 16, new Color(.38f, .52f, .58f), TextAnchor.MiddleLeft);
            Anchor(footer.rectTransform, new Vector2(.083f, .04f), new Vector2(.6f, .1f), new Vector2(0f, .5f), Vector2.zero, Vector2.zero);

            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            EditorSceneManager.SaveScene(scene, FrontendScenePath);
        }

        private static void BuildDepartureHub(IReadOnlyDictionary<string, Material> materials)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var hub = new GameObject("Departure Hub");
            var collision = Child(hub.transform, "Collision");
            var layout = Child(hub.transform, "Layout");
            var stations = Child(hub.transform, "Stations");

            CollisionCube("Floor Collision", collision, new Vector3(0f, -.55f, 0f), new Vector3(32f, 1f, 22f));
            CollisionCube("North Wall Collision", collision, new Vector3(0f, 3f, 11f), new Vector3(32f, 7f, 1f));
            CollisionCube("South Wall Collision", collision, new Vector3(0f, 3f, -11f), new Vector3(32f, 7f, 1f));
            CollisionCube("West Wall Collision", collision, new Vector3(-16f, 3f, 0f), new Vector3(1f, 7f, 22f));
            CollisionCube("East Wall Collision", collision, new Vector3(16f, 3f, 0f), new Vector3(1f, 7f, 22f));

            VisualCube("Floor", layout, new Vector3(0f, -.04f, 0f), new Vector3(31.4f, .12f, 21.4f), materials["Floor"]);
            VisualCube("North Wall", layout, new Vector3(0f, 3f, 10.65f), new Vector3(31.4f, 6.8f, .25f), materials["Structure"]);
            VisualCube("South Wall", layout, new Vector3(0f, 3f, -10.65f), new Vector3(31.4f, 6.8f, .25f), materials["Structure"]);
            VisualCube("West Wall", layout, new Vector3(-15.65f, 3f, 0f), new Vector3(.25f, 6.8f, 21.4f), materials["Structure"]);
            VisualCube("East Wall", layout, new Vector3(15.65f, 3f, 0f), new Vector3(.25f, 6.8f, 21.4f), materials["Structure"]);

            for (var z = -7f; z <= 7f; z += 3.5f)
            {
                VisualCube("Floor Lane " + z, layout, new Vector3(0f, .04f, z), new Vector3(12f, .035f, .16f), materials["Hazard"]);
            }

            BuildDeploymentGate(stations, materials);
            BuildMissionTerminal(stations, materials);
            BuildEquipmentBay(stations, materials);
            BuildCrewDoor(stations, materials);
            BuildCargoAndDetails(layout, materials);

            var player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            player.name = "Player";
            player.transform.position = new Vector3(0f, 1f, -7.2f);
            Object.DestroyImmediate(player.GetComponent<CapsuleCollider>());
            player.GetComponent<Renderer>().sharedMaterial = materials["Player"];
            var characterController = player.AddComponent<CharacterController>();
            characterController.height = 2f;
            characterController.radius = .45f;
            player.AddComponent<HubPlayerController>();

            var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener), typeof(QuarterViewCamera));
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.GetComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 13.5f;
            camera.backgroundColor = new Color(.025f, .04f, .055f);
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 120f;
            var follow = cameraObject.GetComponent<QuarterViewCamera>();
            follow.Target = player.transform;
            follow.Offset = new Vector3(0f, 16f, -16f);
            follow.SnapToTarget();

            var keyLight = new GameObject("Hub Directional Light", typeof(Light));
            var directional = keyLight.GetComponent<Light>();
            directional.type = LightType.Directional;
            directional.color = new Color(.72f, .82f, .9f);
            directional.intensity = .62f;
            keyLight.transform.rotation = Quaternion.Euler(52f, -28f, 0f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.11f, .14f, .18f);

            EditorSceneManager.SaveScene(scene, DepartureHubScenePath);
        }

        private static void BuildDeploymentGate(Transform stations, IReadOnlyDictionary<string, Material> materials)
        {
            var gate = Child(stations, "Deployment Gate");
            VisualCube("Gate Frame Left", gate, new Vector3(-5.2f, 2.8f, 10.15f), new Vector3(1f, 5.6f, .7f), materials["Dark"]);
            VisualCube("Gate Frame Right", gate, new Vector3(5.2f, 2.8f, 10.15f), new Vector3(1f, 5.6f, .7f), materials["Dark"]);
            VisualCube("Gate Header", gate, new Vector3(0f, 5.2f, 10.15f), new Vector3(11.4f, .8f, .7f), materials["Dark"]);
            VisualCube("Sealed Gate", gate, new Vector3(0f, 2.55f, 10.08f), new Vector3(9.2f, 4.5f, .35f), materials["Panel"]);
            for (var x = -3.6f; x <= 3.6f; x += 1.8f)
            {
                var stripe = VisualCube("Hazard Stripe", gate, new Vector3(x, .38f, 9.82f), new Vector3(1.1f, .34f, .08f), materials["Hazard"]);
                stripe.transform.rotation = Quaternion.Euler(0f, 0f, -18f);
            }
            WorldLabel("DEPLOYMENT // LOCKED", gate, new Vector3(0f, 4.2f, 9.78f), 46, AmberColor, TextAnchor.MiddleCenter);
        }

        private static void BuildMissionTerminal(Transform stations, IReadOnlyDictionary<string, Material> materials)
        {
            var terminal = Child(stations, "Mission Terminal");
            VisualCube("Terminal Base", terminal, new Vector3(-12.8f, 1.15f, 1.2f), new Vector3(2.4f, 2.3f, 4.6f), materials["Panel"]);
            VisualCube("Terminal Screen", terminal, new Vector3(-11.54f, 2.25f, 1.2f), new Vector3(.16f, 1.55f, 3.2f), materials["Cyan"]);
            VisualCube("Briefing Table", terminal, new Vector3(-9.5f, .85f, 1.2f), new Vector3(3.2f, .22f, 3.8f), materials["Structure"]);
            VisualCube("Briefing Table Leg", terminal, new Vector3(-9.5f, .4f, 1.2f), new Vector3(.55f, .8f, .55f), materials["Dark"]);
            WorldLabel("MISSION CONTROL", terminal, new Vector3(-11.42f, 3.35f, 1.2f), 34, CyanColor, TextAnchor.MiddleCenter, Quaternion.Euler(0f, 90f, 0f));
        }

        private static void BuildEquipmentBay(Transform stations, IReadOnlyDictionary<string, Material> materials)
        {
            var bay = Child(stations, "Equipment Bay");
            VisualCube("Equipment Backboard", bay, new Vector3(14.9f, 2.4f, .8f), new Vector3(.35f, 4.2f, 7f), materials["Panel"]);
            for (var z = -1.6f; z <= 3.2f; z += 1.6f)
            {
                VisualCube("Equipment Rack", bay, new Vector3(14.55f, 1.5f, z), new Vector3(.45f, 2.3f, 1.1f), materials["Amber"]);
            }
            VisualCube("Equipment Counter", bay, new Vector3(12.7f, .85f, .8f), new Vector3(3.8f, .25f, 6.2f), materials["Structure"]);
            WorldLabel("EQUIPMENT // OFFLINE", bay, new Vector3(14.36f, 4.25f, .8f), 30, AmberColor, TextAnchor.MiddleCenter, Quaternion.Euler(0f, -90f, 0f));
        }

        private static void BuildCrewDoor(Transform stations, IReadOnlyDictionary<string, Material> materials)
        {
            var door = Child(stations, "Crew Access Door");
            VisualCube("Crew Door", door, new Vector3(-7.5f, 2f, -10.15f), new Vector3(4f, 4f, .35f), materials["Panel"]);
            VisualCube("Crew Door Light", door, new Vector3(-7.5f, 4.45f, -9.9f), new Vector3(2f, .18f, .12f), materials["Cyan"]);
            WorldLabel("CREW ACCESS", door, new Vector3(-7.5f, 3.2f, -9.92f), 28, CyanColor, TextAnchor.MiddleCenter);
        }

        private static void BuildCargoAndDetails(Transform layout, IReadOnlyDictionary<string, Material> materials)
        {
            var cargo = Child(layout, "Cargo Staging");
            VisualCube("Cargo Pallet", cargo, new Vector3(2.8f, .18f, 1f), new Vector3(3.5f, .35f, 2.4f), materials["Dark"]);
            VisualCube("Cargo Crate A", cargo, new Vector3(2.1f, .85f, .8f), new Vector3(1.4f, 1.35f, 1.4f), materials["Amber"]);
            VisualCube("Cargo Crate B", cargo, new Vector3(3.7f, .65f, 1.1f), new Vector3(1.2f, .95f, 1.2f), materials["Panel"]);

            for (var i = 0; i < 3; i++)
            {
                VisualCube("West Pipe " + i, layout, new Vector3(-15.2f, 4.8f - i * .55f, -5.5f), new Vector3(.35f, .35f, 7.5f), materials[i == 1 ? "Cyan" : "Dark"]);
                VisualCube("East Barrier " + i, layout, new Vector3(10.6f + i * 1.4f, .45f, -6.2f), new Vector3(1.15f, .9f, .25f), materials[i == 1 ? "Hazard" : "Structure"]);
            }

            WorkLight("West Work Light", layout, new Vector3(-13.5f, 4.8f, -4.5f), AmberColor);
            WorkLight("East Work Light", layout, new Vector3(12.8f, 4.8f, -4.5f), CyanColor);
        }

        private static void RegisterBuildSettings()
        {
            var ordered = new List<EditorBuildSettingsScene>
            {
                new EditorBuildSettingsScene(FrontendScenePath, true),
                new EditorBuildSettingsScene(DepartureHubScenePath, true)
            };

            ordered.AddRange(EditorBuildSettings.scenes.Where(scene =>
                scene.path != FrontendScenePath
                && scene.path != DepartureHubScenePath
                && scene.path != CommanderScenePath));

            if (File.Exists(CommanderScenePath))
            {
                ordered.Add(new EditorBuildSettingsScene(CommanderScenePath, true));
            }

            EditorBuildSettings.scenes = ordered.ToArray();
        }

        private static Transform Child(Transform parent, string name)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent, false);
            return child.transform;
        }

        private static void CollisionCube(string name, Transform parent, Vector3 position, Vector3 scale)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.transform.SetParent(parent, false);
            cube.transform.position = position;
            cube.transform.localScale = scale;
            cube.GetComponent<Renderer>().enabled = false;
        }

        private static GameObject VisualCube(string name, Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.transform.SetParent(parent, false);
            cube.transform.position = position;
            cube.transform.localScale = scale;
            cube.GetComponent<Renderer>().sharedMaterial = material;
            Object.DestroyImmediate(cube.GetComponent<Collider>());
            return cube;
        }

        private static void WorkLight(string name, Transform parent, Vector3 position, Color color)
        {
            var lightObject = new GameObject(name, typeof(Light));
            lightObject.transform.SetParent(parent, false);
            lightObject.transform.position = position;
            var light = lightObject.GetComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = .45f;
            light.range = 5.5f;
        }

        private static TextMesh WorldLabel(string name, Transform parent, Vector3 position, int fontSize, Color color, TextAnchor anchor, Quaternion? rotation = null)
        {
            var labelObject = new GameObject(name, typeof(TextMesh));
            labelObject.transform.SetParent(parent, false);
            labelObject.transform.position = position;
            labelObject.transform.rotation = rotation ?? Quaternion.identity;
            var label = labelObject.GetComponent<TextMesh>();
            label.text = name;
            label.fontSize = fontSize;
            label.characterSize = .055f;
            label.color = color;
            label.anchor = anchor;
            label.alignment = TextAlignment.Center;
            return label;
        }

        private static Image UiImage(string name, Transform parent, Color color)
        {
            var imageObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            imageObject.transform.SetParent(parent, false);
            var image = imageObject.GetComponent<Image>();
            image.color = color;
            return image;
        }

        private static Text UiText(string name, Transform parent, string value, int fontSize, Color color, TextAnchor alignment)
        {
            var textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            textObject.transform.SetParent(parent, false);
            var text = textObject.GetComponent<Text>();
            text.text = value;
            text.font = LoadFrontendFont();
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }

        private static Font LoadFrontendFont()
        {
            var font = AssetDatabase.LoadAssetAtPath<Font>(FrontendFontPath);
            if (font == null)
            {
                throw new InvalidOperationException("Missing bundled frontend font at " + FrontendFontPath);
            }

            return font;
        }

        private static Button UiButton(string name, Transform parent, string label, Color color, bool interactable)
        {
            var buttonObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(LayoutElement));
            buttonObject.transform.SetParent(parent, false);
            buttonObject.GetComponent<Image>().color = color;
            var button = buttonObject.GetComponent<Button>();
            button.interactable = interactable;
            button.targetGraphic = buttonObject.GetComponent<Image>();
            var colors = button.colors;
            colors.highlightedColor = Color.Lerp(color, Color.white, .22f);
            colors.pressedColor = Color.Lerp(color, Color.black, .2f);
            colors.disabledColor = new Color(.16f, .19f, .21f, .72f);
            button.colors = colors;
            var layout = buttonObject.GetComponent<LayoutElement>();
            layout.preferredHeight = 72f;
            layout.minHeight = 58f;

            var text = UiText("Label", buttonObject.transform, label, 26, Color.white, TextAnchor.MiddleCenter);
            Stretch(text.rectTransform);
            return button;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void Anchor(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
