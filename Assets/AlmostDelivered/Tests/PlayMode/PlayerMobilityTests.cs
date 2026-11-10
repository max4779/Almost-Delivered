using AlmostDelivered.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace AlmostDelivered.Tests.PlayMode
{
    public sealed class PlayerMobilityTests
    {
        private GameObject player;
        private PlayerMobility mobility;

        [SetUp]
        public void SetUp()
        {
            player = new GameObject("Mobility Player", typeof(PlayerMobility));
            mobility = player.GetComponent<PlayerMobility>();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(player);

        [Test]
        public void SprintUsesStaminaAndRestoresItOnlyAfterStandingStill()
        {
            Assert.That(mobility.Step(Vector2.right, true, false, 1f, 6f).x, Is.EqualTo(9.6f).Within(.001f));
            Assert.That(mobility.Stamina, Is.EqualTo(82f));
            Assert.That(mobility.IsSprinting, Is.True);

            mobility.Step(Vector2.zero, false, false, 1.9f, 6f);
            Assert.That(mobility.Stamina, Is.EqualTo(82f));
            mobility.Step(Vector2.zero, false, false, .1f, 6f);
            Assert.That(mobility.Stamina, Is.GreaterThan(82f));
            Assert.That(mobility.IsSprinting, Is.False);
        }

        [Test]
        public void RollConsumesStaminaAndKeepsItsDirectionWithoutFurtherInput()
        {
            var first = mobility.Step(Vector2.right, false, true, .1f, 6f);
            Assert.That(first.x, Is.EqualTo(13f).Within(.001f));
            Assert.That(mobility.Stamina, Is.EqualTo(72f));
            Assert.That(mobility.IsRolling, Is.True);

            var second = mobility.Step(Vector2.zero, false, false, .1f, 6f);
            Assert.That(second.x, Is.EqualTo(13f).Within(.001f));
            Assert.That(mobility.Stamina, Is.EqualTo(72f));
            mobility.Step(Vector2.zero, false, false, .2f, 6f);
            Assert.That(mobility.IsRolling, Is.False);
        }
    }
}
