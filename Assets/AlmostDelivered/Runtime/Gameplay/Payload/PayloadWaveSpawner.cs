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
        private readonly HashSet<int> checkpointEnemyIds = new HashSet<int>();
        private int activeCheckpointWaveId = -1;
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
            if (activeCheckpointWaveId >= 0)
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
                checkpointEnemyIds.Add(enemyId);
                pending.Enqueue(new PendingEnemy(
                    waveId,
                    enemyId,
                    MissionMobDefinition.For(archetype),
                    SpawnPosition(random, anchor)));
            }

            activeCheckpointWaveId = waveId;
            WaveStarted?.Invoke(waveId, Array.AsReadOnly(identifiers));
            SpawnUntilCap();
        }

        public bool SpawnTravelGroup(
            PayloadEscortEncounter encounter,
            Vector3 payloadPosition,
            Vector3 routeDirection,
            Rect missionBounds,
            float exclusionRadius,
            Camera visibilityCamera = null)
        {
            if (encounter.GroupSize <= 0 || !IsFinite(payloadPosition))
            {
                return false;
            }

            var forward = HorizontalDirection(routeDirection, Vector3.forward);
            var right = new Vector3(forward.z, 0f, -forward.x);
            var random = new System.Random(unchecked(missionSeed * 486187739 + encounter.SequenceId * 397));
            var approach = ApproachDirection(encounter.Pattern, forward, right);
            var approachDistance = 18f + (float)random.NextDouble() * 6f;
            var minimumDistance = Mathf.Max(0f, exclusionRadius) + 2f;
            var travelGroup = new List<PendingEnemy>(encounter.GroupSize);

            for (var index = 0; index < encounter.GroupSize; index++)
            {
                var angle = (float)(random.NextDouble() * Math.PI * 2d);
                var scatter = 3f + (float)random.NextDouble() * 2f;
                if (!TryResolveTravelPosition(
                        payloadPosition,
                        approach,
                        approachDistance,
                        angle,
                        scatter,
                        missionBounds,
                        minimumDistance,
                        visibilityCamera,
                        out var candidate))
                {
                    return false;
                }

                var enemyId = unchecked(-((encounter.SequenceId + 1) * 1000 + index + 1));
                travelGroup.Add(new PendingEnemy(
                    -(encounter.SequenceId + 1),
                    enemyId,
                    MissionMobDefinition.For(SelectArchetype(random, false, index)),
                    candidate));
            }

            foreach (var enemy in travelGroup)
            {
                pending.Enqueue(enemy);
            }

            SpawnUntilCap();
            return true;
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
            checkpointEnemyIds.Clear();
            activeCheckpointWaveId = -1;
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
            var character = actor.AddComponent<CharacterController>();
            character.height = 2f;
            character.radius = .5f;
            character.center = Vector3.up;
            character.slopeLimit = 45f;
            character.stepOffset = .3f;

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
                visual.transform.localPosition = Vector3.zero;
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
            if (checkpointEnemyIds.Remove(enemy.EnemyId) && checkpointEnemyIds.Count == 0)
            {
                activeCheckpointWaveId = -1;
            }
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

        private static Vector3 ApproachDirection(PayloadAssaultPattern pattern, Vector3 forward, Vector3 right)
        {
            switch (pattern)
            {
                case PayloadAssaultPattern.LeftAmbush:
                    return -right;
                case PayloadAssaultPattern.RightAmbush:
                    return right;
                case PayloadAssaultPattern.RearPursuit:
                    return -forward;
                default:
                    return forward;
            }
        }

        private static bool TryResolveTravelPosition(
            Vector3 payloadPosition,
            Vector3 approach,
            float approachDistance,
            float scatterAngle,
            float scatter,
            Rect bounds,
            float minimumDistance,
            Camera visibilityCamera,
            out Vector3 position)
        {
            const int candidateCount = 16;
            for (var attempt = 0; attempt < candidateCount; attempt++)
            {
                var offset = attempt == 0
                    ? 0f
                    : ((attempt + 1) / 2) * 45f * (attempt % 2 == 0 ? -1f : 1f);
                var rotatedApproach = Quaternion.Euler(0f, offset, 0f) * approach;
                var center = payloadPosition + rotatedApproach * approachDistance;
                var angle = scatterAngle + attempt * 2.39996323f;
                var candidate = center + new Vector3(Mathf.Cos(angle) * scatter, 0f, Mathf.Sin(angle) * scatter);
                if (!TrySanitizeTravelPosition(candidate, payloadPosition, bounds, minimumDistance, out candidate))
                {
                    continue;
                }

                if (visibilityCamera != null && IsVisible(visibilityCamera, candidate))
                {
                    continue;
                }

                position = candidate;
                return true;
            }

            position = default;
            return false;
        }

        private static bool TrySanitizeTravelPosition(
            Vector3 candidate,
            Vector3 payloadPosition,
            Rect bounds,
            float minimumDistance,
            out Vector3 position)
        {
            var insetBounds = Rect.MinMaxRect(
                bounds.xMin + 1f,
                bounds.yMin + 1f,
                bounds.xMax - 1f,
                bounds.yMax - 1f);
            if (insetBounds.width <= 0f || insetBounds.height <= 0f)
            {
                insetBounds = bounds;
            }

            candidate = new Vector3(
                Mathf.Clamp(IsFinite(candidate.x) ? candidate.x : payloadPosition.x, insetBounds.xMin, insetBounds.xMax),
                0f,
                Mathf.Clamp(IsFinite(candidate.z) ? candidate.z : payloadPosition.z, insetBounds.yMin, insetBounds.yMax));
            var delta = Vector3.ProjectOnPlane(candidate - payloadPosition, Vector3.up);
            if (delta.magnitude + .001f >= minimumDistance)
            {
                position = candidate;
                return true;
            }

            var towardCenter = new Vector3(insetBounds.center.x - payloadPosition.x, 0f, insetBounds.center.y - payloadPosition.z);
            var direction = HorizontalDirection(delta, HorizontalDirection(towardCenter, Vector3.back));
            candidate = payloadPosition + direction * (minimumDistance + .05f);
            candidate.x = Mathf.Clamp(candidate.x, insetBounds.xMin, insetBounds.xMax);
            candidate.z = Mathf.Clamp(candidate.z, insetBounds.yMin, insetBounds.yMax);
            if (Vector3.Distance(candidate, payloadPosition) + .001f >= minimumDistance)
            {
                position = candidate;
                return true;
            }

            var corners = new[]
            {
                new Vector3(insetBounds.xMin, 0f, insetBounds.yMin),
                new Vector3(insetBounds.xMin, 0f, insetBounds.yMax),
                new Vector3(insetBounds.xMax, 0f, insetBounds.yMin),
                new Vector3(insetBounds.xMax, 0f, insetBounds.yMax)
            };
            var farthest = corners[0];
            var farthestDistance = (farthest - payloadPosition).sqrMagnitude;
            for (var index = 1; index < corners.Length; index++)
            {
                var distance = (corners[index] - payloadPosition).sqrMagnitude;
                if (distance > farthestDistance)
                {
                    farthest = corners[index];
                    farthestDistance = distance;
                }
            }

            if (Vector3.Distance(farthest, payloadPosition) + .001f >= minimumDistance)
            {
                position = farthest;
                return true;
            }

            position = default;
            return false;
        }

        private static bool IsVisible(Camera camera, Vector3 position)
        {
            var feet = camera.WorldToViewportPoint(position);
            var head = camera.WorldToViewportPoint(position + Vector3.up * 2f);
            return IsInsideViewport(feet) || IsInsideViewport(head);
        }

        private static bool IsInsideViewport(Vector3 viewport)
        {
            return viewport.z > 0f && viewport.x >= 0f && viewport.x <= 1f && viewport.y >= 0f && viewport.y <= 1f;
        }

        private static Vector3 HorizontalDirection(Vector3 value, Vector3 fallback)
        {
            if (!IsFinite(value))
            {
                value = fallback;
            }

            value = Vector3.ProjectOnPlane(value, Vector3.up);
            if (value.sqrMagnitude < .0001f)
            {
                value = Vector3.ProjectOnPlane(fallback, Vector3.up);
            }

            return value.sqrMagnitude < .0001f ? Vector3.forward : value.normalized;
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
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
