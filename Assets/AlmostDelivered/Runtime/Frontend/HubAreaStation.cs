using System.Collections.Generic;
using UnityEngine;

namespace AlmostDelivered.Frontend
{
    public enum HubAreaType { Traits, MissionTerminal }

    [RequireComponent(typeof(Collider))]
    public sealed class HubAreaStation : MonoBehaviour
    {
        [SerializeField] private HubAreaType areaType;
        [SerializeField] private TextMesh prompt;
        [SerializeField] private HubTraitMenu traitMenu;
        [SerializeField] private GameObject informationPanel;
        private readonly HashSet<HubPlayerController> occupants = new HashSet<HubPlayerController>();
        private HubPlayerController activePlayer;

        public HubAreaType AreaType => areaType;
        public bool CanInteract => LocalOccupant != null;

        private HubPlayerController LocalOccupant
        {
            get
            {
                var bounds = GetComponent<Collider>().bounds;
                foreach (var player in occupants)
                {
                    if (player == null || player.GetComponent<PlayerTraitProfile>()?.IsLocalOwner != true)
                        continue;
                    var position = player.transform.position;
                    if (position.x >= bounds.min.x && position.x <= bounds.max.x &&
                        position.z >= bounds.min.z && position.z <= bounds.max.z)
                        return player;
                }
                return null;
            }
        }
        public bool IsMenuOpen => areaType == HubAreaType.Traits
            ? traitMenu != null && traitMenu.IsOpen
            : informationPanel != null && informationPanel.activeSelf;

        public void Configure(HubAreaType type, TextMesh promptText)
        {
            areaType = type;
            prompt = promptText;
            GetComponent<Collider>().isTrigger = true;
            if (prompt != null) prompt.gameObject.SetActive(false);
        }

        public void BindTraitMenu(HubTraitMenu menu) => traitMenu = menu;

        public void BindInformationPanel(GameObject panel)
        {
            informationPanel = panel;
            if (informationPanel != null) informationPanel.SetActive(false);
        }

        private void Update()
        {
            if (prompt != null && prompt.gameObject.activeSelf != CanInteract)
                prompt.gameObject.SetActive(CanInteract);
            if (IsMenuOpen && Input.GetKeyDown(KeyCode.Escape)) Close();
            else if (CanInteract && LocalOccupant != null && !LocalOccupant.IsInputLocked &&
                     Input.GetKeyDown(KeyCode.E)) Open();
        }

        public bool Open()
        {
            var player = LocalOccupant;
            if (player == null || player.IsInputLocked) return false;
            if (areaType == HubAreaType.Traits)
            {
                if (traitMenu == null) return false;
                traitMenu.Open();
                return traitMenu.IsOpen;
            }
            if (informationPanel == null) return false;
            informationPanel.SetActive(true);
            activePlayer = player;
            activePlayer.SetInputLocked(true);
            return true;
        }

        public void Close()
        {
            if (areaType == HubAreaType.Traits)
            {
                if (traitMenu != null && traitMenu.IsOpen) traitMenu.Close();
                return;
            }
            if (informationPanel == null || !informationPanel.activeSelf) return;
            informationPanel.SetActive(false);
            if (activePlayer != null) activePlayer.SetInputLocked(false);
            activePlayer = null;
        }

        private void OnTriggerEnter(Collider other)
        {
            var player = other.GetComponentInParent<HubPlayerController>();
            if (player != null) occupants.Add(player);
        }

        private void OnTriggerExit(Collider other)
        {
            var player = other.GetComponentInParent<HubPlayerController>();
            if (player == null) return;
            occupants.Remove(player);
            if (player == activePlayer || (areaType == HubAreaType.Traits &&
                player.GetComponent<PlayerTraitProfile>()?.IsLocalOwner == true)) Close();
            if (prompt != null) prompt.gameObject.SetActive(false);
        }

        private void OnDisable() => Close();
    }
}
