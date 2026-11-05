using System.Linq;
using AlmostDelivered.Editor.Frontend;
using AlmostDelivered.Frontend;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AlmostDelivered.Tests.EditMode
{
    public sealed class FrontendSceneBuilderTests
    {
        private const string FrontendFontPath = "Assets/AlmostDelivered/Art/Fonts/NotoSansCJKkr-Regular.otf";

        [TearDown]
        public void Cleanup()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        [Test]
        public void FrontendContainsExpectedMenuActions()
        {
            FrontendSceneBuilder.BuildOrUpdateAll();
            var scene = EditorSceneManager.OpenScene(FrontendSceneBuilder.FrontendScenePath, OpenSceneMode.Single);

            Assert.That(FindRoot(scene, "Frontend"), Is.Not.Null);
            Assert.That(Object.FindObjectOfType<FrontendMenuController>(), Is.Not.Null);
            Assert.That(Object.FindObjectOfType<EventSystem>(), Is.Not.Null);
            Assert.That(GameObject.Find("Start Operation").GetComponent<Button>().interactable, Is.True);
            Assert.That(GameObject.Find("Settings Placeholder").GetComponent<Button>().interactable, Is.False);
            Assert.That(GameObject.Find("Quit Game").GetComponent<Button>().interactable, Is.True);
        }

        [Test]
        public void FrontendCanvasUsesResponsiveAnchorsAndScaler()
        {
            FrontendSceneBuilder.BuildOrUpdateAll();
            EditorSceneManager.OpenScene(FrontendSceneBuilder.FrontendScenePath, OpenSceneMode.Single);

            var canvas = Object.FindObjectOfType<Canvas>();
            var scaler = canvas.GetComponent<CanvasScaler>();
            var menuPanel = GameObject.Find("Menu Panel").GetComponent<RectTransform>();

            Assert.That(canvas.renderMode, Is.EqualTo(RenderMode.ScreenSpaceOverlay));
            Assert.That(scaler.uiScaleMode, Is.EqualTo(CanvasScaler.ScaleMode.ScaleWithScreenSize));
            Assert.That(scaler.referenceResolution, Is.EqualTo(new Vector2(1920f, 1080f)));
            Assert.That(menuPanel.anchorMin.x, Is.GreaterThanOrEqualTo(0f));
            Assert.That(menuPanel.anchorMax.x, Is.LessThanOrEqualTo(1f));
            Assert.That(menuPanel.anchorMin.y, Is.GreaterThanOrEqualTo(0f));
            Assert.That(menuPanel.anchorMax.y, Is.LessThanOrEqualTo(1f));
        }

        [Test]
        public void FrontendHasRenderingCameraBehindOverlayCanvas()
        {
            FrontendSceneBuilder.BuildOrUpdateAll();
            EditorSceneManager.OpenScene(FrontendSceneBuilder.FrontendScenePath, OpenSceneMode.Single);

            var camera = GameObject.Find("Frontend Camera").GetComponent<Camera>();
            Assert.That(camera.enabled, Is.True);
            Assert.That(camera.clearFlags, Is.EqualTo(CameraClearFlags.SolidColor));
            Assert.That(Camera.main, Is.EqualTo(camera));
        }

        [Test]
        public void FrontendKoreanLabelsUseBundledFontWithRequiredGlyphs()
        {
            var bundledFont = AssetDatabase.LoadAssetAtPath<Font>(FrontendFontPath);
            Assert.That(bundledFont, Is.Not.Null, "A redistributable Korean font must be bundled with the project.");

            FrontendSceneBuilder.BuildOrUpdateAll();
            EditorSceneManager.OpenScene(FrontendSceneBuilder.FrontendScenePath, OpenSceneMode.Single);

            var koreanLabels = Object.FindObjectsOfType<Text>()
                .Where(label => label.text.Any(character => character >= '\uac00' && character <= '\ud7a3'))
                .ToArray();
            Assert.That(koreanLabels, Is.Not.Empty);
            Assert.That(koreanLabels.All(label => label.font == bundledFont), Is.True);

            var requiredGlyphs = koreanLabels.SelectMany(label => label.text)
                .Where(character => character >= '\uac00' && character <= '\ud7a3')
                .Distinct();
            Assert.That(requiredGlyphs.All(bundledFont.HasCharacter), Is.True);
        }

        [Test]
        public void HubSeparatesLayoutVisualsFromCollisionRoots()
        {
            FrontendSceneBuilder.BuildOrUpdateAll();
            var scene = EditorSceneManager.OpenScene(FrontendSceneBuilder.DepartureHubScenePath, OpenSceneMode.Single);
            var hub = FindRoot(scene, "Departure Hub");
            var collision = hub.transform.Find("Collision");
            var layout = hub.transform.Find("Layout");
            var stations = hub.transform.Find("Stations");

            Assert.That(collision, Is.Not.Null);
            Assert.That(layout, Is.Not.Null);
            Assert.That(stations, Is.Not.Null);
            Assert.That(collision.GetComponentsInChildren<Collider>(true).Length, Is.GreaterThanOrEqualTo(5));
            Assert.That(layout.GetComponentsInChildren<Renderer>(true).Length, Is.GreaterThan(8));
            Assert.That(stations.Find("Mission Terminal"), Is.Not.Null);
            Assert.That(stations.Find("Equipment Bay"), Is.Not.Null);
            Assert.That(stations.Find("Deployment Gate"), Is.Not.Null);
            Assert.That(layout.GetComponentsInChildren<HubPlayerController>(true), Is.Empty);
            Assert.That(stations.GetComponentsInChildren<HubPlayerController>(true), Is.Empty);
        }

        [Test]
        public void HubUsesCutawaySouthSideAndKeepsCollisionBoundary()
        {
            FrontendSceneBuilder.BuildOrUpdateAll();
            var scene = EditorSceneManager.OpenScene(FrontendSceneBuilder.DepartureHubScenePath, OpenSceneMode.Single);
            var hub = FindRoot(scene, "Departure Hub");
            var collision = hub.transform.Find("Collision/South Wall Collision");
            var southWallVisual = hub.transform.Find("Layout/South Wall");
            var floor = hub.transform.Find("Layout/Floor").GetComponent<Renderer>();

            Assert.That(collision, Is.Not.Null);
            Assert.That(collision.GetComponent<Collider>().enabled, Is.True);
            Assert.That(
                southWallVisual == null || !southWallVisual.GetComponent<Renderer>().enabled,
                Is.True,
                "The camera-facing wall must be a cutaway instead of hiding the player.");
            Assert.That(floor.bounds.min.z, Is.LessThanOrEqualTo(-14f));
        }

        [Test]
        public void HubContainsOneConfiguredDeploymentGate()
        {
            FrontendSceneBuilder.BuildOrUpdateAll();
            var hub = EditorSceneManager.OpenScene(FrontendSceneBuilder.DepartureHubScenePath, OpenSceneMode.Single);

            var gateComponents = Object.FindObjectsOfType<MonoBehaviour>()
                .Where(component => component.GetType().FullName == "AlmostDelivered.Frontend.HubDeploymentGate")
                .ToArray();
            var trigger = GameObject.Find("Deployment Gate Trigger");
            var prompt = hub.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .Select(transform => transform.gameObject)
                .FirstOrDefault(candidate => candidate.name == "Deployment Prompt");

            Assert.That(gateComponents.Length, Is.EqualTo(1));
            Assert.That(trigger, Is.Not.Null);
            Assert.That(trigger.GetComponent<Collider>().isTrigger, Is.True);
            Assert.That(prompt, Is.Not.Null);
            Assert.That(prompt.activeSelf, Is.False);
        }

        [Test]
        public void HubPlayerAndCameraAreWired()
        {
            FrontendSceneBuilder.BuildOrUpdateAll();
            EditorSceneManager.OpenScene(FrontendSceneBuilder.DepartureHubScenePath, OpenSceneMode.Single);

            var player = GameObject.Find("Player");
            var camera = GameObject.Find("Main Camera");
            var follow = camera.GetComponent<QuarterViewCamera>();

            Assert.That(player.GetComponent<CharacterController>(), Is.Not.Null);
            Assert.That(player.GetComponent<HubPlayerController>(), Is.Not.Null);
            Assert.That(follow, Is.Not.Null);
            Assert.That(follow.Target, Is.EqualTo(player.transform));
            Assert.That(camera.GetComponent<Camera>().orthographic, Is.True);
        }

        [Test]
        public void HubCameraUsesModerateDiagonalQuarterViewAngle()
        {
            FrontendSceneBuilder.BuildOrUpdateAll();
            EditorSceneManager.OpenScene(FrontendSceneBuilder.DepartureHubScenePath, OpenSceneMode.Single);

            var follow = GameObject.Find("Main Camera").GetComponent<QuarterViewCamera>();
            var downwardAngle = Mathf.Atan2(follow.Offset.y, Mathf.Abs(follow.Offset.z)) * Mathf.Rad2Deg;

            Assert.That(downwardAngle, Is.InRange(43f, 47f));
            Assert.That(Mathf.Abs(follow.Offset.z), Is.EqualTo(follow.Offset.y).Within(0.1f));
        }

        [Test]
        public void HubCameraPersistedSceneUsesCloseActionFraming()
        {
            EditorSceneManager.OpenScene(FrontendSceneBuilder.DepartureHubScenePath, OpenSceneMode.Single);

            var cameraObject = GameObject.Find("Main Camera");
            var camera = cameraObject.GetComponent<Camera>();
            var follow = cameraObject.GetComponent<QuarterViewCamera>();
            var focusOffset = new SerializedObject(follow).FindProperty("focusOffset");

            Assert.That(camera.orthographicSize, Is.EqualTo(9f).Within(0.001f));
            Assert.That(focusOffset, Is.Not.Null);
            Assert.That((focusOffset.vector3Value - new Vector3(0f, 0f, 3f)).sqrMagnitude, Is.LessThan(0.0001f));
        }

        [TestCase(4f / 3f)]
        [TestCase(16f / 9f)]
        [TestCase(21f / 9f)]
        public void HubCameraFramesPlayerProminentlyBelowCenter(float aspect)
        {
            FrontendSceneBuilder.BuildOrUpdateAll();
            EditorSceneManager.OpenScene(FrontendSceneBuilder.DepartureHubScenePath, OpenSceneMode.Single);

            var player = GameObject.Find("Player");
            var camera = GameObject.Find("Main Camera").GetComponent<Camera>();
            camera.aspect = aspect;

            var playerViewportHeight = ViewportHeight(camera, player.GetComponent<Renderer>().bounds);
            var playerViewportY = camera.WorldToViewportPoint(player.transform.position).y;

            Assert.That(playerViewportHeight, Is.InRange(0.10f, 0.12f));
            Assert.That(playerViewportY, Is.InRange(0.35f, 0.45f));
        }

        [Test]
        public void HubCameraPreservesFramingWhenTargetMoves()
        {
            var target = new GameObject("Target");
            var cameraObject = new GameObject("Camera", typeof(Camera), typeof(QuarterViewCamera));
            var follow = cameraObject.GetComponent<QuarterViewCamera>();
            var serializedFollow = new SerializedObject(follow);
            var focusOffset = serializedFollow.FindProperty("focusOffset");
            Assert.That(focusOffset, Is.Not.Null);

            focusOffset.vector3Value = new Vector3(0f, 0f, 3f);
            serializedFollow.ApplyModifiedPropertiesWithoutUndo();
            follow.Target = target.transform;
            follow.Offset = new Vector3(0f, 16f, -16f);
            follow.SnapToTarget();

            var before = cameraObject.transform.position;
            var movement = new Vector3(2.5f, 0f, 4f);
            target.transform.position += movement;
            follow.SnapToTarget();

            Assert.That((cameraObject.transform.position - before - movement).sqrMagnitude, Is.LessThan(0.0001f));
        }

        [Test]
        public void HubCameraDefaultsToZeroFocusOffsetAndIgnoresNullTarget()
        {
            var cameraObject = new GameObject("Camera", typeof(QuarterViewCamera));
            var follow = cameraObject.GetComponent<QuarterViewCamera>();
            var focusOffset = new SerializedObject(follow).FindProperty("focusOffset");
            Assert.That(focusOffset, Is.Not.Null);
            Assert.That(focusOffset.vector3Value.sqrMagnitude, Is.LessThan(0.0001f));

            cameraObject.transform.SetPositionAndRotation(new Vector3(3f, 5f, 7f), Quaternion.Euler(10f, 20f, 30f));
            var positionBefore = cameraObject.transform.position;
            var rotationBefore = cameraObject.transform.rotation;
            follow.SnapToTarget();

            Assert.That((cameraObject.transform.position - positionBefore).sqrMagnitude, Is.LessThan(0.0001f));
            Assert.That(Quaternion.Angle(cameraObject.transform.rotation, rotationBefore), Is.LessThan(0.001f));
        }

        [Test]
        public void RepeatedBuildKeepsStableRequiredRootsAndBuildSettings()
        {
            FrontendSceneBuilder.BuildOrUpdateAll();
            FrontendSceneBuilder.BuildOrUpdateAll();

            var paths = EditorBuildSettings.scenes.Select(item => item.path).ToArray();
            Assert.That(paths[0], Is.EqualTo(FrontendSceneBuilder.FrontendScenePath));
            Assert.That(paths[1], Is.EqualTo(FrontendSceneBuilder.DepartureHubScenePath));
            Assert.That(paths.Count(path => path == FrontendSceneBuilder.FrontendScenePath), Is.EqualTo(1));
            Assert.That(paths.Count(path => path == FrontendSceneBuilder.DepartureHubScenePath), Is.EqualTo(1));

            var frontend = EditorSceneManager.OpenScene(FrontendSceneBuilder.FrontendScenePath, OpenSceneMode.Single);
            Assert.That(frontend.GetRootGameObjects().Count(root => root.name == "Frontend"), Is.EqualTo(1));
            Assert.That(frontend.GetRootGameObjects().Count(root => root.name == "EventSystem"), Is.EqualTo(1));

            var hub = EditorSceneManager.OpenScene(FrontendSceneBuilder.DepartureHubScenePath, OpenSceneMode.Single);
            Assert.That(hub.GetRootGameObjects().Count(root => root.name == "Departure Hub"), Is.EqualTo(1));
            Assert.That(hub.GetRootGameObjects().Count(root => root.name == "Player"), Is.EqualTo(1));
            Assert.That(hub.GetRootGameObjects().Count(root => root.name == "Main Camera"), Is.EqualTo(1));
        }

        private static GameObject FindRoot(Scene scene, string name)
        {
            return scene.GetRootGameObjects().SingleOrDefault(root => root.name == name);
        }

        private static float ViewportHeight(Camera camera, Bounds bounds)
        {
            var minY = float.PositiveInfinity;
            var maxY = float.NegativeInfinity;

            for (var x = -1; x <= 1; x += 2)
            {
                for (var y = -1; y <= 1; y += 2)
                {
                    for (var z = -1; z <= 1; z += 2)
                    {
                        var corner = bounds.center + Vector3.Scale(bounds.extents, new Vector3(x, y, z));
                        var viewportY = camera.WorldToViewportPoint(corner).y;
                        minY = Mathf.Min(minY, viewportY);
                        maxY = Mathf.Max(maxY, viewportY);
                    }
                }
            }

            return maxY - minY;
        }
    }
}
