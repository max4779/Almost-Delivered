using System;
using System.Collections.Generic;
using AlmostDelivered.MapGeneration;
using UnityEngine;

namespace AlmostDelivered.Gameplay.Payload
{
    public enum SideMissionKind { BonusCargo, Commander }

    [Serializable]
    public struct SideMissionPlacement
    {
        public SideMissionKind kind;
        public Vector3 position;
        public Vector3 routeAnchor;

        public SideMissionPlacement(SideMissionKind kind, Vector3 position, Vector3 routeAnchor)
        {
            this.kind = kind;
            this.position = position;
            this.routeAnchor = routeAnchor;
        }
    }

    public static class SideMissionPlanner
    {
        // Add new objective types to this catalog; the seeded selection still fills two slots.
        private static readonly SideMissionKind[] Catalog =
            { SideMissionKind.BonusCargo, SideMissionKind.Commander };

        public static SideMissionPlacement[] Generate(int seed, Rect bounds, PayloadRoutePlan route, int count = 2)
        {
            if (route == null || route.TotalLength <= 0f || count <= 0)
                return Array.Empty<SideMissionPlacement>();

            var random = new System.Random(unchecked(seed * 7919 + 11939));
            var kinds = (SideMissionKind[])Catalog.Clone();
            for (var index = kinds.Length - 1; index > 0; index--)
            {
                var swap = random.Next(index + 1);
                var temporary = kinds[index];
                kinds[index] = kinds[swap];
                kinds[swap] = temporary;
            }

            var placements = new SideMissionPlacement[count];
            var firstSide = random.Next(2) == 0 ? -1f : 1f;
            for (var index = 0; index < count; index++)
            {
                var anchor = route.EvaluateDistance(route.TotalLength * (index + 1f) / (count + 1f));
                var side = index % 2 == 0 ? firstSide : -firstSide;
                var offset = 27f + (float)random.NextDouble() * 5f;
                var position = new Vector3(
                    Mathf.Clamp(anchor.x + side * offset, bounds.xMin + 8f, bounds.xMax - 8f),
                    0f,
                    Mathf.Clamp(anchor.z, bounds.yMin + 8f, bounds.yMax - 8f));
                placements[index] = new SideMissionPlacement(kinds[index % kinds.Length], position, anchor);
            }
            return placements;
        }

        public static MapExclusionZone[] BuildAccessExclusions(IReadOnlyList<SideMissionPlacement> placements)
        {
            var exclusions = new List<MapExclusionZone>();
            if (placements == null) return exclusions.ToArray();
            foreach (var placement in placements)
            {
                exclusions.Add(new MapExclusionZone(new Vector2(placement.position.x, placement.position.z),
                    new Vector2(12f, 12f)));
                var center = (placement.position + placement.routeAnchor) * .5f;
                exclusions.Add(new MapExclusionZone(new Vector2(center.x, center.z),
                    new Vector2(Mathf.Abs(placement.position.x - placement.routeAnchor.x) + 5f,
                        Mathf.Abs(placement.position.z - placement.routeAnchor.z) + 5f)));
            }
            return exclusions.ToArray();
        }
    }
}
