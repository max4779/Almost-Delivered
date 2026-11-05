using System.Collections.Generic;
using UnityEngine;

namespace AlmostDelivered.MapGeneration
{
    public static class ProceduralMapPlanner
    {
        public static IReadOnlyList<MapPlacement> Generate(
            int seed,
            Vector2 halfExtents,
            IReadOnlyList<MapPieceSpec> pieces,
            int requestedCount,
            IReadOnlyList<MapExclusionZone> exclusions,
            int maxAttempts)
        {
            var placements = new List<MapPlacement>(Mathf.Max(0, requestedCount));
            if (requestedCount <= 0 || maxAttempts <= 0 || !IsPositiveFinite(halfExtents) || pieces == null)
            {
                return placements;
            }

            var validPieceIndices = new List<int>(pieces.Count);
            for (var index = 0; index < pieces.Count; index++)
            {
                if (pieces[index] != null && IsPositiveFinite(pieces[index].Footprint))
                {
                    validPieceIndices.Add(index);
                }
            }

            if (validPieceIndices.Count == 0)
            {
                return placements;
            }

            var random = new System.Random(seed);
            for (var attempt = 0; attempt < maxAttempts && placements.Count < requestedCount; attempt++)
            {
                var pieceIndex = validPieceIndices[random.Next(validPieceIndices.Count)];
                var quarterTurns = random.Next(4);
                var footprint = RotatedFootprint(pieces[pieceIndex].Footprint, quarterTurns);
                var footprintHalfExtents = footprint * .5f;
                if (footprintHalfExtents.x > halfExtents.x || footprintHalfExtents.y > halfExtents.y)
                {
                    continue;
                }

                var position = new Vector2(
                    RandomRange(random, -halfExtents.x + footprintHalfExtents.x, halfExtents.x - footprintHalfExtents.x),
                    RandomRange(random, -halfExtents.y + footprintHalfExtents.y, halfExtents.y - footprintHalfExtents.y));

                if (OverlapsAnyExclusion(position, footprint, exclusions) || OverlapsAnyPlacement(position, footprint, placements))
                {
                    continue;
                }

                placements.Add(new MapPlacement(pieceIndex, position, quarterTurns, footprint));
            }

            return placements;
        }

        private static bool OverlapsAnyExclusion(Vector2 position, Vector2 footprint, IReadOnlyList<MapExclusionZone> exclusions)
        {
            if (exclusions == null)
            {
                return false;
            }

            for (var index = 0; index < exclusions.Count; index++)
            {
                var exclusion = exclusions[index];
                if (IsPositiveFinite(exclusion.Size) && Overlaps(position, footprint, exclusion.Center, exclusion.Size))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool OverlapsAnyPlacement(Vector2 position, Vector2 footprint, IReadOnlyList<MapPlacement> placements)
        {
            for (var index = 0; index < placements.Count; index++)
            {
                if (Overlaps(position, footprint, placements[index].Position, placements[index].Footprint))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool Overlaps(Vector2 firstCenter, Vector2 firstSize, Vector2 secondCenter, Vector2 secondSize)
        {
            return Mathf.Abs(firstCenter.x - secondCenter.x) < (firstSize.x + secondSize.x) * .5f &&
                   Mathf.Abs(firstCenter.y - secondCenter.y) < (firstSize.y + secondSize.y) * .5f;
        }

        private static Vector2 RotatedFootprint(Vector2 footprint, int quarterTurns)
        {
            return quarterTurns % 2 == 0 ? footprint : new Vector2(footprint.y, footprint.x);
        }

        private static float RandomRange(System.Random random, float minimum, float maximum)
        {
            return minimum + (float)random.NextDouble() * (maximum - minimum);
        }

        private static bool IsPositiveFinite(Vector2 value)
        {
            return value.x > 0f && value.y > 0f &&
                   !float.IsNaN(value.x) && !float.IsNaN(value.y) &&
                   !float.IsInfinity(value.x) && !float.IsInfinity(value.y);
        }
    }
}
