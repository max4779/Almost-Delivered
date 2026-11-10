using AlmostDelivered.Combat;
using AlmostDelivered.Frontend;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace AlmostDelivered.Tests.PlayMode
{
    public sealed class FirearmMagazineTests
    {
        private GameObject player;
        private GameObject template;
        private GameObject hudRoot;

        [TearDown]
        public void TearDown()
        {
            PlayerWeaponLoadout.Select(PlayerWeaponType.AutomaticRifle);
            foreach (var shot in Object.FindObjectsOfType<CombatProjectile>(true))
                Object.DestroyImmediate(shot.gameObject);
            if (player != null) Object.DestroyImmediate(player);
            if (template != null) Object.DestroyImmediate(template);
            if (hudRoot != null) Object.DestroyImmediate(hudRoot);
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
            Assert.That(attack.IsReloading, Is.False, "The last shot alone must not begin reloading.");
            Assert.That(attack.TryFire(Vector3.forward * 10f, 3.01f), Is.False);
            Assert.That(attack.IsReloading, Is.True);
            Assert.That(attack.TryReload(3.02f), Is.False);
            Assert.That(attack.TryFire(Vector3.forward * 10f, 4f), Is.False);
            attack.TickReload(5.4f);
            Assert.That(attack.RoundsRemaining, Is.Zero);
            attack.TickReload(5.42f);
            Assert.That(attack.RoundsRemaining, Is.EqualTo(6));
            Assert.That(attack.TryFire(Vector3.forward * 10f, 5.42f), Is.True);
        }

        [Test]
        public void RifleHasTwentyLowerDamageRoundsAndRespectsFireInterval()
        {
            var attack = CreateAttack();
            PlayerWeaponLoadout.ConfigureFirearm(attack, PlayerWeaponType.AutomaticRifle);
            Assert.That(attack.MagazineSize, Is.EqualTo(20));
            Assert.That(attack.Damage, Is.EqualTo(8f));
            Assert.That(attack.TryFire(Vector3.forward * 10f, 0f), Is.True);
            Assert.That(attack.TryFire(Vector3.forward * 10f, .05f), Is.False);
            Assert.That(attack.RoundsRemaining, Is.EqualTo(19));
            Assert.That(attack.TryFire(Vector3.forward * 10f, .12f), Is.True);
            Assert.That(attack.RoundsRemaining, Is.EqualTo(18));
            Assert.That(attack.TryReload(.2f), Is.True);
            attack.TickReload(2f);
            Assert.That(attack.RoundsRemaining, Is.EqualTo(20));
        }

        [Test]
        public void EmptyRifleBeginsOneReloadOnNextFireInput()
        {
            var attack = CreateAttack();
            PlayerWeaponLoadout.ConfigureFirearm(attack, PlayerWeaponType.AutomaticRifle);
            for (var i = 0; i < 20; i++)
                Assert.That(attack.TryFire(Vector3.forward * 10f, i * .125f), Is.True);
            Assert.That(attack.RoundsRemaining, Is.Zero);
            Assert.That(attack.IsReloading, Is.False);

            Assert.That(attack.TryFire(Vector3.forward * 10f, 2.38f), Is.False);
            Assert.That(attack.IsReloading, Is.True);
            var firstEndTime = attack.ReloadEndTime;
            Assert.That(attack.TryFire(Vector3.forward * 10f, 2.5f), Is.False);
            Assert.That(attack.ReloadEndTime, Is.EqualTo(firstEndTime));
            attack.TickReload(4.2f);
            Assert.That(attack.RoundsRemaining, Is.EqualTo(20));
            Assert.That(attack.TryFire(Vector3.forward * 10f, 4.2f), Is.True);
            Assert.That(attack.RoundsRemaining, Is.EqualTo(19));
        }

        [Test]
        public void RifleHudShowsRemainingRoundsWhereReloadPromptWas()
        {
            var attack = CreateAttack();
            PlayerWeaponLoadout.Select(PlayerWeaponType.AutomaticRifle);
            PlayerWeaponLoadout.ConfigureFirearm(attack, PlayerWeaponType.AutomaticRifle);
            hudRoot = new GameObject("Ammo HUD", typeof(RectTransform), typeof(PlayerAmmoHud));
            var revolverPanel = new GameObject("Revolver Ammo");
            revolverPanel.transform.SetParent(hudRoot.transform, false);
            var riflePanel = new GameObject("Automatic Rifle Ammo");
            riflePanel.transform.SetParent(hudRoot.transform, false);
            var count = new GameObject("Rifle Ammo Count", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text))
                .GetComponent<Text>();
            count.transform.SetParent(riflePanel.transform, false);
            var status = new GameObject("Reload Status", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text))
                .GetComponent<Text>();
            status.transform.SetParent(hudRoot.transform, false);
            var hud = hudRoot.GetComponent<PlayerAmmoHud>();
            hud.Configure(attack, revolverPanel, riflePanel, null, count, status);

            Assert.That(count.text, Is.EqualTo("20/20"));
            Assert.That(count.verticalOverflow, Is.EqualTo(VerticalWrapMode.Overflow));
            Assert.That(riflePanel.transform.Find("Rifle Muzzle"), Is.Not.Null);
            Assert.That(status.rectTransform.anchorMin.x, Is.Zero);
            Assert.That(attack.TryFire(Vector3.forward * 10f, 0f), Is.True);
            hud.Refresh();
            Assert.That(count.text, Is.EqualTo("19/20"));
            Assert.That(attack.TryReload(.2f), Is.True);
            hud.Refresh();
            Assert.That(status.text, Is.EqualTo("장전 중..."));
            Assert.That(count.text, Is.EqualTo("19/20"));
            attack.TickReload(2f);
            hud.Refresh();
            Assert.That(count.text, Is.EqualTo("20/20"));
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
