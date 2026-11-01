using System;
using AlmostDelivered.Combat;
using AlmostDelivered.CommanderAI.Commander;
using AlmostDelivered.CommanderAI.Domain;
using UnityEngine;

namespace AlmostDelivered.CommanderAI.Perception
{
    [DefaultExecutionOrder(32000)]
    [RequireComponent(typeof(ActorHealth), typeof(StunState), typeof(MonsterPerception))]
    public sealed class AlertSignal : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float duration = 1f;
        private SquadController squad;
        private int reporterId;
        private ActorHealth health;
        private StunState stun;
        private MonsterPerception perception;
        private ObservableActor target;
        private float completesAt;

        public bool IsSignaling => target != null;

        private void Awake()
        {
            health = GetComponent<ActorHealth>();
            stun = GetComponent<StunState>();
            perception = GetComponent<MonsterPerception>();
        }

        private void OnEnable()
        {
            health.Died += Cancel;
            stun.StunChanged += OnStunChanged;
        }

        private void OnDisable()
        {
            Cancel();
            health.Died -= Cancel;
            stun.StunChanged -= OnStunChanged;
        }

        public void Configure(SquadController controller, int memberId, float alertDuration)
        {
            if (float.IsNaN(alertDuration) || float.IsInfinity(alertDuration) || alertDuration < 0f)
                throw new ArgumentOutOfRangeException(nameof(alertDuration));
            Cancel();
            squad = controller;
            reporterId = memberId;
            duration = alertDuration;
        }

        public void Begin(ObservableActor observedTarget)
        {
            if (IsSignaling || !CanSignal() || !perception.CanSee(observedTarget))
                return;
            target = observedTarget;
            completesAt = Time.unscaledTime + duration;
        }

        public void Cancel() => target = null;

        private void OnStunChanged(bool isStunned)
        {
            if (isStunned)
                Cancel();
        }

        private bool CanSignal() => isActiveAndEnabled && squad != null &&
            squad.State == SquadAlertState.Unaware && squad.IsLivingMember(reporterId) &&
            !health.IsDead && !stun.IsStunned && perception.isActiveAndEnabled;

        private void LateUpdate()
        {
            if (!IsSignaling)
                return;
            if (!CanSignal() || !perception.CanSee(target))
            {
                Cancel();
                return;
            }
            if (Time.unscaledTime < completesAt)
                return;

            // Commit after this frame's Update/coroutine cancellations and normal LateUpdates.
            var observedTarget = target;
            Cancel();
            if (squad.TryCompleteAlert(reporterId))
                perception.Observe(observedTarget);
        }
    }
}
