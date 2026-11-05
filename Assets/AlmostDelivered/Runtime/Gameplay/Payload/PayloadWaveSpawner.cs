using System;
using System.Collections.Generic;
using AlmostDelivered.Combat;
using UnityEngine;

namespace AlmostDelivered.Gameplay.Payload
{
    public sealed class PayloadWaveSpawner : MonoBehaviour
    {
        private static readonly int[] CheckpointWaveCounts = { 10, 14, 18 };

        [SerializeField] private Transform playerTarget;
        [SerializeField] private GameObject projectileTemplate;
        [SerializeField] private GameObject meleeVisualPrefab;
        [SerializeField] private GameObject rangedVisualPrefab;
        [SerializeField] private GameObject shieldVisualPrefab;
        [SerializeField] private GameObject commanderVisualPrefab;
        [SerializeField] private GameObject chargerVisualPrefab;
        [SerializeField] private Transform[] spawnAnchors = Array.Empty<Transform>();
        [SerializeField] private int missionSeed;
        [SerializeField, Min(1)] private int activeCap = 18;

        private readonly Queue<PendingEnemy> pending = new Queue<PendingEnemy>();
        private readonly Dictionary<int, MissionMobController> living = new Dictionary<int, MissionMobController>();
        private bool shuttingDown;

        public IReadOnlyCollection<MissionMobController> LivingEnemies => living.Values;
        public int ActiveCount => living.Count;
        public int PendingCount => pending.Count;
        public int ActiveCap => activeCap;

        public static int WaveCountFor(int checkpointIndex, bool finalWave)
        {
            return finalWave
                ? 22
                : CheckpointWaveCounts[Mathf.Clamp(checkpointIndex, 0, CheckpointWaveCounts.Length - 1)];
        }

        public event Action<int, IReadOnlyCollection<int>> WaveStarted;
        public event Action<int, int> EnemyRemoved;

        public void Configure(
            Transform target,
            GameObject projectile,
            GameObject meleeVisual,
            GameObject rangedVisual,
            GameObject shieldVisual,
            GameObject commanderVisual,
            GameObject chargerVisual,
            Transform[] anchors,
            int seed,
            int maximumActiveEnemies)
        {
            playerTarget = target;
            projectileTemplate = projectile;
            meleeVisualPrefab = meleeVisual;
            rangedVisualPrefab = rangedVisual;
            shieldVisualPrefab = shieldVisual;
            commanderVisualPrefab = commanderVisual;
            chargerVisualPrefab = chargerVisual;
            spawnAnchors = anchors ?? Array.Empty<Transform>();
            missionSeed = seed;
            activeCap = Mathf.Max(1, maximumActiveEnemies);
            shuttingDown = false;
        }

        public void SpawnWave(int waveId, int checkpointIndex, bool finalWave)
        {
            if (living.Count > 0 || pending.Count > 0)
            {
                throw new InvalidOperationException("The previous payload wave is still active.");
            }

            var count = WaveCountFor(checkpointIndex, finalWave);
            var random = new System.Random(unchecked(missionSeed * 486187739 + waveId * 397 + checkpointIndex * 31));
            var anchor = ResolveAnchor(checkpointIndex);
            var identifiers = new int[count];

            for (var index = 0; index < count; index++)
            {
                var archetype = SelectArchetype(random, finalWave, index);
                var enemyId = unchecked(waveId * 1000 + index + 1);
                identifiers[index] = enemyId;
                pending.Enqueue(new PendingEnemy(
                    waveId,
                    enemyId,
                    MissionMobDefinition.For(archetype),
                    SpawnPosition(random, anchor)));
            }

            WaveStarted?.Invoke(waveId, Array.AsReadOnly(identifiers));
            SpawnUntilCap();
        }

        private void OnDisable()
        {
            shuttingDown = true;
        }

        private void OnDestroy()
        {
            shuttingDown = true;
            foreach (var enemy in living.Values)
            {
                if (enemy != null)
                {
                    enemy.Removed -= HandleRemoved;
                }
            }

            living.Clear();
            pending.Clear();
        }

        private void SpawnUntilCap()
        {
            while (!shuttingDown && isActiveAndEnabled && gameObject.activeInHierarchy && living.Count < activeCap && pending.Count > 0)
            {
                var next = pending.Dequeue();
                var controller = CreateEnemy(next);
                living.Add(next.EnemyId, controller);
                controller.Removed += HandleRemoved;
            }
        }

