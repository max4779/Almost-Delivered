using UnityEngine;

namespace AlmostDelivered.Frontend
{
    public sealed class QuarterViewCamera : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 offset = new Vector3(0f, 16f, -16f);
        [SerializeField] private Vector3 focusOffset = Vector3.zero;

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

        public Vector3 FocusOffset
        {
            get => focusOffset;
            set => focusOffset = value;
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

            var focusPoint = target.position + focusOffset;
            transform.position = focusPoint + offset;
            transform.LookAt(focusPoint, Vector3.up);
        }
    }
}
