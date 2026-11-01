using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace AlmostDelivered.CommanderAI.Domain
{
    public sealed class TacticEvaluation
    {
        public string TacticId { get; }
        public bool IsValid { get; }
        public float TotalScore { get; }
        public string RejectionReason { get; }
        public IReadOnlyDictionary<string, float> ComponentScores { get; }

        public TacticEvaluation(string tacticId, bool isValid, float totalScore, string rejectionReason,
            IDictionary<string, float> componentScores = null)
        {
            TacticId = tacticId;
            IsValid = isValid && TacticalContext.Finite(totalScore) && totalScore >= 0f && totalScore <= 1f;
            TotalScore = IsValid ? totalScore : 0f;
            RejectionReason = IsValid ? null : rejectionReason ?? "Invalid evaluation score.";
            ComponentScores = new ReadOnlyDictionary<string, float>(componentScores == null
                ? new Dictionary<string, float>() : new Dictionary<string, float>(componentScores));
        }
    }
}
