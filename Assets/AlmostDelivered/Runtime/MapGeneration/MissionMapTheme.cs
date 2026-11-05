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

}