        private MissionMobController CreateEnemy(PendingEnemy pendingEnemy)
        {
            var actor = new GameObject($"Wave {pendingEnemy.WaveId} Enemy {pendingEnemy.EnemyId}");
            actor.transform.SetParent(transform, false);
            actor.transform.position = pendingEnemy.Position;

            var health = actor.AddComponent<ActorHealth>();
            actor.AddComponent<CombatTeam>().Team = CombatTeamId.Enemy;
            var collider = actor.AddComponent<CapsuleCollider>();
            collider.height = 2f;
            collider.radius = .5f;

            RangedAttackController rangedAttack = null;
            if (IsRanged(pendingEnemy.Definition.Archetype) &&
                projectileTemplate != null &&
                projectileTemplate.GetComponent<CombatProjectile>() != null)
            {
                rangedAttack = actor.AddComponent<RangedAttackController>();
                var muzzle = new GameObject("Muzzle").transform;
                muzzle.SetParent(actor.transform, false);
                muzzle.localPosition = new Vector3(0f, .65f, .8f);
                rangedAttack.Configure(projectileTemplate, muzzle, 8f, 1f, 15f, 2.5f);
            }

            var controller = actor.AddComponent<MissionMobController>();
            controller.AssignIdentity(pendingEnemy.WaveId, pendingEnemy.EnemyId);
            controller.Configure(pendingEnemy.Definition, playerTarget, rangedAttack);

            var visualPrefab = VisualFor(pendingEnemy.Definition.Archetype);
            if (visualPrefab != null)
            {
                var visual = Instantiate(visualPrefab, actor.transform);
                visual.name = "VisualRoot";
                visual.transform.localPosition = Vector3.down;
                visual.transform.localRotation = Quaternion.identity;
            }

            WorldHealthBar.Attach(actor, new Vector3(0f, 2.2f, 0f));
            return controller;
        }

        private void HandleRemoved(MissionMobController enemy)
        {
            if (shuttingDown || !isActiveAndEnabled || !gameObject.activeInHierarchy || enemy == null)
            {
                return;
            }

            if (!living.TryGetValue(enemy.EnemyId, out var tracked) || tracked != enemy)
            {
                return;
            }

            enemy.Removed -= HandleRemoved;
            living.Remove(enemy.EnemyId);
            EnemyRemoved?.Invoke(enemy.WaveId, enemy.EnemyId);
            SpawnUntilCap();
        }

        private Transform ResolveAnchor(int checkpointIndex)
        {
            if (spawnAnchors != null && spawnAnchors.Length > 0)
            {
                var anchor = spawnAnchors[Mathf.Clamp(checkpointIndex, 0, spawnAnchors.Length - 1)];
                if (anchor != null)
                {
                    return anchor;
                }
            }

            return transform;
        }

        private GameObject VisualFor(MissionMobArchetype archetype)
        {
            switch (archetype)
            {
                case MissionMobArchetype.Ranged:
                    return rangedVisualPrefab;
                case MissionMobArchetype.Shield:
                    return shieldVisualPrefab;
                case MissionMobArchetype.CommanderBoss:
                    return commanderVisualPrefab;
                case MissionMobArchetype.ChargerBoss:
                    return chargerVisualPrefab;
                default:
                    return meleeVisualPrefab;
            }
        }

        private static MissionMobArchetype SelectArchetype(System.Random random, bool finalWave, int index)
        {
            if (finalWave && index == 0)
            {
                return MissionMobArchetype.CommanderBoss;
            }

            if (finalWave && index == 1)
            {
                return MissionMobArchetype.ChargerBoss;
            }

            return (MissionMobArchetype)random.Next(0, 3);
        }

        private static Vector3 SpawnPosition(System.Random random, Transform anchor)
        {
            var angle = (float)(random.NextDouble() * Math.PI * 2d);
            var radius = 6f + (float)random.NextDouble() * 4f;
            return anchor.position + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
        }

        private static bool IsRanged(MissionMobArchetype archetype)
        {
            return archetype == MissionMobArchetype.Ranged || archetype == MissionMobArchetype.CommanderBoss;
        }

        private readonly struct PendingEnemy
        {
            public PendingEnemy(int waveId, int enemyId, MissionMobDefinition definition, Vector3 position)
            {
                WaveId = waveId;
                EnemyId = enemyId;
                Definition = definition;
                Position = position;
            }

            public int WaveId { get; }
            public int EnemyId { get; }
            public MissionMobDefinition Definition { get; }
            public Vector3 Position { get; }
        }
    }
}
