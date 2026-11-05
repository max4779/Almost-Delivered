using System;
using System.Collections.Generic;
using UnityEngine;

namespace AlmostDelivered.MapGeneration
{
    [Serializable]
    public struct MissionMapProtectedZone
    {
        [SerializeField] private Vector2 center;
        [SerializeField] private Vector2 size;

        public MissionMapProtectedZone(Vector2 center, Vector2 size)
        {
            this.center = center;
            this.size = size;
        }

        public Vector2 Center => center;
        public Vector2 Size => size;
    }

    public sealed class MissionMapGenerator : MonoBehaviour
    {
        public const string GeneratedRootName = "Generated Map";

        [SerializeField] private MissionMapTheme theme;
        [SerializeField] private Vector2 halfExtents = new Vector2(18f, 14f);
        [SerializeField, Min(0)] private int terrainDetailCount = 24;
        [SerializeField, Min(0)] private int blockingObstacleCount = 14;
        [SerializeField, Min(0)] private int maxAttempts = 2400;
        [SerializeField] private MissionMapProtectedZone[] protectedZones = Array.Empty<MissionMapProtectedZone>();
        [SerializeField] private bool randomizeOnStart = true;

        [SerializeField] private Transform generatedRoot;

        public MissionMapTheme Theme => theme;
        public int TerrainDetailCount => terrainDetailCount;
        public int BlockingObstacleCount => blockingObstacleCount;
        public bool RandomizeOnStart => randomizeOnStart;
        public int LastSeed { get; private set; }
        public Transform GeneratedRoot => generatedRoot;

        public void Configure(
            MissionMapTheme valueTheme,
            Vector2 valueHalfExtents,
            int valueTerrainDetailCount,
            int valueBlockingObstacleCount,
            MissionMapProtectedZone[] valueProtectedZones,
            bool valueRandomizeOnStart,
            int valueMaxAttempts = 2400)
        {
            theme = valueTheme;
            halfExtents = valueHalfExtents;
            terrainDetailCount = Mathf.Max(0, valueTerrainDetailCount);
            blockingObstacleCount = Mathf.Max(0, valueBlockingObstacleCount);
            protectedZones = valueProtectedZones ?? Array.Empty<MissionMapProtectedZone>();
            randomizeOnStart = valueRandomizeOnStart;
            maxAttempts = Mathf.Max(0, valueMaxAttempts);
        }

        public void Generate(int seed)
        {
            Generate(seed, Array.Empty<MapExclusionZone>());
        }

        public void Generate(int seed, IReadOnlyList<MapExclusionZone> extraBlockingExclusions)
        {
            LastSeed = seed;
            ReplaceGeneratedRoot();
            if (theme == null)
            {
                return;
            }

            var protectedExclusions = BuildExclusions();
            GenerateCategory(
                theme.TerrainDetails,
                terrainDetailCount,
                unchecked(seed ^ 0x2C9277B5),
                MissionMapPieceCategory.TerrainDetail,
                "Terrain Details",
                .015f,
                protectedExclusions);
            var blockingExclusions = new List<MapExclusionZone>(protectedExclusions);
            if (extraBlockingExclusions != null)
            {
                for (var index = 0; index < extraBlockingExclusions.Count; index++)
                {
                    blockingExclusions.Add(extraBlockingExclusions[index]);
                }
            }

            GenerateCategory(
                theme.BlockingObstacles,
                blockingObstacleCount,
                unchecked(seed ^ (int)0xA341316C),
                MissionMapPieceCategory.BlockingObstacle,
                "Blocking Obstacles",
                0f,
                blockingExclusions);
        }

        private void Start()
        {
            if (randomizeOnStart)
            {
                Generate(unchecked(Environment.TickCount * 397 ^ Guid.NewGuid().GetHashCode() ^ GetInstanceID()));
            }
        }

        private void ReplaceGeneratedRoot()
        {
            if (generatedRoot != null)
            {
                generatedRoot.gameObject.SetActive(false);
                if (Application.isPlaying)
                {
                    Destroy(generatedRoot.gameObject);
                }
                else
                {
                    DestroyImmediate(generatedRoot.gameObject);
                }
            }

            var rootObject = new GameObject(GeneratedRootName);
            generatedRoot = rootObject.transform;
            generatedRoot.SetParent(transform, false);
        }

        private void GenerateCategory(
            IReadOnlyList<MissionMapPiece> catalog,
            int requestedCount,
            int seed,
            MissionMapPieceCategory category,
            string categoryRootName,
            float height,
            IReadOnlyList<MapExclusionZone> exclusions)
        {
            var categoryRoot = new GameObject(categoryRootName).transform;
            categoryRoot.SetParent(generatedRoot, false);
            if (catalog == null || catalog.Count == 0 || requestedCount <= 0)
            {
                return;
            }

            var specs = new List<MapPieceSpec>(catalog.Count);
            for (var index = 0; index < catalog.Count; index++)
            {
                specs.Add(new MapPieceSpec(catalog[index] == null ? Vector2.zero : catalog[index].Footprint));
            }

            var placements = ProceduralMapPlanner.Generate(seed, halfExtents, specs, requestedCount, exclusions, maxAttempts);
            foreach (var placement in placements)
            {
                var piece = catalog[placement.PieceIndex];
                if (piece == null || piece.Prefab == null)
                {
                    continue;
                }

                var instance = Instantiate(piece.Prefab, categoryRoot);
                instance.name = piece.Prefab.name;
                instance.transform.localPosition = new Vector3(placement.Position.x, height, placement.Position.y);
                instance.transform.localRotation = Quaternion.Euler(0f, placement.QuarterTurns * 90f, 0f);
                instance.SetActive(true);
                instance.AddComponent<MissionMapPieceInstance>().Configure(category, placement);
            }
        }

        private IReadOnlyList<MapExclusionZone> BuildExclusions()
        {
            var exclusions = new List<MapExclusionZone>(protectedZones.Length);
            foreach (var zone in protectedZones)
            {
                exclusions.Add(new MapExclusionZone(zone.Center, zone.Size));
            }

            return exclusions;
        }
    }
}
