using System;
using AlmostDelivered.Combat;
using AlmostDelivered.CommanderAI.Domain;
using AlmostDelivered.CommanderAI.Tactics;
using UnityEngine;
using UnityEngine.AI;

namespace AlmostDelivered.CommanderAI.Execution
{
    [RequireComponent(typeof(NavMeshAgent), typeof(ActorHealth))]
    public sealed class MonsterOrderExecutor : MonoBehaviour
    {
        [SerializeField, Min(.01f)] private float attackRange = 1.5f;
        [SerializeField, Min(.01f)] private float attackCooldown = .5f;
        [SerializeField, Min(0)] private float attackDamage = 10;
        private NavMeshAgent agent;
        private ActorHealth health;
        private TacticalSlotAllocator allocator;
        private SquadKnowledge knowledge;
        private int memberId;
        private bool reassignmentRequested;
        private float nextAttackTime;
        private NavMeshPath path;

        public MonsterOrder CurrentOrder { get; private set; }
        public event Action<int> ReassignmentRequested;
        public event Action<int> LocalAttack;

        private void Awake() { agent = GetComponent<NavMeshAgent>(); health = GetComponent<ActorHealth>(); path = new NavMeshPath(); }
        private void OnEnable() => health.Died += Cancel;
        private void OnDisable() { Cancel(); health.Died -= Cancel; }

        public void Configure(int id, TacticalSlotAllocator slots, SquadKnowledge squadKnowledge)
        {
            if (id <= 0) throw new ArgumentOutOfRangeException(nameof(id));
            if (slots == null) throw new ArgumentNullException(nameof(slots));
            if (squadKnowledge == null) throw new ArgumentNullException(nameof(squadKnowledge));
            Cancel(); memberId = id; allocator = slots; knowledge = squadKnowledge;
        }

        public void Assign(in MonsterOrder order)
        {
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

        private void Update()
        {
            if (health.IsDead) { Cancel(); return; }
            switch (CurrentOrder.Type)
            {
                case MonsterOrderType.None: return;
                case MonsterOrderType.MoveToSlot:
                    if (!ValidAnchor()) { RequestOrFallback(); return; }
                    ExecuteMove(CurrentOrder.Anchor.transform.position, true);
                    break;
                case MonsterOrderType.HoldSlot:
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

        private bool ValidAnchor() => CurrentOrder.Anchor != null && CurrentOrder.Anchor.isActiveAndEnabled;

        private void ExecuteMove(Vector3 destination, bool holdOnArrival)
        {
            var result = Move(destination);
            if (result < 0) { RequestOrFallback(); return; }
            reassignmentRequested = false;
            if (result == 1 && holdOnArrival)
                CurrentOrder = new MonsterOrder(MonsterOrderType.HoldSlot, CurrentOrder.Anchor, destination);
        }

        // -1 = unreachable, 0 = moving, 1 = arrived.
        private int Move(Vector3 destination)
        {
            if (Vector3.Distance(transform.position, destination) <= Mathf.Max(.2f, agent.stoppingDistance + .05f))
            { Stop(); return 1; }
            if (!agent.enabled || !agent.isOnNavMesh) return -1;
            if (!agent.CalculatePath(destination, path) || path.status != NavMeshPathStatus.PathComplete) return -1;
            agent.isStopped = false;
            return agent.SetDestination(destination) ? 0 : -1;
        }

        private bool TryLocalAttack(int actorId)
        {
            foreach (var hit in Physics.OverlapSphere(transform.position, attackRange))
            {
                var actor = hit.GetComponentInParent<ObservableActor>();
                if (actor == null || !actor.isActiveAndEnabled || actor.ActorId != actorId || actor.gameObject == gameObject ||
                    Vector3.Distance(transform.position, actor.transform.position) > attackRange) continue;
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
