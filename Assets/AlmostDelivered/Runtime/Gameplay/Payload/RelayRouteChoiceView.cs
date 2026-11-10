using AlmostDelivered.Gameplay;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AlmostDelivered.Gameplay.Payload
{
    /// <summary>In-mission relay choice shown after a supply defense is cleared.</summary>
    public sealed class RelayRouteChoiceView : MonoBehaviour
    {
        private PayloadMissionController mission;
        private GameObject overlay;
        private Font font;

        public void Configure(PayloadMissionController controller)
        {
            mission = controller;
            var hud = FindObjectOfType<MissionHud>();
            var canvas = hud != null ? hud.GetComponent<Canvas>() : null;
            if (canvas == null) return;
            font = hud.GetComponentInChildren<Text>(true)?.font ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (EventSystem.current == null)
                new GameObject("Mission Event System", typeof(EventSystem), typeof(StandaloneInputModule));

            overlay = Panel("Relay Pattern Choice", canvas.transform, new Color(.01f, .025f, .04f, .9f));
            var full = overlay.GetComponent<RectTransform>();
            full.anchorMin = Vector2.zero;
            full.anchorMax = Vector2.one;
            full.offsetMin = Vector2.zero;
            full.offsetMax = Vector2.zero;

            var rows = Mathf.CeilToInt(mission.RouteOptions.Count / 3f);
            var compact = rows == 1;
            var panel = Panel("Relay Pattern Panel", overlay.transform, new Color(.045f, .09f, .12f, 1f));
            Place(panel.GetComponent<RectTransform>(), Vector2.zero,
                new Vector2(1500f, compact ? 550f : 700f));
            Label("Title", panel.transform, "중계 비콘 · 다음 구간 운송 방식", 43,
                new Vector2(0f, compact ? 203f : 280f), new Vector2(1400f, 70f));
            Label("Hint", panel.transform, "이득과 위협을 함께 선택합니다 · 화물은 선택할 때까지 대기합니다", 26,
                new Vector2(0f, compact ? 145f : 220f), new Vector2(1400f, 50f));
            var viewport = new GameObject("Choices Viewport", typeof(RectTransform), typeof(RectMask2D), typeof(ScrollRect));
            viewport.transform.SetParent(panel.transform, false);
            var viewportRect = viewport.GetComponent<RectTransform>();
            Place(viewportRect, new Vector2(0f, compact ? -30f : -55f),
                new Vector2(1450f, compact ? 310f : 500f));
            var cards = new GameObject("Route Options", typeof(RectTransform), typeof(GridLayoutGroup));
            cards.transform.SetParent(viewport.transform, false);
            var cardsRect = cards.GetComponent<RectTransform>();
            cardsRect.anchorMin = cardsRect.anchorMax = cardsRect.pivot = new Vector2(.5f, 1f);
            cardsRect.anchoredPosition = Vector2.zero;
            cardsRect.sizeDelta = new Vector2(1450f, rows * 315f);
            var grid = cards.GetComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(450f, 290f);
            grid.spacing = new Vector2(25f, 25f);
            grid.padding = new RectOffset(25, 0, 10, 0);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 3;
            var scroll = viewport.GetComponent<ScrollRect>();
            scroll.viewport = viewportRect;
            scroll.content = cardsRect;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 60f;
            foreach (var option in mission.RouteOptions)
                if (option != null && !string.IsNullOrEmpty(option.id)) Choice(cards.transform, option);
            overlay.SetActive(false);
        }

        private void Update()
        {
            if (overlay == null || mission == null) return;
            var visible = mission.Snapshot.IsRelayChoicePending;
            if (overlay.activeSelf != visible) overlay.SetActive(visible);
        }

        private void OnDestroy()
        {
            if (overlay != null) Destroy(overlay);
        }

        private void Choice(Transform parent, RelayRouteOptionDefinition option)
        {
            var card = Panel(option.id, parent, option.color);
            var button = card.AddComponent<Button>();
            button.targetGraphic = card.GetComponent<Image>();
            var id = option.id;
            button.onClick.AddListener(() => mission.ChooseRelayOption(id));
            Label("Part Name", card.transform, option.title, 38, new Vector2(0f, 86f), new Vector2(420f, 65f));
            Label("Benefit", card.transform, option.benefit, 27, new Vector2(0f, 3f), new Vector2(420f, 70f));
            Label("Drawback", card.transform, option.drawback, 25, new Vector2(0f, -79f), new Vector2(420f, 75f));
        }

        private static GameObject Panel(string name, Transform parent, Color color)
        {
            var result = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            result.transform.SetParent(parent, false);
            result.GetComponent<Image>().color = color;
            return result;
        }

        private void Label(string name, Transform parent, string value, int size, Vector2 position, Vector2 dimensions)
        {
            var result = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            result.transform.SetParent(parent, false);
            Place(result.GetComponent<RectTransform>(), position, dimensions);
            var label = result.GetComponent<Text>();
            label.text = value;
            label.font = font;
            label.fontSize = size;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
        }

        private static void Place(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }
    }
}
