using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AlmostDelivered.Combat;
using AlmostDelivered.CommanderAI.Commander;
using AlmostDelivered.CommanderAI.Domain;
using AlmostDelivered.CommanderAI.Execution;
using AlmostDelivered.CommanderAI.Tactics;
using NUnit.Framework;
using UnityEngine;

namespace AlmostDelivered.Tests.EditMode
{
    public sealed class CommanderBrainTests
    {
        private GameObject root;
        private SquadController squad;
        private CommanderBrain brain;
        private CommanderDifficultyProfile profile;
        private ActorHealth commander;
        private TacticalSlotAllocator allocator;
        private readonly List<UnityEngine.Object> assets = new List<UnityEngine.Object>();
        private readonly List<TacticalAnchor> anchors = new List<TacticalAnchor>();
        private readonly List<MonsterOrderExecutor> executors = new List<MonsterOrderExecutor>();
        private UtilityTactic first, second;
        private ScoreInput firstScore, secondScore;
        private Reachable reachability;

        private sealed class Reachable : ITacticalReachability
        { public bool Result = true; public bool CanReach(Vector3 from, Vector3 to) => Result; }
        private sealed class ScoreInput : UtilityConsideration
        {
            public float Value;
            public TacticalContext LastContext;
            public ScoreInput(float value) : base("Fixture score", ConsiderationKind.SquadHealth) { Value = value; }
            public override float Score(in TacticalContext context) { LastContext = context; return Value; }
        }

        [SetUp] public void SetUp()
        {
            root = new GameObject("Commander cycle fixture");
            squad = root.AddComponent<SquadController>();
            commander = root.AddComponent<ActorHealth>();
            brain = root.AddComponent<CommanderBrain>(); brain.enabled = false;
            profile = CommanderDifficultyProfile.Create(CommanderDifficulty.Normal); assets.Add(profile);
            anchors.Add(Child("Front").AddComponent<TacticalAnchor>());
            var rear = Child("Rear").AddComponent<TacticalAnchor>(); rear.Tag = TacticalAnchorTag.CoverRear; anchors.Add(rear);
            reachability = new Reachable(); allocator = new TacticalSlotAllocator(anchors, reachability);
            AddMember(1, MonsterRole.Shield); AddMember(2, MonsterRole.Ranged);
            firstScore = new ScoreInput(.6f); secondScore = new ScoreInput(.5f);
            first = Tactic(InitialTacticIds.ObjectiveBlockade, firstScore);
            second = Tactic(InitialTacticIds.Pincer, secondScore);
            brain.Configure(squad, commander, profile, new[] { first, second }, anchors, allocator, Vector3.zero);
        }

        [TearDown] public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(root);
            foreach (var asset in assets) UnityEngine.Object.DestroyImmediate(asset);
            assets.Clear(); anchors.Clear(); executors.Clear();
        }

        [Test] public void Unaware_NeverEvaluatesOrAssignsOrders()
        {
            brain.Tick(0); brain.Tick(100);
            Assert.That(brain.EvaluationHistory, Is.Empty);
            Assert.That(brain.PendingEvaluation, Is.Null);
            Assert.That(executors.All(executor => executor.CurrentOrder.Type == MonsterOrderType.None), Is.True);
        }

        [Test] public void Alerted_EvaluatesOncePerCycleWithoutFrameOrCatchupEvaluations()
        {
            squad.TryCompleteAlert(1);
            brain.Tick(0); brain.Tick(.1f); brain.Tick(2.99f);
            Assert.That(brain.EvaluationHistory.Count, Is.EqualTo(2));
            brain.Tick(3); brain.Tick(3); brain.Tick(100);
            Assert.That(brain.EvaluationHistory.Count, Is.EqualTo(6));
        }

