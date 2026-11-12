using UnityEngine;
using UnityEngine.UI;

namespace AlmostDelivered.Frontend
{
    public sealed class HubGuideHud : MonoBehaviour
    {
        [SerializeField] private HubWeaponStation weaponStation;
        [SerializeField] private HubDeploymentGate deploymentGate;
        [SerializeField] private HubAreaStation traitStation;
        [SerializeField] private HubAreaStation missionTerminal;
        [SerializeField] private Text interactionHint;
        [SerializeField] private Text currentWeapon;

        public void Configure(HubWeaponStation station, HubDeploymentGate gate, Text hint, Text weapon,
            HubAreaStation traits = null, HubAreaStation terminal = null)
        {
            weaponStation = station;
            deploymentGate = gate;
            interactionHint = hint;
            currentWeapon = weapon;
            traitStation = traits;
            missionTerminal = terminal;
            Refresh();
        }

        private void Update() => Refresh();

        public void Refresh()
        {
            if (currentWeapon != null)
                currentWeapon.text = "현재 무기  ·  " + PlayerWeaponLoadout.DisplayName(PlayerWeaponLoadout.Selected);
            if (interactionHint == null) return;
            if (weaponStation != null && weaponStation.IsMenuOpen)
                interactionHint.text = "무기 카드를 클릭해 선택하세요  ·  ESC 닫기";
            else if (traitStation != null && traitStation.IsMenuOpen)
                interactionHint.text = "아래에서 위로 특성을 선택하세요  ·  ESC 닫기";
            else if (missionTerminal != null && missionTerminal.IsMenuOpen)
                interactionHint.text = "작전 정보를 확인하세요  ·  ESC 닫기";
            else if (weaponStation != null && weaponStation.CanSelect)
                interactionHint.text = "E  장비 보관소 열기";
            else if (traitStation != null && traitStation.CanInteract)
                interactionHint.text = "E  승무원 구역에서 특성 관리";
            else if (missionTerminal != null && missionTerminal.CanInteract)
                interactionHint.text = "E  작전 단말기 열기";
            else if (deploymentGate != null && deploymentGate.CanLaunch)
                interactionHint.text = "E  미션 출격";
            else
                interactionHint.text = $"WASD 이동  ·  {PlayerInputBindings.DisplayName(PlayerAction.Sprint)} 달리기  ·  " +
                    $"{PlayerInputBindings.DisplayName(PlayerAction.Roll)} 구르기  ·  좌클릭 공격  ·  R 장전";
        }
    }
}
