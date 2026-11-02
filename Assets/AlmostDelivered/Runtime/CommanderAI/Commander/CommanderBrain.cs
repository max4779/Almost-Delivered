using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using AlmostDelivered.Combat;
using AlmostDelivered.CommanderAI.Domain;
using AlmostDelivered.CommanderAI.Tactics;
using UnityEngine;
using Unity.Profiling;

namespace AlmostDelivered.CommanderAI.Commander
{
    public sealed class CommanderBrain : MonoBehaviour
    {
        private static readonly ProfilerMarker TickMarker = new ProfilerMarker("CommanderAI.CommandTick");
        [SerializeField, Min(.01f)] private float memorySecondsToZero = 10f;
        [SerializeField, Range(0, 128)] private int evaluationHistoryCapacity = 128;
        private SquadController squad;
        private ActorHealth commander;
        private CommanderDifficultyProfile profile;
        private readonly List<UtilityTactic> tactics = new List<UtilityTactic>();
        private readonly List<TacticalAnchor> anchors = new List<TacticalAnchor>();
        private readonly List<TacticEvaluation> history = new List<TacticEvaluation>();
        private readonly Dictionary<string, TacticEvaluation> latest = new Dictionary<string, TacticEvaluation>();
        private readonly Dictionary<int, SquadMember> configuredMembers = new Dictionary<int, SquadMember>();
        private readonly Dictionary<int, Action> memberDeathHandlers = new Dictionary<int, Action>();
        private readonly Dictionary<int, TacticalRoleRequest> activeRequests = new Dictionary<int, TacticalRoleRequest>();
        private readonly HashSet<int> retriedMembers = new HashSet<int>();
        private readonly List<string> allocationFailures = new List<string>();
        private TacticalSlotAllocator allocator;
        private Vector3 objective;
        private float nextCycle = float.NegativeInfinity;
        private float activatedAt;
        private float lastTick = float.NegativeInfinity;
        private UtilityTactic pendingTactic;
        private TacticalContext pendingContext;
        public TacticEvaluation PendingEvaluation { get; private set; }
        public TacticEvaluation ActiveEvaluation { get; private set; }
        public IReadOnlyList<TacticEvaluation> EvaluationHistory => history.AsReadOnly();
        public IReadOnlyDictionary<string, TacticEvaluation> LatestEvaluations => new ReadOnlyDictionary<string, TacticEvaluation>(latest);
        public int CommandCycleCount { get; private set; }
        public int EvaluationHistoryCapacity
        {
            get => evaluationHistoryCapacity;
            set { evaluationHistoryCapacity = Mathf.Clamp(value, 0, 128); TrimHistory(); }
        }
        public IReadOnlyList<string> AllocationFailures => allocationFailures.AsReadOnly();
        public CommandTelegraph Telegraph { get; private set; }
        public float NextCommandTime => nextCycle;
        public string DecisionReason { get; private set; } = "waiting for completed alert";

        public void Configure(SquadController squad, ActorHealth commander, CommanderDifficultyProfile profile,
            IEnumerable<UtilityTactic> tactics, IEnumerable<TacticalAnchor> anchors, TacticalSlotAllocator allocator,
            Vector3 objectivePosition)
        {
            Unsubscribe();
            this.allocator?.ReleaseAll();
            this.squad = squad ?? throw new ArgumentNullException(nameof(squad));
            this.commander = commander ?? throw new ArgumentNullException(nameof(commander));
            this.profile = profile ?? throw new ArgumentNullException(nameof(profile));
            this.allocator = allocator ?? throw new ArgumentNullException(nameof(allocator));
            this.tactics.Clear(); this.tactics.AddRange(tactics ?? throw new ArgumentNullException(nameof(tactics)));
            this.anchors.Clear(); this.anchors.AddRange(anchors ?? throw new ArgumentNullException(nameof(anchors)));
            objective = objectivePosition;
            Telegraph = GetComponent<CommandTelegraph>() ?? gameObject.AddComponent<CommandTelegraph>();
            Telegraph.Cancel(); PendingEvaluation = null; ActiveEvaluation = null; pendingTactic = null;
            nextCycle = lastTick = float.NegativeInfinity; history.Clear(); latest.Clear(); CommandCycleCount = 0; allocationFailures.Clear();
            DecisionReason = "waiting for completed alert";
            ConfigureMembers();
            commander.Died += CommanderDied;
            squad.Confused += ClearOrganization;
            if (commander.IsDead) squad.EnterConfused();
            if (squad.State == SquadAlertState.Confused) ClearOrganization();
        }

        private void CommanderDied() => squad.EnterConfused();
        private void ClearOrganization()
        {
            PendingEvaluation = null; ActiveEvaluation = null; pendingTactic = null;
            Telegraph?.Cancel(); allocator?.ReleaseAll();
            DetachMembers();
            DecisionReason = "coordination ended permanently";
        }
        private void Unsubscribe()
        {
            if (commander != null) commander.Died -= CommanderDied;
            if (squad != null) squad.Confused -= ClearOrganization;
            DetachMembers();
        }
        private void OnDestroy() { Unsubscribe(); ClearOrganization(); }

