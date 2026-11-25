using System;
using System.Collections.Generic;
using UnityEngine;

namespace AlmostDelivered.MapGeneration
{
    public enum ExtractionSiteTheme { RuinedFort, Graveyard }

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
        public const float ExtractionExclusionSize = 26f;
        private const float ExtractionVisualScale = 1.35f;
        public const string GeneratedRootName = "Generated Map";

        [SerializeField] private MissionMapTheme theme;
        [SerializeField] private Vector2 halfExtents = new Vector2(18f, 14f);
        [SerializeField, Min(0)] private int terrainDetailCount = 24;
        [SerializeField, Min(0)] private int traversableTerrainCount;
        [SerializeField, Min(0)] private int blockingObstacleCount = 14;
        [SerializeField, Min(0)] private int foliageCount;
        [SerializeField, Min(0)] private int maxAttempts = 2400;
        [SerializeField] private MissionMapProtectedZone[] protectedZones = Array.Empty<MissionMapProtectedZone>();
        [SerializeField] private bool randomizeOnStart = true;

        [SerializeField] private Transform generatedRoot;
        private Vector3 extractionPosition;
        private bool hasExtractionSite;
        private ExtractionSiteTheme extractionTheme;
        private Transform extractionSite;

        public MissionMapTheme Theme => theme;
        public int TerrainDetailCount => terrainDetailCount;
        public int TraversableTerrainCount => traversableTerrainCount;
        public int BlockingObstacleCount => blockingObstacleCount;
        public int FoliageCount => foliageCount;
        public bool RandomizeOnStart => randomizeOnStart;
        public int LastSeed { get; private set; }
        public Transform GeneratedRoot => generatedRoot;
        public Transform ExtractionSite => extractionSite;
        public bool HasExtractionSite => hasExtractionSite;
        public Vector3 ExtractionPosition => extractionPosition;
        public ExtractionSiteTheme ExtractionTheme => extractionTheme;

        public void ConfigureExtractionSite(int seed, Rect bounds, Gameplay.Payload.PayloadRoutePlan route)
        {
            hasExtractionSite = route != null && bounds.width >= 32f && bounds.height >= 32f;
            if (!hasExtractionSite)
            {
                extractionPosition = Vector3.zero;
                return;
            }
            extractionTheme = (seed & 1) == 0 ? ExtractionSiteTheme.RuinedFort : ExtractionSiteTheme.Graveyard;
            var sideMissions = Gameplay.Payload.SideMissionPlanner.Generate(seed, bounds, route);
            var bestScore = float.NegativeInfinity;
            foreach (var fraction in new[] { .35f, .5f, .65f, .8f })
            {
                var center = route.EvaluateDistance(route.TotalLength * fraction);
                var direction = route.EvaluateDirection(route.TotalLength * fraction);
                var normal = new Vector3(-direction.z, 0f, direction.x);
                foreach (var side in new[] { -1f, 1f })
                {
                    var candidate = ClampInside(center + normal * side * 22f, bounds, 14f);
                    var score = DistanceToRoute(candidate, route);
                    score = Mathf.Min(score, Vector3.Distance(candidate, route.Points[0]) * .5f);
                    foreach (var objective in sideMissions)
                        score = Mathf.Min(score, Vector3.Distance(candidate, objective.position) * .7f);
                    if (score <= bestScore) continue;
                    bestScore = score;
                    extractionPosition = candidate;
                }
            }
        }

        private static float DistanceToRoute(Vector3 point, Gameplay.Payload.PayloadRoutePlan route)
        {
            var best = float.PositiveInfinity;
            for (var index = 1; index < route.Points.Count; index++)
            {
                var a = route.Points[index - 1];
                var b = route.Points[index];
                var segment = new Vector2(b.x - a.x, b.z - a.z);
                var offset = new Vector2(point.x - a.x, point.z - a.z);
                var t = segment.sqrMagnitude > .0001f
                    ? Mathf.Clamp01(Vector2.Dot(offset, segment) / segment.sqrMagnitude) : 0f;
                best = Mathf.Min(best, (offset - segment * t).magnitude);
            }
            return best;
        }

        private static Vector3 ClampInside(Vector3 point, Rect bounds, float inset)
        {
            return new Vector3(Mathf.Clamp(point.x, bounds.xMin + inset, bounds.xMax - inset),
                0f, Mathf.Clamp(point.z, bounds.yMin + inset, bounds.yMax - inset));
        }

        public void SetTheme(MissionMapTheme value)
        {
            theme = value;
        }

