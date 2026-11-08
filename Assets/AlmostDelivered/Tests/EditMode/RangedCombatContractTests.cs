using System.Linq;
using AlmostDelivered.Combat;
using NUnit.Framework;
using UnityEngine;

namespace AlmostDelivered.Tests.EditMode
{
    public sealed class RangedCombatContractTests
    {
        private GameObject owner;
        private GameObject template;
        private RangedAttackController attack;

        [SetUp]
        public void SetUp()
        {
            owner = new GameObject("Owner", typeof(ActorHealth), typeof(CombatTeam), typeof(RangedAttackController));
            owner.GetComponent<CombatTeam>().Team = CombatTeamId.Player;
            var muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(owner.transform, false);
            muzzle.localPosition = new Vector3(0f, .5f, .5f);

            template = new GameObject("Projectile Template", typeof(CombatProjectile));
            template.SetActive(false);
            attack = owner.GetComponent<RangedAttackController>();
            attack.Configure(template, muzzle, 25f, .5f, 20f, 2f);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var projectile in Object.FindObjectsOfType<CombatProjectile>(true).ToArray())
                Object.DestroyImmediate(projectile.gameObject);
            if (owner != null)
                Object.DestroyImmediate(owner);
        }

        [Test]
        public void ValidShotCreatesOneActivePlanarProjectileAndFacesAim()
        {
            var facingBeforeShot = owner.transform.forward;
            Assert.That(attack.TryFire(new Vector3(5f, 99f, 7f), 0f), Is.True);

            var shots = Object.FindObjectsOfType<CombatProjectile>(true)
                .Where(projectile => projectile.gameObject != template && projectile.gameObject.activeSelf)
                .ToArray();
            Assert.That(shots.Length, Is.EqualTo(1));
            Assert.That(shots[0].Direction.y, Is.EqualTo(0f).Within(.0001f));
            Assert.That(owner.transform.forward, Is.EqualTo(facingBeforeShot));
        }

        [Test]
        public void CooldownBlocksSecondShotThenAllowsLaterShot()
        {
            Assert.That(attack.TryFire(Vector3.forward * 10f, 1f), Is.True);
            Assert.That(attack.TryFire(Vector3.right * 10f, 1.49f), Is.False);
            Assert.That(attack.TryFire(Vector3.right * 10f, 1.5f), Is.True);
        }

        [TestCase(float.NaN, 0f, 1f)]
        [TestCase(float.PositiveInfinity, 0f, 1f)]
        [TestCase(0f, 0f, 0f)]
        public void InvalidAimIsRejected(float x, float y, float z)
        {
            var aim = new Vector3(x, y, z);
            if (aim == Vector3.zero)
                aim = owner.transform.Find("Muzzle").position;
            Assert.That(attack.TryFire(aim, 0f), Is.False);
        }

        [Test]
        public void DeadOwnerCannotFire()
        {
            owner.GetComponent<ActorHealth>().ApplyDamage(100f);
            Assert.That(attack.TryFire(Vector3.forward * 10f, 0f), Is.False);
        }
    }
}
