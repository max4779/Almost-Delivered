using System;
using System.Collections.Generic;
using AlmostDelivered.CommanderAI.Domain;
using UnityEngine;

namespace AlmostDelivered.CommanderAI.Tactics
{
    public readonly struct TacticalRoleRequest
    {
        public MonsterRole Role { get; }
        public TacticalSlotRequest Slot { get; }
        public int TargetActorId { get; }
        public TacticalRoleRequest(MonsterRole role, TacticalSlotRequest slot, int targetActorId = 0)
        {
            Role = role; Slot = slot; TargetActorId = targetActorId;
        }
    }

    [Serializable]
    public sealed class PlaybookRoleSlots
    {
        [SerializeField] private MonsterRole role;
        [SerializeField] private TacticalAnchorTag[] tags;
        [SerializeField] private bool pressureObservedSupport;
        public MonsterRole Role => role;
        public IReadOnlyList<TacticalAnchorTag> Tags => Array.AsReadOnly(tags);
        public bool PressureObservedSupport => pressureObservedSupport;
        public PlaybookRoleSlots(MonsterRole role, bool pressureObservedSupport, params TacticalAnchorTag[] tags)
        {
            this.role = role;
            this.pressureObservedSupport = pressureObservedSupport;
            this.tags = tags == null ? Array.Empty<TacticalAnchorTag>() : (TacticalAnchorTag[])tags.Clone();
        }
    }

    [CreateAssetMenu(menuName = "Almost Delivered/Commander AI/Tactical Playbook")]
    public sealed class TacticalPlaybook : ScriptableObject
    {
        [SerializeField] private bool requiresObservedSupport;
        [SerializeField] private PlaybookRoleSlots[] roleSlots = Array.Empty<PlaybookRoleSlots>();
        public bool RequiresObservedSupport { get => requiresObservedSupport; set => requiresObservedSupport = value; }
        public PlaybookRoleSlots[] RoleSlots { get => (PlaybookRoleSlots[])roleSlots.Clone(); set => roleSlots = value == null ? Array.Empty<PlaybookRoleSlots>() : (PlaybookRoleSlots[])value.Clone(); }

        public IReadOnlyList<TacticalRoleRequest> BuildRequests(in TacticalContext context)
        {
            var requests = new List<TacticalRoleRequest>();
            var hasSupport = context.TryGetSupportTarget(out var support);
            if (requiresObservedSupport && !hasSupport) return requests.AsReadOnly();
            foreach (var assignment in roleSlots)
            {
                if (assignment == null || assignment.Tags.Count == 0 || (assignment.PressureObservedSupport && !hasSupport)) continue;
                var target = assignment.PressureObservedSupport ? support.ActorId : 0;
                var position = assignment.PressureObservedSupport ? support.LastKnownPosition : context.ObjectivePosition;
                for (var index = 0; index < context.LivingCount(assignment.Role); index++)
                {
                    var tag = assignment.Tags[index % assignment.Tags.Count];
                    requests.Add(new TacticalRoleRequest(assignment.Role, new TacticalSlotRequest(position, tag), target));
                }
            }
            return requests.AsReadOnly();
        }
    }
}
