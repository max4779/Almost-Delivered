using System;
using System.Collections.Generic;
using AlmostDelivered.Combat;
using AlmostDelivered.CommanderAI.Domain;
using UnityEngine;
using Unity.Profiling;

namespace AlmostDelivered.CommanderAI.Tactics
{
    public sealed class TacticalSlotAllocator
    {
        private static readonly ProfilerMarker AllocationMarker = new ProfilerMarker("CommanderAI.TacticalAllocation");
        private readonly IEnumerable<TacticalAnchor> anchors;
        private readonly ITacticalReachability reachability;
        private readonly Dictionary<int, Member> members = new Dictionary<int, Member>();
        private readonly Dictionary<int, TacticalAnchor> reservations = new Dictionary<int, TacticalAnchor>();

        public TacticalSlotAllocator(IEnumerable<TacticalAnchor> anchors, ITacticalReachability reachability)
        {
            this.anchors = anchors ?? throw new ArgumentNullException(nameof(anchors));
            this.reachability = reachability ?? throw new ArgumentNullException(nameof(reachability));
        }

        public int ReservationCount { get { CleanUp(); return reservations.Count; } }

        public bool RegisterMember(int memberId, MonsterRole role, ActorHealth health)
        {
            if (memberId <= 0 || health == null || health.IsDead) return false;
            if (members.TryGetValue(memberId, out var existing) && existing.Health != null && existing.Health != health) return false;
            members[memberId] = new Member(role, health);
            return true;
        }

        public void UnregisterMember(int memberId) { Release(memberId); members.Remove(memberId); }
        public void Release(int memberId) => reservations.Remove(memberId);
        public void ReleaseAll() => reservations.Clear();

        public bool TryAllocate(int memberId, MonsterRole role, in TacticalSlotRequest request, out TacticalAnchor anchor)
            => TryAllocate(memberId, role, in request, out anchor, out _);

        public bool CanUseAnchor(int memberId, MonsterRole role, TacticalAnchor anchor) =>
            members.TryGetValue(memberId, out var member) && member.IsLiving && member.Role == role &&
            anchor != null && anchor.isActiveAndEnabled && anchor.Allows(role) &&
            reachability.CanReach(member.Health.transform.position, anchor.transform.position);

        public bool TryAllocate(int memberId, MonsterRole role, in TacticalSlotRequest request, out TacticalAnchor anchor,
            out string failureReason)
        {
            using var allocationSample = AllocationMarker.Auto();
            anchor = null; failureReason = null;
            CleanUp();
            if (!members.TryGetValue(memberId, out var member) || !member.IsLiving || member.Role != role)
            { failureReason = "Member unavailable or role mismatch."; return false; }
            if (reservations.TryGetValue(memberId, out var previous))
            {
                if (Eligible(previous, role, in request) && reachability.CanReach(member.Health.transform.position, previous.transform.position))
                { anchor = previous; return true; }
                Release(memberId);
            }

            var bestScore = float.PositiveInfinity;
            var hasTag = false; var hasRole = false; var hasCapacity = false;
            foreach (var candidate in anchors)
            {
                if (candidate == null || !candidate.isActiveAndEnabled || candidate.Tag != request.Tag) continue;
                hasTag = true;
                if (!candidate.Allows(role)) continue;
                hasRole = true;
                if (Occupancy(candidate) >= candidate.Capacity) continue;
                hasCapacity = true;
                if (!reachability.CanReach(member.Health.transform.position, candidate.transform.position)) continue;
                var score = (candidate.transform.position - request.Position).sqrMagnitude;
                if (score < bestScore || (score == bestScore && (anchor == null || candidate.GetInstanceID() < anchor.GetInstanceID())))
                { anchor = candidate; bestScore = score; }
            }
            if (anchor == null)
            {
                failureReason = !hasTag ? "Anchor missing or disabled." : !hasRole ? "Role-incompatible anchors." :
                    !hasCapacity ? "Anchor capacity full." : "All eligible anchors unreachable.";
                return false;
            }
            reservations.Add(memberId, anchor);
            return true;
        }

        private static bool Eligible(TacticalAnchor anchor, MonsterRole role, in TacticalSlotRequest request) =>
            anchor != null && anchor.isActiveAndEnabled && anchor.Tag == request.Tag && anchor.Allows(role);

        private int Occupancy(TacticalAnchor anchor)
        {
            var count = 0;
            foreach (var occupied in reservations.Values) if (occupied == anchor) count++;
            return count;
        }

        private void CleanUp()
        {
            var stale = new List<int>();
            foreach (var entry in reservations)
                if (entry.Value == null || !entry.Value.isActiveAndEnabled || !members.TryGetValue(entry.Key, out var member) || !member.IsLiving)
                    stale.Add(entry.Key);
            foreach (var id in stale) reservations.Remove(id);
        }

        private readonly struct Member
        {
            public readonly MonsterRole Role;
            public readonly ActorHealth Health;
            public bool IsLiving => Health != null && Health.isActiveAndEnabled && !Health.IsDead;
            public Member(MonsterRole role, ActorHealth health) { Role = role; Health = health; }
        }
    }
}