        public void Configure(
            MissionMapTheme valueTheme,
            Vector2 valueHalfExtents,
            int valueTerrainDetailCount,
            int valueBlockingObstacleCount,
            MissionMapProtectedZone[] valueProtectedZones,
            bool valueRandomizeOnStart,
            int valueMaxAttempts = 2400)
        {
            Configure(
                valueTheme,
                valueHalfExtents,
                valueTerrainDetailCount,
                0,
                valueBlockingObstacleCount,
                0,
                valueProtectedZones,
                valueRandomizeOnStart,
                valueMaxAttempts);
        }

        public void Configure(
            MissionMapTheme valueTheme,
            Vector2 valueHalfExtents,
            int valueTerrainDetailCount,
            int valueTraversableTerrainCount,
            int valueBlockingObstacleCount,
            int valueFoliageCount,
            MissionMapProtectedZone[] valueProtectedZones,
            bool valueRandomizeOnStart,
            int valueMaxAttempts = 2400)
        {
            theme = valueTheme;
            halfExtents = valueHalfExtents;
            terrainDetailCount = Mathf.Max(0, valueTerrainDetailCount);
            traversableTerrainCount = Mathf.Max(0, valueTraversableTerrainCount);
            blockingObstacleCount = Mathf.Max(0, valueBlockingObstacleCount);
            foliageCount = Mathf.Max(0, valueFoliageCount);
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

            var environmentExclusions = new List<MapExclusionZone>(protectedExclusions);
            if (hasExtractionSite)
                environmentExclusions.Add(new MapExclusionZone(
                    new Vector2(extractionPosition.x, extractionPosition.z),
                    Vector2.one * ExtractionExclusionSize));
            if (extraBlockingExclusions != null)
            {
                for (var index = 0; index < extraBlockingExclusions.Count; index++)
                {
                    environmentExclusions.Add(extraBlockingExclusions[index]);
                }
            }

            var hillPlacements = GenerateCategory(
                theme.TraversableTerrain,
                traversableTerrainCount,
                unchecked(seed ^ 0x51EAD123),
                MissionMapPieceCategory.TraversableTerrain,
                "Traversable Terrain",
                0f,
                environmentExclusions);

            var blockingExclusions = new List<MapExclusionZone>(environmentExclusions);
            AppendPlacementExclusions(blockingExclusions, hillPlacements);
            var blockerPlacements = GenerateCategory(
                theme.BlockingObstacles,
                blockingObstacleCount,
                unchecked(seed ^ (int)0xA341316C),
                MissionMapPieceCategory.BlockingObstacle,
                "Blocking Obstacles",
                0f,
                blockingExclusions);

            var foliageExclusions = new List<MapExclusionZone>(blockingExclusions);
            AppendPlacementExclusions(foliageExclusions, blockerPlacements);
            GenerateCategory(
                theme.Foliage,
                foliageCount,
                unchecked(seed ^ 0x63D83595),
                MissionMapPieceCategory.Foliage,
                "Foliage",
                .02f,
                foliageExclusions);
            if (hasExtractionSite) BuildExtractionSite();
        }

