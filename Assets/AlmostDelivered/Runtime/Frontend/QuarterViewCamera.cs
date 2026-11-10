using UnityEngine;

namespace AlmostDelivered.Frontend
{
    public sealed class QuarterViewCamera : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 offset = new Vector3(0f, 16f, -16f);
        [SerializeField] private Vector3 focusOffset = Vector3.zero;
        private float impactShakeRemaining;
        private float impactShakeDuration;
        private float impactShakeStrength;

        public bool IsImpactShaking => impactShakeRemaining > 0f;

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
            if (impactShakeRemaining <= 0f || target == null) return;
            impactShakeRemaining = Mathf.Max(0f, impactShakeRemaining - Time.deltaTime);
            var fade = impactShakeDuration > 0f ? impactShakeRemaining / impactShakeDuration : 0f;
            var pulse = Time.unscaledTime * 95f;
            transform.position += transform.right * (Mathf.Sin(pulse) * impactShakeStrength * fade)
                + transform.up * (Mathf.Sin(pulse * 1.37f) * impactShakeStrength * .45f * fade);
        }

        public void PlayImpactShake(float strength = .12f, float duration = .12f)
        {
            impactShakeStrength = Mathf.Max(0f, strength);
            impactShakeDuration = Mathf.Max(0f, duration);
            impactShakeRemaining = impactShakeDuration;
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
