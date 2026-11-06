using System.Collections.Generic;
using UnityEngine;

namespace AlmostDelivered.Gameplay.Payload
{
    public sealed class PayloadNavigationIndicator : MonoBehaviour
    {
        [SerializeField] private PayloadMissionController mission;
        [SerializeField] private List<Transform> relayTargets = new List<Transform>();
        private bool subscribed;

        public Transform ActiveTarget { get; private set; }

        public void Configure(PayloadMissionController missionController, IReadOnlyList<Transform> targets)
        {
            Unsubscribe();
            mission = missionController;
            relayTargets.Clear();
            if (targets != null)
            {
                for (var index = 0; index < targets.Count; index++)
                {
                    if (targets[index] != null)
                    {
                        relayTargets.Add(targets[index]);
                    }
                }
            }

            Subscribe();
            Refresh();
        }

        private void OnEnable()
        {
            Subscribe();
            Refresh();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Update()
        {
            PointAtActiveTarget();
        }

        public void Refresh()
        {
            if (mission == null || relayTargets.Count == 0 || mission.Snapshot.Phase == PayloadMissionPhase.Completed)
            {
                ActiveTarget = null;
                SetChildrenActive(false);
                return;
            }

            var index = Mathf.Clamp(mission.Snapshot.NextCheckpointIndex, 0, relayTargets.Count - 1);
            ActiveTarget = relayTargets[index];
            SetChildrenActive(ActiveTarget != null);
            PointAtActiveTarget();
        }

        private void PointAtActiveTarget()
        {
            if (ActiveTarget == null)
            {
                return;
            }

            var direction = ActiveTarget.position - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude > .0001f)
            {
                transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
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

        private void SetChildrenActive(bool active)
        {
            for (var index = 0; index < transform.childCount; index++)
            {
                var child = transform.GetChild(index).gameObject;
                if (child.activeSelf != active)
                {
                    child.SetActive(active);
                }
            }
        }
    }
}
