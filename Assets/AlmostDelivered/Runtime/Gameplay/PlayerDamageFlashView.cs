using AlmostDelivered.Combat;
using UnityEngine;
using UnityEngine.UI;

namespace AlmostDelivered.Gameplay
{
    [RequireComponent(typeof(Image))]
    public sealed class PlayerDamageFlashView : MonoBehaviour
    {
        [SerializeField] private ActorHealth playerHealth;
        [SerializeField] private Image overlay;
        [SerializeField, Range(0f, 1f)] private float maximumAlpha = .09f;
        [SerializeField, Min(.05f)] private float fadeDuration = .5f;

        private float previousHealth;
        private float flashRemaining;
        private bool subscribed;

        public float CurrentAlpha => overlay == null ? 0f : overlay.color.a;
        public Image Overlay => overlay;

        private void Awake()
        {
            if (overlay == null)
            {
                overlay = GetComponent<Image>();
            }

            ConfigureOverlay();
        }

        private void OnEnable()
        {
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        public void Configure(ActorHealth health, Image image)
        {
            Unsubscribe();
            playerHealth = health;
            overlay = image != null ? image : GetComponent<Image>();
            previousHealth = playerHealth == null ? 0f : playerHealth.CurrentHealth;
            flashRemaining = 0f;
            ConfigureOverlay();
            Subscribe();
        }

        public void Tick(float deltaTime)
        {
            if (overlay == null || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime < 0f)
            {
                return;
            }

            flashRemaining = Mathf.Max(0f, flashRemaining - deltaTime);
            SetAlpha(fadeDuration <= Mathf.Epsilon ? 0f : maximumAlpha * flashRemaining / fadeDuration);
        }

        private void HandleHealthChanged(float current, float maximum)
        {
            if (current + .0001f < previousHealth)
            {
                flashRemaining = fadeDuration;
                SetAlpha(maximumAlpha);
            }

            previousHealth = current;
        }

        private void ConfigureOverlay()
        {
            if (overlay == null)
            {
                return;
            }

            overlay.raycastTarget = false;
            overlay.color = new Color(1f, .28f, .28f, 0f);
        }

        private void SetAlpha(float alpha)
        {
            var color = overlay.color;
            color.a = Mathf.Clamp01(alpha);
            overlay.color = color;
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
    }
}
