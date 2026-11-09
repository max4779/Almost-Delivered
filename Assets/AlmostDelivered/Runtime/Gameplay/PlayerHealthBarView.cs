using AlmostDelivered.Combat;
using UnityEngine;
using UnityEngine.UI;

namespace AlmostDelivered.Gameplay
{
    public sealed class PlayerHealthBarView : MonoBehaviour
    {
        [SerializeField] private ActorHealth playerHealth;
        [SerializeField] private Image fill;
        [SerializeField] private Text label;
        [SerializeField] private RectTransform pulseRoot;
        [SerializeField, Min(.05f)] private float pulseDuration = .2f;
        [SerializeField, Range(0f, .2f)] private float pulseScale = .07f;

        private float previousHealth;
        private float pulseRemaining;
        private bool subscribed;

        public float NormalizedHealth { get; private set; }
        public Image Fill => fill;
        public Text Label => label;

        private void OnEnable()
        {
            Subscribe();
            Refresh();
        }

        private void OnDisable()
        {
            Unsubscribe();
            ResetScale();
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        public void Configure(ActorHealth health, Image fillImage, Text valueLabel, RectTransform scaleRoot)
        {
            Unsubscribe();
            playerHealth = health;
            fill = fillImage;
            label = valueLabel;
            pulseRoot = scaleRoot != null ? scaleRoot : transform as RectTransform;
            previousHealth = playerHealth == null ? 0f : playerHealth.CurrentHealth;
            pulseRemaining = 0f;
            ConfigureFill();
            Subscribe();
            Refresh();
        }

        public void Tick(float deltaTime)
        {
            if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime < 0f)
            {
                return;
            }

            pulseRemaining = Mathf.Max(0f, pulseRemaining - deltaTime);
            if (pulseRoot != null)
            {
                var amount = pulseDuration <= Mathf.Epsilon ? 0f : pulseRemaining / pulseDuration;
                pulseRoot.localScale = Vector3.one * (1f + pulseScale * amount);
            }
        }

        public void Refresh()
        {
            if (playerHealth == null)
            {
                NormalizedHealth = 0f;
                return;
            }

            NormalizedHealth = playerHealth.MaxHealth <= Mathf.Epsilon
                ? 0f
                : Mathf.Clamp01(playerHealth.CurrentHealth / playerHealth.MaxHealth);
            if (fill != null)
            {
                ConfigureFill();
                var scale = fill.rectTransform.localScale;
                scale.x = NormalizedHealth;
                fill.rectTransform.localScale = scale;
                fill.color = HealthColor(NormalizedHealth);
            }

            if (label != null)
            {
                label.text = $"HP  {Mathf.CeilToInt(playerHealth.CurrentHealth)} / {Mathf.CeilToInt(playerHealth.MaxHealth)}";
            }
        }

        private void HandleHealthChanged(float current, float maximum)
        {
            if (current + .0001f < previousHealth)
            {
                pulseRemaining = pulseDuration;
            }

            previousHealth = current;
            Refresh();
        }

        private void ConfigureFill()
        {
            if (fill == null)
            {
                return;
            }

            fill.type = Image.Type.Simple;
            var rect = fill.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0f, .5f);
            rect.offsetMin = new Vector2(8f, 8f);
            rect.offsetMax = new Vector2(-8f, -8f);
            fill.raycastTarget = false;
        }

        private static Color HealthColor(float normalized)
        {
            return new Color(.92f, .16f, .16f, 1f);
        }

        private void Subscribe()
        {
            if (subscribed || playerHealth == null || !isActiveAndEnabled)
            {
                return;
            }

            playerHealth.HealthChanged += HandleHealthChanged;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (subscribed && playerHealth != null)
            {
                playerHealth.HealthChanged -= HandleHealthChanged;
            }

            subscribed = false;
        }

        private void ResetScale()
        {
            if (pulseRoot != null)
            {
                pulseRoot.localScale = Vector3.one;
            }
        }
    }
}
