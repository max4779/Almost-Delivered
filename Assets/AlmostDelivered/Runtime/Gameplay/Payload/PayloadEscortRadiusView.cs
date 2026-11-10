using UnityEngine;

namespace AlmostDelivered.Gameplay.Payload
{
    [RequireComponent(typeof(LineRenderer))]
    public sealed class PayloadEscortRadiusView : MonoBehaviour
    {
        private const int SegmentCount = 64;

        [SerializeField] private PayloadMissionController mission;
        [SerializeField, Min(0f)] private float radius = 4.5f;
        [SerializeField] private LineRenderer line;
        private bool subscribed;

        public float Radius => radius;

        public void Configure(PayloadMissionController missionController, float escortRadius, Material material)
        {
            Unsubscribe();
            mission = missionController;
            radius = Mathf.Max(0f, escortRadius);
            EnsureLine();
            if (material != null)
            {
                line.sharedMaterial = material;
            }

            DrawRing();
            Subscribe();
            Refresh();
        }

        private void OnEnable()
        {
            EnsureLine();
            DrawRing();
            Subscribe();
            Refresh();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        public void Refresh()
        {
            EnsureLine();
            var color = new Color(.2f, .75f, .9f, .45f);
            if (mission != null)
            {
                var snapshot = mission.Snapshot;
                line.enabled = snapshot.Phase != PayloadMissionPhase.Deploying &&
                               snapshot.Phase != PayloadMissionPhase.Activating;
                if (snapshot.Phase == PayloadMissionPhase.Recovering)
                    color = new Color(.15f, .95f, 1f, .9f);
                else if (snapshot.Phase == PayloadMissionPhase.CheckpointDefense || snapshot.Phase == PayloadMissionPhase.FinalDefense)
                    color = new Color(1f, .65f, .15f, .85f);
                else if (snapshot.IsRelayChoicePending)
                    color = new Color(1f, .8f, .2f, .95f);
                else if (snapshot.EnemiesInRange > 0)
                    color = new Color(1f, .18f, .12f, .9f);
                else if (mission.ActiveRouteOption != null)
                    color = mission.ActiveRouteOption.color;
                else if (snapshot.IsEscortActive)
                    color = new Color(.15f, .95f, 1f, .9f);
                else if (snapshot.Phase == PayloadMissionPhase.Completed)
                    color = new Color(.25f, .9f, .4f, .55f);
            }
            else
            {
                line.enabled = true;
            }

            line.startColor = color;
            line.endColor = color;
        }

        private void DrawRing()
        {
            EnsureLine();
            line.positionCount = SegmentCount;
            for (var index = 0; index < SegmentCount; index++)
            {
                var angle = index * Mathf.PI * 2f / SegmentCount;
                line.SetPosition(index, new Vector3(Mathf.Cos(angle) * radius, .08f, Mathf.Sin(angle) * radius));
            }
        }

        private void EnsureLine()
        {
            if (line == null)
            {
                line = GetComponent<LineRenderer>();
            }

            line.useWorldSpace = false;
            line.loop = true;
            line.startWidth = .12f;
            line.endWidth = .12f;
            line.numCornerVertices = 2;
            line.numCapVertices = 2;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
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
            if (subscribed && mission != null)
            {
                mission.SnapshotChanged -= HandleSnapshotChanged;
            }

            subscribed = false;
        }

        private void HandleSnapshotChanged(PayloadMissionSnapshot _)
        {
            Refresh();
        }
    }
}
