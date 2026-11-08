using UnityEngine;

namespace AlmostDelivered.MapGeneration
{
    [CreateAssetMenu(menuName = "Almost Delivered/Environment Theme", fileName = "MissionEnvironmentTheme")]
    public sealed class MissionEnvironmentTheme : ScriptableObject
    {
        [SerializeField] private string displayName = "Environment";
        [SerializeField] private Material groundMaterial;
        [SerializeField] private Material roadMaterial;
        [SerializeField] private Material roadShoulderMaterial;
        [SerializeField] private MissionMapTheme mapTheme;

        public string DisplayName => displayName;
        public Material GroundMaterial => groundMaterial;
        public Material RoadMaterial => roadMaterial;
        public Material RoadShoulderMaterial => roadShoulderMaterial;
        public MissionMapTheme MapTheme => mapTheme;

        public void Configure(
            string themeDisplayName,
            Material ground,
            Material road,
            Material roadShoulder,
            MissionMapTheme environmentMapTheme)
        {
            displayName = string.IsNullOrWhiteSpace(themeDisplayName) ? "Environment" : themeDisplayName;
            groundMaterial = ground;
            roadMaterial = road;
            roadShoulderMaterial = roadShoulder;
            mapTheme = environmentMapTheme;
        }
    }
}
