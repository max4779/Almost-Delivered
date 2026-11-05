using AlmostDelivered.Combat;
using AlmostDelivered.Gameplay.Payload;
using NUnit.Framework;
using UnityEngine;

namespace AlmostDelivered.Tests.PlayMode
{
    public sealed class MissionMobControllerTests
    {
        private GameObject target;
        private GameObject mob;
        private GameObject projectileTemplate;
        private GameObject floor;

        [SetUp]
        public void SetUp()
        {
            target = Actor("Target", CombatTeamId.Player, new Vector3(0f, 0f, 10f));
            mob = Actor("Mob", CombatTeamId.Enemy, Vector3.zero);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var projectile in Object.FindObjectsOfType<CombatProjectile>())
            {
                Object.DestroyImmediate(projectile.gameObject);
            }

            Object.DestroyImmediate(projectileTemplate);
            Object.DestroyImmediate(mob);
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(floor);
        }

        [Test]
        public void MeleeApproachesThenDamagesOnCooldown()
        {
            var controller = mob.AddComponent<MissionMobController>();
            controller.Configure(MissionMobDefinition.For(MissionMobArchetype.Melee), target.transform, null);

            controller.Tick(0f, .5f);
            Assert.That(mob.transform.position.z, Is.EqualTo(2f).Within(.001f));

            mob.transform.position = new Vector3(0f, 0f, 9f);
            controller.Tick(.1f, 0f);
            controller.Tick(.5f, 0f);
            Assert.That(target.GetComponent<ActorHealth>().CurrentHealth, Is.EqualTo(90f));

            controller.Tick(1.2f, 0f);
            Assert.That(target.GetComponent<ActorHealth>().CurrentHealth, Is.EqualTo(80f));
        }

        [Test]
        public void RangedApproachesStandOffRangeThenFires()
        {
            projectileTemplate = new GameObject("Projectile Template", typeof(CombatProjectile));
            projectileTemplate.SetActive(false);
            var attack = mob.AddComponent<RangedAttackController>();
            var muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(mob.transform, false);
            attack.Configure(projectileTemplate, muzzle, 8f, 1f, 15f, 2f);
            var controller = mob.AddComponent<MissionMobController>();
            controller.Configure(MissionMobDefinition.For(MissionMobArchetype.Ranged), target.transform, attack);

            controller.Tick(0f, .5f);
            Assert.That(mob.transform.position.z, Is.EqualTo(1.5f).Within(.001f));

            mob.transform.position = new Vector3(0f, 0f, 4.5f);
            controller.Tick(0f, 0f);
            Assert.That(Object.FindObjectsOfType<CombatProjectile>(), Has.Length.EqualTo(1));
        }

        [Test]
        public void ShieldConfigurationAppliesHealthAndSlowerMovement()
        {
            var definition = MissionMobDefinition.For(MissionMobArchetype.Shield);
            var controller = mob.AddComponent<MissionMobController>();
            controller.Configure(definition, target.transform, null);

            controller.Tick(0f, 1f);

            Assert.That(mob.GetComponent<ActorHealth>().MaxHealth, Is.EqualTo(90f));
            Assert.That(mob.GetComponent<ActorHealth>().CurrentHealth, Is.EqualTo(90f));
            Assert.That(controller.Definition.MoveSpeed, Is.EqualTo(2.2f));
            Assert.That(mob.transform.position.z, Is.EqualTo(2.2f).Within(.001f));
        }

        [Test]
        public void DeathDisablesControllerAndColliders()
        {
            var collider = mob.AddComponent<CapsuleCollider>();
            var controller = mob.AddComponent<MissionMobController>();
            controller.Configure(MissionMobDefinition.For(MissionMobArchetype.Melee), target.transform, null);

            mob.GetComponent<ActorHealth>().ApplyDamage(1000f);

            Assert.That(controller.enabled, Is.False);
            Assert.That(collider.enabled, Is.False);
        }

        [Test]
        public void NullOrDeadTargetLeavesMobIdle()
        {
            var controller = mob.AddComponent<MissionMobController>();
            controller.Configure(MissionMobDefinition.For(MissionMobArchetype.Melee), null, null);
            controller.Tick(0f, 1f);
            Assert.That(mob.transform.position, Is.EqualTo(Vector3.zero));

            controller.Configure(MissionMobDefinition.For(MissionMobArchetype.Melee), target.transform, null);
            target.GetComponent<ActorHealth>().ApplyDamage(1000f);
            controller.Tick(2f, 1f);
            Assert.That(mob.transform.position, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void CharacterControllerMobFallsToGroundWhileStationary()
        {
            floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Ground";
            floor.transform.position = new Vector3(0f, -.5f, 0f);
            floor.transform.localScale = new Vector3(20f, 1f, 20f);
            var character = mob.AddComponent<CharacterController>();
            character.height = 2f;
            character.radius = .5f;
            character.center = Vector3.up;
            mob.transform.position = new Vector3(0f, 4f, 0f);
            target.transform.position = Vector3.zero;
            var controller = mob.AddComponent<MissionMobController>();
            controller.Configure(MissionMobDefinition.For(MissionMobArchetype.Melee), target.transform, null);
            Physics.SyncTransforms();

            for (var step = 0; step < 80; step++)
                controller.Tick(step * .05f, .05f);

            Assert.That(mob.transform.position.y, Is.EqualTo(.08f).Within(.02f));
        }

        private static GameObject Actor(string name, CombatTeamId team, Vector3 position)
        {
            var actor = new GameObject(name);
            actor.transform.position = position;
            actor.AddComponent<ActorHealth>();
            actor.AddComponent<CombatTeam>().Team = team;
            return actor;
        }
    }
}
