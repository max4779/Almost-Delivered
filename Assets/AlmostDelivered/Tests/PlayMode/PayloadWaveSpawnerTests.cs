using System.Collections.Generic;
using System.Linq;
using AlmostDelivered.Combat;
using AlmostDelivered.Gameplay.Payload;
using NUnit.Framework;
using UnityEngine;

namespace AlmostDelivered.Tests.PlayMode
{
    public sealed class PayloadWaveSpawnerTests
    {
        private GameObject root;
        private GameObject player;
        private GameObject projectileTemplate;
        private Transform[] anchors;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Wave test root");
            player = new GameObject("Player");
            player.AddComponent<ActorHealth>();
            player.AddComponent<CombatTeam>().Team = CombatTeamId.Player;
            projectileTemplate = new GameObject("Projectile Template", typeof(CombatProjectile));
            projectileTemplate.SetActive(false);
            anchors = new Transform[4];
            for (var index = 0; index < anchors.Length; index++)
            {
                anchors[index] = new GameObject($"Anchor {index}").transform;
                anchors[index].SetParent(root.transform, false);
                anchors[index].position = new Vector3(index * 20f, 0f, index * 10f);
            }
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(player);
            Object.DestroyImmediate(projectileTemplate);
        }

        [TestCase(0, 10)]
        [TestCase(1, 14)]
        [TestCase(2, 18)]
        public void CheckpointWaveCreatesRequestedCountWithStableUniqueIds(int checkpointIndex, int expectedCount)
        {
            var spawner = CreateSpawner(root.transform, 77, 18);
            IReadOnlyCollection<int> startedIds = null;
            spawner.WaveStarted += (_, ids) => startedIds = ids;

            spawner.SpawnWave(5, checkpointIndex, false);

            Assert.That(spawner.ActiveCount, Is.EqualTo(expectedCount));
            Assert.That(spawner.PendingCount, Is.Zero);
            Assert.That(startedIds, Has.Count.EqualTo(expectedCount));
            Assert.That(startedIds.Distinct().Count(), Is.EqualTo(expectedCount));
            Assert.That(spawner.LivingEnemies.Select(enemy => enemy.EnemyId), Is.EquivalentTo(startedIds));
        }

        [Test]
        public void FinalWaveIncludesBossesAndQueuesAboveActiveCap()
        {
            var spawner = CreateSpawner(root.transform, 91, 18);
            IReadOnlyCollection<int> startedIds = null;
            spawner.WaveStarted += (_, ids) => startedIds = ids;

            spawner.SpawnWave(9, 3, true);

            Assert.That(startedIds, Has.Count.EqualTo(22));
            Assert.That(spawner.ActiveCount, Is.EqualTo(18));
            Assert.That(spawner.PendingCount, Is.EqualTo(4));
            Assert.That(spawner.LivingEnemies.Any(enemy => enemy.Definition.Archetype == MissionMobArchetype.CommanderBoss), Is.True);
            Assert.That(spawner.LivingEnemies.Any(enemy => enemy.Definition.Archetype == MissionMobArchetype.ChargerBoss), Is.True);
        }

        [Test]
        public void SameSeedProducesSameArchetypeOrderAndPositions()
        {
            var firstRoot = new GameObject("First spawner");
            firstRoot.transform.SetParent(root.transform, false);
            var secondRoot = new GameObject("Second spawner");
            secondRoot.transform.SetParent(root.transform, false);
            var first = CreateSpawner(firstRoot.transform, 1234, 18);
            var second = CreateSpawner(secondRoot.transform, 1234, 18);

            first.SpawnWave(4, 1, false);
            second.SpawnWave(4, 1, false);
            var firstEnemies = first.LivingEnemies.OrderBy(enemy => enemy.EnemyId).ToArray();
            var secondEnemies = second.LivingEnemies.OrderBy(enemy => enemy.EnemyId).ToArray();

            Assert.That(secondEnemies, Has.Length.EqualTo(firstEnemies.Length));
            for (var index = 0; index < firstEnemies.Length; index++)
            {
                Assert.That(secondEnemies[index].EnemyId, Is.EqualTo(firstEnemies[index].EnemyId));
                Assert.That(secondEnemies[index].Definition.Archetype, Is.EqualTo(firstEnemies[index].Definition.Archetype));
                Assert.That(secondEnemies[index].transform.position, Is.EqualTo(firstEnemies[index].transform.position));
            }
        }

        [Test]
        public void DeathReportsOnceAndImmediatelyFillsSlotFromQueue()
        {
            var spawner = CreateSpawner(root.transform, 222, 18);
            var removed = new List<int>();
            spawner.EnemyRemoved += (_, enemyId) => removed.Add(enemyId);
            spawner.SpawnWave(6, 3, true);
            var victim = spawner.LivingEnemies.First();
            var victimId = victim.EnemyId;

            victim.GetComponent<ActorHealth>().ApplyDamage(10000f);

            Assert.That(removed, Is.EqualTo(new[] { victimId }));
            Assert.That(spawner.ActiveCount, Is.EqualTo(18));
            Assert.That(spawner.PendingCount, Is.EqualTo(3));

            victim.gameObject.SetActive(false);
            Object.DestroyImmediate(victim.gameObject);
            Assert.That(removed, Is.EqualTo(new[] { victimId }));
        }

        [Test]
        public void DisabledAndDestroyedEnemiesAreRemovedFromLivingSet()
        {
            var spawner = CreateSpawner(root.transform, 333, 18);
            var removed = new List<int>();
            spawner.EnemyRemoved += (_, enemyId) => removed.Add(enemyId);
            spawner.SpawnWave(3, 0, false);
            var enemies = spawner.LivingEnemies.Take(2).ToArray();

            enemies[0].gameObject.SetActive(false);
            Object.DestroyImmediate(enemies[1].gameObject);

            Assert.That(spawner.ActiveCount, Is.EqualTo(8));
            Assert.That(removed, Has.Count.EqualTo(2));
            Assert.That(removed.Distinct().Count(), Is.EqualTo(2));
        }

        private PayloadWaveSpawner CreateSpawner(Transform parent, int seed, int activeCap)
        {
            var spawnerObject = new GameObject("Payload Wave Spawner");
            spawnerObject.transform.SetParent(parent, false);
            var spawner = spawnerObject.AddComponent<PayloadWaveSpawner>();
            spawner.Configure(
                player.transform,
                projectileTemplate,
                null,
                null,
                null,
                null,
                null,
                anchors,
                seed,
                activeCap);
            return spawner;
        }
    }
}
