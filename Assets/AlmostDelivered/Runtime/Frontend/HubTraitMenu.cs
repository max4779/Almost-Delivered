using UnityEngine;
using UnityEngine.UI;

namespace AlmostDelivered.Frontend
{
    /// <summary>Only the local hub player can edit its own account's trait allocation.</summary>
    public sealed class HubTraitMenu : MonoBehaviour
    {
        [SerializeField] private HubPlayerController player;
        [SerializeField] private PlayerTraitProfile profile;
        [SerializeField] private HubWeaponStation weaponStation;
        [SerializeField] private HubAreaStation traitStation;
        [SerializeField] private GameObject overlay;
        [SerializeField] private Text entryLabel;
        [SerializeField] private Text summary;
        [SerializeField] private Text[] traitLabels;
        [SerializeField] private Button[] investButtons;
        [SerializeField] private Image[] traitCards;
        [SerializeField] private Image[] branchLinks;
        private string lastRemoteStatus;

        public bool IsOpen => overlay != null && overlay.activeSelf;

        public void Configure(HubPlayerController localPlayer, HubWeaponStation station,
            GameObject panel, Text entry, Text progressSummary, Text[] labels, Button[] buttons,
            Image[] cards, Image[] links)
        {
            player = localPlayer;
            profile = localPlayer != null ? localPlayer.GetComponent<PlayerTraitProfile>() : null;
            weaponStation = station;
            overlay = panel;
            entryLabel = entry;
            summary = progressSummary;
            traitLabels = labels;
            investButtons = buttons;
            traitCards = cards;
            branchLinks = links;
            if (overlay != null) overlay.SetActive(false);
            Refresh();
        }

        public void BindStation(HubAreaStation station) => traitStation = station;

        private void Start()
        {
            if (profile == null && player != null) profile = player.GetComponent<PlayerTraitProfile>();
            profile?.EnsureLocalAccount();
            if (profile != null) profile.Changed += Refresh;
            Refresh();
        }

        private void OnDestroy()
        {
            if (profile != null) profile.Changed -= Refresh;
            Close();
        }

        private void Update()
        {
            if (IsOpen && Input.GetKeyDown(KeyCode.Escape)) Close();
            if (IsOpen && lastRemoteStatus != RemoteTraitSync.Status) Refresh();
        }

        public void Open()
        {
            if (profile == null || !profile.IsLocalOwner ||
                traitStation == null || !traitStation.CanInteract ||
                (weaponStation != null && weaponStation.IsMenuOpen)) return;
            overlay.SetActive(true);
            player.SetInputLocked(true);
            Refresh();
        }

        public void Close()
        {
            if (!IsOpen) return;
            overlay.SetActive(false);
            if (player != null) player.SetInputLocked(false);
        }

        public void Invest0() => Invest(0);
        public void Invest1() => Invest(1);
        public void Invest2() => Invest(2);
        public void Invest3() => Invest(3);
        public void Invest4() => Invest(4);
        public void Invest5() => Invest(5);

        public void ResetTraits()
        {
            if (profile != null && profile.ResetInHub()) Refresh();
        }

        public void Refresh()
        {
            lastRemoteStatus = RemoteTraitSync.Status;
            var progress = profile != null ? profile.Progress : null;
            var level = progress != null ? progress.level : 1;
            var available = progress != null ? progress.AvailablePoints : 0;
            if (entryLabel != null) entryLabel.text = $"특성  ·  Lv.{level}  ·  {available}P";
            if (summary != null)
                summary.text = $"레벨 {level}  ·  사용 가능 {available}P / 총 {PlayerTraitCatalog.TotalPointLimit}P\n" +
                    RemoteTraitSync.Status;
            if (traitLabels == null) return;
            for (var i = 0; i < PlayerTraitCatalog.All.Count && i < traitLabels.Length; i++)
            {
                var definition = PlayerTraitCatalog.All[i];
                var category = definition.Category == PlayerTraitCategory.Combat ? "전투" :
                    definition.Category == PlayerTraitCategory.Survival ? "생존" : "호위";
                var rank = progress != null ? progress.Rank(definition.Id) : 0;
                var unlocked = definition.RequiredTraitId == null ||
                    (progress != null && progress.Rank(definition.RequiredTraitId) >= definition.RequiredRank);
                if (traitLabels[i] != null)
                    traitLabels[i].text = $"{category}  /  {definition.Name}\n{definition.Description}\n" +
                        (unlocked ? $"{rank}/{PlayerTraitCatalog.MaximumRank} 랭크" :
                            $"잠김  ·  아래 특성 {definition.RequiredRank}단계 필요");
                if (investButtons != null && i < investButtons.Length && investButtons[i] != null)
                    investButtons[i].interactable = progress != null && progress.CanInvest(definition.Id);
                if (traitCards != null && i < traitCards.Length && traitCards[i] != null)
                    traitCards[i].color = unlocked ? new Color(.07f, .15f, .19f, 1f) :
                        new Color(.07f, .075f, .09f, 1f);
                if (i % 2 == 1 && branchLinks != null && i / 2 < branchLinks.Length &&
                    branchLinks[i / 2] != null)
                    branchLinks[i / 2].color = unlocked ? new Color(.12f, .88f, .78f, 1f) :
                        new Color(.3f, .34f, .38f, 1f);
            }
        }

        private void Invest(int index)
        {
            if (!IsOpen || profile == null || index < 0 || index >= PlayerTraitCatalog.All.Count) return;
            profile.TryInvest(PlayerTraitCatalog.All[index].Id);
        }
    }
}
