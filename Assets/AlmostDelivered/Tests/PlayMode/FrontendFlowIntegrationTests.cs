using System.Collections;
using System.Linq;
using AlmostDelivered.Combat;
using AlmostDelivered.Frontend;
using AlmostDelivered.Gameplay;
using AlmostDelivered.Gameplay.Payload;
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
            PlayerWeaponLoadout.Select(PlayerWeaponType.Ranged);
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

        [UnityTest]
        public IEnumerator DeploymentGateRequestLoadsPlayableMission()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(HubPath, new LoadSceneParameters(LoadSceneMode.Single));

            var gate = Object.FindObjectOfType<HubDeploymentGate>();
            var player = GameObject.Find("Player");
            var controller = player.GetComponent<CharacterController>();
            controller.enabled = false;
            player.transform.position = gate.transform.position;
            controller.enabled = true;
            controller.Move(Vector3.zero);
            Physics.SyncTransforms();
            yield return new WaitForFixedUpdate();

            Assert.That(gate.CanLaunch, Is.True);
            gate.RequestLaunch();
            yield return WaitForScene(GameSceneFlow.MissionPrototypeScene);

            Assert.That(GameObject.Find("Player").GetComponent<MissionPlayerController>(), Is.Not.Null);
            Assert.That(GameObject.Find("Payload"), Is.Not.Null);
            Assert.That(Object.FindObjectsOfType<PrototypeBossController>(), Is.Empty);
            Assert.That(Object.FindObjectsOfType<PayloadWaveSpawner>(), Has.Length.EqualTo(1));
            var mission = Object.FindObjectOfType<PayloadMissionController>();
            Assert.That(mission, Is.Not.Null);
            Assert.That(mission.IsAuthority, Is.True);
        }

        [UnityTest]
        public IEnumerator HubWeaponSelectionCarriesIntoMissionCombat()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(HubPath,
                new LoadSceneParameters(LoadSceneMode.Single));
            var player = GameObject.Find("Player");
            var controller = player.GetComponent<CharacterController>();
            var station = Object.FindObjectOfType<HubWeaponStation>();
            Assert.That(station, Is.Not.Null);

            controller.enabled = false;
            player.transform.position = station.transform.position;
            controller.enabled = true;
            controller.Move(Vector3.zero);
            Physics.SyncTransforms();
            yield return new WaitForFixedUpdate();
            Assert.That(station.CanSelect, Is.True);
            Assert.That(station.OpenMenu(), Is.True);
            Assert.That(player.GetComponent<HubPlayerController>().IsInputLocked, Is.True);
            GameObject.Find("Melee Weapon Card").GetComponent<Button>().onClick.Invoke();
            Assert.That(PlayerWeaponLoadout.Selected, Is.EqualTo(PlayerWeaponType.Melee));
            station.CloseMenu();
            Assert.That(player.GetComponent<HubPlayerController>().IsInputLocked, Is.False);

            var gate = Object.FindObjectOfType<HubDeploymentGate>();
            controller.enabled = false;
            player.transform.position = gate.transform.position;
            controller.enabled = true;
            controller.Move(Vector3.zero);
            Physics.SyncTransforms();
            yield return new WaitForFixedUpdate();
            Assert.That(gate.CanLaunch, Is.True);
            gate.RequestLaunch();
            yield return WaitForScene(GameSceneFlow.MissionPrototypeScene);
            yield return null;

            var missionPlayer = GameObject.Find("Player");
            Assert.That(missionPlayer.GetComponent<MissionPlayerController>().WeaponType,
                Is.EqualTo(PlayerWeaponType.Melee));
            Assert.That(missionPlayer.GetComponent<MeleeAttackController>().enabled, Is.True);
            Assert.That(missionPlayer.GetComponent<RangedAttackController>().enabled, Is.False);
        }

        [UnityTest]
        public IEnumerator RifleAmmoAndIconStayConsistentAfterDeployment()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(HubPath,
                new LoadSceneParameters(LoadSceneMode.Single));
            PlayerWeaponLoadout.Select(PlayerWeaponType.AutomaticRifle);
            var player = GameObject.Find("Player");
            player.GetComponent<HubPlayerController>().SetWeaponType(PlayerWeaponType.AutomaticRifle);
            yield return null;
            AssertRifleHud("20/20");

            var gate = Object.FindObjectOfType<HubDeploymentGate>();
            var controller = player.GetComponent<CharacterController>();
            controller.enabled = false;
            player.transform.position = gate.transform.position;
            controller.enabled = true;
            controller.Move(Vector3.zero);
            Physics.SyncTransforms();
            yield return new WaitForFixedUpdate();
            Assert.That(gate.CanLaunch, Is.True);
            gate.RequestLaunch();
            yield return WaitForScene(GameSceneFlow.MissionPrototypeScene);
            yield return null;
            AssertRifleHud("20/20");

            var missionPlayer = GameObject.Find("Player");
            var attack = missionPlayer.GetComponent<RangedAttackController>();
            Assert.That(attack.Muzzle,
                Is.EqualTo(PlayerWeaponLoadout.FindMuzzle(missionPlayer, PlayerWeaponType.AutomaticRifle)));
            var feedback = missionPlayer.GetComponent<AlmostDelivered.Presentation.GunfireFeedback>();
            Assert.That(feedback, Is.Not.Null);
            Assert.That(feedback.IsFlashing, Is.False);
            Assert.That(attack.TryFire(missionPlayer.transform.position + Vector3.forward * 10f, Time.time), Is.True);
            Assert.That(feedback.IsFlashing, Is.True);
            Object.FindObjectOfType<PlayerAmmoHud>().Refresh();
            AssertRifleHud("19/20");
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That(feedback.IsFlashing, Is.False);
        }

        private static void AssertRifleHud(string rounds)
        {
            var hud = Object.FindObjectOfType<PlayerAmmoHud>();
            Assert.That(hud, Is.Not.Null);
            var rifle = hud.transform.Find("Automatic Rifle Ammo");
            Assert.That(rifle.gameObject.activeSelf, Is.True);
            Assert.That(rifle.Find("Rifle Muzzle"), Is.Not.Null);
            var count = rifle.Find("Rifle Ammo Count").GetComponent<Text>();
            Assert.That(count.text, Is.EqualTo(rounds));
            Assert.That(count.verticalOverflow, Is.EqualTo(VerticalWrapMode.Overflow));
        }

        [UnityTest]
        public IEnumerator HubPlayerCanFireAndSlashBeforeDeployment()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(HubPath,
                new LoadSceneParameters(LoadSceneMode.Single));
            var player = GameObject.Find("Player");
            var hubController = player.GetComponent<HubPlayerController>();
            var aimPoint = player.transform.position + Vector3.forward * 5f;

            PlayerWeaponLoadout.Select(PlayerWeaponType.Ranged);
            hubController.SetWeaponType(PlayerWeaponType.Ranged);
            Assert.That(hubController.AimAndFire(aimPoint, Time.time, .1f), Is.True);
            Assert.That(GameObject.Find("Hub Projectile Template (Shot)"), Is.Not.Null);

            PlayerWeaponLoadout.Select(PlayerWeaponType.Melee);
            hubController.SetWeaponType(PlayerWeaponType.Melee);
            Assert.That(player.GetComponent<RangedAttackController>().enabled, Is.False);
            Assert.That(player.GetComponent<MeleeAttackController>().enabled, Is.True);
            Assert.That(hubController.AimAndFire(aimPoint, Time.time, .1f), Is.True);
            Assert.That(player.GetComponent<AlmostDelivered.Presentation.MeleeSlashVfx>().IsVisible, Is.True);
            var slash = player.GetComponent<AlmostDelivered.Presentation.MeleeSlashVfx>();
            Assert.That(new UnityEditor.SerializedObject(slash).FindProperty("slashSound").objectReferenceValue,
                Is.Not.Null);

            hubController.SetInputLocked(true);
            Assert.That(hubController.AimAndFire(aimPoint, Time.time + 1f, .1f), Is.False);
        }

        [UnityTest]
        public IEnumerator EnteringWeaponStationKeepsAmmoAndShotInterval()
        {
            PlayerWeaponLoadout.Select(PlayerWeaponType.AutomaticRifle);
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(HubPath,
                new LoadSceneParameters(LoadSceneMode.Single));
            var player = GameObject.Find("Player");
            var attack = player.GetComponent<RangedAttackController>();
            var station = Object.FindObjectOfType<HubWeaponStation>();
            var controller = player.GetComponent<CharacterController>();
            var now = Time.time + 1f;
            Assert.That(attack.TryFire(player.transform.position + Vector3.forward * 10f, now), Is.True);
            Assert.That(attack.RoundsRemaining, Is.EqualTo(19));

            controller.enabled = false;
            player.transform.position = station.transform.position;
            controller.enabled = true;
            Physics.SyncTransforms();
            station.SendMessage("OnTriggerEnter", controller);
            Assert.That(station.CanSelect, Is.True);
            Assert.That(attack.RoundsRemaining, Is.EqualTo(19));
            Assert.That(attack.TryFire(player.transform.position + Vector3.forward * 10f, now + .05f), Is.False);

            Assert.That(station.OpenMenu(), Is.True);
            Assert.That(station.Select(PlayerWeaponType.AutomaticRifle), Is.True);
            Assert.That(attack.RoundsRemaining, Is.EqualTo(19));
            Assert.That(attack.TryFire(player.transform.position + Vector3.forward * 10f, now + .05f), Is.False);
            station.CloseMenu();
            Assert.That(attack.TryFire(player.transform.position + Vector3.forward * 10f, now + .12f), Is.True);
        }

        [UnityTest]
        public IEnumerator GunshotsStartAtSelectedWeaponBarrelAndMoveGun()
        {
            PlayerWeaponLoadout.Select(PlayerWeaponType.AutomaticRifle);
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(HubPath,
                new LoadSceneParameters(LoadSceneMode.Single));
            var player = GameObject.Find("Player");
            var attack = player.GetComponent<RangedAttackController>();
            var controller = player.GetComponent<HubPlayerController>();
            var gunPivot = player.transform.Find("VisualRoot/BodyPivot/RightArmPivot/Weapon");
            var neutralPosition = gunPivot.localPosition;
            var rifleMuzzle = PlayerWeaponLoadout.FindMuzzle(player, PlayerWeaponType.AutomaticRifle);
            Assert.That(rifleMuzzle.parent.localPosition.y, Is.EqualTo(.12f).Within(.001f));
            Assert.That(attack.Muzzle, Is.EqualTo(rifleMuzzle));
            Assert.That(attack.TryFire(player.transform.position + Vector3.forward * 10f, Time.time + 1f), Is.True);
            var rifleShot = GameObject.Find("Hub Projectile Template (Shot)");
            Assert.That(rifleShot.transform.position, Is.EqualTo(rifleMuzzle.position));
            Assert.That(rifleMuzzle.Find("Muzzle Flash"), Is.Not.Null);
            Assert.That(rifleMuzzle.Find("Muzzle Flash").localPosition.z, Is.GreaterThan(.25f));
            var largestRecoil = 0f;
            for (var frame = 0; frame < 6; frame++)
            {
                yield return null;
                largestRecoil = Mathf.Max(largestRecoil,
                    Vector3.Distance(gunPivot.localPosition, neutralPosition));
            }
            Assert.That(largestRecoil, Is.GreaterThan(.005f));

            Object.DestroyImmediate(rifleShot);
            PlayerWeaponLoadout.Select(PlayerWeaponType.Revolver);
            controller.SetWeaponType(PlayerWeaponType.Revolver);
            var revolverMuzzle = PlayerWeaponLoadout.FindMuzzle(player, PlayerWeaponType.Revolver);
            Assert.That(attack.Muzzle, Is.EqualTo(revolverMuzzle));
            Assert.That(attack.TryFire(player.transform.position + Vector3.forward * 10f, Time.time + 1f), Is.True);
            var revolverShot = GameObject.Find("Hub Projectile Template (Shot)");
            Assert.That(revolverShot.transform.position, Is.EqualTo(revolverMuzzle.position));
            Assert.That(revolverMuzzle.Find("Muzzle Flash"), Is.Not.Null);
            Assert.That(revolverMuzzle.Find("Muzzle Flash").localPosition.z, Is.GreaterThan(.25f));
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
