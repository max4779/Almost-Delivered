using System;
using System.Collections.Generic;
using AlmostDelivered.MapGeneration;
using UnityEngine;

namespace AlmostDelivered.Gameplay.Payload
{
    public sealed class PayloadRoutePlan
    {
        private readonly IReadOnlyList<Vector3> points;
        private readonly IReadOnlyList<float> cumulativeDistances;
        private readonly IReadOnlyList<float> checkpointDistances;
        private readonly IReadOnlyList<MapExclusionZone> corridorExclusions;

        internal PayloadRoutePlan(
            Vector3[] points,
            float[] cumulativeDistances,
            float[] checkpointDistances,
            MapExclusionZone[] corridorExclusions)
        {
            if (points == null || points.Length == 0)
            {
                throw new ArgumentException("A route requires at least one point.", nameof(points));
            }

            if (cumulativeDistances == null || cumulativeDistances.Length != points.Length)
            {
                throw new ArgumentException("Every route point requires a cumulative distance.", nameof(cumulativeDistances));
            }

            this.points = Array.AsReadOnly((Vector3[])points.Clone());
            this.cumulativeDistances = Array.AsReadOnly((float[])cumulativeDistances.Clone());
            this.checkpointDistances = Array.AsReadOnly((float[])(checkpointDistances ?? Array.Empty<float>()).Clone());
            this.corridorExclusions = Array.AsReadOnly((MapExclusionZone[])(corridorExclusions ?? Array.Empty<MapExclusionZone>()).Clone());
        }

        public IReadOnlyList<Vector3> Points => points;

        public IReadOnlyList<float> CumulativeDistances => cumulativeDistances;

        public IReadOnlyList<float> CheckpointDistances => checkpointDistances;

        public IReadOnlyList<MapExclusionZone> CorridorExclusions => corridorExclusions;

        public float TotalLength => cumulativeDistances[cumulativeDistances.Count - 1];

        public Vector3 Destination => points[points.Count - 1];

        public Vector3 EvaluateDistance(float distance)
        {
            if (points.Count == 1 || TotalLength <= Mathf.Epsilon)
            {
                return Destination;
            }

            if (float.IsNaN(distance))
            {
                return points[0];
            }

            if (distance <= 0f)
            {
                return points[0];
            }

            if (float.IsInfinity(distance) || distance >= TotalLength)
            {
                return Destination;
            }

            for (var index = 1; index < cumulativeDistances.Count; index++)
            {
                if (distance > cumulativeDistances[index])
                {
                    continue;
                }

                var segmentStart = cumulativeDistances[index - 1];
                var segmentLength = cumulativeDistances[index] - segmentStart;
                if (segmentLength <= Mathf.Epsilon)
                {
                    return points[index];
                }

                var t = (distance - segmentStart) / segmentLength;
                return Vector3.Lerp(points[index - 1], points[index], t);
            }

            return Destination;
        }
    }
}
