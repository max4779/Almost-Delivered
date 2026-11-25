using UnityEngine;

namespace AlmostDelivered.Gameplay.Support
{
    [CreateAssetMenu(menuName = "Almost Delivered/Support Ability Catalog")]
    public sealed class SupportAbilityCatalog : ScriptableObject
    {
        [SerializeField] private SupportAbilityDefinition[] abilities =
            SupportAbilityDefinition.CreateDefaults();

        public SupportAbilityDefinition[] Abilities => abilities;
    }
}
