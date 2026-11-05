using AlmostDelivered.Combat;
using AlmostDelivered.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace AlmostDelivered.Tests.PlayMode
{
    public sealed class MissionPlayerControllerTests
    {
        private GameObject player;
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
        }

        [Test]
        public void MoveNormalizesDiagonalAndFacesDirection()
        {
            var before = player.transform.position;
            Assert.That(missionPlayer.Move(Vector2.one, 1f), Is.True);
            var displacement = player.transform.position - before;

            Assert.That(displacement.magnitude, Is.EqualTo(6f).Within(.01f));
            Assert.That(Vector3.Dot(player.transform.forward, new Vector3(1f, 0f, 1f).normalized), Is.GreaterThan(.999f));
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
    }
}
