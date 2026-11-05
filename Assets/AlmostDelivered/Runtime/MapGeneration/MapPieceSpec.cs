using System;
using UnityEngine;

namespace AlmostDelivered.MapGeneration
{
    [Serializable]
    public sealed class MapPieceSpec
    {
        [SerializeField] private Vector2 footprint;

        public MapPieceSpec(Vector2 footprint)
        {
            this.footprint = footprint;
        }

        public Vector2 Footprint => footprint;
    }
}
