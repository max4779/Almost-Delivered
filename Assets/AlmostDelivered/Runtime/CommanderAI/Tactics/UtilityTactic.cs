using System;
using System.Collections.Generic;
using AlmostDelivered.CommanderAI.Domain;
using UnityEngine;

namespace AlmostDelivered.CommanderAI.Tactics
{
    public static class InitialTacticIds
    {
        public const string ObjectiveBlockade = "objective-blockade";
        public const string Pincer = "pincer";
        public const string FocusSupport = "focus-support";
    }

    [Serializable]
    public struct RoleRequirement
    {
        [SerializeField] private MonsterRole role;
        [SerializeField] private int minimumCount;
        public MonsterRole Role => role;
        public int MinimumCount => minimumCount;
        public RoleRequirement(MonsterRole role, int minimumCount) { this.role = role; this.minimumCount = minimumCount; }
    }

    [CreateAssetMenu(menuName = "Almost Delivered/Commander AI/Utility Tactic")]
    public sealed class UtilityTactic : ScriptableObject
    {
        [SerializeField] private string tacticId;
        [SerializeField] private RoleRequirement[] requiredRoles = Array.Empty<RoleRequirement>();
        [SerializeField] private UtilityConsideration[] considerations = Array.Empty<UtilityConsideration>();
        [SerializeField, Range(0f, 1f)] private float minimumActivationScore = .2f;
        [SerializeField, Min(0f)] private float minimumHoldTime = 6f;
        [SerializeField, Range(0f, 1f)] private float switchMargin = .1f;
        [SerializeField] private TacticalPlaybook playbook;
        public string TacticId { get => tacticId; set => tacticId = value; }
        public float MinimumActivationScore { get => minimumActivationScore; set => minimumActivationScore = value; }
        public float MinimumHoldTime { get => minimumHoldTime; set => minimumHoldTime = value; }
        public float SwitchMargin { get => switchMargin; set => switchMargin = value; }
        public TacticalPlaybook Playbook { get => playbook; set => playbook = value; }
        public RoleRequirement[] RequiredRoles { get => (RoleRequirement[])requiredRoles.Clone(); set => requiredRoles = value == null ? Array.Empty<RoleRequirement>() : (RoleRequirement[])value.Clone(); }
        public UtilityConsideration[] Considerations { get => (UtilityConsideration[])considerations.Clone(); set => considerations = value == null ? Array.Empty<UtilityConsideration>() : (UtilityConsideration[])value.Clone(); }

        public TacticEvaluation Evaluate(in TacticalContext context)
        {
            var scores = new Dictionary<string, float>(StringComparer.Ordinal);
            if (string.IsNullOrWhiteSpace(tacticId)) return Reject("Missing tactic ID.", scores);
            foreach (var requirement in requiredRoles)
                if (requirement.MinimumCount <= 0 || context.LivingCount(requirement.Role) < requirement.MinimumCount)
                    return Reject("Required role unavailable: " + requirement.Role, scores);
            if (considerations.Length == 0) return Reject("No considerations configured.", scores);
            double logSum = 0d, weightSum = 0d;
            var zero = false;
            foreach (var consideration in considerations)
            {
                if (consideration == null || string.IsNullOrWhiteSpace(consideration.Name)) return Reject("Unnamed consideration.", scores);
                if (scores.ContainsKey(consideration.Name)) return Reject("Duplicate consideration: " + consideration.Name, scores);
                if (!TacticalContext.Finite(consideration.Weight) || consideration.Weight <= 0f) return Reject("Invalid weight: " + consideration.Name, scores);
                var raw = consideration.Score(in context);
                if (!TacticalContext.Finite(raw)) return Reject("Non-finite consideration: " + consideration.Name, scores);
                var score = Mathf.Clamp01(raw);
                scores.Add(consideration.Name, score);
                zero |= score == 0f;
                if (score > 0f) logSum += consideration.Weight * Math.Log(score);
                weightSum += consideration.Weight;
            }
            return new TacticEvaluation(tacticId, true, zero ? 0f : (float)Math.Exp(logSum / weightSum), null, scores);
        }

        private TacticEvaluation Reject(string reason, IDictionary<string, float> scores) => new TacticEvaluation(tacticId, false, 0f, reason, scores);
    }
}
