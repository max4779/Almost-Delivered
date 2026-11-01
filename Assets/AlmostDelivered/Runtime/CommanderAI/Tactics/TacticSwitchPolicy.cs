using System;
using AlmostDelivered.CommanderAI.Domain;

namespace AlmostDelivered.CommanderAI.Tactics
{
    public static class TacticSwitchPolicy
    {
        public static bool ShouldSwitch(TacticEvaluation current, TacticEvaluation candidate, float tacticAge,
            float activationThreshold, float minimumHoldTime, float switchMargin)
        {
            if (candidate == null || !candidate.IsValid || string.IsNullOrWhiteSpace(candidate.TacticId) ||
                !TacticalContext.Finite(tacticAge) || tacticAge < 0f ||
                !TacticalContext.Finite(activationThreshold) || activationThreshold < 0f || activationThreshold > 1f ||
                !TacticalContext.Finite(minimumHoldTime) || minimumHoldTime < 0f ||
                !TacticalContext.Finite(switchMargin) || switchMargin < 0f || switchMargin > 1f ||
                candidate.TotalScore <= activationThreshold)
                return false;
            if (current == null) return true;
            if (string.Equals(current.TacticId, candidate.TacticId, StringComparison.Ordinal) || tacticAge < minimumHoldTime)
                return false;
            // Round the boundary to the same precision as the authored scores on Mono.
            var requiredScore = (float)(current.TotalScore + switchMargin);
            return candidate.TotalScore > requiredScore;
        }
    }
}
