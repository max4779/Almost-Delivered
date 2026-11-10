using AlmostDelivered.Combat;
using UnityEngine;
using UnityEngine.UI;

namespace AlmostDelivered.Frontend
{
    public sealed class PlayerAmmoHud : MonoBehaviour
    {
        [SerializeField] private RangedAttackController attack;
        [SerializeField] private GameObject revolverPanel;
        [SerializeField] private GameObject riflePanel;
        [SerializeField] private AmmoCylinderGraphic cylinder;
        [SerializeField] private Text rifleCount;
        [SerializeField] private Text status;
        private bool layoutPrepared;

        public void Configure(RangedAttackController source, GameObject revolver, GameObject rifle,
            AmmoCylinderGraphic cylinderGraphic, Text count, Text statusText)
        {
            attack = source;
            revolverPanel = revolver;
            riflePanel = rifle;
            cylinder = cylinderGraphic;
            rifleCount = count;
            status = statusText;
            Refresh();
        }

        private void Update() => Refresh();

        public void Refresh()
        {
            if (!layoutPrepared && riflePanel != null)
            {
                EnsureRifleIcon(riflePanel.transform);
                if (rifleCount != null)
                {
                    var countRect = rifleCount.rectTransform;
                    countRect.anchorMin = countRect.anchorMax = countRect.pivot = new Vector2(1f, 0f);
                    countRect.anchoredPosition = new Vector2(-16f, 8f);
                    countRect.sizeDelta = new Vector2(130f, 52f);
                    rifleCount.alignment = TextAnchor.MiddleRight;
                    rifleCount.horizontalOverflow = HorizontalWrapMode.Overflow;
                    rifleCount.verticalOverflow = VerticalWrapMode.Overflow;
                    countRect.SetAsLastSibling();
                }
                layoutPrepared = true;
            }
            var weapon = PlayerWeaponLoadout.Selected;
            var isRevolver = weapon == PlayerWeaponType.Revolver;
            var isRifle = PlayerWeaponLoadout.IsFirearm(weapon) && !isRevolver;
            var background = GetComponent<Image>();
            if (background != null) background.enabled = isRevolver || isRifle;
            if (revolverPanel != null) revolverPanel.SetActive(isRevolver);
            if (riflePanel != null) riflePanel.SetActive(isRifle);
            if (status != null) status.gameObject.SetActive(isRevolver || isRifle);
            if (attack == null) return;
            if (cylinder != null) cylinder.Rounds = isRevolver ? attack.RoundsRemaining : 0;
            if (rifleCount != null) rifleCount.text = attack.RoundsRemaining + "/" + attack.MagazineSize;
            if (status != null)
            {
                var rect = status.rectTransform;
                var anchor = isRifle ? new Vector2(0f, 0f) : new Vector2(1f, 0f);
                if (rect.anchorMin != anchor)
                {
                    rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
                    rect.anchoredPosition = isRifle ? new Vector2(16f, 12f) : new Vector2(-16f, 12f);
                }
                status.alignment = isRifle ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight;
                status.text = attack.IsReloading ? "장전 중..." : "R  장전";
            }
        }

        public static void EnsureRifleIcon(Transform parent)
        {
            if (parent == null) return;
            RiflePart(parent, "Stock Butt", 25f, 4f, 9f, 39f, new Color(.36f, .49f, .54f));
            RiflePart(parent, "Rifle Stock", 33f, 12f, 52f, 15f, new Color(.49f, .65f, .69f));
            RiflePart(parent, "Stock Brace", 43f, -5f, 40f, 9f, new Color(.36f, .49f, .54f), -17f);
            RiflePart(parent, "Rifle Receiver", 84f, 10f, 70f, 25f, new Color(.8f, .87f, .87f));
            RiflePart(parent, "Rifle Rail", 85f, 26f, 93f, 6f, new Color(.3f, .45f, .51f));
            RiflePart(parent, "Rifle Handguard", 154f, 10f, 56f, 18f, new Color(.52f, .68f, .7f));
            RiflePart(parent, "Rifle Barrel", 209f, 13f, 43f, 7f, new Color(.8f, .87f, .87f));
            RiflePart(parent, "Rifle Muzzle", 251f, 13f, 12f, 13f, new Color(.3f, .45f, .51f));
            RiflePart(parent, "Rifle Grip", 95f, -20f, 15f, 31f, new Color(.3f, .45f, .51f), 13f);
            RiflePart(parent, "Rifle Magazine", 111f, -22f, 21f, 40f, new Color(.36f, .49f, .54f), -10f);
            RiflePart(parent, "Rifle Sight", 121f, 36f, 29f, 10f, new Color(.94f, .71f, .35f));
        }

        private static void RiflePart(Transform parent, string name, float x, float y, float width,
            float height, Color color, float angle = 0f)
        {
            var existing = parent.Find(name);
            var item = existing == null
                ? new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image))
                : existing.gameObject;
            if (existing == null) item.transform.SetParent(parent, false);
            var graphic = item.GetComponent<Image>();
            if (graphic == null) graphic = item.AddComponent<Image>();
            graphic.color = color;
            var rect = item.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, .5f);
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(width, height);
            rect.localRotation = Quaternion.Euler(0f, 0f, angle);
        }
    }
}
