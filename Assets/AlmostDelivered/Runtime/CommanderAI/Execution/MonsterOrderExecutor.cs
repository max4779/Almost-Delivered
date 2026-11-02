using System;
using AlmostDelivered.Combat;
using AlmostDelivered.CommanderAI.Domain;
using AlmostDelivered.CommanderAI.Tactics;
using UnityEngine;
using UnityEngine.AI;
using Unity.Profiling;

namespace AlmostDelivered.CommanderAI.Execution
{
    [RequireComponent(typeof(NavMeshAgent), typeof(ActorHealth))]
    public sealed class MonsterOrderExecutor : MonoBehaviour
    {
        private static readonly ProfilerMarker AssignmentMarker = new ProfilerMarker("CommanderAI.OrderReassignment");
        private static readonly ProfilerMarker PathMarker = new ProfilerMarker("CommanderAI.MovementPathRequest");
        [SerializeField, Min(.01f)] private float attackRange = 1.5f;
        [SerializeField, Min(.01f)] private float attackCooldown = .5f;
        [SerializeField, Min(0)] private float attackDamage = 10;
        [SerializeField, Min(.1f)] private float confusionSightRange = 12f;
        private NavMeshAgent agent;
        private ActorHealth health;
        private TacticalSlotAllocator allocator;
        private SquadKnowledge knowledge;
        private int memberId;
        private bool reassignmentRequested;
        private float nextAttackTime;
        private NavMeshPath path;
        private bool confused;
        private Vector3 confusionPosition;

        public MonsterOrder CurrentOrder { get; private set; }
        public ConfusionTendency Tendency { get; private set; }
        public event Action<int> ReassignmentRequested;
        public event Action<int> LocalAttack;

        private void Awake() { agent = GetComponent<NavMeshAgent>(); health = GetComponent<ActorHealth>(); path = new NavMeshPath(); }
        private void OnEnable()
        {
            health.Died += Cancel;
            if (confused && !health.IsDead) CurrentOrder = new MonsterOrder(MonsterOrderType.ConfusedCombat, position: confusionPosition);
        }
        private void OnDisable() { Cancel(); health.Died -= Cancel; }

        public void Configure(int id, TacticalSlotAllocator slots, SquadKnowledge squadKnowledge)
        {
            if (id <= 0) throw new ArgumentOutOfRangeException(nameof(id));
            if (slots == null) throw new ArgumentNullException(nameof(slots));
            if (squadKnowledge == null) throw new ArgumentNullException(nameof(squadKnowledge));
            if (health == null) Awake();
            Cancel(); memberId = id; allocator = slots; knowledge = squadKnowledge;
            if (confused && !health.IsDead) CurrentOrder = new MonsterOrder(MonsterOrderType.ConfusedCombat, position: confusionPosition);
        }

        public void Assign(in MonsterOrder order)
        {
            using var assignmentSample = AssignmentMarker.Auto();
            if (confused) return;
            Stop();
            if (health.IsDead || !isActiveAndEnabled) { Cancel(); return; }
            // Preserve a pending retry across a synchronous reassignment callback.
            CurrentOrder = order;
            if ((order.Type != MonsterOrderType.MoveToSlot && order.Type != MonsterOrderType.HoldSlot) ||
                ReferenceEquals(order.Anchor, null))
                allocator?.Release(memberId);
        }

        public void Cancel()
        {
            Stop(); allocator?.Release(memberId); CurrentOrder = default; reassignmentRequested = false;
        }

        public void EnterConfused(ConfusionTendency tendency)
        {
            if (confused) return;
            if (health == null) Awake();
            Cancel(); confused = true; Tendency = tendency;
            confusionPosition = transform.position;
            if (tendency == ConfusionTendency.SearchLastKnownPosition && knowledge != null)
            {
                var distance = float.PositiveInfinity;
                foreach (var memory in knowledge.Snapshot)
                {
                    var candidate = (memory.LastKnownPosition - transform.position).sqrMagnitude;
                    if (memory.Confidence <= 0 || candidate >= distance) continue;
                    distance = candidate; confusionPosition = memory.LastKnownPosition;
                }
            }
            if (!health.IsDead) CurrentOrder = new MonsterOrder(MonsterOrderType.ConfusedCombat, position: confusionPosition);
        }

        private void Update()
        {
            if (health.IsDead) { Cancel(); return; }
            switch (CurrentOrder.Type)
            {
                case MonsterOrderType.None: return;
                case MonsterOrderType.ConfusedCombat: ExecuteConfused(); return;
                case MonsterOrderType.MoveToSlot:
                    if (!ValidAnchor()) { RequestOrFallback(); return; }
                    AttackNearbyVisibleTarget();
                    ExecuteMove(CurrentOrder.Anchor.transform.position, true);
                    break;
                case MonsterOrderType.HoldSlot:
                    AttackNearbyVisibleTarget();
                    if (!ReferenceEquals(CurrentOrder.Anchor, null))
                    {
                        if (!ValidAnchor()) { RequestOrFallback(); return; }
                        ExecuteMove(CurrentOrder.Anchor.transform.position, false);
                    }
                    else ExecuteMove(CurrentOrder.Position, false);
                    break;
                case MonsterOrderType.SearchLastKnownPosition:
                    ExecuteMove(CurrentOrder.Position, true);
                    break;
                case MonsterOrderType.ProtectUnit:
                    if (CurrentOrder.ProtectedUnit == null || !CurrentOrder.ProtectedUnit.gameObject.activeInHierarchy)
                    { RequestOrFallback(); return; }
                    ExecuteMove(CurrentOrder.ProtectedUnit.position, false);
                    break;
                case MonsterOrderType.AttackObservedTarget:
                    if (knowledge == null || !knowledge.TryGet(CurrentOrder.TargetActorId, out var memory) || memory.Confidence <= 0)
                    { Fallback(); return; }
                    // Pursuit uses reported memory, never a live target transform outside local attack range.
                    if (!TryLocalAttack(memory.ActorId)) ExecuteMove(memory.LastKnownPosition, false);
                    else { Stop(); reassignmentRequested = false; }
                    break;
            }
        }

