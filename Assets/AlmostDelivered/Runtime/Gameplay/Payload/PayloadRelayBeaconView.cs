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
        [SerializeField] private PayloadRelayVisualState visualState;
        private bool subscribed;
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
            pulseRingBaseScale = pulseRing == null ? Vector3.one : pulseRing.transform.localScale;
            Subscribe();
            Refresh();
        }

        private void OnEnable()
        {
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

            var pulse = 1f + Mathf.Sin(Time.time * 3.5f) * .08f;
            pulseRing.transform.localScale = pulseRingBaseScale * pulse;
        }

        public void Refresh()
        {
            var state = mission == null
                ? PayloadRelayVisualState.Future
                : ResolveState(mission.Snapshot);
            ApplyVisualState(state);
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
            visualState = state;
            var activeSignal = state == PayloadRelayVisualState.Active ||
                               state == PayloadRelayVisualState.Loading ||
                               state == PayloadRelayVisualState.DestinationActive;
            SetActive(beam, activeSignal);
            SetActive(pulseRing, activeSignal);
            SetActive(supplyCache, state != PayloadRelayVisualState.Completed);
            SetActive(collectedCargo, state == PayloadRelayVisualState.Completed);
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