        private void Update() { if (squad != null) Tick(Time.unscaledTime); }

        public void Tick(float now)
        {
            using var tickSample = TickMarker.Auto();
            if (!TacticalContext.Finite(now) || now < 0 || now < lastTick) throw new ArgumentOutOfRangeException(nameof(now));
            lastTick = now;
            if (squad == null) return;
            if (commander == null || commander.IsDead) { squad.EnterConfused(); return; }
            if (squad.State != SquadAlertState.Alerted) return;
            if (PendingEvaluation != null)
            {
                if (!Telegraph.IsVisible) { PendingEvaluation = null; pendingTactic = null; }
                else if (Telegraph.IsComplete(now)) Activate(now);
            }
            if (now < nextCycle) return;
            nextCycle = now + Mathf.Max(.01f, profile.CommandCycleSeconds);
            CommandCycleCount++; latest.Clear();
            squad.Knowledge.Decay(now, memorySecondsToZero);
            ConfigureMembers();
            var context = BuildContext(now);
            UtilityTactic winner = null;
            TacticEvaluation best = null, current = null;
            foreach (var tactic in tactics)
            {
                if (tactic == null || !Available(tactic.TacticId)) continue;
                var result = tactic.Evaluate(in context);
                latest[result.TacticId] = result;
                if (evaluationHistoryCapacity > 0) history.Add(result);
                TrimHistory();
                if (ActiveEvaluation != null && result.TacticId == ActiveEvaluation.TacticId) current = result;
                if (!result.IsValid || result.TotalScore <= tactic.MinimumActivationScore || tactic.Playbook == null) continue;
                if (best == null || result.TotalScore > best.TotalScore ||
                    (result.TotalScore == best.TotalScore && string.CompareOrdinal(result.TacticId, best.TacticId) < 0))
                { best = result; winner = tactic; }
            }
            // Keep invalidation visible rather than retaining a stale winning score.
            if (ActiveEvaluation != null) ActiveEvaluation = current;
            if (PendingEvaluation != null) { DecisionReason = "telegraph already in progress"; return; }
            if (best == null)
            {
                DecisionReason = "no valid tactic above activation threshold; fallback orders";
                ActiveEvaluation = null; allocator.ReleaseAll();
                activeRequests.Clear(); retriedMembers.Clear();
                foreach (var member in squad.LivingMembers) AssignFallback(member, in context);
                return;
            }
            var currentForSwitch = ActiveEvaluation != null && ActiveEvaluation.IsValid ? ActiveEvaluation : null;
            if (!TacticSwitchPolicy.ShouldSwitch(currentForSwitch, best, context.TacticAge, winner.MinimumActivationScore,
                profile.MinimumHoldTime, profile.SwitchMargin))
            {
                // A newly available member must get an executable order even when the tactic is retained.
                DecisionReason = currentForSwitch?.TacticId == best.TacticId ? "current tactic remains the winner" :
                    context.TacticAge < profile.MinimumHoldTime ? "minimum hold still active" : "candidate does not exceed switch margin";
                foreach (var member in squad.LivingMembers)
                    if (member.Executor.CurrentOrder.Type == MonsterOrderType.None) AssignFallback(member, in context);
                return;
            }
            PendingEvaluation = best; pendingTactic = winner; pendingContext = context;
            DecisionReason = "selected " + best.TacticId + "; awaiting telegraph completion";
            Telegraph.Begin(best.TacticId, now, profile.TelegraphDuration);
        }

        private bool Available(string id)
        { foreach (var available in profile.AvailableTacticIds) if (available == id) return true; return false; }

        private void ConfigureMembers()
        {
            foreach (var member in squad.LivingMembers)
            {
                allocator.RegisterMember(member.Id, member.Role, member.Health);
                // Reconfiguration cancels orders, so configure each executor only when first attached to this brain.
                if (!configuredMembers.ContainsKey(member.Id))
                {
                    member.Executor.Configure(member.Id, allocator, squad.Knowledge);
                    configuredMembers.Add(member.Id, member);
                    member.Executor.ReassignmentRequested += ReassignMember;
                    var id = member.Id;
                    Action died = () => DetachMember(id);
                    memberDeathHandlers.Add(id, died); member.Health.Died += died;
                }
            }
        }

        private void TrimHistory()
        {
            var capacity = Mathf.Clamp(evaluationHistoryCapacity, 0, 128);
            if (history.Count > capacity) history.RemoveRange(0, history.Count - capacity);
        }

        private void DetachMembers()
        {
            foreach (var id in new List<int>(configuredMembers.Keys)) DetachMember(id);
            activeRequests.Clear(); retriedMembers.Clear();
        }

