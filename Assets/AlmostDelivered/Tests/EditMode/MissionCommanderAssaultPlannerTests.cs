using System.Linq;
using AlmostDelivered.Frontend;
using AlmostDelivered.Gameplay.Payload;
using NUnit.Framework;

namespace AlmostDelivered.Tests.EditMode
{
    public sealed class MissionCommanderAssaultPlannerTests
    {
        [TestCase(PlayerWeaponType.Ranged, MissionMobArchetype.Shield)]
        [TestCase(PlayerWeaponType.Revolver, MissionMobArchetype.Melee)]
        [TestCase(PlayerWeaponType.Melee, MissionMobArchetype.Ranged)]
        public void ObservedWeaponChangesMajorityOfTravelSquad(PlayerWeaponType weapon,
            MissionMobArchetype expected)
        {
            var profile = new MissionScoutCombatProfile(12);
            profile.ObserveStatus(weapon, false, false, 1f);

            var plan = MissionCommanderAssaultPlanner.Plan(profile, 5, false, 337);

            Assert.That(plan, Has.Length.EqualTo(5));
            Assert.That(plan.Count(type => type == expected), Is.EqualTo(3));
            Assert.That(plan, Is.EqualTo(MissionCommanderAssaultPlanner.Plan(profile, 5, false, 337)));
        }

        [Test]
        public void CargoSaboteurKeepsItsSlotWhileCommanderChoosesRemainingEnemies()
        {
            var profile = new MissionScoutCombatProfile(12);
            profile.ObserveStatus(PlayerWeaponType.Ranged, false, false, 1f);

            var plan = MissionCommanderAssaultPlanner.Plan(profile, 5, true, 337);

            Assert.That(plan[0], Is.EqualTo(MissionMobArchetype.CargoSaboteur));
            Assert.That(plan.Count(type => type == MissionMobArchetype.Shield), Is.EqualTo(2));
            Assert.That(plan.Count(type => type == MissionMobArchetype.CargoSaboteur), Is.EqualTo(1));
        }

        [Test]
        public void UnknownWeaponDoesNotCreateACounterSquad()
        {
            var profile = new MissionScoutCombatProfile(12);

            Assert.That(MissionCommanderAssaultPlanner.Plan(profile, 5, false, 337), Is.Empty);
        }
    }
}
