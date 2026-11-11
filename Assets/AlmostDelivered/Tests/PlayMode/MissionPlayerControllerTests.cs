using System.Collections;
using System.Linq;
using AlmostDelivered.Combat;
using AlmostDelivered.Frontend;
using AlmostDelivered.Gameplay;
using AlmostDelivered.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AlmostDelivered.Tests.PlayMode
{
    public sealed class MissionPlayerControllerTests
    {
        private GameObject player;
        private GameObject floor;
        private MissionPlayerController missionPlayer;

        [SetUp]
        public void SetUp()
        {
            player = new GameObject(
                "Mission Player",
                typeof(CharacterController),
                typeof(ActorHealth),
                typeof(CombatTeam),
                typeof(RangedAttackController),
                typeof(MissionPlayerController));
            player.GetComponent<CombatTeam>().Team = CombatTeamId.Player;

            var muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(player.transform, false);
            muzzle.localPosition = Vector3.forward;
            var template = new GameObject("Projectile Template", typeof(CombatProjectile));
            template.SetActive(false);
            var attack = player.GetComponent<RangedAttackController>();
            attack.Configure(template, muzzle, 20f, .25f, 20f, 2f);

            missionPlayer = player.GetComponent<MissionPlayerController>();
            missionPlayer.Configure(player.GetComponent<CharacterController>(), attack, 6f);
        }

        [TearDown]
        public void TearDown()
        {
            AlmostDelivered.Frontend.PlayerWeaponLoadout.Select(AlmostDelivered.Frontend.PlayerWeaponType.Ranged);
            foreach (var projectile in Object.FindObjectsOfType<CombatProjectile>(true))
                Object.DestroyImmediate(projectile.gameObject);
            if (player != null)
                Object.DestroyImmediate(player);
            if (floor != null)
                Object.DestroyImmediate(floor);
        }

        [UnityTest]
        public IEnumerator MissionStartUsesWeaponChosenInHub()
        {
            var melee = player.AddComponent<MeleeAttackController>();
            AlmostDelivered.Frontend.PlayerWeaponLoadout.Select(AlmostDelivered.Frontend.PlayerWeaponType.Melee);
            Object.DestroyImmediate(missionPlayer);
            missionPlayer = player.AddComponent<MissionPlayerController>();
            missionPlayer.Configure(player.GetComponent<CharacterController>(), player.GetComponent<RangedAttackController>(), 6f);
            missionPlayer.ConfigureMelee(melee);
            yield return null;

            Assert.That(missionPlayer.WeaponType, Is.EqualTo(AlmostDelivered.Frontend.PlayerWeaponType.Melee));
            Assert.That(melee.enabled, Is.True);
            Assert.That(player.GetComponent<RangedAttackController>().enabled, Is.False);
        }

        [Test]
        public void MoveNormalizesDiagonalWithoutChangingAimDirection()
        {
            player.transform.forward = Vector3.left;
            var before = player.transform.position;
            Assert.That(missionPlayer.Move(Vector2.one, 1f), Is.True);
            var displacement = player.transform.position - before;

            Assert.That(new Vector2(displacement.x, displacement.z).magnitude, Is.EqualTo(6f).Within(.01f));
            Assert.That(Vector3.Dot(player.transform.forward, Vector3.left), Is.GreaterThan(.999f));
        }

        [Test]
        public void SprintAndRollSpendStaminaAndMoveTheMissionPlayer()
        {
            var stamina = player.GetComponent<PlayerMobility>();
            Assert.That(missionPlayer.MoveWithActions(Vector2.right, .1f, true, false), Is.True);
            Assert.That(stamina.IsSprinting, Is.True);
            Assert.That(stamina.Stamina, Is.LessThan(stamina.MaxStamina));
            Assert.That(missionPlayer.MoveWithActions(Vector2.right, .1f, false, true), Is.True);
            Assert.That(stamina.IsRolling, Is.True);
            Assert.That(missionPlayer.AimAndFire(Vector3.forward * 10f, 0f), Is.False);
        }

        [Test]
        public void AimAtCursorTurnsWithoutFiring()
        {
            player.transform.forward = Vector3.forward;

            Assert.That(missionPlayer.AimAt(Vector3.right * 10f, .05f), Is.True);

            Assert.That(Vector3.Angle(Vector3.forward, player.transform.forward), Is.GreaterThan(1f));
            Assert.That(Vector3.Angle(player.transform.forward, Vector3.right), Is.GreaterThan(1f));
            Assert.That(Object.FindObjectsOfType<CombatProjectile>(), Is.Empty);
        }

        [Test]
        public void AimTurnsTowardCursorWithoutSnappingBeforeShot()
        {
            player.transform.forward = Vector3.forward;

            Assert.That(missionPlayer.AimAndFire(Vector3.right * 10f, 0f, .05f), Is.True);

            Assert.That(Vector3.Angle(Vector3.forward, player.transform.forward), Is.GreaterThan(1f));
            Assert.That(Vector3.Angle(player.transform.forward, Vector3.right), Is.GreaterThan(1f));
        }

        [Test]
        public void AimDelegatesToRangedAttackAndDeadPlayerRejectsActions()
        {
            Assert.That(missionPlayer.AimAndFire(Vector3.forward * 10f, 0f), Is.True);
            player.GetComponent<ActorHealth>().ApplyDamage(100f);

            var position = player.transform.position;
            Assert.That(missionPlayer.Move(Vector2.right, 1f), Is.False);
            Assert.That(missionPlayer.AimAndFire(Vector3.right * 10f, 1f), Is.False);
            Assert.That(player.transform.position, Is.EqualTo(position));
        }

        [Test]
        public void InvalidOrDisabledInputIsRejected()
        {
            Assert.That(missionPlayer.Move(new Vector2(float.NaN, 0f), 1f), Is.False);
            Assert.That(missionPlayer.AimAndFire(new Vector3(float.PositiveInfinity, 0f, 0f), 0f), Is.False);
            missionPlayer.enabled = false;
            Assert.That(missionPlayer.Move(Vector2.right, 1f), Is.False);
        }

        [Test]
        public void MeleeSwingHitsEveryEnemyInArcEvenWithManySceneryColliders()
        {
            var melee = player.AddComponent<MeleeAttackController>();
            melee.Configure(34f, 2.8f, 140f, .34f);
            var wind = player.AddComponent<MeleeSlashVfx>();
            missionPlayer.ConfigureMelee(melee);
            missionPlayer.SetWeaponType(AlmostDelivered.Frontend.PlayerWeaponType.Melee);
            var scenery = new GameObject[40];
            for (var i = 0; i < scenery.Length; i++)
            {
                scenery[i] = new GameObject("Nearby Scenery " + i, typeof(BoxCollider));
                scenery[i].transform.position = new Vector3((i % 8 - 3.5f) * .13f, 0f,
                    (i / 8 - 2f) * .13f);
            }
            var front = Target("Front Enemy", new Vector3(0f, 0f, 1.25f), CombatTeamId.Enemy);
            var rear = Target("Rear Enemy", new Vector3(0f, 0f, -1.25f), CombatTeamId.Enemy);
            var ally = Target("Ally", new Vector3(.4f, 0f, 1.2f), CombatTeamId.Player);
            var farther = Target("Farther Enemy", new Vector3(0f, 0f, 2.45f), CombatTeamId.Enemy);
            var left = Target("Left Enemy", new Vector3(-1.4f, 0f, 1.7f), CombatTeamId.Enemy);
            var outside = Target("Outside Arc", new Vector3(2.4f, 0f, .2f), CombatTeamId.Enemy);
            Physics.SyncTransforms();

            Assert.That(missionPlayer.AimAndFire(Vector3.forward * 3f, 0f, .05f), Is.True);
            Assert.That(front.GetComponent<ActorHealth>().CurrentHealth, Is.EqualTo(66f));
            Assert.That(rear.GetComponent<ActorHealth>().CurrentHealth, Is.EqualTo(100f));
            Assert.That(ally.GetComponent<ActorHealth>().CurrentHealth, Is.EqualTo(100f));
            Assert.That(farther.GetComponent<ActorHealth>().CurrentHealth, Is.EqualTo(66f));
            Assert.That(left.GetComponent<ActorHealth>().CurrentHealth, Is.EqualTo(66f));
            Assert.That(outside.GetComponent<ActorHealth>().CurrentHealth, Is.EqualTo(100f));
            Assert.That(wind.IsVisible, Is.True);
            Assert.That(wind.Reach, Is.EqualTo(2.8f));
            Assert.That(wind.ArcDegrees, Is.EqualTo(140f));
            Assert.That(wind.EffectMesh.vertices.Max(vertex => new Vector2(vertex.x, vertex.z).magnitude),
                Is.EqualTo(2.8f).Within(.001f));
            Assert.That(wind.EffectMesh.colors[0].r, Is.GreaterThan(.7f),
                "A confirmed hit should flash the wind arc white.");
            Assert.That(missionPlayer.AimAndFire(Vector3.forward * 3f, .1f, .05f), Is.False);
            Assert.That(missionPlayer.AimAndFire(Vector3.forward * 3f, .33f, .05f), Is.False);
            Assert.That(front.GetComponent<ActorHealth>().CurrentHealth, Is.EqualTo(66f));
            Assert.That(missionPlayer.AimAndFire(Vector3.forward * 3f, .35f, .05f), Is.True);
            Assert.That(front.GetComponent<ActorHealth>().CurrentHealth, Is.EqualTo(32f));
            Assert.That(farther.GetComponent<ActorHealth>().CurrentHealth, Is.EqualTo(32f));
            Assert.That(left.GetComponent<ActorHealth>().CurrentHealth, Is.EqualTo(32f));

            Object.DestroyImmediate(front);
            Object.DestroyImmediate(rear);
            Object.DestroyImmediate(ally);
            Object.DestroyImmediate(farther);
            Object.DestroyImmediate(left);
            Object.DestroyImmediate(outside);
            foreach (var obstacle in scenery) Object.DestroyImmediate(obstacle);
        }

        [Test]
        public void MeleeSwingHitsWiderEnemyHurtboxWithoutChangingMovementCollider()
        {
            var melee = player.AddComponent<MeleeAttackController>();
            melee.Configure(34f, 2.8f, 140f, .34f);
            var enemy = new GameObject("Enemy", typeof(CharacterController),
                typeof(ActorHealth), typeof(CombatTeam));
            enemy.GetComponent<CombatTeam>().Team = CombatTeamId.Enemy;
            enemy.transform.position = new Vector3(0f, 0f, 3.4f);
            var movement = enemy.GetComponent<CharacterController>();
            movement.radius = .5f;
            movement.height = 2f;
            movement.center = Vector3.up;
            var hurtbox = new GameObject("Combat Hurtbox", typeof(CapsuleCollider), typeof(CombatHurtbox));
            hurtbox.transform.SetParent(enemy.transform, false);
            var capsule = hurtbox.GetComponent<CapsuleCollider>();
            capsule.isTrigger = true;
            capsule.radius = .72f;
            capsule.height = 2.2f;
            capsule.center = Vector3.up * 1.1f;
            Physics.SyncTransforms();

            Assert.That(melee.TryAttack(0f, Vector3.forward), Is.True);
            Assert.That(enemy.GetComponent<ActorHealth>().CurrentHealth, Is.EqualTo(66f));
            Assert.That(movement.radius, Is.EqualTo(.5f));

            Object.DestroyImmediate(enemy);
        }

        [UnityTest]
        public IEnumerator WindArcFadesOutAfterTheSwing()
        {
            var wind = player.AddComponent<MeleeSlashVfx>();
            wind.Play(2.8f, 140f);
            Assert.That(wind.IsVisible, Is.True);
            yield return new WaitForSecondsRealtime(.5f);
            Assert.That(wind.IsVisible, Is.False);
        }

        [UnityTest]
        public IEnumerator MeleeDamageAndWindStayInTheClickedSectorAfterTurning()
        {
            var melee = player.AddComponent<MeleeAttackController>();
            melee.Configure(34f, 2.8f, 140f, .34f);
            var wind = player.AddComponent<MeleeSlashVfx>();
            missionPlayer.ConfigureMelee(melee);
            missionPlayer.SetWeaponType(PlayerWeaponType.Melee);
            var right = Target("Right Enemy", Vector3.right * 1.5f, CombatTeamId.Enemy);
            var forward = Target("Forward Enemy", Vector3.forward * 1.5f, CombatTeamId.Enemy);
            Physics.SyncTransforms();

            Assert.That(missionPlayer.AimAndFire(Vector3.right * 3f, 0f, .01f), Is.True);
            Assert.That(right.GetComponent<ActorHealth>().CurrentHealth, Is.EqualTo(66f));
            Assert.That(forward.GetComponent<ActorHealth>().CurrentHealth, Is.EqualTo(100f));
            Assert.That(Vector3.Dot(wind.AttackDirection, Vector3.right), Is.GreaterThan(.999f));
            player.transform.forward = Vector3.back;
            yield return null;
            Assert.That(Vector3.Dot(wind.transform.Find("Melee Wind Arc").forward, Vector3.right),
                Is.GreaterThan(.999f));

            Object.DestroyImmediate(right);
            Object.DestroyImmediate(forward);
        }

        [UnityTest]
        public IEnumerator MeleeHitRecoilsEnemyWithoutCameraShake()
        {
            var melee = player.AddComponent<MeleeAttackController>();
            melee.Configure(34f, 2.8f, 140f, .34f);
            missionPlayer.ConfigureMelee(melee);
            missionPlayer.SetWeaponType(PlayerWeaponType.Melee);
            var enemy = Target("Recoil Target", Vector3.forward * 1.4f, CombatTeamId.Enemy);
            var visual = new GameObject("VisualRoot");
            visual.transform.SetParent(enemy.transform, false);
            var body = new GameObject("BodyPivot").transform;
            body.SetParent(visual.transform, false);
            visual.AddComponent<ProceduralPoseAnimator>().Configure(body, null, null);
            var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(QuarterViewCamera));
            cameraObject.tag = "MainCamera";
            cameraObject.GetComponent<QuarterViewCamera>().Target = player.transform;
            Physics.SyncTransforms();

            Assert.That(missionPlayer.AimAndFire(Vector3.forward * 3f, 0f, .05f), Is.True);
            Assert.That(cameraObject.GetComponent<QuarterViewCamera>().IsImpactShaking, Is.False);
            yield return new WaitForSecondsRealtime(.08f);
            Assert.That(body.localPosition.z, Is.LessThan(-.02f));
            yield return new WaitForSecondsRealtime(.15f);
            Assert.That(cameraObject.GetComponent<QuarterViewCamera>().IsImpactShaking, Is.False);

            Object.DestroyImmediate(enemy);
            Object.DestroyImmediate(cameraObject);
        }

        private static GameObject Target(string name, Vector3 position, CombatTeamId team)
        {
            var target = new GameObject(name, typeof(CapsuleCollider), typeof(ActorHealth), typeof(CombatTeam));
            target.transform.position = position;
            target.GetComponent<CombatTeam>().Team = team;
            return target;
        }

        [Test]
        public void IdlePlayerFallsToGroundAndRemainsGrounded()
        {
            floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Ground";
            floor.transform.position = new Vector3(0f, -.5f, 0f);
            floor.transform.localScale = new Vector3(20f, 1f, 20f);
            player.transform.position = new Vector3(0f, 4f, 0f);
            Physics.SyncTransforms();

            for (var step = 0; step < 80; step++)
                missionPlayer.Move(Vector2.zero, .05f);

            Assert.That(player.transform.position.y, Is.EqualTo(1.08f).Within(.02f));
            var settledHeight = player.transform.position.y;
            for (var step = 0; step < 20; step++)
                missionPlayer.Move(Vector2.zero, .05f);
            Assert.That(player.transform.position.y, Is.EqualTo(settledHeight).Within(.01f));
        }
    }
}
