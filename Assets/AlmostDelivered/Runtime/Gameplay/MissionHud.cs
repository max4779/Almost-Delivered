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
        private void Awake()
        {
            // The mission's existing HUD is informational. Its Text and Image
            // components must not interrupt held fire when the cursor crosses them.
            // Interactive route choices are added after this HUD is initialized.
            foreach (var graphic in GetComponentsInChildren<Graphic>(true))
                graphic.raycastTarget = false;
        }

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
        [SerializeField] private Text cargoFuelText;
        [SerializeField] private FuelDepotDirector fuelDepots;
        [SerializeField] private MissionCommanderDirector fieldCommander;
        [SerializeField] private Text commanderSignalText;
        [SerializeField] private SideMissionDirector sideMissions;
        [SerializeField] private Text[] sideMissionTexts;

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

        public void BindFuel(Text fuelText, FuelDepotDirector depots)
        {
            cargoFuelText = fuelText;
            fuelDepots = depots;
            Refresh();
        }

        public void BindCommander(MissionCommanderDirector commander, Text signalText)
        {
            fieldCommander = commander;
            commanderSignalText = signalText;
            Refresh();
        }

        public void BindSideMissions(SideMissionDirector director, Text[] labels)
        {
            sideMissions = director;
            sideMissionTexts = labels;
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
            if (commanderSignalText != null)
            {
                var scoutId = 0;
                var live = fieldCommander != null &&
                    fieldCommander.TryGetLiveScoutSight(out scoutId, out _);
                var visible = live || fieldCommander != null &&
                    Time.time - fieldCommander.LastReportTime < 10f;
                commanderSignalText.gameObject.SetActive(visible);
                if (visible)
                {
                    var profile = fieldCommander?.LatestScoutProfile;
                    commanderSignalText.text = live && profile != null
                        ? $"SCOUT {scoutId} LIVE  |  {profile.WeaponLabel}  |  " +
                          $"FIRE {profile.ShotsObserved}  RELOAD {profile.ReloadsObserved}  " +
                          $"MELEE {profile.MeleeAttacksObserved}  ROLL {profile.ObservedRolls}"
                        : "COMMANDER SEARCHING LAST KNOWN POSITION";
                }
            }
            if (sideMissions != null && sideMissionTexts != null)
                for (var index = 0; index < sideMissionTexts.Length; index++)
                    if (sideMissionTexts[index] != null)
                    {
                        sideMissionTexts[index].text = index < sideMissions.Count
                            ? $"SIDE {index + 1}  {sideMissions.LabelAt(index)}"
                            : string.Empty;
                        sideMissionTexts[index].color = index < sideMissions.Count &&
                            sideMissions.StateAt(index) == SideMissionState.Completed
                            ? new Color(.3f, 1f, .55f) : new Color(1f, .83f, .35f);
                    }
        }

        private void RefreshPayload()
        {
            if (payloadMission == null)
                return;

            var state = payloadMission.Snapshot;
            var evacuating = state.Phase == PayloadMissionPhase.EvacuationTravel ||
                             state.Phase == PayloadMissionPhase.EvacuationDefense;
            if (payloadPhaseText != null)
                payloadPhaseText.text = state.IsRelayChoicePending
                    ? "MISSION  SELECT ROUTE PROTOCOL"
                    : $"MISSION  {PhaseLabel(state.Phase)}";
            if (payloadProgressText != null)
                payloadProgressText.text = evacuating || state.Phase == PayloadMissionPhase.Escaped
                    ? "CARGO OBJECTIVE FAILED"
                    : $"PAYLOAD  {Mathf.RoundToInt(state.NormalizedProgress * 100f)}%";
            if (payloadCheckpointText != null)
                payloadCheckpointText.text = state.Phase == PayloadMissionPhase.EvacuationTravel
                    ? $"EVAC SITE  {Mathf.CeilToInt(payloadMission.EvacuationDistance)}m"
                    : state.Phase == PayloadMissionPhase.EvacuationDefense
                        ? $"EXTRACTION  {Mathf.CeilToInt(state.EvacuationSecondsRemaining)}s"
                        : state.Phase == PayloadMissionPhase.Escaped
                            ? "EXTRACTION COMPLETE"
                            : $"CHECKPOINT  {Mathf.Clamp(state.NextCheckpointIndex + 1, 1, 3)} / 3";
            if (payloadEscortText != null)
                payloadEscortText.text = state.Phase == PayloadMissionPhase.EvacuationTravel
                    ? payloadMission.CanActivateEvacuationSignal
                        ? "OBJECTIVE  PRESS E TO SEND EXTRACTION SIGNAL"
                        : "OBJECTIVE  REACH THE MARKED EVACUATION SITE"
                    : state.Phase == PayloadMissionPhase.EvacuationDefense
                        ? state.EvacuationSecondsRemaining <= 0f
                            ? "EXTRACTION READY  ·  RETURN TO THE SITE"
                            : "OBJECTIVE  DEFEND THE SIGNAL SITE"
                    : state.Phase == PayloadMissionPhase.Escaped
                        ? "CARGO LOST  ·  CREW EVACUATED"
                    : state.IsRelayChoicePending
                    ? "ESCORT  WAITING FOR ROUTE CHOICE"
                    : payloadMission.CargoHealth != null && payloadMission.CargoHealth.IsBroken
                    ? "ESCORT  CARGO BROKEN"
                    : state.Phase == PayloadMissionPhase.Recovering ||
                                         state.Phase == PayloadMissionPhase.Activating
                    ? $"OBJECTIVE  {RecoveryLabel(state)}"
                    : $"ESCORT  {EscortLabel(state)}{RouteOptionLabel(payloadMission.ActiveRouteOption)}";
            if (payloadElapsedText != null)
            {
                var totalSeconds = Mathf.Max(0, Mathf.FloorToInt(state.ElapsedTime));
                payloadElapsedText.text = $"TIME  {totalSeconds / 60:00}:{totalSeconds % 60:00}";
            }

            if (cargoFuelText != null && state.UsesFuel)
            {
                if (evacuating || state.Phase == PayloadMissionPhase.Escaped)
                {
                    cargoFuelText.text = state.Phase == PayloadMissionPhase.EvacuationTravel
                        ? "EVACUATION SIGNAL  ·  FOLLOW THE VIOLET BEACON AND PRESS E"
                        : state.Phase == PayloadMissionPhase.EvacuationDefense
                            ? $"EVAC INBOUND  {Mathf.CeilToInt(state.EvacuationSecondsRemaining)}s  ·  DEFEND THE SITE"
                            : "EVACUATED  ·  CARGO DELIVERY FAILED";
                    cargoFuelText.color = new Color(1f, .55f, .3f);
                    RefreshActivationCountdown(state);
                    return;
                }
                var remaining = Mathf.CeilToInt(state.FuelRemaining);
                var capacity = Mathf.CeilToInt(state.FuelCapacity);
                cargoFuelText.text = state.Phase == PayloadMissionPhase.Recovering ||
                                     state.Phase == PayloadMissionPhase.Activating
                    ? $"START FUEL  {Mathf.CeilToInt(payloadMission.StartingFuelSeconds)}s  ·  LOAD ON STARTUP"
                    : fuelDepots != null && fuelDepots.CarryingIndex >= 0
                        ? $"FUEL  {remaining}s / {capacity}s  ·  RETURN CELL TO CARGO · E"
                        : state.IsOutOfFuel
                            ? $"FUEL EMPTY  ·  FIND A CELL AND REFUEL WITH E"
                            : $"FUEL  {remaining}s / {capacity}s";
                cargoFuelText.color = state.IsOutOfFuel
                    ? new Color(1f, .38f, .2f) : new Color(.45f, 1f, .72f);
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
            var defending = state.Phase == PayloadMissionPhase.EvacuationDefense;
            var visible = readyToStart || activating || defending;
            if (payloadActivationCountdownText.gameObject.activeSelf != visible)
            {
                payloadActivationCountdownText.gameObject.SetActive(visible);
            }

            if (!visible)
            {
                return;
            }

            if (defending)
            {
                var seconds = Mathf.Max(0, Mathf.CeilToInt(state.EvacuationSecondsRemaining));
                payloadActivationCountdownText.text = seconds <= 0 && !payloadMission.IsPlayerInEvacuationHoldArea
                    ? "EXTRACTION READY\nRETURN TO EVAC SITE"
                    : $"EXTRACTION IN\n{seconds / 60:00}:{seconds % 60:00}";
                payloadActivationCountdownText.color = !payloadMission.IsPlayerInEvacuationHoldArea
                    ? new Color(1f, .55f, .3f, 1f)
                    : seconds <= 15 ? new Color(1f, .75f, .26f, 1f)
                    : new Color(.12f, .95f, 1f, 1f);
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
                case PayloadMissionPhase.EvacuationTravel:
                    return "FAILED  ·  REACH EVAC SITE";
                case PayloadMissionPhase.EvacuationDefense:
                    return "FAILED  ·  HOLD FOR EVAC";
                case PayloadMissionPhase.Escaped:
                    return "EVACUATED  ·  CARGO LOST";
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
            if (!state.UsesFuel && state.EnemiesInRange > 0)
                return "CONTESTED";
            if (state.IsOutOfFuel)
                return "OUT OF FUEL";
            if (state.IsEscortActive)
                return "MOVING";
            return "WAITING";
        }

        private static string RouteOptionLabel(RelayRouteOptionDefinition option)
        {
            return option == null ? string.Empty : $" · {option.title}";
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
