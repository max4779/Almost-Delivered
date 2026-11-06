namespace AlmostDelivered.Gameplay.Payload
{
    public enum PayloadAssaultPattern
    {
        ForwardBlockade,
        LeftAmbush,
        RightAmbush,
        RearPursuit
    }

    public readonly struct PayloadEscortEncounter
    {
        public PayloadEscortEncounter(int sequenceId, int groupSize, PayloadAssaultPattern pattern)
        {
            SequenceId = sequenceId;
            GroupSize = groupSize;
            Pattern = pattern;
        }

        public int SequenceId { get; }

        public int GroupSize { get; }

        public PayloadAssaultPattern Pattern { get; }
    }
}
