using System;
using System.Collections.Generic;
using AlmostDelivered.Gameplay.Payload;
using NUnit.Framework;

namespace AlmostDelivered.Tests.EditMode
{
    public sealed class PayloadMissionStateMachineTests
    {
        [Test]
        public void DeploymentLocksMovementUntilAFollowingEscortTick()
        {
            var mission = CreateMission(deploymentDuration: 2f);

            mission.Tick(1f, 1, 0);
            Assert.That(mission.Snapshot.Phase, Is.EqualTo(PayloadMissionPhase.Deploying));
            Assert.That(mission.Snapshot.RouteDistance, Is.EqualTo(0f));

            mission.Tick(1.5f, 1, 0);
            Assert.That(mission.Snapshot.Phase, Is.EqualTo(PayloadMissionPhase.Escorting));
            Assert.That(mission.Snapshot.RouteDistance, Is.EqualTo(0f));

            mission.Tick(1f, 1, 0);
            Assert.That(mission.Snapshot.RouteDistance, Is.EqualTo(10f));
        }

        [Test]
        public void RecoveryRequiresTimedCargoActivationBeforeEscort()
        {
            var mission = CreateMission(requiresRecovery: true, activationDuration: 4f);

            Assert.That(mission.Snapshot.Phase, Is.EqualTo(PayloadMissionPhase.Recovering));
            Assert.That(mission.RegisterRecoveryGuards(new[] { 101, 102 }), Is.True);
            mission.Tick(1f, 1, 2);
            Assert.That(mission.Snapshot.RouteDistance, Is.Zero);
            Assert.That(mission.Snapshot.RemainingEnemyCount, Is.EqualTo(2));

            Assert.That(mission.ReportRecoveryGuardRemoved(101), Is.True);
            Assert.That(mission.ReportRecoveryGuardRemoved(102), Is.True);
            mission.Tick(1f, 0, 0);
            Assert.That(mission.Snapshot.Phase, Is.EqualTo(PayloadMissionPhase.Recovering));

            mission.Tick(0f, 1, 0);
            Assert.That(mission.Snapshot.Phase, Is.EqualTo(PayloadMissionPhase.Activating));
            Assert.That(mission.Snapshot.ActivationProgress, Is.Zero);
            Assert.That(mission.Snapshot.RouteDistance, Is.Zero);

            mission.Tick(2f, 1, 0);
            Assert.That(mission.Snapshot.Phase, Is.EqualTo(PayloadMissionPhase.Activating));
            Assert.That(mission.Snapshot.ActivationProgress, Is.EqualTo(.5f).Within(.001f));

            mission.Tick(1f, 0, 0);
            Assert.That(mission.Snapshot.ActivationProgress, Is.EqualTo(.5f).Within(.001f));

            mission.Tick(2f, 1, 0);
            Assert.That(mission.Snapshot.Phase, Is.EqualTo(PayloadMissionPhase.Escorting));
            Assert.That(mission.Snapshot.ActivationProgress, Is.EqualTo(1f));
            Assert.That(mission.Snapshot.RouteDistance, Is.Zero);

            mission.Tick(1f, 1, 0);
            Assert.That(mission.Snapshot.RouteDistance, Is.EqualTo(10f));
        }

        [Test]
        public void EscortRequiresPlayerAndStopsWhenEnemyContests()
        {
            var mission = CreateMission();

            mission.Tick(1f, 0, 0);
            Assert.That(mission.Snapshot.RouteDistance, Is.EqualTo(0f));
            Assert.That(mission.Snapshot.IsEscortActive, Is.False);

            mission.Tick(1f, 1, 1);
            Assert.That(mission.Snapshot.RouteDistance, Is.EqualTo(0f));
            Assert.That(mission.Snapshot.IsEscortActive, Is.False);

            mission.Tick(1f, 1, 0);
            Assert.That(mission.Snapshot.RouteDistance, Is.EqualTo(10f));
            Assert.That(mission.Snapshot.IsEscortActive, Is.True);
        }

        [Test]
        public void ExtraPlayersDoNotIncreasePayloadSpeed()
        {
            var onePlayer = CreateMission();
            var fourPlayers = CreateMission();

            onePlayer.Tick(2f, 1, 0);
            fourPlayers.Tick(2f, 4, 0);

            Assert.That(fourPlayers.Snapshot.RouteDistance, Is.EqualTo(onePlayer.Snapshot.RouteDistance));
            Assert.That(fourPlayers.Snapshot.RouteDistance, Is.EqualTo(20f));
        }

        [Test]
        public void LargeTickClampsExactlyAtCheckpointAndRequestsOneWave()
        {
            var mission = CreateMission();
            var requests = new List<PayloadWaveRequest>();
            mission.WaveRequested += requests.Add;

            mission.Tick(100f, 1, 0);
            mission.Tick(100f, 1, 0);

            Assert.That(mission.Snapshot.RouteDistance, Is.EqualTo(25f));
            Assert.That(mission.Snapshot.Phase, Is.EqualTo(PayloadMissionPhase.CheckpointDefense));
            Assert.That(requests, Has.Count.EqualTo(1));
            Assert.That(requests[0].CheckpointIndex, Is.EqualTo(0));
            Assert.That(requests[0].IsFinal, Is.False);
            Assert.That(requests[0].WaveId, Is.GreaterThan(0));
        }

