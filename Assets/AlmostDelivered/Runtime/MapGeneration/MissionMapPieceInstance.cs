using UnityEngine;

namespace AlmostDelivered.MapGeneration
{
    public sealed class MissionMapPieceInstance : MonoBehaviour
    {
        [SerializeField] private MissionMapPieceCategory category;
        [SerializeField] private int pieceIndex;
        [SerializeField] private Vector2 position;
        [SerializeField] private int quarterTurns;
        [SerializeField] private Vector2 footprint;

        public MissionMapPieceCategory Category => category;
        public int PieceIndex => pieceIndex;
        public Vector2 Position => position;
        public int QuarterTurns => quarterTurns;
        public Vector2 Footprint => footprint;

        public void Configure(MissionMapPieceCategory valueCategory, MapPlacement placement)
        {
            category = valueCategory;
            pieceIndex = placement.PieceIndex;
            position = placement.Position;
            quarterTurns = placement.QuarterTurns;
            footprint = placement.Footprint;
        }
    }
}
