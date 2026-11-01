using System;
using System.Collections.Generic;
using AlmostDelivered.Combat;
using AlmostDelivered.CommanderAI.Domain;
using AlmostDelivered.CommanderAI.Execution;
using UnityEngine;

namespace AlmostDelivered.CommanderAI.Commander
{
    public sealed class SquadController : MonoBehaviour
    {
        private readonly Dictionary<int, ActorHealth> members = new Dictionary<int, ActorHealth>();
        private readonly Dictionary<int, SquadMember> tacticalMembers = new Dictionary<int, SquadMember>();
        [SerializeField] private int confusionSeed;
        public event Action Confused;

        public SquadAlertState State { get; private set; } = SquadAlertState.Unaware;
        public SquadKnowledge Knowledge { get; } = new SquadKnowledge();
        public IReadOnlyList<SquadMember> LivingMembers
        {
            get
            {
                var result = new List<SquadMember>();
                foreach (var member in tacticalMembers.Values)
                    if (IsLivingMember(member.Id) && member.Health.isActiveAndEnabled && member.Executor != null) result.Add(member);
                result.Sort((left, right) => left.Id.CompareTo(right.Id));
                return result.AsReadOnly();
            }
        }

        public bool RegisterMember(int id, MonsterRole role, ActorHealth health, MonsterOrderExecutor executor)
        {
            if (executor == null || executor.gameObject != health?.gameObject || !RegisterMember(id, health)) return false;
            tacticalMembers[id] = new SquadMember(id, role, health, executor);
            if (State == SquadAlertState.Confused) executor.EnterConfused(ConfusionTendencies.Choose(role, id, confusionSeed));
            return true;
        }

        public bool RegisterMember(int reporterId, ActorHealth health)
        {
            if (reporterId <= 0 || health == null || health.IsDead)
                return false;
            if (members.TryGetValue(reporterId, out var existing) && existing != null && existing != health)
                return false;
            members[reporterId] = health;
            return true;
        }

        public void UnregisterMember(int reporterId) { members.Remove(reporterId); tacticalMembers.Remove(reporterId); }

        public bool IsLivingMember(int reporterId) =>
            members.TryGetValue(reporterId, out var health) && health != null && !health.IsDead;

        public bool TryCompleteAlert(int reporterId)
        {
            if (State != SquadAlertState.Unaware || !IsLivingMember(reporterId))
                return false;
            State = SquadAlertState.Alerted;
            return true;
        }

        public bool Report(in ObservationSample sample)
        {
            if (State != SquadAlertState.Alerted || !IsLivingMember(sample.ReporterId))
                return false;
            return Knowledge.Merge(in sample);
        }

        public void EnterConfused()
        {
            if (State == SquadAlertState.Confused) return;
            State = SquadAlertState.Confused;
            // Include inactive survivors so re-enabling a unit cannot restore an old organized order.
            foreach (var member in tacticalMembers.Values)
                if (IsLivingMember(member.Id) && member.Executor != null)
                    member.Executor.EnterConfused(ConfusionTendencies.Choose(member.Role, member.Id, confusionSeed));
            Confused?.Invoke();
        }
    }

    public readonly struct SquadMember
    {
        public int Id { get; }
        public MonsterRole Role { get; }
        public ActorHealth Health { get; }
        public MonsterOrderExecutor Executor { get; }
        public SquadMember(int id, MonsterRole role, ActorHealth health, MonsterOrderExecutor executor)
        { Id = id; Role = role; Health = health; Executor = executor; }
    }
}