        [Test]
        public void ThreeCheckpointClearsLeadToFinalDefenseAndCompletion()
        {
            var mission = CreateMission();
            var requests = new List<PayloadWaveRequest>();
            mission.WaveRequested += requests.Add;

            for (var checkpointIndex = 0; checkpointIndex < 3; checkpointIndex++)
            {
                mission.Tick(100f, 1, 0);
                var request = requests[checkpointIndex];

                Assert.That(request.CheckpointIndex, Is.EqualTo(checkpointIndex));
                Assert.That(request.IsFinal, Is.False);
                Assert.That(mission.RegisterWave(request.WaveId, new[] { 100 + checkpointIndex }), Is.True);
                Assert.That(mission.ReportEnemyRemoved(request.WaveId, 100 + checkpointIndex), Is.True);
                Assert.That(mission.Snapshot.Phase, Is.EqualTo(PayloadMissionPhase.Escorting));
                Assert.That(mission.Snapshot.NextCheckpointIndex, Is.EqualTo(checkpointIndex + 1));
            }

            mission.Tick(100f, 1, 0);
            var finalRequest = requests[3];

            Assert.That(mission.Snapshot.RouteDistance, Is.EqualTo(100f));
            Assert.That(mission.Snapshot.NormalizedProgress, Is.EqualTo(1f));
            Assert.That(mission.Snapshot.Phase, Is.EqualTo(PayloadMissionPhase.FinalDefense));
            Assert.That(finalRequest.CheckpointIndex, Is.EqualTo(3));
            Assert.That(finalRequest.IsFinal, Is.True);

            Assert.That(mission.RegisterWave(finalRequest.WaveId, new[] { 201, 202 }), Is.True);
            Assert.That(mission.ReportEnemyRemoved(finalRequest.WaveId, 201), Is.True);
            Assert.That(mission.Snapshot.Phase, Is.EqualTo(PayloadMissionPhase.FinalDefense));
            Assert.That(mission.Snapshot.RemainingEnemyCount, Is.EqualTo(1));
            Assert.That(mission.ReportEnemyRemoved(finalRequest.WaveId, 202), Is.True);
            Assert.That(mission.Snapshot.Phase, Is.EqualTo(PayloadMissionPhase.Completed));
        }

        [Test]
        public void EmptyWaveAdvancesOnNextTickInsteadOfDeadlocking()
        {
            var mission = CreateMission();
            PayloadWaveRequest request = default;
            mission.WaveRequested += value => request = value;
            mission.Tick(100f, 1, 0);

            Assert.That(mission.RegisterWave(request.WaveId, Array.Empty<int>()), Is.True);
            Assert.That(mission.Snapshot.Phase, Is.EqualTo(PayloadMissionPhase.CheckpointDefense));

            mission.Tick(0f, 0, 0);

            Assert.That(mission.Snapshot.Phase, Is.EqualTo(PayloadMissionPhase.Escorting));
            Assert.That(mission.Snapshot.NextCheckpointIndex, Is.EqualTo(1));
        }

        [Test]
        public void DuplicateAndForeignRemovalReportsCannotCompleteWaveEarly()
        {
            var mission = CreateMission();
            PayloadWaveRequest request = default;
            mission.WaveRequested += value => request = value;
            mission.Tick(100f, 1, 0);
            mission.RegisterWave(request.WaveId, new[] { 10, 11 });

            Assert.That(mission.ReportEnemyRemoved(request.WaveId + 99, 10), Is.False);
            Assert.That(mission.ReportEnemyRemoved(request.WaveId, 10), Is.True);
            Assert.That(mission.ReportEnemyRemoved(request.WaveId, 10), Is.False);
            Assert.That(mission.Snapshot.RemainingEnemyCount, Is.EqualTo(1));
            Assert.That(mission.Snapshot.Phase, Is.EqualTo(PayloadMissionPhase.CheckpointDefense));

            Assert.That(mission.ReportEnemyRemoved(request.WaveId, 11), Is.True);
            Assert.That(mission.Snapshot.Phase, Is.EqualTo(PayloadMissionPhase.Escorting));
        }

        [Test]
        public void ForeignOrRepeatedWaveRegistrationIsRejected()
        {
            var mission = CreateMission();
            PayloadWaveRequest request = default;
            mission.WaveRequested += value => request = value;
            mission.Tick(100f, 1, 0);

            Assert.That(mission.RegisterWave(request.WaveId + 1, new[] { 1 }), Is.False);
            Assert.That(mission.RegisterWave(request.WaveId, new[] { 1 }), Is.True);
            Assert.That(mission.RegisterWave(request.WaveId, new[] { 2 }), Is.False);
            Assert.That(mission.Snapshot.RemainingEnemyCount, Is.EqualTo(1));
        }

        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void InvalidDeltaIsRejectedWithoutChangingSnapshot(float deltaTime)
        {
            var mission = CreateMission();
            var before = mission.Snapshot;

            Assert.Throws<ArgumentOutOfRangeException>(() => mission.Tick(deltaTime, 1, 0));

            Assert.That(mission.Snapshot, Is.EqualTo(before));
        }

        [Test]
        public void PreviouslyCapturedSnapshotDoesNotChangeAfterTick()
        {
            var mission = CreateMission();
            var before = mission.Snapshot;

            mission.Tick(1f, 1, 0);

            Assert.That(before.RouteDistance, Is.EqualTo(0f));
            Assert.That(before.ElapsedTime, Is.EqualTo(0f));
            Assert.That(mission.Snapshot.RouteDistance, Is.EqualTo(10f));
            Assert.That(mission.Snapshot.ElapsedTime, Is.EqualTo(1f));
        }

        private static PayloadMissionStateMachine CreateMission(
            float deploymentDuration = 0f,
            bool requiresRecovery = false,
            float activationDuration = 4f)
        {
            return new PayloadMissionStateMachine(
                100f,
                new[] { 25f, 50f, 75f },
                10f,
                deploymentDuration,
                requiresRecovery,
                activationDuration);
        }
    }
}