        private void ExecuteConfused()
        {
            var target = NearestVisibleTarget(confusionSightRange);
            if (Tendency == ConfusionTendency.RetreatWhenWounded && health.CurrentHealth / health.MaxHealth <= .35f && target != null)
            {
                var away = transform.position - target.transform.position; away.y = 0;
                if (Move(transform.position + away.normalized * 4) < 0) Stop();
                return;
            }
            if (target != null && TryLocalAttack(target.ActorId)) { Stop(); return; }
            var destination = Tendency == ConfusionTendency.NearestVisibleTarget && target != null
                ? target.transform.position : confusionPosition;
            // Autonomous failures hold locally; they never ask the dead command structure for new orders.
            if (Move(destination) < 0) Stop();
        }

        private void AttackNearbyVisibleTarget()
        {
            var target = NearestVisibleTarget(attackRange);
            if (target != null) TryLocalAttack(target.ActorId);
        }

        private ObservableActor NearestVisibleTarget(float range)
        {
            ObservableActor nearest = null;
            var best = float.PositiveInfinity;
            foreach (var hit in Physics.OverlapSphere(transform.position, range, ~0, QueryTriggerInteraction.Collide))
            {
                var actor = hit.GetComponentInParent<ObservableActor>();
                if (actor == null || !actor.isActiveAndEnabled || actor.gameObject == gameObject) continue;
                var targetHealth = actor.GetComponent<ActorHealth>();
                if (targetHealth == null || targetHealth.IsDead) continue;
                var delta = actor.transform.position - transform.position;
                var distance = delta.sqrMagnitude;
                if (distance > best || (distance == best && nearest != null && actor.ActorId >= nearest.ActorId)) continue;
                if (distance <= range * range && HasLocalSight(actor)) { nearest = actor; best = distance; }
            }
            return nearest;
        }

        private bool HasLocalSight(ObservableActor actor)
        {
            var origin = transform.position + Vector3.up * .4f;
            var delta = actor.transform.position + Vector3.up * .4f - origin;
            foreach (var obstruction in Physics.RaycastAll(origin, delta.normalized, delta.magnitude,
                         Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                if (obstruction.collider.transform.IsChildOf(transform) ||
                    obstruction.collider.GetComponentInParent<ObservableActor>() == actor) continue;
                return false;
            }
            return true;
        }

        private bool ValidAnchor() => CurrentOrder.Anchor != null && CurrentOrder.Anchor.isActiveAndEnabled;

        private void ExecuteMove(Vector3 destination, bool holdOnArrival)
        {
            var result = Move(destination);
            if (result < 0) { RequestOrFallback(); return; }
            reassignmentRequested = false;
            if (result == 1 && holdOnArrival)
            {
                if (CurrentOrder.TargetActorId > 0)
                {
                    var targetId = CurrentOrder.TargetActorId;
                    allocator?.Release(memberId);
                    CurrentOrder = new MonsterOrder(MonsterOrderType.AttackObservedTarget, targetActorId: targetId);
                }
                else CurrentOrder = new MonsterOrder(MonsterOrderType.HoldSlot, CurrentOrder.Anchor, destination);
            }
        }

        // -1 = unreachable, 0 = moving, 1 = arrived.
        private int Move(Vector3 destination)
        {
            if (Vector3.Distance(transform.position, destination) <= Mathf.Max(.2f, agent.stoppingDistance + .05f))
            { Stop(); return 1; }
            if (!agent.enabled || !agent.isOnNavMesh) return -1;
            using var pathSample = PathMarker.Auto();
            if (!agent.CalculatePath(destination, path) || path.status != NavMeshPathStatus.PathComplete) return -1;
            agent.isStopped = false;
            return agent.SetDestination(destination) ? 0 : -1;
        }

        private bool TryLocalAttack(int actorId)
        {
            foreach (var hit in Physics.OverlapSphere(transform.position, attackRange, ~0, QueryTriggerInteraction.Collide))
            {
                var actor = hit.GetComponentInParent<ObservableActor>();
                if (actor == null || !actor.isActiveAndEnabled || actor.ActorId != actorId || actor.gameObject == gameObject ||
                    Vector3.Distance(transform.position, actor.transform.position) > attackRange || !HasLocalSight(actor)) continue;
                var targetHealth = actor.GetComponent<ActorHealth>();
                if (targetHealth == null || targetHealth.IsDead) continue;
                if (Time.unscaledTime >= nextAttackTime)
                {
                    nextAttackTime = Time.unscaledTime + attackCooldown;
                    targetHealth.ApplyDamage(attackDamage);
                    LocalAttack?.Invoke(actorId);
                }
                return true;
            }
            return false;
        }

        private void RequestOrFallback()
        {
            Stop(); allocator?.Release(memberId);
            if (reassignmentRequested) { Fallback(); return; }
            reassignmentRequested = true;
            ReassignmentRequested?.Invoke(memberId);
        }

        private void Fallback()
        {
            Stop(); allocator?.Release(memberId);
            CurrentOrder = new MonsterOrder(MonsterOrderType.HoldSlot, position: transform.position);
            reassignmentRequested = false;
        }

        private void Stop()
        {
            if (agent != null && agent.enabled && agent.isOnNavMesh) { agent.isStopped = true; agent.ResetPath(); }
        }
    }
}
