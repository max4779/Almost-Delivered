using AlmostDelivered.Combat;
using AlmostDelivered.Frontend;
using NUnit.Framework;
using UnityEngine;

namespace AlmostDelivered.Tests.PlayMode
{
    public sealed class FirearmMagazineTests
    {
        private GameObject player;
        private GameObject template;

        [TearDown]
        public void TearDown()
        {
            PlayerWeaponLoadout.Select(PlayerWeaponType.AutomaticRifle);
            foreach (var shot in Object.FindObjectsOfType<CombatProjectile>(true))
                Object.DestroyImmediate(shot.gameObject);
            if (player != null) Object.DestroyImmediate(player);
            if (template != null) Object.DestroyImmediate(template);
        }

        [Test]
        public void RevolverSpendsSixRoundsAndReloadsOnlyAfterDelay()
        {
            var attack = CreateAttack();
            PlayerWeaponLoadout.ConfigureFirearm(attack, PlayerWeaponType.Revolver);
            Assert.That(attack.MagazineSize, Is.EqualTo(6));
            Assert.That(attack.Damage, Is.EqualTo(55f));
            for (var i = 0; i < 6; i++)
                Assert.That(attack.TryFire(Vector3.forward * 10f, i * .6f), Is.True);
            Assert.That(attack.RoundsRemaining, Is.Zero);
            Assert.That(attack.TryFire(Vector3.forward * 10f, 4f), Is.False);
            Assert.That(attack.TryReload(4f), Is.True);
            Assert.That(attack.TryFire(Vector3.forward * 10f, 5f), Is.False);
            attack.TickReload(6.39f);
            Assert.That(attack.RoundsRemaining, Is.Zero);
            attack.TickReload(6.4f);
            Assert.That(attack.RoundsRemaining, Is.EqualTo(6));
            Assert.That(attack.TryFire(Vector3.forward * 10f, 6.4f), Is.True);
        }

        [Test]
        public void RifleHasThirtyLowerDamageRoundsAndRespectsFireInterval()
        {
            var attack = CreateAttack();
            PlayerWeaponLoadout.ConfigureFirearm(attack, PlayerWeaponType.AutomaticRifle);
            Assert.That(attack.MagazineSize, Is.EqualTo(30));
            Assert.That(attack.Damage, Is.EqualTo(12f));
            Assert.That(attack.TryFire(Vector3.forward * 10f, 0f), Is.True);
            Assert.That(attack.TryFire(Vector3.forward * 10f, .05f), Is.False);
            Assert.That(attack.RoundsRemaining, Is.EqualTo(29));
            Assert.That(attack.TryFire(Vector3.forward * 10f, .12f), Is.True);
            Assert.That(attack.RoundsRemaining, Is.EqualTo(28));
            Assert.That(attack.TryReload(.2f), Is.True);
            attack.TickReload(2f);
            Assert.That(attack.RoundsRemaining, Is.EqualTo(30));
        }

        private RangedAttackController CreateAttack()
        {
            player = new GameObject("Player", typeof(CombatTeam), typeof(RangedAttackController));
            player.GetComponent<CombatTeam>().Team = CombatTeamId.Player;
            var muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(player.transform, false);
            template = new GameObject("Projectile Template", typeof(CombatProjectile));
            template.SetActive(false);
            var attack = player.GetComponent<RangedAttackController>();
            attack.Configure(template, muzzle, 20f, .25f, 20f, 2f, 0f);
            return attack;
        }
    }
}