        [Test] public void Telegraph_PreventsReservationsAndOrdersUntilCompletion()
        {
            squad.TryCompleteAlert(1); brain.Tick(0);
            Assert.That(brain.PendingEvaluation.TacticId, Is.EqualTo(InitialTacticIds.ObjectiveBlockade));
            Assert.That(brain.Telegraph.IsVisible, Is.True);
            Assert.That(brain.ActiveEvaluation, Is.Null);
            Assert.That(allocator.ReservationCount, Is.Zero);
            brain.Tick(.999f);
            Assert.That(executors[0].CurrentOrder.Type, Is.EqualTo(MonsterOrderType.None));
            brain.Tick(1);
            Assert.That(brain.Telegraph.IsVisible, Is.False);
            Assert.That(brain.PendingEvaluation, Is.Null);
            Assert.That(brain.ActiveEvaluation.TacticId, Is.EqualTo(InitialTacticIds.ObjectiveBlockade));
            Assert.That(executors[0].CurrentOrder.Type, Is.EqualTo(MonsterOrderType.MoveToSlot));
            Assert.That(allocator.ReservationCount, Is.EqualTo(2));
        }

        [Test] public void NoValidTactic_AssignsFallbackAndRecordsRejections()
        {
            first.RequiredRoles = new[] { new RoleRequirement(MonsterRole.Support, 1) };
            second.RequiredRoles = new[] { new RoleRequirement(MonsterRole.Support, 1) };
            squad.TryCompleteAlert(1); brain.Tick(0);
            Assert.That(brain.PendingEvaluation, Is.Null);
            Assert.That(brain.EvaluationHistory.All(result => !result.IsValid && result.RejectionReason.Contains("Support")), Is.True);
            Assert.That(executors.All(executor => executor.CurrentOrder.Type == MonsterOrderType.HoldSlot), Is.True);
        }

        [Test] public void SwitchPolicy_RetainsUntilHoldAndMarginAreSatisfied()
        {
            squad.TryCompleteAlert(1); brain.Tick(0); brain.Tick(1);
            secondScore.Value = .95f;
            brain.Tick(3); brain.Tick(6);
            Assert.That(brain.ActiveEvaluation.TacticId, Is.EqualTo(InitialTacticIds.ObjectiveBlockade));
            Assert.That(brain.PendingEvaluation, Is.Null);
            brain.Tick(9);
            Assert.That(brain.PendingEvaluation.TacticId, Is.EqualTo(InitialTacticIds.Pincer));
            Assert.That(brain.ActiveEvaluation.TacticId, Is.EqualTo(InitialTacticIds.ObjectiveBlockade));
            brain.Tick(10);
            Assert.That(brain.ActiveEvaluation.TacticId, Is.EqualTo(InitialTacticIds.Pincer));
        }

        [Test] public void SameTactic_IsRetainedWithoutRestartingTelegraph()
        {
            squad.TryCompleteAlert(1); brain.Tick(0); brain.Tick(1); brain.Tick(3); brain.Tick(6); brain.Tick(9);
            Assert.That(brain.ActiveEvaluation.TacticId, Is.EqualTo(InitialTacticIds.ObjectiveBlockade));
            Assert.That(brain.PendingEvaluation, Is.Null);
            Assert.That(brain.Telegraph.IsVisible, Is.False);
        }

