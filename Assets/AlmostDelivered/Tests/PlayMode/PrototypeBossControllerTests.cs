using AlmostDelivered.Combat;
using AlmostDelivered.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace AlmostDelivered.Tests.PlayMode
{
    public sealed class PrototypeBossControllerTests
    {
        [TearDown]
        public void TearDown()
        {
            foreach (var actor in Object.FindObjectsOfType<ActorHealth>(true))
                Object.DestroyImmediate(actor.gameObject);
            foreach (var projectile in Object.FindObjectsOfType<CombatProjectile>(true))
                Object.DestroyImmediate(projectile.gameObject);
        }

        [Test]
        public void WardenFacesTargetAndFiresThroughRangedController()
        {
            var target = Actor("Target", CombatTeamId.Player, new Vector3(0f, 0f, 8f));
            var boss = Boss("Warden", CombatTeamId.Enemy, Vector3.zero, out var controller, out var attack);
            controller.Configure(PrototypeBossMode.RangedWarden, target.transform, attack, 0f, 2f, 10f, 1f);

            controller.Tick(0f, .1f);

            Assert.That(Vector3.Dot(boss.transform.forward, Vector3.forward), Is.GreaterThan(.999f));
            Assert.That(Object.FindObjectsOfType<CombatProjectile>().Length, Is.EqualTo(1));
        }

        [Test]
        public void ChargerApproachesThenDealsCooldownContactDamage()
        {
            var target = Actor("Target", CombatTeamId.Player, new Vector3(0f, 0f, 5f));
            var boss = Boss("Charger", CombatTeamId.Enemy, Vector3.zero, out var controller, out _);
            controller.Configure(PrototypeBossMode.IronCharger, target.transform, null, 4f, 1.5f, 15f, 1f);

            controller.Tick(0f, .5f);
            Assert.That(boss.transform.position.z, Is.EqualTo(2f).Within(.01f));

            boss.transform.position = new Vector3(0f, 0f, 4f);
            controller.Tick(1f, .1f);
            controller.Tick(1.5f, .1f);
            Assert.That(target.GetComponent<ActorHealth>().CurrentHealth, Is.EqualTo(85f));
            controller.Tick(2f, .1f);
            Assert.That(target.GetComponent<ActorHealth>().CurrentHealth, Is.EqualTo(70f));
        }

        [Test]
        public void DeadBossImmediatelyDisablesMovementAttackAndColliders()
        {
            var target = Actor("Target", CombatTeamId.Player, Vector3.forward * 5f);
            var boss = Boss("Warden", CombatTeamId.Enemy, Vector3.zero, out var controller, out var attack);
            var collider = boss.AddComponent<BoxCollider>();
            controller.Configure(PrototypeBossMode.RangedWarden, target.transform, attack, 0f, 2f, 10f, 1f);

            boss.GetComponent<ActorHealth>().ApplyDamage(100f);

            Assert.That(controller.IsDefeated, Is.True);
            Assert.That(controller.enabled, Is.False);
            Assert.That(attack.enabled, Is.False);
            Assert.That(collider.enabled, Is.False);
        }

        [Test]
        public void NullTargetKeepsBossIdle()
        {
            var boss = Boss("Charger", CombatTeamId.Enemy, Vector3.zero, out var controller, out _);
            controller.Configure(PrototypeBossMode.IronCharger, null, null, 4f, 1.5f, 15f, 1f);
            controller.Tick(0f, 1f);
            Assert.That(boss.transform.position, Is.EqualTo(Vector3.zero));
        }

        private static GameObject Actor(string name, CombatTeamId team, Vector3 position)
        {
            var actor = new GameObject(name, typeof(ActorHealth), typeof(CombatTeam));
            actor.transform.position = position;
            actor.GetComponent<CombatTeam>().Team = team;
            return actor;
        }

        private static GameObject Boss(
            string name,
            CombatTeamId team,
            Vector3 position,
            out PrototypeBossController controller,
            out RangedAttackController attack)
        {
            var boss = Actor(name, team, position);
            attack = boss.AddComponent<RangedAttackController>();
            controller = boss.AddComponent<PrototypeBossController>();
            var muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(boss.transform, false);
            muzzle.localPosition = Vector3.forward;
            var template = new GameObject("Projectile Template", typeof(CombatProjectile));
            template.SetActive(false);
            attack.Configure(template, muzzle, 10f, .5f, 20f, 2f);
            return boss;
        }
    }
}
