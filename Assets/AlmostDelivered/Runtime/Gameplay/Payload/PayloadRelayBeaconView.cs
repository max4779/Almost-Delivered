using UnityEngine;

namespace AlmostDelivered.Gameplay.Payload
{
    public enum PayloadRelayVisualState
    {
        Future,
        Active,
        Loading,
        Completed,
        DestinationActive
    }

    public sealed class PayloadRelayBeaconView : MonoBehaviour
    {
        [SerializeField] private PayloadMissionController mission;
        [SerializeField] private int checkpointIndex;
        [SerializeField] private bool finalDestination;
        [SerializeField] private GameObject beam;
        [SerializeField] private GameObject pulseRing;
        [SerializeField] private GameObject supplyCache;
        [SerializeField] private GameObject collectedCargo;
        [SerializeField] private TextMesh relayLabel;
        [SerializeField] private PayloadRelayVisualState visualState;
        private bool subscribed;
        private bool visualsInitialized;
        private Vector3 pulseRingBaseScale = Vector3.one;

        public int CheckpointIndex => checkpointIndex;
        public bool IsFinalDestination => finalDestination;
        public PayloadRelayVisualState VisualState => visualState;

        public void Configure(
            PayloadMissionController missionController,
            int relayCheckpointIndex,
            bool isFinalDestination,
            GameObject beamVisual,
            GameObject ringVisual,
            GameObject cacheVisual,
            GameObject cargoVisual)
        {
            Unsubscribe();
            mission = missionController;
            checkpointIndex = Mathf.Max(0, relayCheckpointIndex);
            finalDestination = isFinalDestination;
            beam = beamVisual;
            pulseRing = ringVisual;
            supplyCache = cacheVisual;
            collectedCargo = cargoVisual;
            relayLabel = transform.Find("Relay Label")?.GetComponent<TextMesh>();
            visualsInitialized = false;
            pulseRingBaseScale = pulseRing == null ? Vector3.one : pulseRing.transform.localScale;
            Subscribe();
            Refresh();
        }

        private void OnEnable()
        {
            ResolveRelayLabel();
            if (pulseRing != null)
            {
                pulseRingBaseScale = pulseRing.transform.localScale;
            }

            Subscribe();
            Refresh();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Update()
        {
            if (pulseRing == null || !pulseRing.activeSelf)
            {
                return;
            }

            if (visualState == PayloadRelayVisualState.Completed)
            {
                pulseRing.transform.localScale = pulseRingBaseScale;
                return;
            }

            var pulse = 1f + Mathf.Sin(Time.time * 3.5f) * .08f;
            pulseRing.transform.localScale = pulseRingBaseScale * pulse;
        }

        public void Refresh()
        {
            ResolveRelayLabel();
            var state = mission == null
                ? PayloadRelayVisualState.Future
                : ResolveState(mission.Snapshot);
            if (!visualsInitialized || state != visualState)
            {
                ApplyVisualState(state);
            }
            UpdateDistanceLabel();
        }

        private PayloadRelayVisualState ResolveState(PayloadMissionSnapshot snapshot)
        {
            if (snapshot.Phase == PayloadMissionPhase.Completed)
            {
                return PayloadRelayVisualState.Completed;
            }

            if (snapshot.Phase == PayloadMissionPhase.Failed)
            {
                return PayloadRelayVisualState.Future;
            }

            if (snapshot.Phase == PayloadMissionPhase.Deploying ||
                snapshot.Phase == PayloadMissionPhase.Recovering ||
                snapshot.Phase == PayloadMissionPhase.Activating)
            {
                return PayloadRelayVisualState.Future;
            }

            if (!finalDestination && snapshot.NextCheckpointIndex > checkpointIndex)
            {
                return PayloadRelayVisualState.Completed;
            }

            if (snapshot.NextCheckpointIndex != checkpointIndex)
            {
                return PayloadRelayVisualState.Future;
            }

            if (snapshot.Phase == PayloadMissionPhase.CheckpointDefense ||
                snapshot.Phase == PayloadMissionPhase.FinalDefense)
            {
                return PayloadRelayVisualState.Loading;
            }

            return finalDestination
                ? PayloadRelayVisualState.DestinationActive
                : PayloadRelayVisualState.Active;
        }

        private void ApplyVisualState(PayloadRelayVisualState state)
        {
            visualsInitialized = true;
            visualState = state;
            var activeSignal = state == PayloadRelayVisualState.Active ||
                               state == PayloadRelayVisualState.Loading ||
                               state == PayloadRelayVisualState.Completed ||
                               state == PayloadRelayVisualState.DestinationActive;
            SetActive(beam, activeSignal);
            SetActive(pulseRing, activeSignal);
            SetActive(supplyCache, state != PayloadRelayVisualState.Completed);
            SetActive(collectedCargo, state == PayloadRelayVisualState.Completed);
            if (pulseRing != null && state == PayloadRelayVisualState.Completed)
            {
                pulseRing.transform.localScale = pulseRingBaseScale;
            }

            var signalColor = state == PayloadRelayVisualState.Completed
                ? new Color(.25f, 1f, .45f)
                : state == PayloadRelayVisualState.Loading
                    ? new Color(1f, .65f, .15f)
                    : new Color(.2f, .9f, 1f);
            ApplySignalColor(beam, signalColor);
            ApplySignalColor(pulseRing, signalColor);
        }

        private void UpdateDistanceLabel()
        {
            if (relayLabel == null || mission == null || mission.RoutePlan == null)
            {
                return;
            }

            if (!finalDestination && mission.RoutePlan.CheckpointDistances.Count == 0)
            {
                return;
            }

            var targetDistance = finalDestination
                ? mission.RoutePlan.TotalLength
                : mission.RoutePlan.CheckpointDistances[Mathf.Clamp(checkpointIndex, 0, mission.RoutePlan.CheckpointDistances.Count - 1)];
            var remaining = Mathf.Max(0f, targetDistance - mission.Snapshot.RouteDistance);
            relayLabel.text = finalDestination
                ? $"DESTINATION\n{Mathf.CeilToInt(remaining)}m"
                : $"SUPPLY {checkpointIndex + 1}\n{Mathf.CeilToInt(remaining)}m";
        }

        private void ResolveRelayLabel()
        {
            if (relayLabel == null)
            {
                relayLabel = transform.Find("Relay Label")?.GetComponent<TextMesh>();
            }
        }

        private static void ApplySignalColor(GameObject value, Color color)
        {
            if (value == null)
            {
                return;
            }

            var renderers = value.GetComponentsInChildren<Renderer>(true);
            var properties = new MaterialPropertyBlock();
            properties.SetColor("_Color", color);
            for (var index = 0; index < renderers.Length; index++)
            {
                renderers[index].SetPropertyBlock(properties);
            }
        }

        private void Subscribe()
        {
            if (subscribed || mission == null || !isActiveAndEnabled)
            {
                return;
            }

            mission.SnapshotChanged += HandleSnapshotChanged;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed || mission == null)
            {
                subscribed = false;
                return;
            }

            mission.SnapshotChanged -= HandleSnapshotChanged;
            subscribed = false;
        }

        private void HandleSnapshotChanged(PayloadMissionSnapshot _)
        {
            Refresh();
        }

        private static void SetActive(GameObject value, bool active)
        {
            if (value != null && value.activeSelf != active)
            {
                value.SetActive(active);
            }
        }
    }
}
