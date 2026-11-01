using System;
using System.Collections.Generic;
using AlmostDelivered.CommanderAI.Domain;
using AlmostDelivered.CommanderAI.Tactics;
using NUnit.Framework;
using UnityEngine;

namespace AlmostDelivered.Tests.EditMode
{
    public sealed class UtilityTacticTests
    {
        private UtilityTactic tactic;

        [SetUp] public void SetUp() { tactic = ScriptableObject.CreateInstance<UtilityTactic>(); }
        [TearDown] public void TearDown() { UnityEngine.Object.DestroyImmediate(tactic); }

        [Test] public void Scores_AreNamedAndClamped()
        {
            Configure(new Raw("High", 4f), new Raw("Low", -2f));
            var result = tactic.Evaluate(Context());
            Assert.That(result.IsValid, Is.True);
            Assert.That(result.ComponentScores["High"], Is.EqualTo(1f));
            Assert.That(result.ComponentScores["Low"], Is.Zero);
            Assert.That(result.TotalScore, Is.Zero);
            Assert.Throws<NotSupportedException>(() => ((IDictionary<string, float>)result.ComponentScores).Add("Other", 1f));
        }

        [Test] public void MissingRequiredRole_RejectsWithReason()
        {
            Configure(new Raw("Health", 1f));
            tactic.RequiredRoles = new[] { new RoleRequirement(MonsterRole.Charger, 2) };
            var result = tactic.Evaluate(Context());
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.RejectionReason, Does.Contain("Charger"));
            Assert.That(result.TotalScore, Is.Zero);
        }

        [Test] public void WeightedGeometricMean_UsesAuthoredWeightsAndIsRepeatable()
        {
            Configure(new Raw("First", .25f, 1f), new Raw("Second", 1f, 3f));
            var context = Context();
            var first = tactic.Evaluate(in context);
            var second = tactic.Evaluate(in context);
            Assert.That(first.TotalScore, Is.EqualTo(.70710678f).Within(.000001f));
            Assert.That(second.TotalScore, Is.EqualTo(first.TotalScore));
            Assert.That(second.ComponentScores, Is.EquivalentTo(first.ComponentScores));
        }

        [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)] [TestCase(float.NegativeInfinity)]
        public void NonFiniteScore_InvalidatesInsteadOfWinning(float score)
        {
            Configure(new Raw("Broken", score));
            var result = tactic.Evaluate(Context());
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.RejectionReason, Does.Contain("Broken"));
            Assert.That(result.TotalScore, Is.Zero);
        }

        [Test] public void EmptyNamesDuplicateNamesAndInvalidWeights_AreRejected()
        {
            foreach (var components in new[] {
                new UtilityConsideration[] { new Raw("", 1f) },
                new UtilityConsideration[] { new Raw("Same", 1f), new Raw("Same", 1f) },
                new UtilityConsideration[] { new Raw("Weight", 1f, 0f) },
                new UtilityConsideration[] { new Raw("Weight", 1f, float.NaN) },
                new UtilityConsideration[0] })
            {
                Configure(components);
                Assert.That(tactic.Evaluate(Context()).IsValid, Is.False);
            }
        }

        [Test] public void Context_CopiesKnowledgeRoleCountsAndAnchors()
        {
            var knowledge = new SquadKnowledge();
            var sample = new ObservationSample(7, Vector3.right, Vector3.zero, 0f, 1f, ObservedCombatTag.Healing, 1);
            knowledge.Merge(in sample);
            var roles = new Dictionary<MonsterRole, int> { [MonsterRole.Shield] = 1 };
            var anchors = new Dictionary<TacticalAnchorTag, int> { [TacticalAnchorTag.ObjectiveFront] = 1 };
            var context = new TacticalContext(knowledge.Snapshot, roles, Vector3.zero, anchors, .75f, "old", 3f);
            roles.Clear(); anchors.Clear(); knowledge.Clear();
            Assert.That(context.LivingCount(MonsterRole.Shield), Is.EqualTo(1));
            Assert.That(context.AnchorCount(TacticalAnchorTag.ObjectiveFront), Is.EqualTo(1));
            Assert.That(context.Knowledge.Count, Is.EqualTo(1));
            Assert.That(context.CurrentTacticId, Is.EqualTo("old"));
            Assert.That(context.TacticAge, Is.EqualTo(3f));
        }

        private void Configure(params UtilityConsideration[] components)
        {
            tactic.TacticId = "test";
            tactic.Considerations = components;
        }

        private static TacticalContext Context() => new TacticalContext(new SquadKnowledge().Snapshot,
            new Dictionary<MonsterRole, int>(), Vector3.zero, new Dictionary<TacticalAnchorTag, int>(), 1f);

        private sealed class Raw : UtilityConsideration
        {
            private readonly float score;
            public Raw(string name, float score, float weight = 1f) : base(name, ConsiderationKind.SquadHealth, weight) { this.score = score; }
            public override float Score(in TacticalContext context) => score;
        }
    }
}
