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
    }
}
