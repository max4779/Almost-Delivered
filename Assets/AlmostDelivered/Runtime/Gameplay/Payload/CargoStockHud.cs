using UnityEngine;
using UnityEngine.UI;

namespace AlmostDelivered.Gameplay.Payload
{
    /// <summary>Three cargo lives; the schematic warns left, right, then center.</summary>
    public sealed class CargoStockHud : MonoBehaviour
    {
        [SerializeField] private PayloadMissionController mission;
        [SerializeField] private CargoBlueprintGraphic blueprint;
        [SerializeField] private Text healthText;
        [SerializeField] private Image healthFill;
        [SerializeField] private GameObject gameOverPanel;

        private static readonly Color HealthyBar = new Color(.18f, .78f, .87f, .95f);
        private static readonly Color WarningBar = new Color(1f, .78f, .18f, .98f);
        private static readonly Color Destroyed = new Color(.94f, .15f, .12f, .95f);

        public void Configure(PayloadMissionController controller, CargoBlueprintGraphic schematic,
            Text health, Image fill, GameObject gameOver)
        {
            if (mission != null) mission.SnapshotChanged -= Refresh;
            mission = controller;
            blueprint = schematic;
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
            if (blueprint != null) blueprint.Destructions = snapshot.CargoDestructions;

            var health = Mathf.Max(0f, snapshot.CargoHealth);
            var maximum = Mathf.Max(0f, snapshot.CargoMaximumHealth);
            var ratio = maximum > 0f ? Mathf.Clamp01(health / maximum) : 0f;
            if (healthText != null)
                healthText.text = $"{(ratio > 0f ? Mathf.Max(1, Mathf.RoundToInt(ratio * 100f)) : 0)}%";
            if (healthFill != null)
            {
                // A sprite-less Filled Image renders a full quad in Unity. Resize the
                // actual RectTransform so the visible bar follows the cargo health.
                healthFill.type = Image.Type.Simple;
                var fillRect = healthFill.rectTransform;
                fillRect.anchorMin = Vector2.zero;
                fillRect.anchorMax = new Vector2(ratio, 1f);
                fillRect.offsetMin = Vector2.zero;
                fillRect.offsetMax = Vector2.zero;
                healthFill.fillAmount = ratio;
                healthFill.color = ratio <= 1f / 3f ? Destroyed
                    : ratio <= 2f / 3f ? WarningBar : HealthyBar;
            }
            if (gameOverPanel != null)
            {
                var escaped = snapshot.Phase == PayloadMissionPhase.Escaped;
                gameOverPanel.SetActive(snapshot.Phase == PayloadMissionPhase.Failed || escaped);
                if (escaped || snapshot.Phase == PayloadMissionPhase.Failed && snapshot.CargoDestructions >= 3)
                {
                    var title = gameOverPanel.transform.Find("Game Over Title")?.GetComponent<Text>();
                    var detail = gameOverPanel.transform.Find("Game Over Detail")?.GetComponent<Text>();
                    if (title != null)
                    {
                        title.text = escaped ? "EXTRACTION SUCCESS" : "EXTRACTION FAILED";
                        title.color = escaped ? new Color(.36f, 1f, .83f) : new Color(1f, .24f, .2f);
                    }
                    if (detail != null) detail.text = escaped
                        ? "CARGO MISSION FAILED  ·  RETURNING TO BASE"
                        : "CREW LOST  ·  ESC TO RETURN TO BASE";
                }
            }
        }

    }
}
