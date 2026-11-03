using UnityEngine;

namespace AlmostDelivered.Frontend
{
    public sealed class QuarterViewCamera : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 offset = new Vector3(0f, 12f, -9f);

        public Transform Target
        {
            get => target;
            set => target = value;
        }

        public Vector3 Offset
        {
            get => offset;
            set => offset = value;
        }

        private void LateUpdate()
        {
            SnapToTarget();
        }

        public void SnapToTarget()
        {
            if (target == null)
            {
                return;
            }

            transform.position = target.position + offset;
            transform.LookAt(target.position, Vector3.up);
        }
    }
}
