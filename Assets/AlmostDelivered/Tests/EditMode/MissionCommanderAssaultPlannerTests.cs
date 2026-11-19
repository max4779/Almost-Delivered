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
            Assert.That(plan.Count(type => type == expected), Is.GreaterThanOrEqualTo(2));
            Assert.That(plan.Count(type => type == MissionMobArchetype.Shield),
                Is.LessThanOrEqualTo(plan.Count(type => type == MissionMobArchetype.Ranged) * 2));
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

        [TestCase(PlayerWeaponType.Ranged)]
        [TestCase(PlayerWeaponType.Revolver)]
        [TestCase(PlayerWeaponType.Melee)]
        public void BudgetedSquadNeverOverspendsOrUsesThreeShieldsPerRanged(PlayerWeaponType weapon)
        {
            var profile = new MissionScoutCombatProfile(12);
            profile.ObserveStatus(weapon, false, false, 1f);
            var settings = new CommanderWaveBudget { tokensPerWave = 10 };

            var plan = MissionCommanderAssaultPlanner.PlanBudget(profile, settings,
                settings.tokensPerWave, 12, true, 4711);

            Assert.That(plan[0], Is.EqualTo(MissionMobArchetype.CargoSaboteur));
            Assert.That(MissionCommanderAssaultPlanner.SpentTokens(plan, settings),
                Is.LessThanOrEqualTo(10));
            Assert.That(plan.Count(type => type == MissionMobArchetype.Shield),
                Is.LessThanOrEqualTo(plan.Count(type => type == MissionMobArchetype.Ranged) * 2));
            Assert.That(plan, Is.EqualTo(MissionCommanderAssaultPlanner.PlanBudget(profile,
                settings, 10, 12, true, 4711)));
        }

        [Test]
        public void CostsAreEditableAndUnknownIntelStillProducesBalancedAffordableSquad()
        {
            var settings = new CommanderWaveBudget
            {
                tokensPerWave = 7,
                rangedCost = 3,
                shieldCost = 3,
                meleeCost = 2
            };
            var plan = MissionCommanderAssaultPlanner.PlanBudget(null, settings,
                settings.tokensPerWave, 12, false, 5);

            Assert.That(plan, Is.Not.Empty);
            Assert.That(MissionCommanderAssaultPlanner.SpentTokens(plan, settings),
                Is.LessThanOrEqualTo(7));
            Assert.That(plan.Count(type => type == MissionMobArchetype.Shield),
                Is.LessThanOrEqualTo(plan.Count(type => type == MissionMobArchetype.Ranged) * 2));
        }
    }
}
