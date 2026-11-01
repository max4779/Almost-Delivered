using AlmostDelivered.CommanderAI.Commander;
using AlmostDelivered.CommanderAI.Domain;
using AlmostDelivered.CommanderAI.Tactics;
using NUnit.Framework;
using UnityEngine;

namespace AlmostDelivered.Tests.EditMode
{
    public sealed class TacticSwitchPolicyTests
    {
        [TestCase(.29f)] [TestCase(.3f)]
        public void CandidateMustExceedActivationThreshold(float score)
        {
            Assert.That(Switch(.1f, score, 10f), Is.False);
        }

        [Test] public void MinimumHold_BlocksUntilBoundary()
        {
            Assert.That(Switch(.4f, .9f, 1.99f), Is.False);
            Assert.That(Switch(.4f, .9f, 2f), Is.True);
        }

        [Test] public void Margin_MustBeExceeded()
        {
            Assert.That(Switch(.5f, .59f, 10f), Is.False);
            Assert.That(Switch(.5f, .6f, 10f), Is.False);
            Assert.That(Switch(.5f, .61f, 10f), Is.True);
        }

        [Test] public void ExactTie_RetainsCurrentEvenWithZeroMargin()
        {
            Assert.That(TacticSwitchPolicy.ShouldSwitch(Eval("A", .8f), Eval("B", .8f), 10f, .3f, 2f, 0f), Is.False);
        }

        [Test] public void AlternatingNearEqualScores_NeverThrashes()
        {
            for (var cycle = 0; cycle < 100; cycle++)
            {
                var a = cycle % 2 == 0 ? .6f : .62f;
                var b = cycle % 2 == 0 ? .62f : .6f;
                Assert.That(Switch(a, b, cycle * 3f), Is.False, "Cycle " + cycle);
            }
        }

        [Test] public void InvalidCandidateAndSameTactic_DoNotSwitch()
        {
            Assert.That(TacticSwitchPolicy.ShouldSwitch(Eval("A", .4f), new TacticEvaluation("B", false, 1f, "Missing role"), 10f, .3f, 2f, .1f), Is.False);
            Assert.That(TacticSwitchPolicy.ShouldSwitch(Eval("A", .4f), Eval("A", .9f), 10f, .3f, 2f, .1f), Is.False);
            Assert.That(TacticSwitchPolicy.ShouldSwitch(Eval("A", .4f), Eval("B", float.NaN), 10f, .3f, 2f, .1f), Is.False);
        }

        [Test] public void NoCurrentTactic_CanActivateWithoutWaitingForHold()
        {
            Assert.That(TacticSwitchPolicy.ShouldSwitch(null, Eval("B", .8f), 0f, .3f, 2f, .1f), Is.True);
        }

        [Test] public void MalformedPolicyInputs_DoNotSwitch()
        {
            Assert.That(TacticSwitchPolicy.ShouldSwitch(Eval("A", .4f), Eval("B", .9f), float.NaN, .3f, 2f, .1f), Is.False);
            Assert.That(TacticSwitchPolicy.ShouldSwitch(Eval("A", .4f), Eval("B", .9f), 10f, .3f, 2f, -.1f), Is.False);
        }

        [Test] public void DifficultyProfiles_ExposeTimingAndTacticCapacity()
        {
            var easy = CommanderDifficultyProfile.Create(CommanderDifficulty.Easy);
            var normal = CommanderDifficultyProfile.Create(CommanderDifficulty.Normal);
            var hard = CommanderDifficultyProfile.Create(CommanderDifficulty.Hard);
            try
            {
                Assert.That(easy.CommandCycleSeconds, Is.EqualTo(5f));
                Assert.That(normal.CommandCycleSeconds, Is.EqualTo(3f));
                Assert.That(hard.CommandCycleSeconds, Is.EqualTo(1.5f));
                Assert.That(easy.AvailableTacticIds, Is.EqualTo(new[] { "objective-blockade" }));
                Assert.That(normal.AvailableTacticIds, Is.EqualTo(new[] { "objective-blockade", "pincer", "focus-support" }));
                Assert.That(hard.AvailableTacticIds, Is.EqualTo(normal.AvailableTacticIds));
                Assert.That(easy.TelegraphDuration, Is.GreaterThan(normal.TelegraphDuration));
                Assert.That(normal.TelegraphDuration, Is.GreaterThan(hard.TelegraphDuration));
                Assert.That(easy.MinimumHoldTime, Is.GreaterThan(normal.MinimumHoldTime));
                Assert.That(normal.MinimumHoldTime, Is.GreaterThan(hard.MinimumHoldTime));
                Assert.That(easy.SwitchMargin, Is.GreaterThan(normal.SwitchMargin));
                Assert.That(normal.SwitchMargin, Is.GreaterThan(hard.SwitchMargin));
            }
            finally { Object.DestroyImmediate(easy); Object.DestroyImmediate(normal); Object.DestroyImmediate(hard); }
        }

        private static TacticEvaluation Eval(string id, float score) => new TacticEvaluation(id, true, score, null);
        private static bool Switch(float current, float candidate, float age) =>
            TacticSwitchPolicy.ShouldSwitch(Eval("A", current), Eval("B", candidate), age, .3f, 2f, .1f);
    }
}
