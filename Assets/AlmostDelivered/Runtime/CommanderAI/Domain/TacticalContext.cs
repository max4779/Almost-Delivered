using System;
using System.Collections.Generic;
using AlmostDelivered.CommanderAI.Tactics;
using UnityEngine;

namespace AlmostDelivered.CommanderAI.Domain
{
    public readonly struct TacticalContext
    {
        private readonly IReadOnlyList<ObservedTargetMemory> knowledge;
        private readonly IReadOnlyDictionary<MonsterRole, int> roles;
        private readonly IReadOnlyDictionary<TacticalAnchorTag, int> anchors;
        public IReadOnlyList<ObservedTargetMemory> Knowledge => knowledge ?? Array.Empty<ObservedTargetMemory>();
        public Vector3 ObjectivePosition { get; }
        public float SquadHealthRatio { get; }
        public string CurrentTacticId { get; }
        public float TacticAge { get; }

        public TacticalContext(IEnumerable<ObservedTargetMemory> knowledge, IReadOnlyDictionary<MonsterRole, int> livingRoles,
            Vector3 objectivePosition, IReadOnlyDictionary<TacticalAnchorTag, int> anchorAvailability,
            float squadHealthRatio, string currentTacticId = null, float tacticAge = 0f)
        {
            if (!Finite(squadHealthRatio) || !Finite(tacticAge) || tacticAge < 0f ||
                !Finite(objectivePosition.x) || !Finite(objectivePosition.y) || !Finite(objectivePosition.z))
                throw new ArgumentException("Context values must be finite and tactic age non-negative.");
            var memories = new List<ObservedTargetMemory>(knowledge ?? throw new ArgumentNullException(nameof(knowledge)));
            memories.Sort((left, right) => left.ActorId.CompareTo(right.ActorId));
            this.knowledge = memories.AsReadOnly();
            roles = CopyCounts(livingRoles);
            anchors = CopyCounts(anchorAvailability);
            ObjectivePosition = objectivePosition;
            SquadHealthRatio = Mathf.Clamp01(squadHealthRatio);
            CurrentTacticId = currentTacticId;
            TacticAge = tacticAge;
        }

        public int LivingCount(MonsterRole role) => roles != null && roles.TryGetValue(role, out var count) ? count : 0;
        public int AnchorCount(TacticalAnchorTag tag) => anchors != null && anchors.TryGetValue(tag, out var count) ? count : 0;

        public bool TryGetSupportTarget(out ObservedTargetMemory target)
        {
            target = default;
            foreach (var memory in Knowledge)
            {
                if (memory.Confidence <= 0f || (memory.Tags & (ObservedCombatTag.Healing | ObservedCombatTag.Shield)) == 0)
                    continue;
                if (target.ActorId == 0 || memory.Confidence > target.Confidence ||
                    (memory.Confidence == target.Confidence && memory.ActorId < target.ActorId))
                    target = memory;
            }
            return target.ActorId != 0;
        }

        private static IReadOnlyDictionary<T, int> CopyCounts<T>(IReadOnlyDictionary<T, int> source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            var copy = new Dictionary<T, int>();
            foreach (var pair in source)
            {
                if (pair.Value < 0) throw new ArgumentOutOfRangeException(nameof(source));
                copy.Add(pair.Key, pair.Value);
            }
            return copy;
        }

        internal static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
