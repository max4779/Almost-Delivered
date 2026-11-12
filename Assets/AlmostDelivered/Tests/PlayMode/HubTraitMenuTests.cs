using System.Collections;
using AlmostDelivered.Frontend;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace AlmostDelivered.Tests.PlayMode
{
    public sealed class HubTraitMenuTests
    {
        private sealed class MemoryStore : IPlayerTraitStore
        {
            private PlayerTraitProgress saved = new PlayerTraitProgress();
            public PlayerTraitProgress Load(string accountId) => saved.Copy();
            public void Save(string accountId, PlayerTraitProgress progress) => saved = progress.Copy();
        }

        [UnityTest]
        public IEnumerator CrewAreaOpensBottomUpTraitsAndTerminalHasItsOwnInteraction()
        {
            var previous = SceneManager.GetActiveScene();
            yield return SceneManager.LoadSceneAsync(GameSceneFlow.DepartureHubScene, LoadSceneMode.Additive);
            var hub = SceneManager.GetSceneByName(GameSceneFlow.DepartureHubScene);
            SceneManager.SetActiveScene(hub);
            yield return null;
            GameObject remote = null;
            try
            {
                var player = Object.FindObjectOfType<HubPlayerController>();
                var menu = Object.FindObjectOfType<HubTraitMenu>();
                Assert.That(player, Is.Not.Null);
                Assert.That(menu, Is.Not.Null);
                var profile = player.GetComponent<PlayerTraitProfile>();
                Assert.That(profile, Is.Not.Null);
                Assert.That(profile.AccountId, Is.Not.Empty);
                Assert.That(profile.IsLocalOwner, Is.True);
                var floors = new[] { "Gate Zone Floor", "Equipment Zone Floor",
                    "Terminal Zone Floor", "Crew Zone Floor" };
                var triggers = new[] { "Deployment Gate Trigger", "Weapon Station Trigger",
                    "Mission Terminal Trigger", "Crew Trait Trigger" };
                for (var area = 0; area < floors.Length; area++)
                {
                    var floor = GameObject.Find(floors[area]);
                    var trigger = GameObject.Find(triggers[area]);
                    Assert.That(floor, Is.Not.Null);
                    Assert.That(trigger, Is.Not.Null);
                    Assert.That(floor.GetComponent<Collider>(), Is.Null);
                    Assert.That(floor.transform.position.y, Is.EqualTo(.025f).Within(.001f));
                    var colorBounds = floor.GetComponent<Renderer>().bounds;
                    var interactionBounds = trigger.GetComponent<Collider>().bounds;
                    Assert.That(colorBounds.min.x, Is.EqualTo(interactionBounds.min.x).Within(.01f));
                    Assert.That(colorBounds.max.x, Is.EqualTo(interactionBounds.max.x).Within(.01f));
                    Assert.That(colorBounds.min.z, Is.EqualTo(interactionBounds.min.z).Within(.01f));
                    Assert.That(colorBounds.max.z, Is.EqualTo(interactionBounds.max.z).Within(.01f));
                }
                profile.BindLocalAccount("hub-ui-test", new MemoryStore());
                Assert.That(GameObject.Find("Open Traits"), Is.Null);
                menu.Open();
                Assert.That(menu.IsOpen, Is.False);
                for (var step = 0; step < 15; step++)
                {
                    player.Move(Vector2.right, .1f);
                    yield return new WaitForFixedUpdate();
                }
                var crew = GameObject.Find("Crew Trait Trigger").GetComponent<HubAreaStation>();
                Assert.That(crew.CanInteract, Is.True);
                remote = new GameObject("Remote Test Player", typeof(CharacterController),
                    typeof(HubPlayerController), typeof(PlayerTraitProfile));
                remote.transform.position = new Vector3(4f, 1f, -7.2f);
                remote.GetComponent<PlayerTraitProfile>().BindRemoteAccount("remote-test", new PlayerTraitProgress());
                yield return null;
                remote.GetComponent<HubPlayerController>().Move(Vector2.right, .5f);
                yield return new WaitForFixedUpdate();
                Assert.That(remote.GetComponent<PlayerTraitProfile>().IsLocalOwner, Is.False);
                Assert.That(crew.CanInteract, Is.True,
                    "A remote player entering the same area must not replace the local player.");
                Assert.That(crew.Open(), Is.True);
                Assert.That(menu.IsOpen, Is.True);
                Assert.That(player.IsInputLocked, Is.True);
                Assert.That(profile.Progress.AvailablePoints, Is.EqualTo(1));
                Assert.That(profile.Progress.CanInvest("combat.counter"), Is.False);
                menu.Invest0();
                Assert.That(profile.Progress.Rank("combat.cargo_fire"), Is.EqualTo(1));
                Assert.That(profile.Progress.AvailablePoints, Is.Zero);
                Assert.That(profile.Progress.CanInvest("combat.counter"), Is.False);
                menu.ResetTraits();
                Assert.That(profile.Progress.Rank("combat.cargo_fire"), Is.Zero);
                menu.Close();
                Assert.That(menu.IsOpen, Is.False);
                Assert.That(player.IsInputLocked, Is.False);

                for (var step = 0; step < 36; step++)
                {
                    player.Move(new Vector2(-1f, .426f), .1f);
                    yield return new WaitForFixedUpdate();
                }
                var terminal = GameObject.Find("Mission Terminal Trigger").GetComponent<HubAreaStation>();
                Assert.That(terminal.CanInteract, Is.True);
                Assert.That(terminal.Open(), Is.True);
                Assert.That(terminal.IsMenuOpen, Is.True);
                terminal.Close();
                Assert.That(player.IsInputLocked, Is.False);
            }
            finally
            {
                if (remote != null) Object.DestroyImmediate(remote);
                SceneManager.SetActiveScene(previous);
            }
            yield return SceneManager.UnloadSceneAsync(hub);
        }
    }
}
