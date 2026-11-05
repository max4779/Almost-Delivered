using UnityEngine;

namespace AlmostDelivered.MapGeneration
{
    public readonly struct MapPlacement
    {
        public MapPlacement(int pieceIndex, Vector2 position, int quarterTurns, Vector2 footprint)
        {
            PieceIndex = pieceIndex;
            Position = position;
            QuarterTurns = quarterTurns;
            Footprint = footprint;
        }

        public int PieceIndex { get; }
        public Vector2 Position { get; }
        public int QuarterTurns { get; }
        public Vector2 Footprint { get; }
    }
}
