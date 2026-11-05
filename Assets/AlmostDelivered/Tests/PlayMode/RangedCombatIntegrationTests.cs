using System.Collections;
using AlmostDelivered.Combat;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AlmostDelivered.Tests.PlayMode
{
    public sealed class RangedCombatIntegrationTests
    {
        [UnityTest]
        public IEnumerator HighSpeedProjectileSweepsThinEnemyAndDamagesOnce()
        {
            var owner = Actor("Owner", CombatTeamId.Player, Vector3.zero, false);
            var enemy = Actor("Enemy", CombatTeamId.Enemy, new Vector3(0f, 0f, 3f), true);
            enemy.transform.localScale = new Vector3(1f, 1f, .05f);
            var projectile = Projectile(owner, CombatTeamId.Player, Vector3.forward, 300f, 30f, 1f);

            yield return new WaitForFixedUpdate();
            yield return null;

            Assert.That(enemy.GetComponent<ActorHealth>().CurrentHealth, Is.EqualTo(70f));
            Assert.That(projectile == null, Is.True);
            Object.Destroy(owner);
            Object.Destroy(enemy);
        }

        [UnityTest]
        public IEnumerator ProjectileIgnoresOwnerAndAllyThenHitsEnemy()
        {
            var owner = Actor("Owner", CombatTeamId.Player, Vector3.zero, true);
            var ally = Actor("Ally", CombatTeamId.Player, new Vector3(0f, 0f, 1f), true);
            var enemy = Actor("Enemy", CombatTeamId.Enemy, new Vector3(0f, 0f, 2f), true);
            Projectile(owner, CombatTeamId.Player, Vector3.forward, 150f, 20f, 1f);

            yield return new WaitForFixedUpdate();

            Assert.That(owner.GetComponent<ActorHealth>().CurrentHealth, Is.EqualTo(100f));
            Assert.That(ally.GetComponent<ActorHealth>().CurrentHealth, Is.EqualTo(100f));
            Assert.That(enemy.GetComponent<ActorHealth>().CurrentHealth, Is.EqualTo(80f));
            Object.Destroy(owner);
            Object.Destroy(ally);
            Object.Destroy(enemy);
        }

        [UnityTest]
        public IEnumerator ProjectileDiesOnWorldGeometry()
        {
            var owner = Actor("Owner", CombatTeamId.Player, Vector3.zero, false);
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = new Vector3(0f, 0f, 1f);
            var projectile = Projectile(owner, CombatTeamId.Player, Vector3.forward, 100f, 20f, 1f);

            yield return new WaitForFixedUpdate();
            yield return null;

            Assert.That(projectile == null, Is.True);
            Object.Destroy(owner);
            Object.Destroy(wall);
        }

        [UnityTest]
        public IEnumerator ProjectileExpiresAfterLifetime()
        {
            var owner = Actor("Owner", CombatTeamId.Player, Vector3.zero, false);
            var projectile = Projectile(owner, CombatTeamId.Player, Vector3.forward, 1f, 20f, .02f);

            yield return new WaitForFixedUpdate();
            yield return null;

            Assert.That(projectile == null, Is.True);
            Object.Destroy(owner);
        }

        private static GameObject Actor(string name, CombatTeamId team, Vector3 position, bool collider)
        {
            var actor = collider
                ? GameObject.CreatePrimitive(PrimitiveType.Cube)
                : new GameObject(name);
            actor.name = name;
            actor.transform.position = position;
            actor.AddComponent<ActorHealth>();
            actor.AddComponent<CombatTeam>().Team = team;
            return actor;
        }

        private static GameObject Projectile(
            GameObject owner,
            CombatTeamId team,
            Vector3 direction,
            float speed,
            float damage,
            float lifetime)
        {
            var projectile = new GameObject("Projectile", typeof(CombatProjectile));
            projectile.transform.position = owner.transform.position;
            projectile.GetComponent<CombatProjectile>().Configure(owner, team, direction, speed, damage, lifetime);
            Physics.SyncTransforms();
            return projectile;
        }
    }
}
