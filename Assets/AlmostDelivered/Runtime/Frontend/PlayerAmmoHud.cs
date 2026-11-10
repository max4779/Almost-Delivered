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
                status.text = attack.IsReloading ? "장전 중..." : "R  장전";
        }
    }
}
