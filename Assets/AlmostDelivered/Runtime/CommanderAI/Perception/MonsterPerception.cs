using System;
using System.Collections.Generic;
using AlmostDelivered.Combat;
using AlmostDelivered.CommanderAI.Commander;
using AlmostDelivered.CommanderAI.Domain;
using UnityEngine;
using Unity.Profiling;

namespace AlmostDelivered.CommanderAI.Perception
{
    [RequireComponent(typeof(ActorHealth), typeof(StunState))]
    public sealed class MonsterPerception : MonoBehaviour
    {
        private static readonly ProfilerMarker BatchMarker = new ProfilerMarker("CommanderAI.PerceptionBatch");
        [SerializeField, Min(0f)] private float sightRadius = 12f;
        [SerializeField, Range(0f, 360f)] private float fieldOfViewAngle = 120f;
        [SerializeField] private LayerMask obstructionMask = ~0;
        [SerializeField] private LayerMask targetMask = ~0;
        [SerializeField, Min(0.01f)] private float sensingInterval = 0.2f;
        private SquadController squad;
        private int reporterId;
        private float nextSenseTime;
        private ActorHealth health;
        private StunState stun;
        private readonly HashSet<int> sensedActors = new HashSet<int>();

        public float SightRadius { get => sightRadius; set => sightRadius = Mathf.Max(0f, value); }
        public float FieldOfViewAngle { get => fieldOfViewAngle; set => fieldOfViewAngle = Mathf.Clamp(value, 0f, 360f); }
        public LayerMask ObstructionMask { get => obstructionMask; set => obstructionMask = value; }
        public LayerMask TargetMask { get => targetMask; set => targetMask = value; }
        public float SensingInterval { get => sensingInterval; set => sensingInterval = Mathf.Max(0.01f, value); }
        public event Action<ObservationSample> ObservationProduced;

        private void Awake()
        {
            health = GetComponent<ActorHealth>();
            stun = GetComponent<StunState>();
        }

        public void Configure(SquadController controller, int memberId)
        {
            squad = controller;
            reporterId = memberId;
            // A stable phase spreads member queries without a global perception registry.
            nextSenseTime = Time.unscaledTime + sensingInterval * ((memberId & 15) + 1) / 17f;
        }

        private void Update()
        {
            if (Time.unscaledTime < nextSenseTime)
                return;
            nextSenseTime = Time.unscaledTime + sensingInterval;
            Sense();
        }

        public void Sense()
        {
            if (!CanReport() || squad.State == SquadAlertState.Confused)
                return;
            using var batchSample = BatchMarker.Auto();
            sensedActors.Clear();
            foreach (var collider in Physics.OverlapSphere(transform.position, sightRadius,
                         targetMask, QueryTriggerInteraction.Collide))
            {
                var target = collider.GetComponentInParent<ObservableActor>();
                if (target == null || !sensedActors.Add(target.ActorId) || !CanSee(target))
                    continue;
                if (squad.State == SquadAlertState.Unaware)
                {
                    var signal = GetComponent<AlertSignal>();
                    if (signal != null)
                        signal.Begin(target);
                }
                else
                {
                    Observe(target);
                }
            }
        }

        public bool CanSee(ObservableActor target)
        {
            if (target == null || !target.isActiveAndEnabled || target.transform.IsChildOf(transform))
                return false;
            var targetHealth = target.GetComponent<ActorHealth>();
            if (targetHealth != null && targetHealth.IsDead)
                return false;
            var offset = target.transform.position - transform.position;
            if (offset.sqrMagnitude > sightRadius * sightRadius ||
                Vector3.Angle(transform.forward, offset) > fieldOfViewAngle * 0.5f)
                return false;
            foreach (var hit in Physics.RaycastAll(transform.position, offset.normalized, offset.magnitude,
                         obstructionMask, QueryTriggerInteraction.Ignore))
            {
                if (!hit.transform.IsChildOf(transform) && !hit.transform.IsChildOf(target.transform))
                    return false;
            }
            return true;
        }

        internal void Observe(ObservableActor target)
        {
            if (!CanReport() || squad.State != SquadAlertState.Alerted || !CanSee(target))
                return;
            var sample = new ObservationSample(target.ActorId, target.transform.position, target.Velocity,
                Time.unscaledTime, 1f, target.ObservedTags, reporterId);
            // This data boundary is the only path from local sight into squad decisions.
            squad.Report(in sample);
            ObservationProduced?.Invoke(sample);
        }

        private bool CanReport() => isActiveAndEnabled && squad != null &&
            health != null && !health.IsDead && stun != null && !stun.IsStunned && squad.IsLivingMember(reporterId);
    }
}
