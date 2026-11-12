using AlmostDelivered.Frontend;
using NUnit.Framework;

namespace AlmostDelivered.Tests.EditMode
{
    public sealed class PlayerTraitProgressTests
    {
        [Test]
        public void LevelPointsRespectTotalCategoryAndTraitLimits()
        {
            var progress = new PlayerTraitProgress();
            Assert.That(progress.AvailablePoints, Is.EqualTo(1));
            for (var i = 0; i < 19; i++) Assert.That(progress.GrantLevel(), Is.True);
            Assert.That(progress.GrantLevel(), Is.False);
            Assert.That(progress.level, Is.EqualTo(20));

            for (var i = 0; i < 5; i++)
                Assert.That(progress.TryInvest("combat.cargo_fire"), Is.True);
            for (var i = 0; i < 5; i++)
                Assert.That(progress.TryInvest("combat.counter"), Is.True);
            Assert.That(progress.TryInvest("combat.cargo_fire"), Is.False);
            Assert.That(progress.CategoryPoints(PlayerTraitCategory.Combat), Is.EqualTo(10));
            for (var i = 0; i < 5; i++)
            {
                Assert.That(progress.TryInvest("survival.armor"), Is.True);
                Assert.That(progress.TryInvest("escort.lead"), Is.True);
            }
            Assert.That(progress.AvailablePoints, Is.Zero);
            Assert.That(progress.TryInvest("escort.repair"), Is.False);
            Assert.That(progress.SpentPoints, Is.EqualTo(20));
            progress.ResetRanks();
            Assert.That(progress.AvailablePoints, Is.EqualTo(20));
        }

        [Test]
        public void UnknownTraitCannotConsumePointsAndStableIdSurvivesCopy()
        {
            var progress = new PlayerTraitProgress();
            Assert.That(progress.TryInvest("renamed label"), Is.False);
            Assert.That(progress.TryInvest("escort.lead"), Is.True);
            var saved = progress.Copy();
            Assert.That(saved.Rank("escort.lead"), Is.EqualTo(1));
            Assert.That(saved.AvailablePoints, Is.Zero);
        }

        [Test]
        public void UpperTraitsRequireThreeRanksInTheirOwnLowerBranch()
        {
            var progress = new PlayerTraitProgress { level = 20 };
            var lower = new[] { "combat.cargo_fire", "survival.armor", "escort.lead" };
            var upper = new[] { "combat.counter", "survival.mobility", "escort.repair" };
            for (var branch = 0; branch < lower.Length; branch++)
            {
                Assert.That(progress.CanInvest(upper[branch]), Is.False);
                for (var rank = 0; rank < 2; rank++)
                    Assert.That(progress.TryInvest(lower[branch]), Is.True);
                Assert.That(progress.TryInvest(upper[branch]), Is.False);
                Assert.That(progress.TryInvest(lower[branch]), Is.True);
                Assert.That(progress.TryInvest(upper[branch]), Is.True);
            }
            Assert.That(progress.Rank("combat.counter"), Is.EqualTo(1));
            Assert.That(progress.Rank("survival.mobility"), Is.EqualTo(1));
            Assert.That(progress.Rank("escort.repair"), Is.EqualTo(1));
        }
    }
}
