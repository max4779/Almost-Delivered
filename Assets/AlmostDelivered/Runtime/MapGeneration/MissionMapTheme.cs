using System;
using System.Collections.Generic;
using UnityEngine;

namespace AlmostDelivered.MapGeneration
{
    public enum MissionMapPieceCategory
    {
        TerrainDetail,
        BlockingObstacle
    }

    [Serializable]
    public sealed class MissionMapPiece
    {
        [SerializeField] private GameObject prefab;
        [SerializeField] private Vector2 footprint = Vector2.one;

        public MissionMapPiece(GameObject prefab, Vector2 footprint)
        {
            this.prefab = prefab;
            this.footprint = footprint;
        }

        public GameObject Prefab => prefab;
        public Vector2 Footprint => footprint;
    }

    [CreateAssetMenu(menuName = "Almost Delivered/Map Theme", fileName = "MissionMapTheme")]
    public sealed class MissionMapTheme : ScriptableObject
    {
        [SerializeField] private MissionMapPiece[] terrainDetails = Array.Empty<MissionMapPiece>();
        [SerializeField] private MissionMapPiece[] blockingObstacles = Array.Empty<MissionMapPiece>();

        public IReadOnlyList<MissionMapPiece> TerrainDetails => terrainDetails;
        public IReadOnlyList<MissionMapPiece> BlockingObstacles => blockingObstacles;

        public void Configure(MissionMapPiece[] terrain, MissionMapPiece[] obstacles)
        {
            terrainDetails = terrain ?? Array.Empty<MissionMapPiece>();
            blockingObstacles = obstacles ?? Array.Empty<MissionMapPiece>();
        }
    }

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
