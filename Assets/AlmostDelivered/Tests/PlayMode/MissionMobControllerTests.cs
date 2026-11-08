using System.Collections;
using AlmostDelivered.Combat;
using AlmostDelivered.Gameplay.Payload;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

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
        public void CargoAssaultMobHeadsForCargoThenPursuesVisiblePlayerWithoutExclamation()
        {
            var cargo = new GameObject("Cargo");
            try
            {
                cargo.transform.position = new Vector3(0f, 0f, 10f);
                target.transform.position = new Vector3(0f, 0f, 20f);
                var controller = mob.AddComponent<MissionMobController>();
                controller.Configure(MissionMobDefinition.For(MissionMobArchetype.Melee), target.transform, null);
                controller.ConfigureCargoAssault(cargo.transform);

                controller.Tick(0f, .5f);
                Assert.That(mob.transform.position.z, Is.EqualTo(2f).Within(.001f));
                Assert.That(controller.IsPursuingPlayer, Is.False);

                target.transform.position = new Vector3(2f, 0f, 5f);
                Physics.SyncTransforms();
                controller.Tick(.5f, .1f);
                Assert.That(controller.IsPursuingPlayer, Is.True);
                Assert.That(controller.IsAlerted, Is.False);
                Assert.That(controller.AlertIndicator, Is.Null);
                Assert.That(mob.transform.position.x, Is.GreaterThan(0f));
            }
            finally
            {
                Object.DestroyImmediate(cargo);
            }
        }

        [Test]
        public void ShootingCargoAssaultMobAlertsNearbyAssaultMobsWithoutExclamations()
        {
            var cargo = new GameObject("Cargo");
            var ally = Actor("Assault Ally", CombatTeamId.Enemy, Vector3.right * 3f);
            try
            {
                cargo.transform.position = Vector3.forward * 10f;
                target.transform.position = Vector3.forward * 20f;
                var attacked = mob.AddComponent<MissionMobController>();
                var nearby = ally.AddComponent<MissionMobController>();
                attacked.Configure(MissionMobDefinition.For(MissionMobArchetype.Melee), target.transform, null);
                nearby.Configure(MissionMobDefinition.For(MissionMobArchetype.Melee), target.transform, null);
                attacked.ConfigureCargoAssault(cargo.transform);
                nearby.ConfigureCargoAssault(cargo.transform);

                mob.GetComponent<ActorHealth>().ApplyDamage(5f);

                Assert.That(attacked.IsPursuingPlayer, Is.True);
                Assert.That(nearby.IsPursuingPlayer, Is.True);
                Assert.That(attacked.IsAlerted, Is.False);
                Assert.That(nearby.IsAlerted, Is.False);
                Assert.That(attacked.AlertIndicator, Is.Null);
                Assert.That(nearby.AlertIndicator, Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(ally);
                Object.DestroyImmediate(cargo);
            }
        }

        [Test]
        public void TrackingTurnsTowardTargetWithoutSnapping()
        {
            target.transform.position = Vector3.right * 10f;
            mob.transform.forward = Vector3.forward;
            var controller = mob.AddComponent<MissionMobController>();
            controller.Configure(MissionMobDefinition.For(MissionMobArchetype.Melee), target.transform, null);

            controller.Tick(0f, .05f);

            Assert.That(Vector3.Angle(Vector3.forward, mob.transform.forward), Is.GreaterThan(1f));
            Assert.That(Vector3.Angle(mob.transform.forward, Vector3.right), Is.GreaterThan(1f));
        }

        [Test]
        public void DamageAlertsOnlyNearbyGuardsAndShowsTemporaryYellowExclamation()
        {
            target.transform.position = new Vector3(0f, 0f, 20f);
            var nearby = Actor("Nearby Guard", CombatTeamId.Enemy, new Vector3(0f, 0f, 6f));
            var distant = Actor("Distant Guard", CombatTeamId.Enemy, new Vector3(0f, 0f, 8f));
            try
            {
                var attackedController = mob.AddComponent<MissionMobController>();
                var nearbyController = nearby.AddComponent<MissionMobController>();
                var distantController = distant.AddComponent<MissionMobController>();
                attackedController.Configure(MissionMobDefinition.For(MissionMobArchetype.Melee), target.transform, null);
                nearbyController.Configure(MissionMobDefinition.For(MissionMobArchetype.Melee), target.transform, null);
                distantController.Configure(MissionMobDefinition.For(MissionMobArchetype.Melee), target.transform, null);
                attackedController.ConfigureGuardPost(mob.transform.position, 1f);
                nearbyController.ConfigureGuardPost(nearby.transform.position, 1f);
                distantController.ConfigureGuardPost(distant.transform.position, 1f);

                mob.GetComponent<ActorHealth>().ApplyDamage(1f);

                Assert.That(attackedController.IsAlerted, Is.True);
                Assert.That(nearbyController.IsAlerted, Is.True);
                Assert.That(nearbyController.IsGuardPostEngaged, Is.True);
                Assert.That(distantController.IsAlerted, Is.False);
                Assert.That(distantController.IsGuardPostEngaged, Is.False);
                Assert.That(nearbyController.AlertIndicator, Is.Not.Null);
                Assert.That(nearbyController.AlertIndicator.activeSelf, Is.True);
                var alertText = nearbyController.AlertIndicator.GetComponent<TextMesh>();
                Assert.That(alertText.text, Is.EqualTo("!"));
                Assert.That(alertText.color.r, Is.GreaterThan(.9f));
                Assert.That(alertText.color.g, Is.GreaterThan(.7f));
                Assert.That(alertText.color.b, Is.LessThan(.3f));

                var nearbyStart = nearby.transform.position;
                var distantStart = distant.transform.position;
                nearbyController.Tick(0f, .5f);
                distantController.Tick(0f, .5f);
                Assert.That(nearby.transform.position.z, Is.GreaterThan(nearbyStart.z));
                Assert.That(distant.transform.position.z, Is.EqualTo(distantStart.z).Within(.001f));

                nearbyController.Tick(2f, 2f);
                Assert.That(nearbyController.AlertIndicator.activeSelf, Is.False);
                Assert.That(nearbyController.IsGuardPostEngaged, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(nearby);
                Object.DestroyImmediate(distant);
            }
        }

        [Test]
        public void GuardUsesForwardSightAndOmnidirectionalCloseAwareness()
        {
            var controller = mob.AddComponent<MissionMobController>();
            controller.Configure(MissionMobDefinition.For(MissionMobArchetype.Melee), target.transform, null);
            controller.ConfigureGuardPost(mob.transform.position, 10f);
            mob.transform.forward = Vector3.back;

            controller.Tick(0f, 0f);
            Assert.That(controller.IsGuardPostEngaged, Is.False);

            mob.transform.forward = Vector3.forward;
            controller.Tick(0f, 0f);
            Assert.That(controller.IsGuardPostEngaged, Is.True);
            Assert.That(controller.IsAlerted, Is.True);

            controller.ConfigureGuardPost(mob.transform.position, 10f);
            mob.transform.forward = Vector3.back;
            target.transform.position = Vector3.forward * 2f;
            controller.Tick(0f, 0f);
            Assert.That(controller.IsGuardPostEngaged, Is.True);
        }

        [Test]
        public void SolidObstacleBlocksGuardSight()
        {
            floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Sight Blocker";
            floor.transform.position = new Vector3(0f, 1f, 5f);
            floor.transform.localScale = new Vector3(3f, 3f, 1f);
            var controller = mob.AddComponent<MissionMobController>();
            controller.Configure(MissionMobDefinition.For(MissionMobArchetype.Melee), target.transform, null);
            controller.ConfigureGuardPost(mob.transform.position, 10f);
            mob.transform.forward = Vector3.forward;
            Physics.SyncTransforms();

            controller.Tick(0f, 0f);
            Assert.That(controller.IsGuardPostEngaged, Is.False);

            floor.transform.position = Vector3.right * 20f;
            Physics.SyncTransforms();
            controller.Tick(0f, 0f);
            Assert.That(controller.IsGuardPostEngaged, Is.True);
        }

        [Test]
        public void NoiseAlertsOnlyGuardsInsideHearingRadius()
        {
            target.transform.position = Vector3.forward * 20f;
            var farGuard = Actor("Far Guard", CombatTeamId.Enemy, Vector3.right * 8f);
            try
            {
                var nearController = mob.AddComponent<MissionMobController>();
                var farController = farGuard.AddComponent<MissionMobController>();
                nearController.Configure(MissionMobDefinition.For(MissionMobArchetype.Melee), target.transform, null);
                farController.Configure(MissionMobDefinition.For(MissionMobArchetype.Melee), target.transform, null);
                nearController.ConfigureGuardPost(mob.transform.position, 1f);
                farController.ConfigureGuardPost(farGuard.transform.position, 1f);

                CombatNoiseSystem.Emit(Vector3.right * 2f, 5f, CombatTeamId.Player);

                Assert.That(nearController.IsGuardPostEngaged, Is.True);
                Assert.That(nearController.IsAlerted, Is.False);
                Assert.That(nearController.AlertIndicator, Is.Null);
                Assert.That(farController.IsGuardPostEngaged, Is.False);
                Assert.That(farController.IsAlerted, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(farGuard);
            }
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

        [UnityTest]
        public IEnumerator DeathFallsThenSinksAndDestroysMob()
        {
            var collider = mob.AddComponent<CapsuleCollider>();
            var visual = new GameObject("VisualRoot").transform;
            visual.SetParent(mob.transform, false);
            var controller = mob.AddComponent<MissionMobController>();
            controller.Configure(MissionMobDefinition.For(MissionMobArchetype.Melee), target.transform, null);

            mob.GetComponent<ActorHealth>().ApplyDamage(1000f);

            Assert.That(controller.enabled, Is.True, "The controller must stay alive long enough to animate the corpse.");
            Assert.That(collider.enabled, Is.False);

            controller.Tick(0f, .55f);
            Assert.That(Quaternion.Angle(Quaternion.identity, visual.localRotation), Is.GreaterThan(30f));
            Assert.That(visual.localPosition.y, Is.LessThan(-.05f));

            controller.Tick(0f, .6f);
            yield return null;
            Assert.That(mob == null, Is.True);
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

        [Test]
        public void CharacterControllerMobWalksAroundCargoToReachPlayer()
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "Ground";
            ground.transform.position = new Vector3(0f, -.5f, 5f);
            ground.transform.localScale = new Vector3(24f, 1f, 24f);
            floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Cargo";
            floor.transform.position = new Vector3(0f, 1f, 5f);
            floor.transform.localScale = new Vector3(2.2f, 2f, 2.7f);
            var character = mob.AddComponent<CharacterController>();
            character.height = 2f;
            character.radius = .5f;
            character.center = Vector3.up;
            var controller = mob.AddComponent<MissionMobController>();
            controller.Configure(MissionMobDefinition.For(MissionMobArchetype.Melee), target.transform, null);
            Physics.SyncTransforms();

            try
            {
                for (var step = 0; step < 180; step++)
                    controller.Tick(step * .05f, .05f);

                Assert.That(mob.transform.position.z, Is.GreaterThan(7f));
                Assert.That(target.GetComponent<ActorHealth>().CurrentHealth, Is.LessThan(100f));
            }
            finally
            {
                Object.DestroyImmediate(ground);
            }
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
