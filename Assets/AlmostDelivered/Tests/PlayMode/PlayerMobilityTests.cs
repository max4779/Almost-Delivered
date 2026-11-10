using AlmostDelivered.Gameplay;
using AlmostDelivered.Frontend;
using AlmostDelivered.Combat;
using NUnit.Framework;
using UnityEditor;
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
        public void StaminaRecoversWhileWalkingAfterSprintStops()
        {
            mobility.Step(Vector2.right, true, false, 1f, 6f);
            Assert.That(mobility.Stamina, Is.EqualTo(82f));

            mobility.Step(Vector2.right, false, false, 1.9f, 6f);
            Assert.That(mobility.Stamina, Is.EqualTo(82f));
            mobility.Step(Vector2.right, false, false, .1f, 6f);
            Assert.That(mobility.Stamina, Is.GreaterThan(82f));
            Assert.That(mobility.IsSprinting, Is.False);
        }

        [Test]
        public void WeaponSettingsApplyDistinctStaminaValuesWithoutRefillingOnSwitch()
        {
            var rifle = new GameObject("Automatic Rifle", typeof(AutomaticRifleStats));
            rifle.transform.SetParent(player.transform, false);
            var revolver = new GameObject("Revolver", typeof(RevolverStats));
            revolver.transform.SetParent(player.transform, false);
            var melee = new GameObject("Melee Weapon", typeof(MeleeWeaponStats));
            melee.transform.SetParent(player.transform, false);
            SetMobilityValue(rifle.GetComponent<AutomaticRifleStats>(), "maxStamina", 120f);
            SetMobilityValue(rifle.GetComponent<AutomaticRifleStats>(), "sprintDrainPerSecond", 30f);
            SetMobilityValue(revolver.GetComponent<RevolverStats>(), "maxStamina", 80f);
            SetMobilityValue(melee.GetComponent<MeleeWeaponStats>(), "maxStamina", 150f);

            PlayerWeaponLoadout.ConfigureMobility(mobility, PlayerWeaponType.AutomaticRifle);
            Assert.That(mobility.MaxStamina, Is.EqualTo(120f));
            mobility.Step(Vector2.right, true, false, 1f, 6f);
            Assert.That(mobility.Stamina, Is.EqualTo(90f));

            PlayerWeaponLoadout.ConfigureMobility(mobility, PlayerWeaponType.Revolver);
            Assert.That(mobility.MaxStamina, Is.EqualTo(80f));
            Assert.That(mobility.Stamina, Is.EqualTo(60f));
            PlayerWeaponLoadout.ConfigureMobility(mobility, PlayerWeaponType.Melee);
            Assert.That(mobility.MaxStamina, Is.EqualTo(150f));
            Assert.That(mobility.Stamina, Is.EqualTo(112.5f));
        }

        private static void SetMobilityValue(Object stats, string name, float value)
        {
            var serialized = new SerializedObject(stats);
            serialized.FindProperty("mobilitySettings").FindPropertyRelative(name).floatValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
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

        [Test]
        public void RollBlocksDamageUntilTheRollEnds()
        {
            var health = player.AddComponent<ActorHealth>();
            mobility.Step(Vector2.right, false, true, .1f, 6f);
            Assert.That(mobility.IsRollInvulnerable, Is.True);
            health.ApplyDamage(50f);
            Assert.That(health.CurrentHealth, Is.EqualTo(100f));

            mobility.Step(Vector2.zero, false, false, .13f, 6f);
            Assert.That(mobility.IsRolling, Is.True);
            Assert.That(mobility.IsRollInvulnerable, Is.True);
            health.ApplyDamage(50f);
            Assert.That(health.CurrentHealth, Is.EqualTo(100f));

            mobility.Step(Vector2.zero, false, false, .16f, 6f);
            Assert.That(mobility.IsRolling, Is.False);
            Assert.That(mobility.IsRollInvulnerable, Is.False);
            health.ApplyDamage(50f);
            Assert.That(health.CurrentHealth, Is.EqualTo(50f));
        }

        [Test]
        public void RollWithoutEnoughStaminaDoesNotGrantInvulnerability()
        {
            var health = player.AddComponent<ActorHealth>();
            mobility.Step(Vector2.right, true, false, 10f, 6f);
            mobility.Step(Vector2.right, false, true, .02f, 6f);
            Assert.That(mobility.IsRolling, Is.False);
            Assert.That(health.IsInvulnerable, Is.False);
        }
    }
}
