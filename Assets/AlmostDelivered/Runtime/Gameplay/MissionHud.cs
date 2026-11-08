using System.Linq;
using AlmostDelivered.Combat;
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

        public int RemainingBosses => bosses == null ? 0 : bosses.Count(boss => boss != null && !boss.IsDefeated);
        public bool HasPayloadViews => payloadMission != null && payloadPhaseText != null && payloadProgressText != null &&
                                       payloadCheckpointText != null && payloadEscortText != null && payloadElapsedText != null;

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

        private void Update()
        {
            Refresh();
        }

        public void Refresh()
        {
            if (controlsText != null)
                controlsText.text = "WASD 이동  |  마우스 조준  |  좌클릭 발사  |  ESC 귀환";
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
                payloadEscortText.text = state.Phase == PayloadMissionPhase.Recovering ||
                                         state.Phase == PayloadMissionPhase.Activating
                    ? $"OBJECTIVE  {RecoveryLabel(state)}"
                    : $"ESCORT  {EscortLabel(state)}";
            if (payloadElapsedText != null)
            {
                var totalSeconds = Mathf.Max(0, Mathf.FloorToInt(state.ElapsedTime));
                payloadElapsedText.text = $"TIME  {totalSeconds / 60:00}:{totalSeconds % 60:00}";
            }
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
                    ? $"CARGO STARTUP {Mathf.RoundToInt(state.ActivationProgress * 100f)}%"
                    : "RETURN TO CARGO";
            }

            if (state.RemainingEnemyCount > 0)
                return "CLEAR CARGO GUARDS";
            if (state.PlayersInRange > 0)
                return "RESTARTING CARGO";
            return "APPROACH CARGO";
        }
    }
}
