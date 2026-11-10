using AlmostDelivered.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace AlmostDelivered.Frontend
{
    public sealed class PlayerStaminaBarView : MonoBehaviour
    {
        [SerializeField] private PlayerMobility mobility;
        [SerializeField] private Image fill;
        [SerializeField] private Text label;

        public void Configure(PlayerMobility source, Image fillImage, Text valueLabel)
        {
            mobility = source;
            fill = fillImage;
            label = valueLabel;
            Refresh();
        }

        private void LateUpdate() => Refresh();

        public void Refresh()
        {
            if (mobility == null) return;
            var ratio = mobility.MaxStamina > 0f ? mobility.Stamina / mobility.MaxStamina : 0f;
            if (fill != null) fill.fillAmount = Mathf.Clamp01(ratio);
            if (label != null)
                label.text = $"STAMINA  {Mathf.CeilToInt(mobility.Stamina)} / {Mathf.CeilToInt(mobility.MaxStamina)}";
        }
    }
}
