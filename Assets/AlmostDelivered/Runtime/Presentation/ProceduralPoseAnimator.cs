using System;
using UnityEngine;

namespace AlmostDelivered.Presentation
{
    /// <summary>Animates visual pivots from a read-only gameplay motion source.</summary>
    public sealed class ProceduralPoseAnimator : MonoBehaviour
    {
        private const float AttackDuration = .35f;
        private const float HitDuration = .25f;
        private const float IdleDamping = 14f;
        [SerializeField] private NeutralPose body;
        [SerializeField] private NeutralPose leftArm;
        [SerializeField] private NeutralPose rightArm;
        private Transform motionSource;
        private Vector3 previousSourcePosition;
        private float movementPhase;
        private float attackRemaining;
        private float hitRemaining;

        public void Configure(Transform body, Transform leftArm, Transform rightArm)
        {
            if (body == null) throw new ArgumentNullException(nameof(body));
            ValidatePivot(body);
            ValidatePivot(leftArm);
            ValidatePivot(rightArm);
            RestoreNeutral();
            this.body = new NeutralPose(body);
            this.leftArm = new NeutralPose(leftArm);
            this.rightArm = new NeutralPose(rightArm);
            ResetMotion();
        }

        public void BindMotionSource(Transform source)
        {
            if (source != null && source.IsChildOf(transform))
                throw new ArgumentException("Motion source must be outside the visual hierarchy.", nameof(source));
            motionSource = source;
            ResetMotion();
        }

        public void PlayAttack()
        {
            if (isActiveAndEnabled) attackRemaining = AttackDuration;
        }

        public void PlayHit()
        {
            if (isActiveAndEnabled) hitRemaining = HitDuration;
        }

        private void LateUpdate()
        {
            var moving = false;
            if (motionSource != null)
            {
                var current = motionSource.position;
                var displacement = current - previousSourcePosition;
                displacement.y = 0f;
                moving = displacement.sqrMagnitude > .000001f;
                previousSourcePosition = current;
            }

            var delta = Time.deltaTime;
            if (moving) movementPhase = Mathf.Repeat(movementPhase + delta * 10f, Mathf.PI * 2f);
            var swing = moving ? Mathf.Sin(movementPhase) : 0f;
            attackRemaining = Mathf.Max(0f, attackRemaining - delta);
            hitRemaining = Mathf.Max(0f, hitRemaining - delta);
            var attack = Mathf.Sin(Mathf.PI * attackRemaining / AttackDuration);
            var hit = Mathf.Sin(Mathf.PI * hitRemaining / HitDuration);
            var blend = 1f - Mathf.Exp(-IdleDamping * delta);

            body.Apply(new Vector3(0f, Mathf.Abs(swing) * .025f, -hit * .035f),
                new Vector3(-hit * 14f, 0f, 0f), blend);
            leftArm.Apply(Vector3.zero, new Vector3(swing * 25f - attack * 15f, 0f, 0f), blend);
            rightArm.Apply(Vector3.zero, new Vector3(-swing * 25f - attack * 75f, 0f, 0f), blend);
        }

        private void OnEnable() => ResetMotion();

        private void OnDisable()
        {
            RestoreNeutral();
            ResetMotion();
        }

        private void ValidatePivot(Transform pivot)
        {
            if (pivot != null && (pivot == transform || !pivot.IsChildOf(transform)))
                throw new ArgumentException("Pose pivots must be children of the animator's visual root.", nameof(pivot));
        }

        private void ResetMotion()
        {
            previousSourcePosition = motionSource != null ? motionSource.position : Vector3.zero;
            movementPhase = 0f;
            attackRemaining = 0f;
            hitRemaining = 0f;
        }

        private void RestoreNeutral()
        {
            body.Restore();
            leftArm.Restore();
            rightArm.Restore();
        }

        [Serializable]
        private struct NeutralPose
        {
            [SerializeField] private Transform pivot;
            [SerializeField] private Vector3 position;
            [SerializeField] private Quaternion rotation;
            [SerializeField] private Vector3 scale;

            public NeutralPose(Transform pivot)
            {
                this.pivot = pivot;
                position = pivot != null ? pivot.localPosition : Vector3.zero;
                rotation = pivot != null ? pivot.localRotation : Quaternion.identity;
                scale = pivot != null ? pivot.localScale : Vector3.one;
            }

            public void Apply(Vector3 offset, Vector3 angles, float blend)
            {
                if (pivot == null) return;
                pivot.localPosition = Vector3.Lerp(pivot.localPosition, position + offset, blend);
                pivot.localRotation = Quaternion.Slerp(pivot.localRotation, rotation * Quaternion.Euler(angles), blend);
                pivot.localScale = Vector3.Lerp(pivot.localScale, scale, blend);
            }

            public void Restore()
            {
                if (pivot == null) return;
                pivot.localPosition = position;
                pivot.localRotation = rotation;
                pivot.localScale = scale;
            }
        }
    }
}
