using System.Collections.Generic;
using AlmostDelivered.Combat;
using AlmostDelivered.CommanderAI.Domain;
using UnityEngine;

namespace AlmostDelivered.CommanderAI.Commander
{
    public sealed class SquadController : MonoBehaviour
    {
        private readonly Dictionary<int, ActorHealth> members = new Dictionary<int, ActorHealth>();

        public SquadAlertState State { get; private set; } = SquadAlertState.Unaware;
        public SquadKnowledge Knowledge { get; } = new SquadKnowledge();

        public bool RegisterMember(int reporterId, ActorHealth health)
        {
            if (reporterId <= 0 || health == null || health.IsDead)
                return false;
            if (members.TryGetValue(reporterId, out var existing) && existing != null && existing != health)
                return false;
            members[reporterId] = health;
            return true;
        }

        public void UnregisterMember(int reporterId) => members.Remove(reporterId);

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

        public void EnterConfused() => State = SquadAlertState.Confused;
    }
}
