using System;
using AlmostDelivered.MapGeneration;
using UnityEngine;

namespace AlmostDelivered.Gameplay.Payload
{
    public static class PayloadRoutePlanner
    {
        private const int CheckpointCount = 3;

        public static PayloadRoutePlan Generate(int seed, Rect bounds, int interiorPointCount, float corridorWidth)
        {
            if (!IsFinitePositive(bounds.width) ||
                !IsFinitePositive(bounds.height) ||
                !IsFinite(bounds.xMin) ||
                !IsFinite(bounds.yMin))
            {
                return CreateStationaryPlan();
            }

            var interiorCount = Mathf.Max(0, interiorPointCount);
            var pointCount = interiorCount + 2;
            var points = new Vector3[pointCount];
            var centerX = bounds.center.x;
            var endMargin = Mathf.Min(4f, bounds.height * .08f);
            var startZ = bounds.yMin + endMargin;
            var endZ = bounds.yMax - endMargin;
            var longitudinalSpan = endZ - startZ;

            if (!IsFinitePositive(longitudinalSpan))
            {
                return CreateStationaryPlan();
            }

            points[0] = new Vector3(centerX, 0f, startZ);
            points[pointCount - 1] = new Vector3(centerX, 0f, endZ);

            var random = new System.Random(seed);
            var step = longitudinalSpan / (interiorCount + 1);
            var maximumAmplitude = Mathf.Min(bounds.width * .18f, longitudinalSpan * .1f);
            var availableHalfWidth = Mathf.Max(0f, bounds.width * .5f - Mathf.Max(0f, corridorWidth) * .5f);
            maximumAmplitude = Mathf.Min(maximumAmplitude, availableHalfWidth);
            var firstSide = random.Next(0, 2) == 0 ? -1f : 1f;

            for (var index = 1; index <= interiorCount; index++)
            {
                var amplitudeScale = Mathf.Lerp(.85f, 1f, NextFloat(random));
                var side = index % 2 == 1 ? firstSide : -firstSide;
                var x = centerX + side * maximumAmplitude * amplitudeScale;
                var zJitter = (NextFloat(random) * 2f - 1f) * step * .08f;
                var z = startZ + step * index + zJitter;
                points[index] = new Vector3(x, 0f, z);
            }

            var cumulativeDistances = BuildCumulativeDistances(points);
            var totalLength = cumulativeDistances[cumulativeDistances.Length - 1];
            var checkpointDistances = new float[CheckpointCount];
            for (var index = 0; index < CheckpointCount; index++)
            {
                checkpointDistances[index] = totalLength * ((index + 1f) / (CheckpointCount + 1f));
            }

            var exclusions = BuildCorridorExclusions(points, corridorWidth);
            return new PayloadRoutePlan(points, cumulativeDistances, checkpointDistances, exclusions);
        }

        private static float[] BuildCumulativeDistances(Vector3[] points)
        {
            var distances = new float[points.Length];
            for (var index = 1; index < points.Length; index++)
            {
                distances[index] = distances[index - 1] + Vector3.Distance(points[index - 1], points[index]);
            }

            return distances;
        }

        private static MapExclusionZone[] BuildCorridorExclusions(Vector3[] points, float corridorWidth)
        {
            if (points.Length < 2)
            {
                return Array.Empty<MapExclusionZone>();
            }

            var padding = IsFinite(corridorWidth) ? Mathf.Max(0f, corridorWidth) : 0f;
            var exclusions = new MapExclusionZone[points.Length - 1];
            for (var index = 0; index < exclusions.Length; index++)
            {
                var start = points[index];
                var end = points[index + 1];
                var minX = Mathf.Min(start.x, end.x);
                var maxX = Mathf.Max(start.x, end.x);
                var minZ = Mathf.Min(start.z, end.z);
                var maxZ = Mathf.Max(start.z, end.z);
                var center = new Vector2((minX + maxX) * .5f, (minZ + maxZ) * .5f);
                var size = new Vector2(maxX - minX + padding, maxZ - minZ + padding);
                exclusions[index] = new MapExclusionZone(center, size);
            }

            return exclusions;
        }

        private static PayloadRoutePlan CreateStationaryPlan()
        {
            return new PayloadRoutePlan(
                new[] { Vector3.zero },
                new[] { 0f },
                new[] { 0f, 0f, 0f },
                Array.Empty<MapExclusionZone>());
        }

        private static float NextFloat(System.Random random)
        {
            return (float)random.NextDouble();
        }

        private static bool IsFinitePositive(float value)
        {
            return IsFinite(value) && value > 0f;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
