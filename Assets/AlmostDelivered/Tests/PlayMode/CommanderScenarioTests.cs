using System.Collections;
using System.Linq;
using AlmostDelivered.Combat;
using AlmostDelivered.CommanderAI.Debugging;
using AlmostDelivered.CommanderAI.Domain;
using AlmostDelivered.CommanderAI.Perception;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace AlmostDelivered.Tests.PlayMode
{
    public sealed class CommanderScenarioTests
    {
        private CommanderArena arena;
        private float now;
        [UnitySetUp] public IEnumerator LoadArena()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/AlmostDelivered/Scenes/CommanderAITest.unity", new LoadSceneParameters(LoadSceneMode.Single));
            arena = Object.FindObjectOfType<CommanderArena>();
            Assert.That(arena, Is.Not.Null);
            arena.Brain.enabled = false;
            foreach (var member in arena.Members)
            {
                var perception = member.GetComponent<MonsterPerception>();
                perception.SensingInterval = 1000;
                perception.Configure(arena.Squad, System.Array.IndexOf(arena.Members, member) + 1);
                member.GetComponent<AlertSignal>().Cancel();
            }
            now = Time.unscaledTime;
            Assert.That(arena.Squad.State, Is.EqualTo(SquadAlertState.Unaware));
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            var empty = SceneManager.CreateScene("Scenario cleanup");
            SceneManager.SetActiveScene(empty);
            yield return SceneManager.UnloadSceneAsync(arena.gameObject.scene);
        }
        private IEnumerator Alert()
        {
            var signal = arena.Members[0].GetComponent<AlertSignal>();
            signal.Configure(arena.Squad, 1, 0);
            Physics.SyncTransforms();
            signal.Begin(arena.Players[0]);
            Assert.That(signal.IsSignaling, Is.True);
            yield return null; yield return null; yield return null;
            Assert.That(arena.Squad.State, Is.EqualTo(SquadAlertState.Alerted));
            Share();
        }
        private void Share() { Physics.SyncTransforms(); arena.Members[0].GetComponent<MonsterPerception>().Sense(); }

        [UnityTest] public IEnumerator PreAlertAndInterruptedSignal_NeverExposePlayerKnowledgeOrTactics()
        {
            Share(); arena.Brain.Tick(now);
            Assert.That(arena.Squad.Knowledge.Snapshot, Is.Empty);
            Assert.That(arena.Brain.EvaluationHistory, Is.Empty);
            Assert.That(arena.Members[0].GetComponent<AlertSignal>().IsSignaling, Is.True);
            arena.Members[0].GetComponent<StunState>().Apply(5, Time.unscaledTime);
            yield return null; yield return null;
            Assert.That(arena.Squad.State, Is.EqualTo(SquadAlertState.Unaware));
            Assert.That(arena.Squad.Knowledge.Snapshot, Is.Empty);
            StringAssert.Contains("Unaware", arena.GetComponent<CommanderDebugView>().Describe(now));
        }

        [UnityTest] public IEnumerator SuccessfulBlockade_KeepsFormationAndDamagesNearbyPlayer() => FormationAttacks(ArenaScenario.Blockade, "objective-blockade");
        [UnityTest] public IEnumerator SuccessfulPincer_KeepsFormationAndDamagesNearbyPlayer() => FormationAttacks(ArenaScenario.Pincer, "pincer");

        private IEnumerator FormationAttacks(ArenaScenario scenario, string tactic)
        {
            arena.Arrange(scenario); yield return Alert();
            arena.Brain.Tick(now); arena.Brain.Tick(now + 1);
            Assert.That(arena.Brain.ActiveEvaluation.TacticId, Is.EqualTo(tactic));
            Assert.That(arena.Brain.AllocationFailures, Is.Empty);
            var member = arena.Members[0]; var anchor = member.CurrentOrder.Anchor;
            var player = arena.Players[1]; var health = player.GetComponent<ActorHealth>();
            player.transform.position = member.transform.position + Vector3.forward * .9f;
            Physics.SyncTransforms();
            yield return new WaitForSecondsRealtime(.1f);
            Assert.That(health.CurrentHealth, Is.EqualTo(90), "Moving formation members also attack locally.");
            Assert.That(member.CurrentOrder.Type, Is.EqualTo(MonsterOrderType.MoveToSlot));
            Assert.That(member.CurrentOrder.Anchor, Is.EqualTo(anchor));
            Assert.That(arena.Allocator.ReservationCount, Is.EqualTo(8));
            player.transform.position = new Vector3(40, 1, 0); Physics.SyncTransforms();
            var deadline = Time.realtimeSinceStartup + 10;
            while (member.CurrentOrder.Type != MonsterOrderType.HoldSlot && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(member.CurrentOrder.Type, Is.EqualTo(MonsterOrderType.HoldSlot));
            player.transform.position = member.transform.position + Vector3.forward * .9f;
            Physics.SyncTransforms();
            var before = health.CurrentHealth;
            yield return new WaitForSecondsRealtime(.1f);
            Assert.That(health.CurrentHealth, Is.EqualTo(before - 10));
            yield return new WaitForSecondsRealtime(.1f);
            Assert.That(health.CurrentHealth, Is.EqualTo(before - 10), "Local attacks retain their cooldown.");
            Assert.That(member.CurrentOrder.Type, Is.EqualTo(MonsterOrderType.HoldSlot));
            Assert.That(member.CurrentOrder.Anchor, Is.EqualTo(anchor));
            Assert.That(arena.Allocator.ReservationCount, Is.EqualTo(8));
            Assert.That(arena.Brain.AllocationFailures, Is.Empty);
            Assert.That(arena.Commander.IsDead, Is.False);
        }

        [UnityTest] public IEnumerator InvalidReleasedAnchor_IsNotDisplayedAsReservedWhileExecutionIsPaused()
        {
            yield return Alert(); arena.Brain.Tick(now); arena.Brain.Tick(now + 1);
            var anchor = arena.Members[0].CurrentOrder.Anchor;
            anchor.enabled = false;
            Assert.That(arena.Allocator.ReservationCount, Is.EqualTo(7));
            Assert.That(arena.GetComponent<CommanderGizmoDrawer>().Classify(anchor), Is.EqualTo(AnchorDebugState.Unreachable));
            yield return null;
        }

        [UnityTest] public IEnumerator ControlledConfigurations_ExplainWinnerAndPlaceEveryRole()
        {
            arena.Arrange(ArenaScenario.Blockade);
            yield return Alert();
            arena.Brain.Tick(now);
            Assert.That(arena.Brain.PendingEvaluation.TacticId, Is.EqualTo("objective-blockade"));
            Assert.That(arena.Allocator.ReservationCount, Is.Zero);
            StringAssert.Contains("Objective proximity=1.00", arena.GetComponent<CommanderDebugView>().Describe(now));
            arena.Brain.Tick(now + 1);
            Assert.That(arena.Brain.ActiveEvaluation.TacticId, Is.EqualTo("objective-blockade"));
            Assert.That(arena.Allocator.ReservationCount, Is.EqualTo(8));
            foreach (var member in arena.Squad.LivingMembers)
            {
                Assert.That(member.Executor.CurrentOrder.Type, Is.EqualTo(MonsterOrderType.MoveToSlot));
                Assert.That(member.Executor.CurrentOrder.Anchor.Allows(member.Role), Is.True);
                NavMeshPath path = new NavMeshPath();
                Assert.That(NavMesh.CalculatePath(member.Health.transform.position, member.Executor.CurrentOrder.Anchor.transform.position, NavMesh.AllAreas, path), Is.True);
                Assert.That(path.status, Is.EqualTo(NavMeshPathStatus.PathComplete));
            }
            Assert.That(arena.Members[0].CurrentOrder.Anchor.Tag.ToString(), Is.EqualTo("ObjectiveFront"));
            Assert.That(arena.Members[2].CurrentOrder.Anchor.Tag.ToString(), Is.EqualTo("CoverRear"));
            Assert.That(arena.Members[4].CurrentOrder.Anchor.Tag.ToString(), Is.EqualTo("LeftFlank"));
            Assert.That(arena.Members[5].CurrentOrder.Anchor.Tag.ToString(), Is.EqualTo("RightFlank"));
            Assert.That(arena.Members[6].CurrentOrder.Anchor.Tag.ToString(), Is.EqualTo("SupportRear"));
        }

        [UnityTest] public IEnumerator ClusteredArena_ActivatesPincer()
        {
            arena.Arrange(ArenaScenario.Pincer); yield return Alert();
            arena.Brain.Tick(now); Assert.That(arena.Brain.PendingEvaluation.TacticId, Is.EqualTo("pincer"));
            arena.Brain.Tick(now + 1); Assert.That(arena.Brain.ActiveEvaluation.TacticId, Is.EqualTo("pincer"));
            StringAssert.Contains("Player clustering", arena.GetComponent<CommanderDebugView>().Describe(now + 1));
        }

        [UnityTest] public IEnumerator ObservedSupportArena_ActivatesFocusSupportWithObservedTarget()
        {
            arena.Arrange(ArenaScenario.FocusSupport); yield return Alert();
            arena.Brain.Tick(now); Assert.That(arena.Brain.PendingEvaluation.TacticId, Is.EqualTo("focus-support"));
            arena.Brain.Tick(now + 1); Assert.That(arena.Brain.ActiveEvaluation.TacticId, Is.EqualTo("focus-support"));
            Assert.That(arena.Members[4].CurrentOrder.TargetActorId, Is.EqualTo(arena.Players[2].ActorId));
        }

        [UnityTest] public IEnumerator SharingIsImmediate_ReassignmentWaitsForCycleHoldAndTelegraph()
        {
            yield return Alert(); arena.Brain.Tick(now); arena.Brain.Tick(now + 1);
            var original = arena.Members[0].CurrentOrder.Anchor;
            arena.Arrange(ArenaScenario.Pincer); Share();
            Assert.That(arena.Squad.Knowledge.TryGet(arena.Players[0].ActorId, out var memory), Is.True);
            Assert.That(memory.LastKnownPosition, Is.EqualTo(arena.Players[0].transform.position));
            arena.Brain.Tick(now + 2);
            Assert.That(arena.Brain.PendingEvaluation, Is.Null);
            arena.Brain.Tick(now + 3); // Due cycle, still inside minimum hold.
            Assert.That(arena.Brain.PendingEvaluation, Is.Null);
            StringAssert.Contains("minimum hold", arena.GetComponent<CommanderDebugView>().Describe(now + 3));
            Assert.That(arena.Members[0].CurrentOrder.Anchor, Is.SameAs(original));
            arena.Brain.Tick(now + 7.1f); // Safely beyond the six-second hold boundary.
            Assert.That(arena.Brain.PendingEvaluation.TacticId, Is.EqualTo("pincer"));
            Assert.That(arena.Brain.ActiveEvaluation.TacticId, Is.EqualTo("objective-blockade"));
            arena.Brain.Tick(now + 8f);
            Assert.That(arena.Brain.ActiveEvaluation.TacticId, Is.EqualTo("objective-blockade"));
            arena.Brain.Tick(now + 8.2f); // Safely beyond the one-second telegraph boundary.
            Assert.That(arena.Brain.ActiveEvaluation.TacticId, Is.EqualTo("pincer"));
        }

        [UnityTest] public IEnumerator CommanderDeath_PermanentlyClearsVisibleCoordination()
        {
            yield return Alert(); arena.Brain.Tick(now); arena.Brain.Tick(now + 1);
            arena.Commander.ApplyDamage(1000);
            Assert.That(arena.Squad.State, Is.EqualTo(SquadAlertState.Confused));
            Assert.That(arena.Allocator.ReservationCount, Is.Zero);
            Assert.That(arena.Brain.ActiveEvaluation, Is.Null);
            Assert.That(arena.Brain.Telegraph.IsVisible, Is.False);
            Assert.That(arena.Squad.LivingMembers.All(member => member.Executor.CurrentOrder.Type == MonsterOrderType.ConfusedCombat), Is.True);
            StringAssert.Contains("PERMANENT CONFUSION", arena.GetComponent<CommanderDebugView>().Describe(now + 1));
            var count = arena.Brain.EvaluationHistory.Count;
            Share(); arena.Brain.Tick(now + 100);
            Assert.That(arena.Brain.EvaluationHistory.Count, Is.EqualTo(count));
            Assert.That(arena.Squad.TryCompleteAlert(1), Is.False);
        }

        [UnityTest] public IEnumerator DifficultyChangesTimingAndComplexityWithoutChangingSharedKnowledge()
        {
            arena.SelectDifficulty(0); arena.Arrange(ArenaScenario.FocusSupport); yield return Alert();
            arena.Brain.Tick(now);
            Assert.That(arena.Profile.CommandCycleSeconds, Is.EqualTo(5));
            Assert.That(arena.Brain.EvaluationHistory.Select(e => e.TacticId), Is.EquivalentTo(new[] { "objective-blockade" }));
            var memory = arena.Squad.Knowledge.Snapshot.ToArray();
            arena.SelectDifficulty(1); arena.Brain.enabled = false; arena.Brain.Tick(now);
            Assert.That(arena.Profile.CommandCycleSeconds, Is.EqualTo(3));
            Assert.That(arena.Squad.Knowledge.Snapshot, Is.EqualTo(memory));
            arena.SelectDifficulty(2); arena.Brain.enabled = false; arena.Brain.Tick(now);
            Assert.That(arena.Profile.CommandCycleSeconds, Is.EqualTo(1.5f));
            Assert.That(arena.Brain.PendingEvaluation.TacticId, Is.EqualTo("focus-support"));
            Assert.That(arena.Squad.Knowledge.Snapshot, Is.EqualTo(memory));
        }

        [UnityTest] public IEnumerator BakedArena_RealExecutorsReachTheirRoleAnchorsAndDiagnosticsTrackReservations()
        {
            yield return Alert(); arena.Brain.Tick(now); arena.Brain.Tick(now + 1);
            var drawer = arena.GetComponent<CommanderGizmoDrawer>();
            var target = arena.Members[0].CurrentOrder.Anchor;
            Assert.That(drawer.Classify(target), Is.EqualTo(AnchorDebugState.Reserved));
            var unused = arena.Anchors.First(anchor => anchor.Tag.ToString() == "CommanderSafe");
            Assert.That(drawer.Classify(unused), Is.EqualTo(AnchorDebugState.Available));
            unused.enabled = false;
            Assert.That(drawer.Classify(unused), Is.EqualTo(AnchorDebugState.Unreachable));
            yield return new WaitForSecondsRealtime(8);
            foreach (var member in arena.Squad.LivingMembers)
            {
                Assert.That(member.Executor.CurrentOrder.Type, Is.EqualTo(MonsterOrderType.HoldSlot), member.Role.ToString());
                Assert.That(Vector3.Distance(member.Health.transform.position, member.Executor.CurrentOrder.Anchor.transform.position), Is.LessThan(.5f));
            }
            var debug = arena.GetComponent<CommanderDebugView>().Describe(now + 1);
            StringAssert.Contains("RESERVATIONS 8", debug);
            StringAssert.Contains("Ranged -> HoldSlot", debug);
            StringAssert.Contains("below activation threshold", debug);
        }
    }
}
