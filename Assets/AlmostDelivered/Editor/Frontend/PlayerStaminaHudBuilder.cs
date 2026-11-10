using AlmostDelivered.Frontend;
using AlmostDelivered.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace AlmostDelivered.Editor.Frontend
{
    internal static class PlayerStaminaHudBuilder
    {
        public static void Build(Transform canvas, PlayerMobility mobility, Font font, bool aboveHealth)
        {
            var frame = new GameObject("Player Stamina Bar", typeof(RectTransform), typeof(CanvasRenderer),
                typeof(Image), typeof(PlayerStaminaBarView));
            frame.transform.SetParent(canvas, false);
            var rect = frame.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, 0f);
            rect.anchoredPosition = new Vector2(aboveHealth ? 64f : 0f, aboveHealth ? 96f : 38f);
            rect.sizeDelta = new Vector2(aboveHealth ? 620f : 520f, 34f);
            frame.GetComponent<Image>().color = new Color(.015f, .025f, .04f, .94f);

            var fillObject = new GameObject("Stamina Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            fillObject.transform.SetParent(frame.transform, false);
            var fillRect = fillObject.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = new Vector2(6f, 5f);
            fillRect.offsetMax = new Vector2(-6f, -5f);
            var fill = fillObject.GetComponent<Image>();
            fill.color = new Color(.3f, .82f, .54f, 1f);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;

            var labelObject = new GameObject("Stamina Value", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            labelObject.transform.SetParent(frame.transform, false);
            var labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
            var label = labelObject.GetComponent<Text>();
            label.font = font;
            label.fontSize = 20;
            label.fontStyle = FontStyle.Bold;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            frame.GetComponent<PlayerStaminaBarView>().Configure(mobility, fill, label);
        }
    }
}
