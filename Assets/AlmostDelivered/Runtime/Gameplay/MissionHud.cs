using System.Linq;
using AlmostDelivered.Combat;
using AlmostDelivered.Frontend;
using AlmostDelivered.Gameplay.Payload;
using UnityEngine;
using UnityEngine.UI;

namespace AlmostDelivered.Gameplay
{
    public sealed class MissionHud : MonoBehaviour
    {
        [SerializeField] private Text controlsText;
        [SerializeField] private Text playerHealthText;
        [SerializeField] private Text bossCountText;
        [SerializeField] private ActorHealth playerHealth;
        [SerializeField] private PrototypeBossController[] bosses;
        [SerializeField] private PayloadMissionController payloadMission;
        [SerializeField] private Text payloadPhaseText;
        [SerializeField] private Text payloadProgressText;
        [SerializeField] private Text payloadCheckpointText;
        [SerializeField] private Text payloadEscortText;
        [SerializeField] private Text payloadElapsedText;
        [SerializeField] private Text payloadActivationCountdownText;
        [SerializeField] private Text cargoHealthText;

        public int RemainingBosses => bosses == null ? 0 : bosses.Count(boss => boss != null && !boss.IsDefeated);
        public bool HasPayloadViews => payloadMission != null && payloadPhaseText != null && payloadProgressText != null &&
                                       payloadCheckpointText != null && payloadEscortText != null && payloadElapsedText != null;
        public Text ActivationCountdownText => payloadActivationCountdownText;

        public void BindViews(Text controls, Text health, Text bossCount)
        {
            controlsText = controls;
            playerHealthText = health;
            bossCountText = bossCount;
        }

        public void Configure(ActorHealth player, PrototypeBossController[] missionBosses)
        {
            playerHealth = player;
            bosses = missionBosses;
            Refresh();
        }

        public void ConfigurePayload(
            PayloadMissionController mission,
            Text phase,
            Text progress,
            Text checkpoint,
            Text escort,
            Text elapsed)
        {
            payloadMission = mission;
            payloadPhaseText = phase;
            payloadProgressText = progress;
            payloadCheckpointText = checkpoint;
            payloadEscortText = escort;
            payloadElapsedText = elapsed;
            Refresh();
        }

        public void BindActivationCountdown(Text countdown)
        {
            payloadActivationCountdownText = countdown;
            Refresh();
        }

        public void BindCargoHealth(Text healthText)
        {
            cargoHealthText = healthText;
            Refresh();
        }

        private void Update()
        {
            Refresh();
        }

        public void Refresh()
        {
            if (controlsText != null)
                controlsText.text = $"WASD 이동  |  {PlayerInputBindings.DisplayName(PlayerAction.Sprint)} 달리기  |  " +
                    $"{PlayerInputBindings.DisplayName(PlayerAction.Roll)} 구르기  |  좌클릭 공격  |  R 장전  |  E 상호작용  |  ESC 귀환";
            if (playerHealthText != null && playerHealth != null)
                playerHealthText.text = $"PLAYER  {Mathf.CeilToInt(playerHealth.CurrentHealth)} / {Mathf.CeilToInt(playerHealth.MaxHealth)}";
            if (bossCountText != null)
            {
                var remainingTargets = payloadMission != null
                    ? payloadMission.Snapshot.RemainingEnemyCount
                    : RemainingBosses;
                bossCountText.text = $"TARGETS  {remainingTargets}";
            }

            RefreshPayload();
        }

