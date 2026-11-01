using AlmostDelivered.Combat;
using AlmostDelivered.CommanderAI.Commander;
using AlmostDelivered.CommanderAI.Domain;
using NUnit.Framework;
using UnityEngine;

namespace AlmostDelivered.Tests.EditMode
{
    public sealed class SquadControllerTests
    {
        private GameObject root;
        private SquadController squad;
        private ActorHealth reporter;
        private const int ReporterId = 17;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Squad");
            squad = root.AddComponent<SquadController>();
            var member = new GameObject("Reporter");
            member.transform.SetParent(root.transform);
            reporter = member.AddComponent<ActorHealth>();
            squad.RegisterMember(ReporterId, reporter);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(root);

        private static ObservationSample Sample(int reporterId = ReporterId) =>
            new ObservationSample(42, Vector3.forward, Vector3.right, 1f, 1f,
                ObservedCombatTag.Shield, reporterId);

        [Test]
        public void InitialState_IsUnaware() => Assert.That(squad.State, Is.EqualTo(SquadAlertState.Unaware));

        [Test]
        public void Report_WhileUnaware_IsIgnored()
        {
            Assert.That(squad.Report(Sample()), Is.False);
            Assert.That(squad.Knowledge.Snapshot, Is.Empty);
        }

        [Test]
        public void TryCompleteAlert_LivingRegisteredReporter_ChangesStateOnce()
        {
            Assert.That(squad.TryCompleteAlert(ReporterId), Is.True);
            Assert.That(squad.State, Is.EqualTo(SquadAlertState.Alerted));
            Assert.That(squad.TryCompleteAlert(ReporterId), Is.False);
            Assert.That(squad.Knowledge.Snapshot, Is.Empty);
        }

        [Test]
        public void Report_AfterAlert_MergesImmediately()
        {
            squad.TryCompleteAlert(ReporterId);
            Assert.That(squad.Report(Sample()), Is.True);
            Assert.That(squad.Knowledge.TryGet(42, out var memory), Is.True);
            Assert.That(memory.LastKnownPosition, Is.EqualTo(Vector3.forward));
            Assert.That(memory.ReporterId, Is.EqualTo(ReporterId));
        }

        [Test]
        public void Confused_RejectsAlertsAndReports()
        {
            squad.EnterConfused();
            Assert.That(squad.State, Is.EqualTo(SquadAlertState.Confused));
            Assert.That(squad.TryCompleteAlert(ReporterId), Is.False);
            Assert.That(squad.Report(Sample()), Is.False);
            Assert.That(squad.Knowledge.Snapshot, Is.Empty);
        }

        [Test]
        public void UnknownReporter_CannotAlertOrReport()
        {
            Assert.That(squad.TryCompleteAlert(99), Is.False);
            squad.TryCompleteAlert(ReporterId);
            Assert.That(squad.Report(Sample(99)), Is.False);
            Assert.That(squad.Knowledge.Snapshot, Is.Empty);
        }

        [Test]
        public void DeadReporter_CannotAlertOrReport()
        {
            reporter.ApplyDamage(100f);
            Assert.That(squad.TryCompleteAlert(ReporterId), Is.False);
            var replacement = root.AddComponent<ActorHealth>();
            squad.RegisterMember(18, replacement);
            squad.TryCompleteAlert(18);
            Assert.That(squad.Report(Sample()), Is.False);
        }

        [Test]
        public void UnregisteredReporter_CannotAlertOrReport()
        {
            squad.UnregisterMember(ReporterId);
            Assert.That(squad.TryCompleteAlert(ReporterId), Is.False);
            squad.RegisterMember(18, reporter);
            squad.TryCompleteAlert(18);
            Assert.That(squad.Report(Sample()), Is.False);
        }

        [Test]
        public void Confused_AfterAlert_RejectsFurtherKnowledge()
        {
            squad.TryCompleteAlert(ReporterId);
            squad.EnterConfused();
            Assert.That(squad.Report(Sample()), Is.False);
            Assert.That(squad.TryCompleteAlert(ReporterId), Is.False);
            Assert.That(squad.Knowledge.Snapshot, Is.Empty);
        }
    }
}
