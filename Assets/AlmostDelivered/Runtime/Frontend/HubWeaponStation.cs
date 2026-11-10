using UnityEngine;
using UnityEngine.UI;

namespace AlmostDelivered.Frontend
{
    [RequireComponent(typeof(Collider))]
    public sealed class HubWeaponStation : MonoBehaviour
    {
        [SerializeField] private TextMesh prompt;
        [SerializeField] private TextMesh selectionLabel;
        [SerializeField] private GameObject selectionPanel;
        [SerializeField] private Text menuSelection;
        [SerializeField] private Image rangedCard;
        [SerializeField] private Image meleeCard;
        private HubPlayerController occupant;

        public bool CanSelect
        {
            get
            {
                if (occupant == null) return false;
                var area = GetComponent<Collider>().bounds;
                var position = occupant.transform.position;
                return position.x >= area.min.x && position.x <= area.max.x &&
                       position.z >= area.min.z && position.z <= area.max.z;
            }
        }
        public bool IsMenuOpen => selectionPanel != null && selectionPanel.activeSelf;

        public void Configure(TextMesh promptText, TextMesh currentSelection)
        {
            prompt = promptText;
            selectionLabel = currentSelection;
            Refresh();
        }

        public void ConfigureMenu(GameObject panel, Text currentSelection, Image rangedImage, Image meleeImage)
        {
            selectionPanel = panel;
            menuSelection = currentSelection;
            rangedCard = rangedImage;
            meleeCard = meleeImage;
            CloseMenu();
            Refresh();
        }

        private void Awake()
        {
            GetComponent<Collider>().isTrigger = true;
            Refresh();
        }

        private void Update()
        {
            if (prompt != null && prompt.gameObject.activeSelf != CanSelect)
                prompt.gameObject.SetActive(CanSelect);
            if (IsMenuOpen && Input.GetKeyDown(KeyCode.Escape))
                CloseMenu();
            else if (CanSelect && !IsMenuOpen && Input.GetKeyDown(KeyCode.E))
                OpenMenu();
        }

        public bool OpenMenu()
        {
            if (!CanSelect || selectionPanel == null) return false;
            selectionPanel.SetActive(true);
            occupant.SetInputLocked(true);
            Refresh();
            return true;
        }

        public void CloseMenu()
        {
            if (selectionPanel != null) selectionPanel.SetActive(false);
            if (occupant != null) occupant.SetInputLocked(false);
            Refresh();
        }

        public bool Select(PlayerWeaponType weapon)
        {
            if (!CanSelect || !IsMenuOpen) return false;
            PlayerWeaponLoadout.Select(weapon);
            occupant.SetWeaponType(weapon);
            Refresh();
            return true;
        }

        public void SelectRanged() => Select(PlayerWeaponType.Ranged);
        public void SelectMelee() => Select(PlayerWeaponType.Melee);

        private void OnTriggerEnter(Collider other)
        {
            var player = other.GetComponentInParent<HubPlayerController>();
            if (player == null) return;
            occupant = player;
            occupant.SetWeaponType(PlayerWeaponLoadout.Selected);
            Refresh();
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.GetComponentInParent<HubPlayerController>() != occupant) return;
            CloseMenu();
            occupant = null;
            Refresh();
        }

        private void OnDisable() => CloseMenu();

        private void Refresh()
        {
            if (prompt != null) prompt.gameObject.SetActive(CanSelect);
            if (selectionLabel != null)
                selectionLabel.text = PlayerWeaponLoadout.Selected == PlayerWeaponType.Melee
                    ? "장착: 근접 무기" : "장착: 원거리 무기";
            if (menuSelection != null)
                menuSelection.text = PlayerWeaponLoadout.Selected == PlayerWeaponType.Melee
                    ? "현재 장착: 근접 무기" : "현재 장착: 원거리 무기";
            if (rangedCard != null)
                rangedCard.color = PlayerWeaponLoadout.Selected == PlayerWeaponType.Ranged
                    ? new Color(.08f, .42f, .48f, 1f) : new Color(.08f, .15f, .2f, 1f);
            if (meleeCard != null)
                meleeCard.color = PlayerWeaponLoadout.Selected == PlayerWeaponType.Melee
                    ? new Color(.43f, .27f, .08f, 1f) : new Color(.19f, .14f, .09f, 1f);
        }
    }
}
