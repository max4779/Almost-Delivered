using System;
using AlmostDelivered.Combat;
using UnityEngine;

namespace AlmostDelivered.Gameplay.Payload
{
    public enum SideMissionState { Available, Carrying, Completed, Missed }

    public sealed class SideMissionDirector : MonoBehaviour
    {
        [SerializeField] private PayloadMissionController mission;
        [SerializeField] private PayloadWaveSpawner waves;
        [SerializeField] private Transform player;
        [SerializeField] private Transform payload;
        [SerializeField] private Rect missionBounds;
        [SerializeField] private SideMissionPlacement[] placements = Array.Empty<SideMissionPlacement>();
        [SerializeField] private GameObject[] objectiveViews = Array.Empty<GameObject>();
        [SerializeField] private GameObject[] siteMarkers = Array.Empty<GameObject>();
        private SideMissionState[] states = Array.Empty<SideMissionState>();
        private bool surgePending;
        private bool surgeTriggered;
        private int bonusCargoCount;

        public int Count => placements?.Length ?? 0;
        public int BonusCargoCount => bonusCargoCount;
        public bool SurgeTriggered => surgeTriggered;
        public event Action<int, SideMissionKind> ObjectiveCompleted;

        public void Configure(PayloadMissionController missionController, PayloadWaveSpawner waveSpawner,
            Transform playerTarget, Transform cargoTarget, Rect bounds,
            SideMissionPlacement[] objectives, GameObject[] views, GameObject[] markers)
        {
            mission = missionController;
            waves = waveSpawner;
            player = playerTarget;
            payload = cargoTarget;
            missionBounds = bounds;
            placements = objectives ?? Array.Empty<SideMissionPlacement>();
            objectiveViews = views ?? Array.Empty<GameObject>();
            siteMarkers = markers ?? Array.Empty<GameObject>();
            states = new SideMissionState[placements.Length];
        }

        public SideMissionPlacement PlacementAt(int index) => placements[index];
        public SideMissionState StateAt(int index) =>
            states != null && index < states.Length ? states[index] : SideMissionState.Available;

        public string LabelAt(int index)
        {
            var kind = placements[index].kind;
            switch (StateAt(index))
            {
                case SideMissionState.Carrying: return "BONUS CARGO  RETURN TO PAYLOAD · E";
                case SideMissionState.Completed:
                    return kind == SideMissionKind.Commander
                        ? "COMMANDER DOWN  ENEMIES RUSH THE CARGO"
                        : "BONUS CARGO  ATTACHED";
                case SideMissionState.Missed: return kind == SideMissionKind.Commander
                    ? "COMMANDER  MISSED" : "BONUS CARGO  MISSED";
                default: return kind == SideMissionKind.Commander
                    ? "COMMANDER  ELIMINATE AT OUTPOST"
                    : "BONUS CARGO  FIND AND PRESS E";
            }
        }

        private void Start()
        {
            if (states == null || states.Length != Count)
                states = new SideMissionState[Count];
            if (waves != null) waves.EnemyRemoved += HandleEnemyRemoved;
            if (mission == null || !mission.IsAuthority || waves == null) return;
            for (var index = 0; index < Count; index++)
                if (placements[index].kind == SideMissionKind.Commander)
                    waves.SpawnSideCommanderGroup(placements[index].position);
        }

        private void OnDestroy()
        {
            if (waves != null) waves.EnemyRemoved -= HandleEnemyRemoved;
        }

        private void Update()
        {
            if (mission == null || !mission.IsAuthority || player == null || payload == null) return;
            var phase = mission.Snapshot.Phase;
            if (phase == PayloadMissionPhase.Completed || phase == PayloadMissionPhase.Failed ||
                phase == PayloadMissionPhase.EvacuationTravel ||
                phase == PayloadMissionPhase.EvacuationDefense ||
                phase == PayloadMissionPhase.Escaped)
            {
                for (var index = 0; index < states.Length; index++)
                    if (states[index] != SideMissionState.Completed) states[index] = SideMissionState.Missed;
                return;
            }
            if (surgePending) TryStartSurge();
            if (phase == PayloadMissionPhase.Recovering || phase == PayloadMissionPhase.Activating ||
                phase == PayloadMissionPhase.Deploying || !Input.GetKeyDown(KeyCode.E)) return;
            var health = player.GetComponent<ActorHealth>();
            if (health != null && health.IsDead) return;

            for (var index = 0; index < Count; index++)
            {
                if (placements[index].kind != SideMissionKind.BonusCargo) continue;
                if (states[index] == SideMissionState.Available &&
                    HorizontalDistance(player.position, placements[index].position) <= 3f)
                {
                    states[index] = SideMissionState.Carrying;
                    if (index < siteMarkers.Length && siteMarkers[index] != null)
                        siteMarkers[index].SetActive(false);
                    if (index < objectiveViews.Length && objectiveViews[index] != null)
                    {
                        objectiveViews[index].transform.SetParent(player, false);
                        objectiveViews[index].transform.localPosition = new Vector3(-.7f, 1.3f, -.4f);
                        objectiveViews[index].transform.localScale = Vector3.one * .65f;
                    }
                    return;
                }
                if (states[index] == SideMissionState.Carrying &&
                    HorizontalDistance(player.position, payload.position) <= mission.CargoInteractionRadius)
                {
                    states[index] = SideMissionState.Completed;
                    bonusCargoCount++;
                    ObjectiveCompleted?.Invoke(index, placements[index].kind);
                    if (index < objectiveViews.Length && objectiveViews[index] != null)
                    {
                        objectiveViews[index].transform.SetParent(payload, false);
                        objectiveViews[index].transform.localPosition = new Vector3(-1.05f, 1.5f, -.4f);
                        objectiveViews[index].transform.localRotation = Quaternion.identity;
                        objectiveViews[index].transform.localScale = Vector3.one * .8f;
                    }
                    return;
                }
            }
        }

        private void HandleEnemyRemoved(int waveId, int enemyId)
        {
            if (mission == null || !mission.IsAuthority || enemyId != PayloadWaveSpawner.SideCommanderEnemyId)
                return;
            for (var index = 0; index < Count; index++)
                if (placements[index].kind == SideMissionKind.Commander &&
                    states[index] == SideMissionState.Available)
                {
                    states[index] = SideMissionState.Completed;
                    ObjectiveCompleted?.Invoke(index, placements[index].kind);
                    surgePending = true;
                    if (index < siteMarkers.Length && siteMarkers[index] != null)
                        siteMarkers[index].SetActive(false);
                    waves.ReleaseSideCommanderGroup();
                    TryStartSurge();
                    break;
                }
        }

        private void TryStartSurge()
        {
            if (waves == null || payload == null || mission.RoutePlan == null) return;
            var routeDistance = mission.Snapshot.RouteDistance;
            var direction = mission.RoutePlan.EvaluateDirection(routeDistance);
            if (!waves.SpawnTravelGroup(
                    new PayloadEscortEncounter(3002, 10, PayloadAssaultPattern.RearPursuit),
                    payload.position, direction, missionBounds, mission.CargoInteractionRadius,
                    Camera.main, true)) return;
            surgePending = false;
            surgeTriggered = true;
        }

        private static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            a.y = b.y = 0f;
            return Vector3.Distance(a, b);
        }
    }
}
