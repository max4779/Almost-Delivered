using System.Collections.Generic;
using AlmostDelivered.Gameplay.Payload;
using NUnit.Framework;

namespace AlmostDelivered.Tests.EditMode
{
    public sealed class PayloadEscortDirectorTests
    {
        [Test]
        public void FirstEncounterArrivesWithinEightEscortSeconds()
        {
            var director = new PayloadEscortDirector(4711);
            Assert.That(director.Tick(0f, PayloadMissionPhase.Escorting, 0, out _), Is.False);

            PayloadEscortEncounter encounter = default;
            var requested = false;
            for (var second = 1; second <= 8; second++)
            {
                requested = director.Tick(1f, PayloadMissionPhase.Escorting, 0, out encounter);
                if (requested)
                    break;
            }

            Assert.That(requested, Is.True);
            Assert.That(encounter.GroupSize, Is.InRange(3, 6));
            Assert.That(encounter.SequenceId, Is.EqualTo(0));
        }

        [Test]
        public void SubsequentEncounterWaitsAtLeastTwelveSeconds()
        {
            var director = new PayloadEscortDirector(88);
            director.Tick(0f, PayloadMissionPhase.Escorting, 0, out _);
            PayloadEscortEncounter first = default;
            for (var second = 0; second < 8 && first.GroupSize == 0; second++)
                director.Tick(1f, PayloadMissionPhase.Escorting, 0, out first);

            Assert.That(first.GroupSize, Is.GreaterThan(0));
            Assert.That(director.Tick(11.99f, PayloadMissionPhase.Escorting, 0, out _), Is.False);

            var requested = false;
            for (var second = 0; second < 9; second++)
            {
                requested = director.Tick(1f, PayloadMissionPhase.Escorting, 0, out _);
                if (requested)
                    break;
            }

            Assert.That(requested, Is.True);
        }

        [Test]
        public void NonEscortPhasesPauseRequests()
        {
            var director = new PayloadEscortDirector(22);
            director.Tick(0f, PayloadMissionPhase.Escorting, 0, out _);
            director.Tick(4f, PayloadMissionPhase.Escorting, 0, out _);

            Assert.That(director.Tick(100f, PayloadMissionPhase.CheckpointDefense, 0, out _), Is.False);
            Assert.That(director.Tick(100f, PayloadMissionPhase.FinalDefense, 0, out _), Is.False);
            Assert.That(director.Tick(100f, PayloadMissionPhase.Completed, 0, out _), Is.False);
            Assert.That(director.Tick(100f, PayloadMissionPhase.Failed, 0, out _), Is.False);
            Assert.That(director.Tick(100f, PayloadMissionPhase.Deploying, 0, out _), Is.False);
        }

        [Test]
        public void SameSeedProducesSameEncounterSequence()
        {
            var first = Sequence(1234);
            var second = Sequence(1234);

            Assert.That(second, Is.EqualTo(first));
        }

        [Test]
        public void EnteringEscortStartsOneInitialCountdown()
        {
            var director = new PayloadEscortDirector(17);

            Assert.That(director.Tick(1000f, PayloadMissionPhase.Escorting, 0, out _), Is.False);
            Assert.That(director.Tick(8f, PayloadMissionPhase.Escorting, 0, out var encounter), Is.True);
            Assert.That(encounter.SequenceId, Is.EqualTo(0));
        }

        [Test]
        public void EncounterNeverExceedsRemainingPressureCapacity()
        {
            var director = new PayloadEscortDirector(99, 12);
            director.Tick(0f, PayloadMissionPhase.Escorting, 10, out _);

            Assert.That(director.Tick(8f, PayloadMissionPhase.Escorting, 10, out var encounter), Is.True);
            Assert.That(encounter.GroupSize, Is.EqualTo(2));
            Assert.That(director.Tick(20f, PayloadMissionPhase.Escorting, 12, out _), Is.False);
        }

        private static List<string> Sequence(int seed)
        {
            var director = new PayloadEscortDirector(seed);
            var sequence = new List<string>();
            director.Tick(0f, PayloadMissionPhase.Escorting, 0, out _);
            for (var second = 0; second < 80; second++)
                if (director.Tick(1f, PayloadMissionPhase.Escorting, 0, out var encounter))
                    sequence.Add($"{encounter.SequenceId}:{encounter.GroupSize}:{encounter.Pattern}");
            return sequence;
        }
    }
}
