using AlmostDelivered.Combat;
using AlmostDelivered.Frontend;
using UnityEngine;
using UnityEngine.UI;

namespace AlmostDelivered.Editor.Frontend
{
    internal static class AmmoHudBuilder
    {
        public static void Build(Transform canvas, RangedAttackController attack, Font font)
        {
            var root = Image("Player Ammo HUD", canvas, new Color(.015f, .035f, .05f, .9f));
            Place(root.rectTransform, new Vector2(0f, 0f), new Vector2(24f, 24f), new Vector2(260f, 184f));
            var revolver = new GameObject("Revolver Ammo", typeof(RectTransform));
            revolver.transform.SetParent(root.transform, false);
            Stretch(revolver.GetComponent<RectTransform>());
            var cylinderObject = new GameObject("Revolver Cylinder", typeof(RectTransform), typeof(CanvasRenderer), typeof(AmmoCylinderGraphic));
            cylinderObject.transform.SetParent(revolver.transform, false);
            Place(cylinderObject.GetComponent<RectTransform>(), new Vector2(.5f, .5f), new Vector2(-62f, 4f), new Vector2(130f, 130f));
            var revolverTitle = Label("Revolver Label", revolver.transform, font, "리볼버", 24);
            Place(revolverTitle.rectTransform, new Vector2(.5f, 1f), new Vector2(0f, -8f), new Vector2(210f, 32f));

            var rifle = new GameObject("Automatic Rifle Ammo", typeof(RectTransform));
            rifle.transform.SetParent(root.transform, false);
            Stretch(rifle.GetComponent<RectTransform>());
            var rifleTitle = Label("Rifle Label", rifle.transform, font, "자동소총", 24);
            Place(rifleTitle.rectTransform, new Vector2(.5f, 1f), new Vector2(0f, -8f), new Vector2(210f, 32f));
            // Compact rifle silhouette beside the numeric magazine counter.
            var stock = Image("Rifle Stock", rifle.transform, new Color(.48f, .64f, .7f));
            Place(stock.rectTransform, new Vector2(0f, .5f), new Vector2(31f, 8f), new Vector2(45f, 18f));
            var receiver = Image("Rifle Receiver", rifle.transform, new Color(.75f, .83f, .86f));
            Place(receiver.rectTransform, new Vector2(0f, .5f), new Vector2(80f, 12f), new Vector2(62f, 24f));
            var barrel = Image("Rifle Barrel", rifle.transform, new Color(.48f, .64f, .7f));
            Place(barrel.rectTransform, new Vector2(0f, .5f), new Vector2(146f, 15f), new Vector2(66f, 9f));
            var magazine = Image("Rifle Magazine", rifle.transform, new Color(.32f, .47f, .53f));
            Place(magazine.rectTransform, new Vector2(0f, .5f), new Vector2(105f, -15f), new Vector2(21f, 36f));
            var count = Label("Rifle Ammo Count", rifle.transform, font, "30/30", 38);
            Place(count.rectTransform, new Vector2(.5f, 0f), new Vector2(0f, 13f), new Vector2(230f, 48f));

            var status = Label("Reload Status", root.transform, font, "R  장전", 21);
            Place(status.rectTransform, new Vector2(1f, 0f), new Vector2(-60f, 6f), new Vector2(115f, 30f));
            root.gameObject.AddComponent<PlayerAmmoHud>().Configure(attack, revolver, rifle,
                cylinderObject.GetComponent<AmmoCylinderGraphic>(), count, status);
        }

        private static Image Image(string name, Transform parent, Color color)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            obj.transform.SetParent(parent, false);
            var image = obj.GetComponent<Image>();
            image.color = color;
            return image;
        }

        private static Text Label(string name, Transform parent, Font font, string value, int size)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            obj.transform.SetParent(parent, false);
            var label = obj.GetComponent<Text>();
            label.font = font;
            label.fontSize = size;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            label.text = value;
            return label;
        }

        private static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
