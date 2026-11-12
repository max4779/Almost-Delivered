using AlmostDelivered.Frontend;
using NUnit.Framework;
using UnityEngine;

namespace AlmostDelivered.Tests.PlayMode
{
    public sealed class HubWeaponStationTests
    {
        private sealed class MemoryStore : IPlayerTraitStore
        {
            public PlayerTraitProgress Load(string accountId) => new PlayerTraitProgress();
            public void Save(string accountId, PlayerTraitProgress progress) { }
        }

        private GameObject player;
        private GameObject stationObject;
        private GameObject promptObject;
        private GameObject labelObject;
        private GameObject menuObject;

        [TearDown]
        public void TearDown()
        {
            PlayerWeaponLoadout.Select(PlayerWeaponType.Ranged);
            if (player != null) Object.DestroyImmediate(player);
            if (stationObject != null) Object.DestroyImmediate(stationObject);
            if (promptObject != null) Object.DestroyImmediate(promptObject);
            if (labelObject != null) Object.DestroyImmediate(labelObject);
            if (menuObject != null) Object.DestroyImmediate(menuObject);
        }

        [Test]
        public void StationChangesWeaponOnlyWhenPlayerIsNearby()
        {
            player = new GameObject("Player", typeof(CharacterController), typeof(HubPlayerController));
            player.AddComponent<PlayerTraitProfile>().BindLocalAccount("test-local", new MemoryStore());
            var visual = new GameObject("VisualRoot").transform;
            visual.SetParent(player.transform, false);
            var ranged = new GameObject("RangedWeapon").transform;
            ranged.SetParent(visual, false);
            var melee = new GameObject("MeleeWeapon").transform;
            melee.SetParent(visual, false);
            melee.gameObject.SetActive(false);
            stationObject = new GameObject("Station", typeof(BoxCollider), typeof(HubWeaponStation));
            promptObject = new GameObject("Prompt", typeof(TextMesh));
            labelObject = new GameObject("Selection", typeof(TextMesh));
            menuObject = new GameObject("Weapon Menu");
            var station = stationObject.GetComponent<HubWeaponStation>();
            station.Configure(promptObject.GetComponent<TextMesh>(), labelObject.GetComponent<TextMesh>());
            station.ConfigureMenu(menuObject, null, null, null);

            Assert.That(station.Select(PlayerWeaponType.Melee), Is.False);
            stationObject.SendMessage("OnTriggerEnter", player.GetComponent<CharacterController>());
            Assert.That(station.CanSelect, Is.True);
            Assert.That(station.OpenMenu(), Is.True);
            Assert.That(player.GetComponent<HubPlayerController>().IsInputLocked, Is.True);
            Assert.That(station.Select(PlayerWeaponType.Melee), Is.True);
            Assert.That(PlayerWeaponLoadout.Selected, Is.EqualTo(PlayerWeaponType.Melee));
            Assert.That(ranged.gameObject.activeSelf, Is.False);
            Assert.That(melee.gameObject.activeSelf, Is.True);
            Assert.That(labelObject.GetComponent<TextMesh>().text, Does.Contain("근접"));
            station.CloseMenu();
            Assert.That(station.IsMenuOpen, Is.False);
            Assert.That(player.GetComponent<HubPlayerController>().IsInputLocked, Is.False);
            Assert.That(station.Select(PlayerWeaponType.Ranged), Is.False);
            stationObject.SendMessage("OnTriggerExit", player.GetComponent<CharacterController>());
            Assert.That(station.CanSelect, Is.False);
        }

        [Test]
        public void SelectionRequiresPlayerCenterInsideColoredFloorBounds()
        {
            player = new GameObject("Player", typeof(CharacterController), typeof(HubPlayerController));
            player.AddComponent<PlayerTraitProfile>().BindLocalAccount("test-local", new MemoryStore());
            stationObject = new GameObject("Station", typeof(BoxCollider), typeof(HubWeaponStation));
            stationObject.transform.position = new Vector3(11.5f, 1.5f, .8f);
            stationObject.GetComponent<BoxCollider>().size = new Vector3(6.5f, 3f, 7f);
            var station = stationObject.GetComponent<HubWeaponStation>();
            var controller = player.GetComponent<CharacterController>();

            player.transform.position = new Vector3(8.3f, 1f, .8f);
            Physics.SyncTransforms();
            stationObject.SendMessage("OnTriggerEnter", controller);
            Assert.That(station.CanSelect, Is.True);

            player.transform.position = new Vector3(8.2f, 1f, .8f);
            Physics.SyncTransforms();
            Assert.That(station.CanSelect, Is.False);
        }
    }
}
