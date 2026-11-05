using UnityEngine;

namespace AlmostDelivered.MapGeneration
{
    public readonly struct MapExclusionZone
    {
        public MapExclusionZone(Vector2 center, Vector2 size)
        {
            Center = center;
            Size = size;
        }

        public Vector2 Center { get; }
        public Vector2 Size { get; }
    }
}
