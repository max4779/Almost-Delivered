using System.Collections;
using System.Linq;
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
            Assert.That(
                GameObject.Find("Main Camera").transform.position,
                Is.EqualTo(player.transform.position + follow.FocusOffset + follow.Offset));
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

        [UnityTest]
        public IEnumerator DeploymentGateTracksPlayerAndPrompt()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(HubPath, new LoadSceneParameters(LoadSceneMode.Single));

            var gate = Object.FindObjectOfType<HubDeploymentGate>();
            Assert.That(gate, Is.Not.Null);
            var prompt = SceneManager.GetActiveScene().GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .Select(transform => transform.gameObject)
                .FirstOrDefault(candidate => candidate.name == "Deployment Prompt");
            Assert.That(prompt, Is.Not.Null);
            Assert.That(gate.CanLaunch, Is.False, "The gate must start empty before the player enters it.");
            Assert.That(prompt.activeSelf, Is.False, "The prompt must start hidden before the player enters the gate.");

            var player = GameObject.Find("Player");
            var controller = player.GetComponent<CharacterController>();
            controller.enabled = false;
            player.transform.position = gate.transform.position;
            controller.enabled = true;
            controller.Move(Vector3.zero);
            Physics.SyncTransforms();
            yield return new WaitForFixedUpdate();

            Assert.That(gate.CanLaunch, Is.True, "Entering the trigger must arm the deployment gate.");
            Assert.That(prompt.activeSelf, Is.True, "Entering the trigger must reveal the deployment prompt.");

            controller.Move(Vector3.back * 8f);
            Physics.SyncTransforms();
            yield return new WaitForFixedUpdate();

            Assert.That(gate.CanLaunch, Is.False, "Leaving the trigger must disarm the deployment gate.");
            Assert.That(prompt.activeSelf, Is.False, "Leaving the trigger must hide the deployment prompt.");
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
