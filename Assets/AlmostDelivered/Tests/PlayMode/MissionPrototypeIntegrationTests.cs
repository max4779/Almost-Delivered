using System.Collections;
using AlmostDelivered.Combat;
using AlmostDelivered.Frontend;
using AlmostDelivered.Gameplay;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace AlmostDelivered.Tests.PlayMode
{
    public sealed class MissionPrototypeIntegrationTests
    {
        private const string MissionPath = "Assets/AlmostDelivered/Scenes/MissionPrototype.unity";

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            var loaded = SceneManager.GetActiveScene();
            var empty = SceneManager.CreateScene("Mission prototype cleanup");
            SceneManager.SetActiveScene(empty);
            if (loaded.IsValid() && loaded.isLoaded)
                yield return SceneManager.UnloadSceneAsync(loaded);
        }

        [UnityTest]
        public IEnumerator PlayerMovementUpdatesTrackingCamera()
        {
            yield return LoadMission();
            var player = GameObject.Find("Player");
            var controller = player.GetComponent<MissionPlayerController>();
            var follow = GameObject.Find("Main Camera").GetComponent<QuarterViewCamera>();
            foreach (var boss in Object.FindObjectsOfType<PrototypeBossController>())
                boss.enabled = false;

            var playerStart = player.transform.position;
            var cameraStart = follow.transform.position;
            Assert.That(controller.Move(Vector2.right, .25f), Is.True);
            yield return null;

            var playerDelta = player.transform.position - playerStart;
            var cameraDelta = follow.transform.position - cameraStart;
            Assert.That(playerDelta.x, Is.GreaterThan(1f));
            Assert.That((cameraDelta - playerDelta).sqrMagnitude, Is.LessThan(.0001f));
        }

        [UnityTest]
        public IEnumerator PlayerProjectileDamagesWarden()
        {
            yield return LoadMission();
            var player = GameObject.Find("Player").GetComponent<MissionPlayerController>();
            var warden = GameObject.Find("Warden Boss");
            var wardenHealth = warden.GetComponent<ActorHealth>();
            foreach (var boss in Object.FindObjectsOfType<PrototypeBossController>())
                boss.enabled = false;

            warden.transform.position = player.transform.position + Vector3.forward * 6f;
            var healthBefore = wardenHealth.CurrentHealth;
            Assert.That(player.AimAndFire(warden.transform.position, Time.time + 1f), Is.True);
            Physics.SyncTransforms();
            for (var step = 0; step < 90 && wardenHealth.CurrentHealth >= healthBefore; step++)
                yield return new WaitForFixedUpdate();

            Assert.That(wardenHealth.CurrentHealth, Is.LessThan(healthBefore));
        }

        [UnityTest]
        public IEnumerator BossContactAndHudReflectMissionState()
        {
            yield return LoadMission();
            var player = GameObject.Find("Player");
            var playerHealth = player.GetComponent<ActorHealth>();
            var charger = GameObject.Find("Charger Boss").GetComponent<PrototypeBossController>();
            var warden = GameObject.Find("Warden Boss");
            var hud = Object.FindObjectOfType<MissionHud>();

            warden.GetComponent<PrototypeBossController>().enabled = false;
            charger.transform.position = player.transform.position + Vector3.right;
            Physics.SyncTransforms();
            var playerHealthBefore = playerHealth.CurrentHealth;
            charger.Tick(Time.time + 10f, 0f);

            Assert.That(playerHealth.CurrentHealth, Is.LessThan(playerHealthBefore));
            Assert.That(hud.RemainingBosses, Is.EqualTo(2));

            warden.GetComponent<ActorHealth>().ApplyDamage(float.MaxValue);
            hud.Refresh();

            Assert.That(hud.RemainingBosses, Is.EqualTo(1));
        }

        private static IEnumerator LoadMission()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(MissionPath, new LoadSceneParameters(LoadSceneMode.Single));
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(GameSceneFlow.MissionPrototypeScene));
        }
    }
}
