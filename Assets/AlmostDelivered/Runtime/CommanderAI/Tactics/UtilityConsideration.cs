using System;
using AlmostDelivered.CommanderAI.Domain;
using UnityEngine;

namespace AlmostDelivered.CommanderAI.Tactics
{
    public enum ConsiderationKind { SquadHealth, RoleAvailability, AnchorAvailability, ObjectiveProximity, PlayerClustering, ObservedSupport }

    [Serializable]
    public class UtilityConsideration
    {
        [SerializeField] private string name;
        [SerializeField] private ConsiderationKind kind;
        [SerializeField] private float weight = 1f;
        [SerializeField] private float scale = 1f;
        [SerializeField] private MonsterRole role;
        [SerializeField] private TacticalAnchorTag anchorTag;
        [SerializeField] private AnimationCurve curve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        public string Name => name;
        public float Weight => weight;

        public UtilityConsideration(string name, ConsiderationKind kind, float weight = 1f, float scale = 1f,
            MonsterRole role = MonsterRole.Shield, TacticalAnchorTag anchorTag = TacticalAnchorTag.ObjectiveFront)
        {
            this.name = name; this.kind = kind; this.weight = weight; this.scale = scale;
            this.role = role; this.anchorTag = anchorTag;
        }

        public virtual float Score(in TacticalContext context)
        {
            if (!TacticalContext.Finite(scale) || scale <= 0f || curve == null) return float.NaN;
            float raw;
            switch (kind)
            {
                case ConsiderationKind.SquadHealth: raw = context.SquadHealthRatio; break;
                case ConsiderationKind.RoleAvailability: raw = context.LivingCount(role) / scale; break;
                case ConsiderationKind.AnchorAvailability: raw = context.AnchorCount(anchorTag) / scale; break;
                case ConsiderationKind.ObservedSupport:
                    raw = context.TryGetSupportTarget(out var support) ? support.Confidence : 0f; break;
                case ConsiderationKind.ObjectiveProximity:
                    raw = 0f;
                    foreach (var memory in context.Knowledge)
                        raw = Mathf.Max(raw, memory.Confidence * Mathf.Clamp01(1f - Vector3.Distance(memory.LastKnownPosition, context.ObjectivePosition) / scale));
                    break;
                case ConsiderationKind.PlayerClustering:
                    var sum = 0f; var pairs = 0;
                    for (var i = 0; i < context.Knowledge.Count; i++)
                        for (var j = i + 1; j < context.Knowledge.Count; j++)
                        {
                            var left = context.Knowledge[i]; var right = context.Knowledge[j];
                            if (left.Confidence <= 0f || right.Confidence <= 0f) continue;
                            sum += Mathf.Min(left.Confidence, right.Confidence) * Mathf.Clamp01(1f - Vector3.Distance(left.LastKnownPosition, right.LastKnownPosition) / scale);
                            pairs++;
                        }
                    raw = pairs == 0 ? 0f : sum / pairs;
                    break;
                default: return float.NaN;
            }
            return curve.Evaluate(Mathf.Clamp01(raw));
        }
    }
}
