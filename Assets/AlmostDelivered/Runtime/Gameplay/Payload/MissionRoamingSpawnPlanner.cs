using System;
using System.Collections.Generic;
using AlmostDelivered.MapGeneration;
using UnityEngine;

namespace AlmostDelivered.Gameplay.Payload
{
    public static class MissionRoamingSpawnPlanner
    {
        public const float StartClearance = 45f;
        private const float CargoClearance = 28f;
        private const float ObjectiveClearance = 18f;
        private const float RoadClearance = 9f;
        private const float UnitSeparation = 19f;

        public static Vector3[] Generate(int seed, Rect bounds, PayloadRoutePlan route,
            Vector3 playerSpawn, IReadOnlyList<Vector3> objectives,
            IReadOnlyList<MapExclusionZone> blockers, int count)
        {
            if (route == null || count <= 0 || bounds.width <= 16f || bounds.height <= 16f)
                return Array.Empty<Vector3>();

            var scale = Mathf.Clamp01(Mathf.Min(bounds.width / 250f, bounds.height / 500f));
            var margin = 8f * scale;
            var random = new System.Random(unchecked(seed * 15485863 + 32719));
            var positions = new List<Vector3>(count);
            for (var attempt = 0; attempt < 12000 && positions.Count < count; attempt++)
            {
                var x = Mathf.Lerp(bounds.xMin + margin, bounds.xMax - margin, (float)random.NextDouble());
                var z = Mathf.Lerp(bounds.yMin + margin, bounds.yMax - margin, (float)random.NextDouble());
                var candidate = new Vector3(x, 0f, z);
                if (PlanarDistance(candidate, playerSpawn) < StartClearance * scale ||
                    PlanarDistance(candidate, route.Points[0]) < CargoClearance * scale ||
                    DistanceToRoute(candidate, route) < RoadClearance * scale ||
                    NearAny(candidate, objectives, ObjectiveClearance * scale) ||
                    NearAny(candidate, positions, UnitSeparation * scale) ||
                    InsideBlocker(candidate, blockers, scale))
                    continue;
                positions.Add(candidate);
            }
            return positions.ToArray();
        }

        private static bool NearAny(Vector3 point, IReadOnlyList<Vector3> positions, float radius)
        {
            if (positions == null) return false;
            for (var index = 0; index < positions.Count; index++)
                if (PlanarDistance(point, positions[index]) < radius) return true;
            return false;
        }

        private static bool InsideBlocker(Vector3 point, IReadOnlyList<MapExclusionZone> blockers,
            float scale)
        {
            if (blockers == null) return false;
            for (var index = 0; index < blockers.Count; index++)
            {
                var blocker = blockers[index];
                if (Mathf.Abs(point.x - blocker.Center.x) < blocker.Size.x * .5f + 2f * scale &&
                    Mathf.Abs(point.z - blocker.Center.y) < blocker.Size.y * .5f + 2f * scale)
                    return true;
            }
            return false;
        }

        private static float DistanceToRoute(Vector3 point, PayloadRoutePlan route)
        {
            var best = float.PositiveInfinity;
            for (var index = 1; index < route.Points.Count; index++)
            {
                var start = route.Points[index - 1];
                var end = route.Points[index];
                var delta = new Vector2(end.x - start.x, end.z - start.z);
                var offset = new Vector2(point.x - start.x, point.z - start.z);
                var t = delta.sqrMagnitude > .0001f
                    ? Mathf.Clamp01(Vector2.Dot(offset, delta) / delta.sqrMagnitude) : 0f;
                var nearest = new Vector2(start.x, start.z) + delta * t;
                best = Mathf.Min(best, Vector2.Distance(new Vector2(point.x, point.z), nearest));
            }
            return best;
        }

        private static float PlanarDistance(Vector3 a, Vector3 b) =>
            Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
    }
}
