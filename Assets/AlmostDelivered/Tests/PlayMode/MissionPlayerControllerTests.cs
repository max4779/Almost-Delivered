using AlmostDelivered.Combat;
using AlmostDelivered.Gameplay;
using NUnit.Framework;
using UnityEngine;

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
            foreach (var projectile in Object.FindObjectsOfType<CombatProjectile>(true))
                Object.DestroyImmediate(projectile.gameObject);
            if (player != null)
                Object.DestroyImmediate(player);
            if (floor != null)
                Object.DestroyImmediate(floor);
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
