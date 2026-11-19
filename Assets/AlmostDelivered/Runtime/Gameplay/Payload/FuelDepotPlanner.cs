using System;
using AlmostDelivered.MapGeneration;
using UnityEngine;

namespace AlmostDelivered.Gameplay.Payload
{
    [Serializable]
    public struct FuelDepotPlacement
    {
        public Vector3 position;
        public Vector3 routeAnchor;

        public FuelDepotPlacement(Vector3 position, Vector3 routeAnchor)
        {
            this.position = position;
            this.routeAnchor = routeAnchor;
        }
    }

    public static class FuelDepotPlanner
    {
        public static FuelDepotPlacement[] Generate(int seed, Rect bounds, PayloadRoutePlan route, int count)
        {
            if (route == null || route.TotalLength <= 0f || count <= 0)
                return Array.Empty<FuelDepotPlacement>();
            var random = new System.Random(unchecked(seed * 1039 + 7727));
            var result = new FuelDepotPlacement[count];
            for (var index = 0; index < count; index++)
            {
                var anchor = route.EvaluateDistance(route.TotalLength * (index + 1f) / (count + 1f));
                var side = index % 2 == 0 ? -1f : 1f;
                var distance = 14f + (float)random.NextDouble() * 7f;
                var position = new Vector3(
                    Mathf.Clamp(anchor.x + side * distance, bounds.xMin + 6f, bounds.xMax - 6f),
                    0f, Mathf.Clamp(anchor.z, bounds.yMin + 6f, bounds.yMax - 6f));
                result[index] = new FuelDepotPlacement(position, anchor);
            }
            return result;
        }

        public static MapExclusionZone[] BuildAccessExclusions(FuelDepotPlacement[] depots)
        {
            if (depots == null) return Array.Empty<MapExclusionZone>();
            var result = new MapExclusionZone[depots.Length];
            for (var index = 0; index < depots.Length; index++)
            {
                var depot = depots[index];
                result[index] = new MapExclusionZone(
                    new Vector2((depot.position.x + depot.routeAnchor.x) * .5f,
                        (depot.position.z + depot.routeAnchor.z) * .5f),
                    new Vector2(Mathf.Abs(depot.position.x - depot.routeAnchor.x) + 7f,
                        Mathf.Abs(depot.position.z - depot.routeAnchor.z) + 7f));
            }
            return result;
        }
    }
}
