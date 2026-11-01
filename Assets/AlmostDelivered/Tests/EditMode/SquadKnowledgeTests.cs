using System;
using AlmostDelivered.CommanderAI.Domain;
using NUnit.Framework;
using UnityEngine;

namespace AlmostDelivered.Tests.EditMode
{
    public sealed class SquadKnowledgeTests
    {
        [Test]
        public void Merge_NewTarget_CreatesMemoryWithPositionTimestampAndTags()
        {
            var knowledge = new SquadKnowledge();
            var sample = new ObservationSample(1, new Vector3(2, 3, 4), Vector3.right, 10f, 0.8f,
                ObservedCombatTag.RangedWeapon | ObservedCombatTag.Mobility, 7);

            Assert.That(knowledge.Merge(in sample), Is.True);
            Assert.That(knowledge.TryGet(1, out var memory), Is.True);
            Assert.That(memory.ActorId, Is.EqualTo(1));
            Assert.That(memory.LastKnownPosition, Is.EqualTo(new Vector3(2, 3, 4)));
            Assert.That(memory.LastKnownVelocity, Is.EqualTo(Vector3.right));
            Assert.That(memory.LastObservedTime, Is.EqualTo(10f));
            Assert.That(memory.Confidence, Is.EqualTo(0.8f));
            Assert.That(memory.Tags, Is.EqualTo(ObservedCombatTag.RangedWeapon | ObservedCombatTag.Mobility));
            Assert.That(memory.ReporterId, Is.EqualTo(7));
            Assert.That(knowledge.Snapshot.Count, Is.EqualTo(1));
        }

        [Test]
        public void Merge_OlderSample_DoesNotReplaceNewerPositionOrTimestamp()
        {
            var knowledge = new SquadKnowledge();
            var newer = Sample(10f, ObservedCombatTag.Shield);
            var older = new ObservationSample(1, Vector3.zero, Vector3.left, 5f, 0.2f,
                ObservedCombatTag.Shield, 8);
            knowledge.Merge(in newer);

            Assert.That(knowledge.Merge(in older), Is.False);
            knowledge.TryGet(1, out var memory);
            Assert.That(memory.LastKnownPosition, Is.EqualTo(new Vector3(3, 0, 5)));
            Assert.That(memory.LastKnownVelocity, Is.EqualTo(Vector3.forward));
            Assert.That(memory.LastObservedTime, Is.EqualTo(10f));
            Assert.That(memory.Confidence, Is.EqualTo(0.8f));
            Assert.That(memory.ReporterId, Is.EqualTo(7));
        }

        [Test]
        public void Merge_OlderSample_UnionsNewlyObservedTags()
        {
            var knowledge = new SquadKnowledge();
            var newer = Sample(10f, ObservedCombatTag.Shield);
            var older = Sample(5f, ObservedCombatTag.Healing);
            knowledge.Merge(in newer);

            Assert.That(knowledge.Merge(in older), Is.True);
            knowledge.TryGet(1, out var memory);
            Assert.That(memory.Tags, Is.EqualTo(ObservedCombatTag.Shield | ObservedCombatTag.Healing));
            Assert.That(memory.LastObservedTime, Is.EqualTo(10f));
            Assert.That(knowledge.Merge(in older), Is.False);
        }

        [Test]
        public void Decay_StaleMemory_ReachesZeroConfidenceWithoutMovingPosition()
        {
            var knowledge = new SquadKnowledge();
            var sample = Sample(10f, ObservedCombatTag.Healing);
            knowledge.Merge(in sample);

            knowledge.Decay(20f, 10f);

            Assert.That(knowledge.TryGet(1, out var memory), Is.True);
            Assert.That(memory.Confidence, Is.Zero);
            Assert.That(memory.LastKnownPosition, Is.EqualTo(new Vector3(3, 0, 5)));
            Assert.That(memory.LastObservedTime, Is.EqualTo(10f));
            Assert.That(memory.Tags, Is.EqualTo(ObservedCombatTag.Healing));
        }

        [Test]
        public void TryGet_UnknownActor_ReturnsFalse()
        {
            Assert.That(new SquadKnowledge().TryGet(99, out _), Is.False);
        }