        private void BuildExtractionSite()
        {
            extractionSite = new GameObject(extractionTheme == ExtractionSiteTheme.RuinedFort
                ? "Extraction Ruined Fort" : "Extraction Graveyard").transform;
            extractionSite.SetParent(generatedRoot, false);
            extractionSite.position = extractionPosition;
            extractionSite.localScale = Vector3.one * ExtractionVisualScale;
            var ground = extractionTheme == ExtractionSiteTheme.RuinedFort
                ? new Color(.34f, .25f, .39f) : new Color(.28f, .24f, .36f);
            CreateCover("Weathered Extraction Ground", new Vector3(0f, .01f, 0f),
                new Vector3(7.6f, .025f, 7.6f), ground, false, PrimitiveType.Cylinder);
            CreateCover("West Ground Edge", new Vector3(-3.2f, .008f, -.5f),
                new Vector3(5.1f, .022f, 5.1f), ground, false, PrimitiveType.Cylinder);
            CreateCover("East Ground Edge", new Vector3(2.7f, .006f, 1.4f),
                new Vector3(4.8f, .02f, 4.8f), ground, false, PrimitiveType.Cylinder);
            var signal = new Color(1f, .22f, .72f);
            CreateExtractionSignal("Extraction Landing Ring", new Vector3(0f, .045f, 0f),
                new Vector3(3.2f, .025f, 3.2f), signal, PrimitiveType.Cylinder);
            CreateExtractionSignal("Extraction Landing Center", new Vector3(0f, .075f, 0f),
                new Vector3(2.35f, .025f, 2.35f), ground, PrimitiveType.Cylinder);
            for (var index = 0; index < 4; index++)
            {
                var angle = index * Mathf.PI * .5f + Mathf.PI * .25f;
                var direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                var position = direction * 7f;
                CreateCover($"Extraction Signal Post {index + 1}", position + Vector3.up * 1f,
                    new Vector3(.25f, 2f, .25f), new Color(.25f, .2f, .31f), false);
                CreateExtractionSignal($"Extraction Signal Light {index + 1}",
                    position + Vector3.up * 2.12f, new Vector3(.75f, .55f, .75f),
                    signal, PrimitiveType.Sphere);
            }
            if (extractionTheme == ExtractionSiteTheme.RuinedFort)
            {
                var stone = new Color(.44f, .47f, .48f);
                CreateCover("West Ruin", new Vector3(-6f, .8f, -3f), new Vector3(1.4f, 1.6f, 5f), stone);
                CreateCover("East Ruin", new Vector3(6f, .8f, 3f), new Vector3(1.4f, 1.6f, 5f), stone);
                CreateCover("North Broken Wall", new Vector3(-3f, .8f, 6f), new Vector3(4f, 1.6f, 1f), stone);
                CreateCover("South Broken Wall", new Vector3(3f, .8f, -6f), new Vector3(4f, 1.6f, 1f), stone);
                CreateCover("Fallen Parapet", new Vector3(-2f, .35f, 2f), new Vector3(3f, .7f, .8f), stone);
            }
            else
            {
                var stone = new Color(.49f, .52f, .5f);
                var earth = new Color(.25f, .31f, .25f);
                for (var index = 0; index < 11; index++)
                {
                    var angle = index * Mathf.PI * 2f / 11f;
                    var radius = index % 2 == 0 ? 5.7f : 6.7f;
                    var direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                    var place = direction * radius;
                    var rotation = Quaternion.Euler(0f, -angle * Mathf.Rad2Deg, 0f);
                    var mound = CreateCover($"Grave Mound {index + 1}",
                        place - direction * .85f + Vector3.up * .12f,
                        new Vector3(.72f, .12f, 1.05f), earth, false, PrimitiveType.Cylinder);
                    mound.transform.localRotation = rotation;
                    var marker = CreateCover($"Grave Marker {index + 1}",
                        place + Vector3.up * .57f, new Vector3(.98f, 1.14f, .34f), stone);
                    marker.transform.localRotation = rotation * Quaternion.Euler(0f, 0f,
                        index % 4 == 0 ? 8f : -3f);
                    var crown = CreateCover($"Grave Crown {index + 1}",
                        place + Vector3.up * 1.16f, new Vector3(.5f, .19f, .2f), stone,
                        false, PrimitiveType.Sphere);
                    crown.transform.localRotation = marker.transform.localRotation;
                }
                CreateCover("Collapsed Monument", new Vector3(-2f, .5f, 1.2f),
                    new Vector3(2.1f, 1f, 1f), stone);
                CreateCover("Monument Cap", new Vector3(-2.3f, 1.05f, 1.3f),
                    new Vector3(2.5f, .23f, 1.2f), stone, false);
            }
            CreateExtractionSignal("Extraction Beacon", new Vector3(0f, 1.1f, 0f),
                new Vector3(.5f, 2.2f, .5f), signal, PrimitiveType.Cube);
        }

        private GameObject CreateExtractionSignal(string label, Vector3 position, Vector3 scale,
            Color color, PrimitiveType primitive)
        {
            var piece = CreateCover(label, position, scale, color, false, primitive);
            var renderer = piece.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.EnableKeyword("_EMISSION");
                renderer.material.SetColor("_EmissionColor", color * .75f);
            }
            return piece;
        }

        private GameObject CreateCover(string label, Vector3 localPosition, Vector3 scale, Color color,
            bool blocking = true, PrimitiveType primitive = PrimitiveType.Cube)
        {
            var piece = GameObject.CreatePrimitive(primitive);
            piece.name = label;
            piece.transform.SetParent(extractionSite, false);
            piece.transform.localPosition = localPosition;
            piece.transform.localScale = scale;
            if (!blocking)
            {
                var collider = piece.GetComponent<Collider>();
                if (Application.isPlaying) Destroy(collider);
                else DestroyImmediate(collider);
            }
            var shader = Shader.Find("Standard");
            if (shader != null)
            {
                var material = new Material(shader) { color = color };
                piece.GetComponent<Renderer>().material = material;
            }
            return piece;
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
            extractionSite = null;
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

        private IReadOnlyList<MapPlacement> GenerateCategory(
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
                return Array.Empty<MapPlacement>();
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

            return placements;
        }

        private static void AppendPlacementExclusions(
            ICollection<MapExclusionZone> exclusions,
            IReadOnlyList<MapPlacement> placements)
        {
            for (var index = 0; index < placements.Count; index++)
            {
                exclusions.Add(new MapExclusionZone(placements[index].Position, placements[index].Footprint));
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
