using System.Collections;
using AlmostDelivered.Frontend;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace AlmostDelivered.Tests.PlayMode
{
    public sealed class FrontendFlowIntegrationTests
    {
        private const string FrontendPath = "Assets/AlmostDelivered/Scenes/Frontend.unity";
        private const string HubPath = "Assets/AlmostDelivered/Scenes/DepartureHub.unity";

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            var loaded = SceneManager.GetActiveScene();
            var empty = SceneManager.CreateScene("Frontend flow cleanup");
            SceneManager.SetActiveScene(empty);
            if (loaded.IsValid() && loaded.isLoaded)
            {
                yield return SceneManager.UnloadSceneAsync(loaded);
            }
        }

        [UnityTest]
        public IEnumerator StartButtonLoadsWalkableHubWithTrackingCamera()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(FrontendPath, new LoadSceneParameters(LoadSceneMode.Single));
            var startButton = GameObject.Find("Start Operation").GetComponent<Button>();

            startButton.onClick.Invoke();
            yield return WaitForScene(GameSceneFlow.DepartureHubScene);

            var player = GameObject.Find("Player");
            var movement = player.GetComponent<HubPlayerController>();
            var controller = player.GetComponent<CharacterController>();
            var follow = GameObject.Find("Main Camera").GetComponent<QuarterViewCamera>();
            Assert.That(controller.enabled, Is.True);
            var floorProbe = player.transform.position + (Vector3.right * (controller.radius + .1f)) + Vector3.up;
            Assert.That(Physics.Raycast(floorProbe, Vector3.down, out var hit, 3f), Is.True);
            Assert.That(hit.collider.transform.IsChildOf(GameObject.Find("Departure Hub").transform), Is.True);

            var start = player.transform.position;
            movement.Move(Vector2.right, .25f);
            Physics.SyncTransforms();
            yield return null;

            Assert.That(player.transform.position.x, Is.GreaterThan(start.x + 1f));
            Assert.That(follow.Target, Is.EqualTo(player.transform));
            Assert.That(GameObject.Find("Main Camera").transform.position, Is.EqualTo(player.transform.position + follow.Offset));
        }

        [UnityTest]
        public IEnumerator ReturnToFrontendLoadsMenuScene()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(HubPath, new LoadSceneParameters(LoadSceneMode.Single));
            var controller = GameObject.Find("Player").GetComponent<HubPlayerController>();

            controller.ReturnToFrontend();
            yield return WaitForScene(GameSceneFlow.FrontendScene);

            Assert.That(GameObject.Find("Start Operation").GetComponent<Button>().interactable, Is.True);
        }

        private static IEnumerator WaitForScene(string sceneName)
        {
            var framesRemaining = 120;
            while (SceneManager.GetActiveScene().name != sceneName && framesRemaining-- > 0)
            {
                yield return null;
            }

            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(sceneName));
        }
    }
}
