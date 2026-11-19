using UnityEngine;
using UnityEngine.UI;

namespace AlmostDelivered.Gameplay.Payload
{
    /// <summary>Three cargo lives; blueprint modules fail right, left, then center.</summary>
    public sealed class CargoStockHud : MonoBehaviour
    {
        [SerializeField] private PayloadMissionController mission;
        [SerializeField] private Image[] modules;
        [SerializeField] private Text stockText;
        [SerializeField] private Text healthText;
        [SerializeField] private Image healthFill;
        [SerializeField] private GameObject gameOverPanel;

        private static readonly Color Healthy = new Color(.18f, .78f, .87f, .85f);
        private static readonly Color Destroyed = new Color(.94f, .15f, .12f, .95f);

        public void Configure(PayloadMissionController controller, Image[] blueprintModules,
            Text stocks, Text health, Image fill, GameObject gameOver)
        {
            if (mission != null) mission.SnapshotChanged -= Refresh;
            mission = controller;
            modules = blueprintModules;
            stockText = stocks;
            healthText = health;
            healthFill = fill;
            gameOverPanel = gameOver;
            if (mission != null) mission.SnapshotChanged += Refresh;
            Refresh(mission != null ? mission.Snapshot : default);
        }

        private void OnEnable()
        {
            if (mission == null) return;
            mission.SnapshotChanged -= Refresh;
            mission.SnapshotChanged += Refresh;
            Refresh(mission.Snapshot);
        }

        private void OnDisable()
        {
            if (mission != null) mission.SnapshotChanged -= Refresh;
        }

        private void OnDestroy()
        {
            if (mission != null) mission.SnapshotChanged -= Refresh;
        }

        public void Refresh(PayloadMissionSnapshot snapshot)
        {
            if (stockText != null)
                stockText.text = $"CARGO STOCK  {snapshot.CargoStocksRemaining} / 3";
            if (modules != null && modules.Length >= 3)
            {
                // Blueprint layout is left, center, right. Loss order is right, left, center.
                SetModule(2, snapshot.CargoDestructions >= 1);
                SetModule(0, snapshot.CargoDestructions >= 2);
                SetModule(1, snapshot.CargoDestructions >= 3);
            }

            var health = Mathf.Max(0f, snapshot.CargoHealth);
            var maximum = Mathf.Max(0f, snapshot.CargoMaximumHealth);
            if (healthText != null)
                healthText.text = snapshot.Phase == PayloadMissionPhase.Failed
                    ? "CARGO DESTROYED"
                    : $"CARGO HP  {Mathf.CeilToInt(health)} / {Mathf.CeilToInt(maximum)}";
            if (healthFill != null)
            {
                healthFill.fillAmount = maximum > 0f ? Mathf.Clamp01(health / maximum) : 0f;
                healthFill.color = health <= maximum * .3f ? Destroyed : Healthy;
            }
            if (gameOverPanel != null)
                gameOverPanel.SetActive(snapshot.Phase == PayloadMissionPhase.Failed);
        }

        private void SetModule(int index, bool destroyed)
        {
            if (modules[index] != null)
                modules[index].color = destroyed ? Destroyed : Healthy;
        }
    }
}