        private void DetachMember(int id)
        {
            if (!configuredMembers.TryGetValue(id, out var member)) return;
            if (member.Executor != null)
            {
                member.Executor.ReassignmentRequested -= ReassignMember;
                if (member.Executor.CurrentOrder.Type != MonsterOrderType.ConfusedCombat) member.Executor.Cancel();
            }
            if (member.Health != null && memberDeathHandlers.TryGetValue(id, out var died)) member.Health.Died -= died;
            allocator?.UnregisterMember(id);
            configuredMembers.Remove(id); memberDeathHandlers.Remove(id);
            activeRequests.Remove(id); retriedMembers.Remove(id);
        }

        private void ReassignMember(int id)
        {
            if (squad == null || squad.State != SquadAlertState.Alerted || commander == null || commander.IsDead ||
                !configuredMembers.TryGetValue(id, out var member) || !squad.IsLivingMember(id) ||
                member.Executor == null || !member.Executor.isActiveAndEnabled) return;
            if (!activeRequests.TryGetValue(id, out var request)) return;
            if (retriedMembers.Add(id))
            {
                if (allocator.TryAllocate(id, member.Role, request.Slot, out var replacement, out var reason))
                {
                    member.Executor.Assign(new MonsterOrder(MonsterOrderType.MoveToSlot, replacement, targetActorId: request.TargetActorId));
                    return;
                }
                allocationFailures.Add("Member " + id + " reassignment / " + request.Slot.Tag + ": " + reason);
            }
            // One allocation attempt per active playbook request. A failed replacement
            // cannot restart a per-frame allocation loop, even if movement later fails.
            activeRequests.Remove(id);
            var context = BuildContext(lastTick);
            AssignFallback(member, in context);
        }

        private TacticalContext BuildContext(float now)
        {
            var roles = new Dictionary<MonsterRole, int>();
            var slots = new Dictionary<TacticalAnchorTag, int>();
            float health = 0, maximum = 0;
            foreach (var member in squad.LivingMembers)
            {
                roles.TryGetValue(member.Role, out var count); roles[member.Role] = count + 1;
                health += member.Health.CurrentHealth; maximum += member.Health.MaxHealth;
            }
            foreach (var anchor in anchors)
            {
                if (anchor == null || !anchor.isActiveAndEnabled) continue;
                foreach (var member in squad.LivingMembers)
                {
                    if (!allocator.CanUseAnchor(member.Id, member.Role, anchor)) continue;
                    slots.TryGetValue(anchor.Tag, out var count); slots[anchor.Tag] = count + anchor.Capacity; break;
                }
            }
            return new TacticalContext(squad.Knowledge.Snapshot, roles, objective, slots, maximum > 0 ? health / maximum : 0,
                ActiveEvaluation?.TacticId, ActiveEvaluation == null ? 0 : now - activatedAt);
        }

        private void Activate(float now)
        {
            // Members may register between command cycles while the telegraph is visible.
            ConfigureMembers();
            allocationFailures.Clear();
            activeRequests.Clear(); retriedMembers.Clear();
            allocator.ReleaseAll();
            foreach (var member in squad.LivingMembers) member.Executor.Cancel();
            var used = new HashSet<int>();
            foreach (var request in pendingTactic.Playbook.BuildRequests(in pendingContext))
                foreach (var member in squad.LivingMembers)
                {
                    if (used.Contains(member.Id) || member.Role != request.Role) continue;
                    used.Add(member.Id);
                    if (allocator.TryAllocate(member.Id, member.Role, request.Slot, out var anchor, out var failureReason))
                    {
                        activeRequests[member.Id] = request;
                        member.Executor.Assign(new MonsterOrder(MonsterOrderType.MoveToSlot, anchor, targetActorId: request.TargetActorId));
                    }
                    else
                    {
                        allocationFailures.Add("Member " + member.Id + " / " + request.Slot.Tag + ": " + failureReason);
                        AssignFallback(member, in pendingContext);
                    }
                    break;
                }
            foreach (var member in squad.LivingMembers)
                if (!used.Contains(member.Id))
                {
                    allocationFailures.Add("Member " + member.Id + ": no playbook request for " + member.Role + ".");
                    AssignFallback(member, in pendingContext);
                }
            ActiveEvaluation = PendingEvaluation; activatedAt = now;
            PendingEvaluation = null; pendingTactic = null; Telegraph.Cancel();
            DecisionReason = "activated " + ActiveEvaluation.TacticId + "; holding until next command cycle";
        }

        private static void AssignFallback(SquadMember member, in TacticalContext context)
        {
            member.Executor.Cancel();
            foreach (var memory in context.Knowledge)
                if (memory.Confidence > 0)
                { member.Executor.Assign(new MonsterOrder(MonsterOrderType.AttackObservedTarget, targetActorId: memory.ActorId)); return; }
            member.Executor.Assign(new MonsterOrder(MonsterOrderType.HoldSlot, position: member.Health.transform.position));
        }
    }
}
