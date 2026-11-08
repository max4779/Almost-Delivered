using UnityEngine;

namespace AlmostDelivered.MapGeneration
{
    [ExecuteAlways]
    public sealed class MissionEnvironmentThemeApplier : MonoBehaviour
    {
        [SerializeField] private MissionEnvironmentTheme theme;
        [SerializeField] private Renderer groundRenderer;
        [SerializeField] private MeshRenderer roadRenderer;
        [SerializeField] private MissionMapGenerator mapGenerator;

        public MissionEnvironmentTheme Theme => theme;

        public void Configure(
            MissionEnvironmentTheme environmentTheme,
            Renderer ground,
            MeshRenderer road,
            MissionMapGenerator generator)
        {
            theme = environmentTheme;
            groundRenderer = ground;
            roadRenderer = road;
            mapGenerator = generator;
            Apply();
        }

        public bool Apply()
        {
            if (theme == null || groundRenderer == null || roadRenderer == null || mapGenerator == null ||
                theme.GroundMaterial == null || theme.RoadMaterial == null ||
                theme.RoadShoulderMaterial == null || theme.MapTheme == null)
            {
                return false;
            }

            groundRenderer.sharedMaterial = theme.GroundMaterial;
            roadRenderer.sharedMaterials = new[] { theme.RoadMaterial, theme.RoadShoulderMaterial };
            mapGenerator.SetTheme(theme.MapTheme);
            return true;
        }

        private void Awake()
        {
            Apply();
        }

        private void OnValidate()
        {
            Apply();
        }
    }
}
