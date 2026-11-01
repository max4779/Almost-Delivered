using System.Collections.Generic;
using System.Linq;
using AlmostDelivered.CommanderAI.Domain;
using AlmostDelivered.CommanderAI.Tactics;
using NUnit.Framework;
using UnityEngine;

namespace AlmostDelivered.Tests.EditMode
{
    public sealed class InitialTacticScenarioTests
    {
        private UtilityTactic[] tactics;
        [SetUp] public void SetUp() { tactics = InitialTacticDefinitions.Create(); }
        [TearDown] public void TearDown()
        {
            foreach (var tactic in tactics) { Object.DestroyImmediate(tactic.Playbook); Object.DestroyImmediate(tactic); }
        }

        [Test] public void ObjectiveBlockade_WinsWhenPlayersThreatenObjectiveAndAreSpreadOut()
        {
            var context = Context(Vector3.zero, Vector3.right * 30f);
            var winner = Winner(in context);
            Assert.That(winner.TacticId, Is.EqualTo("objective-blockade"));
            Assert.That(winner.ComponentScores["Objective proximity"], Is.EqualTo(1f));
            Assert.That(tactics[1].Evaluate(in context).ComponentScores["Player clustering"], Is.Zero);
        }

        [Test] public void Pincer_WinsWhenPlayersClusterAwayFromObjectiveAndBothFlanksExist()
        {
            var context = Context(Vector3.right * 30f, Vector3.right * 31f);
            var winner = Winner(in context);
            Assert.That(winner.TacticId, Is.EqualTo("pincer"));
            Assert.That(winner.ComponentScores["Player clustering"], Is.EqualTo(.875f).Within(.00001f));
            Assert.That(winner.ComponentScores["Left flank availability"], Is.EqualTo(1f));
            Assert.That(winner.ComponentScores["Right flank availability"], Is.EqualTo(1f));
        }

        [TestCase(ObservedCombatTag.Healing)] [TestCase(ObservedCombatTag.Shield)]
        public void FocusSupport_WinsOnlyWithObservedSupportTags(ObservedCombatTag tag)
        {
            var context = Context(Vector3.right * 30f, Vector3.right * 31f, tag);
            var winner = Winner(in context);
            Assert.That(winner.TacticId, Is.EqualTo("focus-support"));
            Assert.That(winner.ComponentScores["Observed support"], Is.EqualTo(1f));
            Assert.That(context.Knowledge.Single(memory => memory.ActorId == 20).Tags, Is.EqualTo(tag));
        }

        [Test] public void NoObservedSupportOrExpiredConfidence_SuppressesFocusSupport()
        {
            foreach (var tag in new[] { ObservedCombatTag.None, ObservedCombatTag.RangedWeapon, ObservedCombatTag.Mobility })
            {
                var context = Context(Vector3.right * 30f, Vector3.right * 31f, tag);
                var focus = tactics[2].Evaluate(in context);
                Assert.That(focus.ComponentScores["Observed support"], Is.Zero);
                Assert.That(focus.TotalScore, Is.Zero);
                Assert.That(tactics[2].Playbook.BuildRequests(in context), Is.Empty);
            }
            var expired = Context(Vector3.right * 30f, Vector3.right * 31f, ObservedCombatTag.Healing, 0f);
            Assert.That(tactics[2].Evaluate(in expired).TotalScore, Is.Zero);
            Assert.That(tactics[2].Playbook.BuildRequests(in expired), Is.Empty);
        }

        [Test] public void MissingOneFlank_SuppressesPincer()
        {
            var context = Context(Vector3.right * 30f, Vector3.right * 31f, rightFlank: 0);
            var evaluation = tactics[1].Evaluate(in context);
            Assert.That(evaluation.ComponentScores["Right flank availability"], Is.Zero);
            Assert.That(evaluation.TotalScore, Is.Zero);
        }

        [Test] public void Blockade_RequestsShieldsInFrontAndRangedInRear()
        {
            var context = Context(Vector3.zero, Vector3.right * 30f);
            var requests = tactics[0].Playbook.BuildRequests(in context);
            Assert.That(requests.Count(request => request.Role == MonsterRole.Shield && request.Slot.Tag == TacticalAnchorTag.ObjectiveFront), Is.EqualTo(2));
            Assert.That(requests.Count(request => request.Role == MonsterRole.Ranged && request.Slot.Tag == TacticalAnchorTag.CoverRear), Is.EqualTo(2));
            Assert.That(requests.All(request => request.TargetActorId == 0), Is.True);
            Assert.That(requests.All(request => request.Slot.Position == Vector3.zero), Is.True);
        }

