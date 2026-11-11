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
            Assert.That(spawner.LivingEnemies.Count(enemy => enemy.IsCargoSaboteur), Is.EqualTo(1));
        }

        [Test]
        public void FinalWaveKeepsChargerButOmitsCommanderAndQueuesAboveActiveCap()
        {
            var spawner = CreateSpawner(root.transform, 91, 18);
            IReadOnlyCollection<int> startedIds = null;
            spawner.WaveStarted += (_, ids) => startedIds = ids;

            spawner.SpawnWave(9, 3, true);

            Assert.That(startedIds, Has.Count.EqualTo(22));
            Assert.That(spawner.ActiveCount, Is.EqualTo(18));
            Assert.That(spawner.PendingCount, Is.EqualTo(4));
            Assert.That(spawner.LivingEnemies.Any(enemy => enemy.Definition.Archetype == MissionMobArchetype.CommanderBoss), Is.False);
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

        [Test]
        public void SpawnedEnemiesUseTerrainAwareCharacterControllers()
        {
            var visualPrefab = new GameObject("Test Enemy Visual");
            visualPrefab.transform.SetParent(root.transform, false);
            var spawner = CreateSpawner(root.transform, 444, 18, visualPrefab);

            spawner.SpawnWave(2, 0, false);

            foreach (var enemy in spawner.LivingEnemies)
            {
                var character = enemy.GetComponent<CharacterController>();
                var hurtbox = enemy.GetComponentInChildren<CombatHurtbox>();
                var visual = enemy.transform.Find("VisualRoot");
                Assert.That(character, Is.Not.Null, enemy.name);
                Assert.That(enemy.GetComponent<CapsuleCollider>(), Is.Null, enemy.name);
                Assert.That(character.center, Is.EqualTo(Vector3.up), enemy.name);
                Assert.That(character.height, Is.EqualTo(2f), enemy.name);
                Assert.That(character.radius, Is.EqualTo(.5f), enemy.name);
                Assert.That(hurtbox, Is.Not.Null, enemy.name);
                Assert.That(hurtbox.GetComponent<CapsuleCollider>().isTrigger, Is.True, enemy.name);
                Assert.That(hurtbox.GetComponent<CapsuleCollider>().radius, Is.GreaterThan(character.radius), enemy.name);
                Assert.That(visual, Is.Not.Null, enemy.name);
                Assert.That(visual.localPosition, Is.EqualTo(Vector3.zero), enemy.name);
            }
        }

        [Test]
        public void TravelGroupUsesRequestedCountAndStableNegativeIds()
        {
            var firstRoot = new GameObject("First travel spawner");
            firstRoot.transform.SetParent(root.transform, false);
            var secondRoot = new GameObject("Second travel spawner");
            secondRoot.transform.SetParent(root.transform, false);
            var first = CreateSpawner(firstRoot.transform, 707, 18);
            var second = CreateSpawner(secondRoot.transform, 707, 18);
            var encounter = new PayloadEscortEncounter(4, 5, PayloadAssaultPattern.LeftAmbush);
            var bounds = new Rect(-28f, -50f, 56f, 100f);

            first.SpawnTravelGroup(encounter, Vector3.zero, Vector3.forward, bounds, 4.5f);
            second.SpawnTravelGroup(encounter, Vector3.zero, Vector3.forward, bounds, 4.5f);
            var firstEnemies = first.LivingEnemies.OrderBy(enemy => enemy.EnemyId).ToArray();
            var secondEnemies = second.LivingEnemies.OrderBy(enemy => enemy.EnemyId).ToArray();

            Assert.That(firstEnemies, Has.Length.EqualTo(5));
            Assert.That(firstEnemies.All(enemy => enemy.EnemyId < 0), Is.True);
            Assert.That(firstEnemies.Select(enemy => enemy.EnemyId).Distinct().Count(), Is.EqualTo(5));
            for (var index = 0; index < firstEnemies.Length; index++)
            {
                Assert.That(secondEnemies[index].EnemyId, Is.EqualTo(firstEnemies[index].EnemyId));
                Assert.That(secondEnemies[index].Definition.Archetype, Is.EqualTo(firstEnemies[index].Definition.Archetype));
                Assert.That(secondEnemies[index].transform.position, Is.EqualTo(firstEnemies[index].transform.position));
            }
        }

        [Test]
        public void TravelGroupsCanOverlapLivingEnemies()
        {
            var spawner = CreateSpawner(root.transform, 808, 18);
            var bounds = new Rect(-28f, -50f, 56f, 100f);

            spawner.SpawnTravelGroup(
                new PayloadEscortEncounter(1, 4, PayloadAssaultPattern.ForwardBlockade),
                Vector3.zero,
                Vector3.forward,
                bounds,
                4.5f);
            spawner.SpawnTravelGroup(
                new PayloadEscortEncounter(2, 3, PayloadAssaultPattern.RearPursuit),
                Vector3.zero,
                Vector3.forward,
                bounds,
                4.5f);

            Assert.That(spawner.ActiveCount, Is.EqualTo(7));
            Assert.That(spawner.PendingCount, Is.Zero);
        }

        [Test]
        public void PreplacedSentriesUseDedicatedIdentityAndHoldGuardPosts()
        {
            var spawner = CreateSpawner(root.transform, 811, 18);
            var positions = new[]
            {
                new Vector3(-8f, 0f, 12f),
                new Vector3(-6f, 0f, 14f),
                new Vector3(7f, 0f, 28f),
                new Vector3(9f, 0f, 30f)
            };
            var facings = new[] { Vector3.right, Vector3.right, Vector3.left, Vector3.left };

            var identifiers = spawner.SpawnPreplacedSentries(positions, facings);

            Assert.That(identifiers, Has.Count.EqualTo(4));
            Assert.That(identifiers.Distinct().Count(), Is.EqualTo(4));
            Assert.That(spawner.LivingEnemies, Has.Count.EqualTo(4));
            Assert.That(spawner.LivingEnemies.All(enemy => enemy.WaveId == PayloadWaveSpawner.PreplacedSentryWaveId), Is.True);
            Assert.That(spawner.LivingEnemies.All(enemy => enemy.HasGuardPost), Is.True);
            Assert.That(spawner.LivingEnemies.Select(enemy => enemy.transform.position), Is.EquivalentTo(positions));
        }

        [Test]
        public void ActivationNoiseResponseArrivesAsDistinctThreeEnemyWaves()
        {
            var spawner = CreateSpawner(root.transform, 812, 18);
            var bounds = new Rect(-28f, -50f, 56f, 100f);

            Assert.That(spawner.SpawnActivationResponseWave(0, Vector3.zero, Vector3.forward, bounds, 4.5f), Is.True);
            Assert.That(spawner.SpawnActivationResponseWave(1, Vector3.zero, Vector3.forward, bounds, 4.5f), Is.True);

            Assert.That(spawner.LivingEnemies.Count(enemy => enemy.WaveId == PayloadWaveSpawner.ActivationWaveId(0)), Is.EqualTo(3));
            Assert.That(spawner.LivingEnemies.Count(enemy => enemy.WaveId == PayloadWaveSpawner.ActivationWaveId(1)), Is.EqualTo(3));
            Assert.That(spawner.LivingEnemies.Select(enemy => enemy.EnemyId).Distinct().Count(), Is.EqualTo(6));
        }

        [Test]
        public void CheckpointWaveCanJoinLivingTravelEnemies()
        {
            var spawner = CreateSpawner(root.transform, 909, 18);
            var started = new List<int>();
            spawner.WaveStarted += (_, ids) => started.AddRange(ids);
            spawner.SpawnTravelGroup(
                new PayloadEscortEncounter(3, 5, PayloadAssaultPattern.RightAmbush),
                Vector3.zero,
                Vector3.forward,
                new Rect(-28f, -50f, 56f, 100f),
                4.5f);

            Assert.DoesNotThrow(() => spawner.SpawnWave(6, 0, false));
            Assert.That(started, Has.Count.EqualTo(10));
            Assert.That(started.All(id => id > 0), Is.True);
            Assert.That(spawner.ActiveCount, Is.EqualTo(15));
        }

        [Test]
        public void TravelSpawnSanitizesDirectionAndBounds()
        {
            var spawner = CreateSpawner(root.transform, 1001, 18);
            var bounds = new Rect(-28f, -50f, 56f, 100f);
            var payloadPosition = new Vector3(27f, 0f, 49f);

            spawner.SpawnTravelGroup(
                new PayloadEscortEncounter(5, 6, PayloadAssaultPattern.ForwardBlockade),
                payloadPosition,
                new Vector3(float.NaN, 0f, float.PositiveInfinity),
                bounds,
                4.5f);

            foreach (var enemy in spawner.LivingEnemies)
            {
                var position = enemy.transform.position;
                Assert.That(float.IsNaN(position.x) || float.IsInfinity(position.x), Is.False, enemy.name);
                Assert.That(float.IsNaN(position.z) || float.IsInfinity(position.z), Is.False, enemy.name);
                Assert.That(position.x, Is.InRange(-27f, 27f), enemy.name);
                Assert.That(position.z, Is.InRange(-49f, 49f), enemy.name);
                Assert.That(Vector3.Distance(position, payloadPosition), Is.GreaterThanOrEqualTo(6.5f), enemy.name);
            }
        }

        [Test]
        public void TravelSpawnUsesOffCameraCandidatesWhenCameraIsProvided()
        {
            var cameraObject = new GameObject("Travel Spawn Camera", typeof(Camera));
            cameraObject.transform.SetParent(root.transform, false);
            cameraObject.transform.position = new Vector3(0f, 16f, -16f);
            cameraObject.transform.rotation = Quaternion.LookRotation(new Vector3(0f, 0f, 3f) - cameraObject.transform.position);
            var camera = cameraObject.GetComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 9f;
            camera.aspect = 16f / 9f;
            var spawner = CreateSpawner(root.transform, 1201, 18);

            spawner.SpawnTravelGroup(
                new PayloadEscortEncounter(0, 6, PayloadAssaultPattern.ForwardBlockade),
                Vector3.zero,
                Vector3.forward,
                new Rect(-28f, -50f, 56f, 100f),
                4.5f,
                camera);

            Assert.That(spawner.LivingEnemies, Is.Not.Empty);
            foreach (var enemy in spawner.LivingEnemies)
            {
                var viewport = camera.WorldToViewportPoint(enemy.transform.position + Vector3.up);
                var visible = viewport.z > 0f && viewport.x >= 0f && viewport.x <= 1f && viewport.y >= 0f && viewport.y <= 1f;
                Assert.That(visible, Is.False, $"{enemy.name} spawned on camera at {viewport}.");
            }
        }

        [Test]
        public void TravelSpawnDefersWhenBoundsCannotSatisfyEscortExclusion()
        {
            var spawner = CreateSpawner(root.transform, 1202, 18);

            var spawned = spawner.SpawnTravelGroup(
                new PayloadEscortEncounter(0, 4, PayloadAssaultPattern.ForwardBlockade),
                Vector3.zero,
                Vector3.forward,
                new Rect(-1f, -1f, 2f, 2f),
                10f);

            Assert.That(spawned, Is.False);
            Assert.That(spawner.ActiveCount, Is.Zero);
            Assert.That(spawner.PendingCount, Is.Zero);
        }

        private PayloadWaveSpawner CreateSpawner(Transform parent, int seed, int activeCap, GameObject visualPrefab = null)
        {
            var spawnerObject = new GameObject("Payload Wave Spawner");
            spawnerObject.transform.SetParent(parent, false);
            var spawner = spawnerObject.AddComponent<PayloadWaveSpawner>();
            spawner.Configure(
                player.transform,
                projectileTemplate,
                visualPrefab,
                visualPrefab,
                visualPrefab,
                visualPrefab,
                visualPrefab,
                anchors,
                seed,
                activeCap);
            return spawner;
        }
    }
}