        [Test] public void RequiredRoleDeath_InvalidActiveTacticCannotBlockLowerScoringReplacement()
        {
            first.RequiredRoles = new[] { new RoleRequirement(MonsterRole.Shield, 1) };
            firstScore.Value = .95f; secondScore.Value = .3f;
            squad.TryCompleteAlert(1); brain.Tick(0); brain.Tick(1);
            executors[0].GetComponent<ActorHealth>().ApplyDamage(100);
            brain.Tick(9);
            Assert.That(brain.EvaluationHistory.Last(result => result.TacticId == first.TacticId).IsValid, Is.False);
            Assert.That(brain.PendingEvaluation, Is.Not.Null);
            Assert.That(brain.PendingEvaluation.TacticId, Is.EqualTo(second.TacticId));
            Assert.That(brain.ActiveEvaluation.IsValid, Is.False);
            brain.Tick(10);
            Assert.That(brain.ActiveEvaluation.TacticId, Is.EqualTo(second.TacticId));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void MemberJoiningDuringTelegraph_FallbackCanExecuteAndSurvivesNextCycle(bool advanceToNextCycle)
        {
            squad.TryCompleteAlert(1);
            squad.Report(new ObservationSample(50, Vector3.zero, Vector3.zero, 0, 1, ObservedCombatTag.None, 1));
            brain.Tick(0); AddMember(3, MonsterRole.Shield);
            // Match normal runtime Awake while leaving allocator/knowledge attachment to the brain.
            typeof(MonsterOrderExecutor).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(executors[2], null);
            brain.Tick(1);
            Assert.That(executors[2].CurrentOrder.Type, Is.EqualTo(MonsterOrderType.AttackObservedTarget));
            if (advanceToNextCycle) brain.Tick(3);
            typeof(MonsterOrderExecutor).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(executors[2], null);
            Assert.That(executors[2].CurrentOrder.Type, Is.EqualTo(MonsterOrderType.AttackObservedTarget));
            Assert.That(executors[2].CurrentOrder.TargetActorId, Is.EqualTo(50));
            Assert.That(brain.PendingEvaluation, Is.Null);
        }

        [Test] public void MemberJoiningRetainedActiveTactic_ReceivesExecutableFallbackWithoutResettingOtherOrders()
        {
            squad.TryCompleteAlert(1);
            squad.Report(new ObservationSample(50, Vector3.zero, Vector3.zero, 0, 1, ObservedCombatTag.None, 1));
            brain.Tick(0); brain.Tick(1);
            AddMember(3, MonsterRole.Shield);
            typeof(MonsterOrderExecutor).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(executors[2], null);
            var originalAnchor = executors[0].CurrentOrder.Anchor;
            brain.Tick(3);
            Assert.That(brain.PendingEvaluation, Is.Null);
            Assert.That(brain.ActiveEvaluation.TacticId, Is.EqualTo(first.TacticId));
            Assert.That(executors[2].CurrentOrder.Type, Is.EqualTo(MonsterOrderType.AttackObservedTarget));
            typeof(MonsterOrderExecutor).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(executors[2], null);
            Assert.That(executors[2].CurrentOrder.TargetActorId, Is.EqualTo(50));
            Assert.That(executors[0].CurrentOrder.Anchor, Is.EqualTo(originalAnchor));
            Assert.That(allocator.ReservationCount, Is.EqualTo(2));
            brain.Tick(6);
            Assert.That(executors[2].CurrentOrder.Type, Is.EqualTo(MonsterOrderType.AttackObservedTarget));
        }

        [Test] public void Context_ContainsOnlySquadSnapshotAndNeverLiveActorReferences()
        {
            squad.TryCompleteAlert(1);
            squad.Report(new ObservationSample(50, Vector3.right * 4, Vector3.zero, 0, 1, ObservedCombatTag.Healing, 1));
            brain.Tick(0);
            Assert.That(firstScore.LastContext.Knowledge.Single().LastKnownPosition, Is.EqualTo(Vector3.right * 4));
            squad.Knowledge.Clear();
            Assert.That(firstScore.LastContext.Knowledge.Count, Is.EqualTo(1));
            foreach (var field in typeof(CommanderBrain).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                Assert.That(field.FieldType, Is.Not.EqualTo(typeof(ObservableActor)));
            foreach (var method in typeof(CommanderBrain).GetMethods())
                Assert.That(method.GetParameters().Any(parameter => parameter.ParameterType == typeof(ObservableActor)), Is.False);
        }

        [Test] public void CommandCycle_DecaysStaleConfidenceWithoutMovingLastKnownFacts()
        {
            squad.TryCompleteAlert(1);
            squad.Report(new ObservationSample(50, Vector3.right * 4, Vector3.forward, 0, 1, ObservedCombatTag.Healing, 1));
            Assert.That(squad.Knowledge.Snapshot.Single().Confidence, Is.EqualTo(1), "sharing is immediate");
            brain.Tick(0); brain.Tick(2.99f);
            Assert.That(squad.Knowledge.Snapshot.Single().Confidence, Is.EqualTo(1), "no per-frame decay");
            brain.Tick(3);
            Assert.That(firstScore.LastContext.Knowledge.Single().Confidence, Is.EqualTo(.7f).Within(.00001f));
            brain.Tick(12);
            var stale = squad.Knowledge.Snapshot.Single();
            Assert.That(stale.Confidence, Is.Zero);
            Assert.That(firstScore.LastContext.Knowledge.Single().Confidence, Is.Zero);
            Assert.That(stale.LastKnownPosition, Is.EqualTo(Vector3.right * 4));
            Assert.That(stale.LastKnownVelocity, Is.EqualTo(Vector3.forward));
            Assert.That(stale.LastObservedTime, Is.Zero);
            Assert.That(stale.Tags, Is.EqualTo(ObservedCombatTag.Healing));
        }

        [Test] public void MemoryLifetime_IsConfigurableAndFreshReportsRestoreConfidenceImmediately()
        {
            var setting = typeof(CommanderBrain).GetField("memorySecondsToZero", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(setting, Is.Not.Null, "memory lifetime is independent of difficulty and serializable");
            Assert.That(setting.GetCustomAttribute<SerializeField>(), Is.Not.Null);
            setting.SetValue(brain, 6f);
            squad.TryCompleteAlert(1);
            squad.Report(new ObservationSample(50, Vector3.right * 4, Vector3.zero, 0, 1, ObservedCombatTag.None, 1));
            brain.Tick(0); brain.Tick(3);
            Assert.That(firstScore.LastContext.Knowledge.Single().Confidence, Is.EqualTo(.5f).Within(.00001f));
            squad.Report(new ObservationSample(50, Vector3.right * 6, Vector3.zero, 4, 1, ObservedCombatTag.Healing, 1));
            Assert.That(squad.Knowledge.Snapshot.Single().Confidence, Is.EqualTo(1));
            brain.Tick(6);
            Assert.That(firstScore.LastContext.Knowledge.Single().Confidence, Is.EqualTo(2f / 3f).Within(.00001f));
            brain.Tick(12);
            Assert.That(squad.Knowledge.Snapshot.Single().Confidence, Is.Zero);
            Assert.That(squad.Knowledge.Snapshot.Single().LastKnownPosition, Is.EqualTo(Vector3.right * 6));
        }

        [Test] public void Difficulty_FiltersTacticsWithoutChangingKnowledge()
        {
            var easy = CommanderDifficultyProfile.Create(CommanderDifficulty.Easy); assets.Add(easy);
            brain.Configure(squad, commander, easy, new[] { first, second }, anchors, allocator, Vector3.zero);
            squad.TryCompleteAlert(1);
            Assert.That(squad.Report(new ObservationSample(50, Vector3.one, Vector3.zero, 0, 1, ObservedCombatTag.Healing, 1)), Is.True);
            brain.Tick(0); brain.Tick(1);
            Assert.That(brain.EvaluationHistory.Count, Is.EqualTo(1));
            Assert.That(brain.ActiveEvaluation, Is.Null);
            Assert.That(firstScore.LastContext.Knowledge.Single().ActorId, Is.EqualTo(50));
            brain.Tick(2);
            Assert.That(brain.ActiveEvaluation.TacticId, Is.EqualTo(InitialTacticIds.ObjectiveBlockade));
        }

        [Test] public void PartialPlaybook_AllocatedMemberExecutesAndUnallocatedMemberHolds()
        {
            anchors[1].enabled = false;
            squad.TryCompleteAlert(1); brain.Tick(0); brain.Tick(1);
            Assert.That(executors[0].CurrentOrder.Type, Is.EqualTo(MonsterOrderType.MoveToSlot));
            Assert.That(executors[0].CurrentOrder.Anchor, Is.EqualTo(anchors[0]));
            Assert.That(executors[1].CurrentOrder.Type, Is.EqualTo(MonsterOrderType.HoldSlot));
            Assert.That(allocator.ReservationCount, Is.EqualTo(1));
            Assert.That(brain.AllocationFailures.Count, Is.EqualTo(1));
            Assert.That(brain.AllocationFailures[0], Does.Contain("2").And.Contain("CoverRear"));
            Assert.That(brain.AllocationFailures[0], Does.Contain("disabled").And.Not.Contain("unreachable"));
        }

        [Test] public void PartialPlaybook_UnallocatedMemberAttacksReportedTarget()
        {
            anchors[1].enabled = false;
            squad.TryCompleteAlert(1);
            squad.Report(new ObservationSample(50, Vector3.right * 4, Vector3.zero, 0, 1, ObservedCombatTag.None, 1));
            brain.Tick(0); brain.Tick(1);
            Assert.That(executors[0].CurrentOrder.Type, Is.EqualTo(MonsterOrderType.MoveToSlot));
            Assert.That(executors[1].CurrentOrder.Type, Is.EqualTo(MonsterOrderType.AttackObservedTarget));
            Assert.That(executors[1].CurrentOrder.TargetActorId, Is.EqualTo(50));
        }

        [Test] public void PlaybookWithoutRoleRequest_UnassignedMemberAlsoGetsFallback()
        {
            first.Playbook.RoleSlots = new[] { new PlaybookRoleSlots(MonsterRole.Shield, false, TacticalAnchorTag.ObjectiveFront) };
            squad.TryCompleteAlert(1); brain.Tick(0); brain.Tick(1);
            Assert.That(executors[1].CurrentOrder.Type, Is.EqualTo(MonsterOrderType.HoldSlot));
            Assert.That(brain.AllocationFailures[0], Does.Contain("2").And.Contain("request"));
        }

        [Test] public void Context_ExcludesUnreachableAnchorsAndReportsActualAllocationFailure()
        {
            reachability.Result = false;
            squad.TryCompleteAlert(1); brain.Tick(0);
            Assert.That(firstScore.LastContext.AnchorCount(TacticalAnchorTag.ObjectiveFront), Is.Zero);
            Assert.That(firstScore.LastContext.AnchorCount(TacticalAnchorTag.CoverRear), Is.Zero);
            brain.Tick(1);
            Assert.That(brain.AllocationFailures.Count, Is.EqualTo(2));
            Assert.That(brain.AllocationFailures.All(reason => reason.Contains("unreachable") && !reason.Contains("disabled")), Is.True);
            Assert.That(executors.All(executor => executor.CurrentOrder.Type == MonsterOrderType.HoldSlot), Is.True);
        }

        private UtilityTactic Tactic(string id, ScoreInput score)
        {
            var playbook = ScriptableObject.CreateInstance<TacticalPlaybook>(); assets.Add(playbook);
            playbook.RoleSlots = new[] { new PlaybookRoleSlots(MonsterRole.Shield, false, TacticalAnchorTag.ObjectiveFront),
                new PlaybookRoleSlots(MonsterRole.Ranged, false, TacticalAnchorTag.CoverRear) };
            var tactic = ScriptableObject.CreateInstance<UtilityTactic>(); assets.Add(tactic);
            tactic.TacticId = id; tactic.Considerations = new UtilityConsideration[] { score }; tactic.Playbook = playbook;
            return tactic;
        }
        private void AddMember(int id, MonsterRole role)
        {
            var member = Child("Member " + id); var health = member.AddComponent<ActorHealth>();
            var executor = member.AddComponent<MonsterOrderExecutor>(); executors.Add(executor);
            Assert.That(squad.RegisterMember(id, role, health, executor), Is.True);
        }
        private GameObject Child(string name)
        { var child = new GameObject(name); child.transform.SetParent(root.transform); return child; }
    }
}