        [Test] public void Pincer_SplitsLivingChargersAcrossBothFlanksWithoutDuplication()
        {
            var context = Context(Vector3.right * 30f, Vector3.right * 31f);
            var chargers = tactics[1].Playbook.BuildRequests(in context).Where(request => request.Role == MonsterRole.Charger).ToArray();
            Assert.That(chargers.Length, Is.EqualTo(3));
            Assert.That(chargers.Count(request => request.Slot.Tag == TacticalAnchorTag.LeftFlank), Is.EqualTo(2));
            Assert.That(chargers.Count(request => request.Slot.Tag == TacticalAnchorTag.RightFlank), Is.EqualTo(1));
        }

        [Test] public void FocusSupport_PressuresObservedTargetAndKeepsSupportInRear()
        {
            var context = Context(Vector3.right * 30f, Vector3.right * 31f, ObservedCombatTag.Healing);
            var requests = tactics[2].Playbook.BuildRequests(in context);
            var attackers = requests.Where(request => request.Role == MonsterRole.Charger || request.Role == MonsterRole.Ranged).ToArray();
            Assert.That(attackers.Length, Is.EqualTo(5));
            Assert.That(attackers.All(request => request.TargetActorId == 20 && request.Slot.Position == Vector3.right * 30f), Is.True);
            var support = requests.Single(request => request.Role == MonsterRole.Support);
            Assert.That(support.Slot.Tag, Is.EqualTo(TacticalAnchorTag.SupportRear));
            Assert.That(support.TargetActorId, Is.Zero);
            Assert.That(support.Slot.Position, Is.EqualTo(Vector3.zero));
        }

        [Test] public void SupportTargetTie_UsesLowestObservedIdRegardlessOfInputOrder()
        {
            var knowledge = new SquadKnowledge();
            foreach (var id in new[] { 30, 10 })
            {
                var sample = new ObservationSample(id, Vector3.right * id, Vector3.zero, 0f, .8f, ObservedCombatTag.Shield, 1);
                knowledge.Merge(in sample);
            }
            var context = MakeContext(knowledge);
            Assert.That(context.TryGetSupportTarget(out var target), Is.True);
            Assert.That(target.ActorId, Is.EqualTo(10));
            Assert.That(tactics[2].Playbook.BuildRequests(in context).First().TargetActorId, Is.EqualTo(10));
        }

        private TacticEvaluation Winner(in TacticalContext context)
        {
            TacticEvaluation winner = null;
            foreach (var tactic in tactics)
            {
                var result = tactic.Evaluate(in context);
                if (result.IsValid && (winner == null || result.TotalScore > winner.TotalScore)) winner = result;
            }
            return winner;
        }

        private static TacticalContext Context(Vector3 first, Vector3 second, ObservedCombatTag tag = ObservedCombatTag.None,
            float confidence = 1f, int rightFlank = 2)
        {
            var knowledge = new SquadKnowledge();
            var a = new ObservationSample(20, first, Vector3.zero, 0f, confidence, tag, 1);
            var b = new ObservationSample(21, second, Vector3.zero, 0f, 1f, ObservedCombatTag.None, 1);
            knowledge.Merge(in a); knowledge.Merge(in b);
            return MakeContext(knowledge, rightFlank);
        }

        private static TacticalContext MakeContext(SquadKnowledge knowledge, int rightFlank = 2) => new TacticalContext(
            knowledge.Snapshot, new Dictionary<MonsterRole, int> { [MonsterRole.Shield] = 2, [MonsterRole.Ranged] = 2,
                [MonsterRole.Charger] = 3, [MonsterRole.Support] = 1 }, Vector3.zero,
            new Dictionary<TacticalAnchorTag, int> { [TacticalAnchorTag.ObjectiveFront] = 2, [TacticalAnchorTag.CoverRear] = 2,
                [TacticalAnchorTag.LeftFlank] = 2, [TacticalAnchorTag.RightFlank] = rightFlank, [TacticalAnchorTag.SupportRear] = 1 }, 1f);
    }
}
