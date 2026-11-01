using System.Collections;
using System.Collections.Generic;
using AlmostDelivered.Combat;
using AlmostDelivered.CommanderAI.Commander;
using AlmostDelivered.CommanderAI.Domain;
using AlmostDelivered.CommanderAI.Execution;
using AlmostDelivered.CommanderAI.Tactics;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;

namespace AlmostDelivered.Tests.PlayMode
{
    public sealed class CommanderLifecycleTests
    {
        private GameObject root;
        private SquadController squad;
        private CommanderBrain brain;
        private ActorHealth commander;
        private CommanderDifficultyProfile profile;
        private UtilityTactic tactic;
        private TacticalSlotAllocator allocator;
        private readonly List<MonsterOrderExecutor> executors = new List<MonsterOrderExecutor>();
        private NavMeshData data;
        private NavMeshDataInstance mesh;

        [SetUp] public void SetUp()
        {
            data = NavMeshBuilder.BuildNavMeshData(NavMesh.GetSettingsByID(0), new List<NavMeshBuildSource> {
                new NavMeshBuildSource { shape = NavMeshBuildSourceShape.Box, size = new Vector3(40, .2f, 40),
                    transform = Matrix4x4.TRS(Vector3.down * .1f, Quaternion.identity, Vector3.one), area = 0 } },
                new Bounds(Vector3.zero, new Vector3(50, 10, 50)), Vector3.zero, Quaternion.identity);
            Assert.That(data, Is.Not.Null); mesh = NavMesh.AddNavMeshData(data);
            root = new GameObject("Lifecycle fixture"); squad = root.AddComponent<SquadController>();
            commander = root.AddComponent<ActorHealth>(); brain = root.AddComponent<CommanderBrain>(); brain.enabled = false;
            profile = CommanderDifficultyProfile.Create(CommanderDifficulty.Normal);
            tactic = ScriptableObject.CreateInstance<UtilityTactic>();
            tactic.TacticId = InitialTacticIds.ObjectiveBlockade;
            tactic.Considerations = new[] { new UtilityConsideration("Health", ConsiderationKind.SquadHealth) };
            tactic.Playbook = ScriptableObject.CreateInstance<TacticalPlaybook>();
            tactic.Playbook.RoleSlots = new[] { new PlaybookRoleSlots(MonsterRole.Shield, false, TacticalAnchorTag.ObjectiveFront) };
            var anchor = Child("Front", Vector3.right * 2).AddComponent<TacticalAnchor>(); anchor.Capacity = 2;
            allocator = new TacticalSlotAllocator(new[] { anchor }, new NavMeshTacticalReachability());
            AddMember(1); AddMember(2);
            brain.Configure(squad, commander, profile, new[] { tactic }, new[] { anchor }, allocator, Vector3.zero);
        }

        [UnityTearDown] public IEnumerator TearDown()
        {
            Object.Destroy(root); Object.Destroy(profile); Object.Destroy(tactic.Playbook); Object.Destroy(tactic);
            yield return null; mesh.Remove(); Object.Destroy(data); executors.Clear();
        }

        [UnityTest] public IEnumerator CommanderDiesWhileIdle_SurvivorsImmediatelyBecomePermanentlyConfused()
        {
            commander.ApplyDamage(100); AssertConfused();
            var tendency = executors[0].Tendency;
            Assert.That(squad.TryCompleteAlert(1), Is.False);
            brain.Tick(100); yield return null; AssertConfused();
            Assert.That(executors[0].Tendency, Is.EqualTo(tendency));
        }

        [UnityTest] public IEnumerator CommanderDiesWhileActive_ClearsOrdersAndAllReservations()
        {
            squad.TryCompleteAlert(1); brain.Tick(0); brain.Tick(1);
            Assert.That(allocator.ReservationCount, Is.EqualTo(2));
            Assert.That(brain.ActiveEvaluation, Is.Not.Null);
            commander.ApplyDamage(100); AssertConfused();
            var count = brain.EvaluationHistory.Count;
            brain.Tick(100); brain.Tick(200); yield return null; AssertConfused();
            Assert.That(brain.EvaluationHistory.Count, Is.EqualTo(count));
        }

        [UnityTest] public IEnumerator CommanderDiesDuringTelegraph_PendingTacticNeverActivates()
        {
            squad.TryCompleteAlert(1); brain.Tick(0);
            Assert.That(brain.PendingEvaluation, Is.Not.Null); Assert.That(brain.Telegraph.IsVisible, Is.True);
            commander.ApplyDamage(100); AssertConfused();
            brain.Tick(1); brain.Tick(3); brain.Tick(100); yield return null; AssertConfused();
            Assert.That(squad.Report(new ObservationSample(50, Vector3.one, Vector3.zero, 100, 1, ObservedCombatTag.Healing, 1)), Is.False);
            Assert.That(squad.Knowledge.Snapshot, Is.Empty);
        }