        private void RefreshPayload()
        {
            if (payloadMission == null)
                return;

            var state = payloadMission.Snapshot;
            if (payloadPhaseText != null)
                payloadPhaseText.text = $"MISSION  {PhaseLabel(state.Phase)}";
            if (payloadProgressText != null)
                payloadProgressText.text = $"PAYLOAD  {Mathf.RoundToInt(state.NormalizedProgress * 100f)}%";
            if (payloadCheckpointText != null)
                payloadCheckpointText.text = $"CHECKPOINT  {Mathf.Clamp(state.NextCheckpointIndex + 1, 1, 3)} / 3";
            if (payloadEscortText != null)
                payloadEscortText.text = payloadMission.CargoHealth != null && payloadMission.CargoHealth.IsBroken
                    ? "ESCORT  CARGO BROKEN"
                    : state.Phase == PayloadMissionPhase.Recovering ||
                                         state.Phase == PayloadMissionPhase.Activating
                    ? $"OBJECTIVE  {RecoveryLabel(state)}"
                    : $"ESCORT  {EscortLabel(state)}";
            if (payloadElapsedText != null)
            {
                var totalSeconds = Mathf.Max(0, Mathf.FloorToInt(state.ElapsedTime));
                payloadElapsedText.text = $"TIME  {totalSeconds / 60:00}:{totalSeconds % 60:00}";
            }

            if (cargoHealthText != null && payloadMission.CargoHealth != null)
            {
                var cargo = payloadMission.CargoHealth;
                cargoHealthText.text = cargo.IsBroken
                    ? $"CARGO BROKEN  {Mathf.CeilToInt(cargo.CurrentHealth)} / {Mathf.CeilToInt(cargo.MaximumHealth)}  ·  HOLD E TO REPAIR"
                    : $"CARGO HP  {Mathf.CeilToInt(cargo.CurrentHealth)} / {Mathf.CeilToInt(cargo.MaximumHealth)}";
                cargoHealthText.color = cargo.IsBroken
                    ? new Color(1f, .22f, .18f, 1f)
                    : cargo.CurrentHealth <= cargo.MaximumHealth * .3f
                        ? new Color(1f, .65f, .12f, 1f)
                        : Color.white;
            }

            RefreshActivationCountdown(state);
        }

        private void RefreshActivationCountdown(PayloadMissionSnapshot state)
        {
            if (payloadActivationCountdownText == null)
            {
                return;
            }

            var readyToStart = state.Phase == PayloadMissionPhase.Recovering &&
                               state.RemainingEnemyCount == 0 &&
                               state.PlayersInRange > 0;
            var activating = state.Phase == PayloadMissionPhase.Activating;
            var visible = readyToStart || activating;
            if (payloadActivationCountdownText.gameObject.activeSelf != visible)
            {
                payloadActivationCountdownText.gameObject.SetActive(visible);
            }

            if (!visible)
            {
                return;
            }

            var progress = activating ? state.ActivationProgress : 0f;
            var remaining = Mathf.Max(0f, payloadMission.CargoActivationDuration * (1f - progress));
            var running = activating && state.IsActivationInputHeld;
            payloadActivationCountdownText.text = running
                ? $"CARGO STARTUP\n{remaining:0.0}"
                : $"HOLD E\n{remaining:0.0}";
            payloadActivationCountdownText.color = running
                ? new Color(.12f, .95f, 1f, 1f)
                : new Color(1f, .66f, .12f, 1f);
        }

        private static string PhaseLabel(PayloadMissionPhase phase)
        {
            switch (phase)
            {
                case PayloadMissionPhase.CheckpointDefense:
                    return "LOADING SUPPLIES";
                case PayloadMissionPhase.FinalDefense:
                    return "FINAL DEFENSE";
                case PayloadMissionPhase.Completed:
                    return "DELIVERED";
                case PayloadMissionPhase.Deploying:
                    return "DEPLOYING";
                case PayloadMissionPhase.Recovering:
                    return "RECOVER LOST CARGO";
                case PayloadMissionPhase.Activating:
                    return "STARTING CARGO";
                case PayloadMissionPhase.Failed:
                    return "FAILED";
                default:
                    return "ESCORTING";
            }
        }

        private static string EscortLabel(PayloadMissionSnapshot state)
        {
            if (state.Phase == PayloadMissionPhase.CheckpointDefense)
                return "LOADING SUPPLIES";
            if (state.Phase == PayloadMissionPhase.FinalDefense)
                return "DEFEND DESTINATION";
            if (state.EnemiesInRange > 0)
                return "CONTESTED";
            if (state.IsEscortActive)
                return "MOVING";
            return "WAITING";
        }

        private static string RecoveryLabel(PayloadMissionSnapshot state)
        {
            if (state.Phase == PayloadMissionPhase.Activating)
            {
                return state.PlayersInRange > 0
                    ? state.IsActivationInputHeld
                        ? $"CARGO STARTUP {Mathf.RoundToInt(state.ActivationProgress * 100f)}%"
                        : $"HOLD E TO CONTINUE {Mathf.RoundToInt(state.ActivationProgress * 100f)}%"
                    : "RETURN TO CARGO";
            }

            if (state.RemainingEnemyCount > 0)
                return "CLEAR CARGO GUARDS";
            if (state.PlayersInRange > 0)
                return "HOLD E TO START CARGO";
            return "APPROACH CARGO";
        }
    }
}
