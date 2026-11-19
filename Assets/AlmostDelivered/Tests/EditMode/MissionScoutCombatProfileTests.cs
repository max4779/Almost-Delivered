using AlmostDelivered.Frontend;
using AlmostDelivered.Gameplay.Payload;
using NUnit.Framework;

namespace AlmostDelivered.Tests.EditMode
{
    public sealed class MissionScoutCombatProfileTests
    {
        [Test]
        public void RepeatedVisibleShotsAndMovementBuildAWeaponHabit()
        {
            var profile = new MissionScoutCombatProfile(42);
            profile.ObserveStatus(PlayerWeaponType.AutomaticRifle, false, false, 1f);
            profile.ObserveShot(1f);
            profile.ObserveShot(1.2f);
            profile.ObserveShot(1.4f);
            profile.ObserveReload(1.5f);
            profile.ObserveStatus(PlayerWeaponType.AutomaticRifle, true, true, 1.6f);
            profile.ObserveStatus(PlayerWeaponType.AutomaticRifle, true, true, 1.8f);
            profile.ObserveStatus(PlayerWeaponType.Revolver, false, false, 2f);

            Assert.That(profile.ActorId, Is.EqualTo(42));
            Assert.That(profile.IsRapidFire, Is.True);
            Assert.That(profile.ShotsObserved, Is.EqualTo(3));
            Assert.That(profile.ReloadsObserved, Is.EqualTo(1));
            Assert.That(profile.SprintEpisodesObserved, Is.EqualTo(1));
            Assert.That(profile.ObservedRolls, Is.EqualTo(1));
            Assert.That(profile.WeaponSwitches, Is.EqualTo(1));
            Assert.That(profile.WeaponLabel, Is.EqualTo("REVOLVER"));
        }
    }
}
