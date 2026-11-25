using System;
using AlmostDelivered.Combat;
using UnityEngine;

namespace AlmostDelivered.Gameplay.Payload
{
    public sealed class FuelDepotDirector : MonoBehaviour
    {
        [SerializeField] private PayloadMissionController mission;
        [SerializeField] private Transform player;
        [SerializeField] private Transform payload;
        [SerializeField] private FuelDepotPlacement[] depots = Array.Empty<FuelDepotPlacement>();
        [SerializeField] private GameObject[] cells = Array.Empty<GameObject>();
        [SerializeField] private GameObject[] markers = Array.Empty<GameObject>();
        [SerializeField, Min(1f)] private float fuelPerCellSeconds = 150f;
        private bool[] collected = Array.Empty<bool>();
        private int carryingIndex = -1;

        public int Count => depots?.Length ?? 0;
        public int CarryingIndex => carryingIndex;
        public FuelDepotPlacement PlacementAt(int index) => depots[index];
        public bool IsCollected(int index) => collected != null && index < collected.Length && collected[index];

        public void Configure(PayloadMissionController missionController, Transform playerTarget,
            Transform cargoTarget, FuelDepotPlacement[] placements, GameObject[] fuelCells,
            GameObject[] siteMarkers, float secondsPerCell)
        {
            mission = missionController;
            player = playerTarget;
            payload = cargoTarget;
            depots = placements ?? Array.Empty<FuelDepotPlacement>();
            cells = fuelCells ?? Array.Empty<GameObject>();
            markers = siteMarkers ?? Array.Empty<GameObject>();
            fuelPerCellSeconds = Mathf.Max(1f, secondsPerCell);
            collected = new bool[depots.Length];
        }

        private void Start()
        {
            if (collected == null || collected.Length != Count) collected = new bool[Count];
        }

        private void Update()
        {
            if (mission == null || !mission.IsAuthority || player == null || payload == null ||
                !Input.GetKeyDown(KeyCode.E)) return;
            var phase = mission.Snapshot.Phase;
            if (phase == PayloadMissionPhase.Deploying || phase == PayloadMissionPhase.Recovering ||
                phase == PayloadMissionPhase.Activating || phase == PayloadMissionPhase.Completed ||
                phase == PayloadMissionPhase.Failed ||
                phase == PayloadMissionPhase.EvacuationTravel ||
                phase == PayloadMissionPhase.EvacuationDefense ||
                phase == PayloadMissionPhase.Escaped) return;
            var health = player.GetComponent<ActorHealth>();
            if (health != null && health.IsDead) return;

            if (carryingIndex >= 0)
            {
                if (HorizontalDistance(player.position, payload.position) > mission.CargoInteractionRadius)
                    return;
                if (mission.AddFuel(fuelPerCellSeconds) <= 0f) return;
                collected[carryingIndex] = true;
                if (carryingIndex < cells.Length && cells[carryingIndex] != null)
                    cells[carryingIndex].SetActive(false);
                carryingIndex = -1;
                return;
            }

            for (var index = 0; index < Count; index++)
            {
                if (collected[index] || HorizontalDistance(player.position, depots[index].position) > 3f)
                    continue;
                carryingIndex = index;
                if (index < markers.Length && markers[index] != null)
                    markers[index].SetActive(false);
                if (index < cells.Length && cells[index] != null)
                {
                    cells[index].transform.SetParent(player, false);
                    cells[index].transform.localPosition = new Vector3(.7f, 1.25f, -.35f);
                    cells[index].transform.localScale = Vector3.one * .65f;
                }
                return;
            }
        }

        private static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            a.y = b.y = 0f;
            return Vector3.Distance(a, b);
        }
    }
}