        [UnityTest] public IEnumerator ExternalConfusion_AlsoInterruptsPendingCommand()
        {
            squad.TryCompleteAlert(1); brain.Tick(0); squad.EnterConfused();
            AssertConfused(); brain.Tick(100); yield return null; AssertConfused();
        }

        [UnityTest] public IEnumerator InterruptedTelegraph_NeverActivatesOnItsOldDeadline()
        {
            squad.TryCompleteAlert(1); brain.Tick(0); brain.Telegraph.Cancel(); brain.Tick(1);
            Assert.That(brain.PendingEvaluation, Is.Null); Assert.That(brain.ActiveEvaluation, Is.Null);
            Assert.That(allocator.ReservationCount, Is.Zero); yield return null;
        }

        [UnityTest] public IEnumerator ConfusedExecutor_RejectsOrganizedOrdersAndLateMembersCannotRestoreSquad()
        {
            commander.ApplyDamage(100);
            executors[0].Assign(new MonsterOrder(MonsterOrderType.AttackObservedTarget, targetActorId: 50));
            AddMember(3);
            Assert.That(executors[2].CurrentOrder.Type, Is.EqualTo(MonsterOrderType.ConfusedCombat));
            brain.Tick(10); yield return null; AssertConfused();
        }

        [UnityTest] public IEnumerator Tendency_IsStableFromRoleIdAndSeedWithoutRandomState()
        {
            Random.InitState(1);
            Assert.That(ConfusionTendencies.Choose(MonsterRole.Shield, 1), Is.EqualTo(ConfusionTendency.HoldArea));
            Assert.That(ConfusionTendencies.Choose(MonsterRole.Charger, 1), Is.EqualTo(ConfusionTendency.NearestVisibleTarget));
            Assert.That(ConfusionTendencies.Choose(MonsterRole.Ranged, 1), Is.EqualTo(ConfusionTendency.RetreatWhenWounded));
            Assert.That(ConfusionTendencies.Choose(MonsterRole.Support, 1), Is.EqualTo(ConfusionTendency.SearchLastKnownPosition));
            var first = ConfusionTendencies.Choose(MonsterRole.Shield, 17, 90);
            Random.InitState(9000);
            for (var index = 0; index < 100; index++)
                Assert.That(ConfusionTendencies.Choose(MonsterRole.Shield, 17, 90), Is.EqualTo(first));
            yield return null;
        }

        [UnityTest] public IEnumerator NearestVisibleFallback_AttacksLocallyWithoutSquadReports()
        {
            var target = Target(Vector3.forward * .7f);
            executors[0].EnterConfused(ConfusionTendency.NearestVisibleTarget);
            yield return null;
            Assert.That(target.GetComponent<ActorHealth>().CurrentHealth, Is.EqualTo(90));
            Assert.That(squad.Knowledge.Snapshot, Is.Empty);
            Assert.That(executors[0].CurrentOrder.Type, Is.EqualTo(MonsterOrderType.ConfusedCombat));
        }

        [UnityTest] public IEnumerator NearestVisibleFallback_DoesNotSeeThroughWalls()
        {
            var target = Target(Vector3.forward * 4);
            var wall = Child("Wall", Vector3.forward * 2); wall.AddComponent<BoxCollider>().size = new Vector3(5, 5, .4f);
            Physics.SyncTransforms(); executors[0].EnterConfused(ConfusionTendency.NearestVisibleTarget);
            yield return new WaitForSecondsRealtime(.3f);
            Assert.That(executors[0].transform.position.z, Is.LessThan(.2f));
            Assert.That(target.GetComponent<ActorHealth>().CurrentHealth, Is.EqualTo(100));
        }

        [UnityTest] public IEnumerator SearchFallback_UsesFrozenLastKnownPosition()
        {
            squad.TryCompleteAlert(1);
            squad.Report(new ObservationSample(50, Vector3.right * 3, Vector3.zero, 0, 1, ObservedCombatTag.None, 1));
            executors[0].EnterConfused(ConfusionTendency.SearchLastKnownPosition);
            squad.Knowledge.Clear();
            yield return new WaitForSecondsRealtime(.8f);
            Assert.That(executors[0].transform.position.x, Is.GreaterThan(2));
            Assert.That(executors[0].CurrentOrder.Type, Is.EqualTo(MonsterOrderType.ConfusedCombat));
        }

