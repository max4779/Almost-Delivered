using System;
using AlmostDelivered.Combat;
using UnityEngine;
using UnityEngine.UI;

namespace AlmostDelivered.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class WorldHealthBar : MonoBehaviour
    {
        private const float WorldScale = .01f;
        private static readonly Vector2 CanvasSize = new Vector2(160f, 16f);

        [SerializeField] private ActorHealth health;
        [SerializeField] private Image fillImage;

        public float FillAmount => fillImage != null ? fillImage.fillAmount : 0f;

        public static WorldHealthBar Attach(GameObject actor, Vector3 localOffset)
        {
            if (actor == null)
            {
                throw new ArgumentNullException(nameof(actor));
            }

            var actorHealth = actor.GetComponent<ActorHealth>();
            if (actorHealth == null)
            {
                throw new InvalidOperationException("A world health bar requires ActorHealth on the actor root.");
            }

            var existing = actor.GetComponentInChildren<WorldHealthBar>(true);
            if (existing != null)
            {
                existing.transform.localPosition = localOffset;
                existing.Bind(actorHealth);
                return existing;
            }

            var root = new GameObject("World Health Bar", typeof(RectTransform), typeof(Canvas), typeof(WorldHealthBar));
            var rect = (RectTransform)root.transform;
            rect.SetParent(actor.transform, false);
            rect.localPosition = localOffset;
            rect.localRotation = Quaternion.identity;
            rect.localScale = Vector3.one * WorldScale;
            rect.sizeDelta = CanvasSize;

            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 50;

            CreateImage("Background", rect, new Color(.06f, .07f, .08f, .92f));
            var fill = CreateImage("Fill", rect, new Color(.18f, .92f, .4f, 1f));
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 1f;
            var fillRect = fill.rectTransform;
            fillRect.pivot = new Vector2(0f, .5f);
            fillRect.offsetMin = new Vector2(2f, 2f);
            fillRect.offsetMax = new Vector2(-2f, -2f);

            var bar = root.GetComponent<WorldHealthBar>();
            bar.fillImage = fill;
            bar.Bind(actorHealth);
            return bar;
        }

        private void LateUpdate()
        {
            var activeCamera = Camera.main;
            if (activeCamera == null)
            {
                return;
            }

            var forward = transform.position - activeCamera.transform.position;
            if (forward.sqrMagnitude <= Mathf.Epsilon)
            {
                return;
            }

            transform.rotation = Quaternion.LookRotation(forward.normalized, activeCamera.transform.up);
        }

        private void OnDestroy()
        {
            Unbind();
        }

        private void Bind(ActorHealth actorHealth)
        {
            if (health != actorHealth)
            {
                Unbind();
                health = actorHealth;
                health.HealthChanged += HandleHealthChanged;
                health.Died += HandleDeath;
            }

            gameObject.SetActive(!health.IsDead);
            Refresh(health.CurrentHealth, health.MaxHealth);
        }

        private void Unbind()
        {
            if (health == null)
            {
                return;
            }

            health.HealthChanged -= HandleHealthChanged;
            health.Died -= HandleDeath;
            health = null;
        }

        private void HandleHealthChanged(float current, float maximum)
        {
            Refresh(current, maximum);
            if (current <= 0f)
            {
                gameObject.SetActive(false);
            }
        }

        private void HandleDeath()
        {
            gameObject.SetActive(false);
        }

        private void Refresh(float current, float maximum)
        {
            if (fillImage == null)
            {
                return;
            }

            var ratio = maximum > Mathf.Epsilon
                ? Mathf.Clamp01(current / maximum)
                : 0f;
            fillImage.fillAmount = ratio;
            fillImage.rectTransform.localScale = new Vector3(ratio, 1f, 1f);
        }

        private static Image CreateImage(string objectName, RectTransform parent, Color color)
        {
            var child = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rect = (RectTransform)child.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var image = child.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }
    }
}
