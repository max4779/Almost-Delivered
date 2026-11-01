using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Profiling;

namespace AlmostDelivered.CommanderAI.Domain
{
    public sealed class SquadKnowledge
    {
        private static readonly ProfilerMarker MergeMarker = new ProfilerMarker("CommanderAI.KnowledgeMerge");
        private static readonly ProfilerMarker DecayMarker = new ProfilerMarker("CommanderAI.KnowledgeDecay");
        private readonly Dictionary<int, ObservedTargetMemory> memories =
            new Dictionary<int, ObservedTargetMemory>();

        public IReadOnlyCollection<ObservedTargetMemory> Snapshot
        {
            get
            {
                var snapshot = new ObservedTargetMemory[memories.Count];
                memories.Values.CopyTo(snapshot, 0);
                Array.Sort(snapshot, (left, right) => left.ActorId.CompareTo(right.ActorId));
                return Array.AsReadOnly(snapshot);
            }
        }

        public bool Merge(in ObservationSample sample)
        {
            using var mergeSample = MergeMarker.Auto();
            Validate(in sample);
            if (!memories.TryGetValue(sample.ActorId, out var current))
            {
                memories.Add(sample.ActorId, new ObservedTargetMemory(in sample, sample.Tags));
                return true;
            }

            var tags = current.Tags | sample.Tags;
            var next = sample.Timestamp >= current.LastObservedTime
                ? new ObservedTargetMemory(in sample, tags)
                : current.With(current.Confidence, tags);
            if (current.Equals(next))
                return false;

            memories[sample.ActorId] = next;
            return true;
        }

        public void Decay(float now, float secondsToZero)
        {
            using var decaySample = DecayMarker.Auto();
            if (!IsFinite(now) || now < 0f)
                throw new ArgumentOutOfRangeException(nameof(now));
            if (!IsFinite(secondsToZero) || secondsToZero <= 0f)
                throw new ArgumentOutOfRangeException(nameof(secondsToZero));

            foreach (var memory in Snapshot)
            {
                // Always measure from the observation, so repeated decay does not compound.
                var age = Math.Max(0f, now - memory.LastObservedTime);
                var confidence = memory.ObservedConfidence * Math.Max(0f, 1f - age / secondsToZero);
                memories[memory.ActorId] = memory.With(Math.Min(memory.Confidence, confidence), memory.Tags);
            }
        }

        public bool TryGet(int actorId, out ObservedTargetMemory memory)
        {
            return memories.TryGetValue(actorId, out memory);
        }

        public void Clear()
        {
            memories.Clear();
        }

        private static void Validate(in ObservationSample sample)
        {
            if (sample.ActorId <= 0)
                throw new ArgumentOutOfRangeException(nameof(sample), "Actor IDs must be positive.");
            if (!IsFinite(sample.Timestamp) || sample.Timestamp < 0f)
                throw new ArgumentOutOfRangeException(nameof(sample), "Timestamps must be finite and non-negative.");
            if (!IsFinite(sample.Confidence) || sample.Confidence < 0f || sample.Confidence > 1f)
                throw new ArgumentOutOfRangeException(nameof(sample), "Confidence must be in [0, 1].");
            if (!IsFinite(sample.Position) || !IsFinite(sample.Velocity))
                throw new ArgumentException("Observation coordinates must be finite.", nameof(sample));
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