        [UnityTest] public IEnumerator WoundedFallback_RetreatsFromLocallyVisibleThreat()
        {
            Target(Vector3.forward * 3); executors[0].GetComponent<ActorHealth>().ApplyDamage(75);
            executors[0].EnterConfused(ConfusionTendency.RetreatWhenWounded);
            yield return new WaitForSecondsRealtime(.5f);
            Assert.That(executors[0].transform.position.z, Is.LessThan(-1));
        }

        [UnityTest] public IEnumerator InactiveLivingMember_ConfusionIsAppliedBeforeReenable()
        {
            executors[0].gameObject.SetActive(false);
            commander.ApplyDamage(100);
            Assert.That(executors[0].CurrentOrder.Type, Is.EqualTo(MonsterOrderType.ConfusedCombat));
            var tendency = executors[0].Tendency;
            executors[0].gameObject.SetActive(true);
            yield return null;
            Assert.That(executors[0].CurrentOrder.Type, Is.EqualTo(MonsterOrderType.ConfusedCombat));
            Assert.That(executors[0].Tendency, Is.EqualTo(tendency));
        }

        [UnityTest] public IEnumerator ReplacementCommander_CannotRecoverConfusedSquad()
        {
            commander.ApplyDamage(100);
            var replacement = Child("Replacement", Vector3.zero).AddComponent<ActorHealth>();
            brain.Configure(squad, replacement, profile, new[] { tactic }, new TacticalAnchor[0], allocator, Vector3.zero);
            brain.Tick(0); brain.Tick(100); yield return null; AssertConfused();
            Assert.That(brain.EvaluationHistory, Is.Empty);
        }

        [UnityTest] public IEnumerator HoldAreaFallback_ReturnsAfterLocalDisplacement()
        {
            executors[0].EnterConfused(ConfusionTendency.HoldArea);
            Assert.That(executors[0].GetComponent<NavMeshAgent>().Warp(Vector3.left * 3), Is.True);
            yield return new WaitForSecondsRealtime(.8f);
            Assert.That(executors[0].transform.position.magnitude, Is.LessThan(.4f));
            Assert.That(executors[0].CurrentOrder.Type, Is.EqualTo(MonsterOrderType.ConfusedCombat));
        }

        [UnityTest] public IEnumerator PlaybookPressure_AttacksObservedTargetAfterReachingSlot()
        {
            var target = Target(Vector3.right * 2.6f);
            tactic.Playbook.RoleSlots = new[] { new PlaybookRoleSlots(MonsterRole.Shield, true, TacticalAnchorTag.ObjectiveFront) };
            squad.TryCompleteAlert(1);
            squad.Report(new ObservationSample(target.ActorId, target.transform.position, Vector3.zero, 0, 1, ObservedCombatTag.Healing, 1));
            brain.Tick(0); brain.Tick(1);
            Assert.That(executors[0].CurrentOrder.Type, Is.EqualTo(MonsterOrderType.MoveToSlot));
            yield return new WaitForSecondsRealtime(.8f);
            Assert.That(executors[0].CurrentOrder.Type, Is.EqualTo(MonsterOrderType.AttackObservedTarget));
            Assert.That(executors[0].CurrentOrder.TargetActorId, Is.EqualTo(target.ActorId));
            Assert.That(target.GetComponent<ActorHealth>().CurrentHealth, Is.LessThan(100));
            Assert.That(allocator.ReservationCount, Is.Zero);
        }

        private void AssertConfused()
        {
            Assert.That(squad.State, Is.EqualTo(SquadAlertState.Confused));
            Assert.That(brain.PendingEvaluation, Is.Null); Assert.That(brain.ActiveEvaluation, Is.Null);
            Assert.That(brain.Telegraph.IsVisible, Is.False); Assert.That(allocator.ReservationCount, Is.Zero);
            foreach (var executor in executors) Assert.That(executor.CurrentOrder.Type, Is.EqualTo(MonsterOrderType.ConfusedCombat));
        }
        private void AddMember(int id)
        {
            var member = Child("Member " + id, Vector3.left * (id - 1)); var health = member.AddComponent<ActorHealth>();
            var agent = member.AddComponent<NavMeshAgent>(); agent.speed = 10; agent.acceleration = 100; agent.stoppingDistance = .1f;
            Assert.That(agent.Warp(member.transform.position), Is.True);
            var executor = member.AddComponent<MonsterOrderExecutor>(); executors.Add(executor);
            squad.RegisterMember(id, MonsterRole.Shield, health, executor);
        }
        private ObservableActor Target(Vector3 position)
        {
            var target = Child("Target", position); target.AddComponent<ActorHealth>(); target.AddComponent<SphereCollider>();
            var actor = target.AddComponent<ObservableActor>(); Physics.SyncTransforms(); return actor;
        }
        private GameObject Child(string name, Vector3 position)
        { var child = new GameObject(name); child.transform.SetParent(root.transform); child.transform.position = position; return child; }
    }
}