        [Test]
        public void Merge_NewerSample_RefreshesFactsAndPreservesTags()
        {
            var knowledge = new SquadKnowledge();
            var first = Sample(10f, ObservedCombatTag.Shield);
            var next = new ObservationSample(1, Vector3.zero, Vector3.left, 12f, 1f,
                ObservedCombatTag.Healing, 8);
            knowledge.Merge(in first);
            knowledge.Decay(11f, 10f);

            Assert.That(knowledge.Merge(in next), Is.True);
            knowledge.TryGet(1, out var memory);
            Assert.That(memory.LastKnownPosition, Is.EqualTo(Vector3.zero));
            Assert.That(memory.LastKnownVelocity, Is.EqualTo(Vector3.left));
            Assert.That(memory.LastObservedTime, Is.EqualTo(12f));
            Assert.That(memory.Confidence, Is.EqualTo(1f));
            Assert.That(memory.ReporterId, Is.EqualTo(8));
            Assert.That(memory.Tags, Is.EqualTo(ObservedCombatTag.Shield | ObservedCombatTag.Healing));
            Assert.That(knowledge.Merge(in next), Is.False);
        }

        [Test]
        public void Decay_RepeatedCallAtSameTime_DoesNotCompoundLoss()
        {
            var knowledge = new SquadKnowledge();
            var sample = Sample(10f, ObservedCombatTag.None);
            knowledge.Merge(in sample);
            knowledge.Decay(15f, 10f);
            knowledge.Decay(15f, 10f);
            knowledge.TryGet(1, out var memory);
            Assert.That(memory.Confidence, Is.EqualTo(0.4f).Within(0.0001f));
        }

        [Test]
        public void Clear_RemovesAllMemoryAndLeavesExistingSnapshotUnchanged()
        {
            var knowledge = new SquadKnowledge();
            var sample = Sample(10f, ObservedCombatTag.None);
            knowledge.Merge(in sample);
            var snapshot = knowledge.Snapshot;
            knowledge.Clear();
            Assert.That(knowledge.Snapshot, Is.Empty);
            Assert.That(knowledge.TryGet(1, out _), Is.False);
            Assert.That(snapshot.Count, Is.EqualTo(1));
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void Merge_NonPositiveActorId_RejectsSample(int actorId)
        {
            var sample = new ObservationSample(actorId, Vector3.zero, Vector3.zero, 0f, 1f,
                ObservedCombatTag.None, 7);
            Assert.Throws<ArgumentOutOfRangeException>(() => new SquadKnowledge().Merge(in sample));
        }

        [TestCase(-1f, 1f)]
        [TestCase(float.NaN, 1f)]
        [TestCase(0f, -0.1f)]
        [TestCase(0f, 1.1f)]
        [TestCase(0f, float.NaN)]
        public void Merge_InvalidTimestampOrConfidence_RejectsSample(float timestamp, float confidence)
        {
            var sample = new ObservationSample(1, Vector3.zero, Vector3.zero, timestamp, confidence,
                ObservedCombatTag.None, 7);
            Assert.Throws<ArgumentOutOfRangeException>(() => new SquadKnowledge().Merge(in sample));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void Merge_NaNPositionOrVelocity_RejectsSample(bool invalidPosition)
        {
            var invalid = new Vector3(float.NaN, 0, 0);
            var sample = new ObservationSample(1, invalidPosition ? invalid : Vector3.zero,
                invalidPosition ? Vector3.zero : invalid, 0f, 1f, ObservedCombatTag.None, 7);
            Assert.Throws<ArgumentException>(() => new SquadKnowledge().Merge(in sample));
        }

        [TestCase(-1f, 10f)]
        [TestCase(float.NaN, 10f)]
        [TestCase(0f, 0f)]
        [TestCase(0f, -1f)]
        public void Decay_InvalidTiming_RejectsInput(float now, float secondsToZero)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new SquadKnowledge().Decay(now, secondsToZero));
        }

        private static ObservationSample Sample(float time, ObservedCombatTag tags)
        {
            return new ObservationSample(1, new Vector3(3, 0, 5), Vector3.forward, time, 0.8f, tags, 7);
        }
    }
}
