using AlmostDelivered.Gameplay.Payload;
using NUnit.Framework;
using UnityEngine;

namespace AlmostDelivered.Tests.PlayMode
{
    public sealed class PayloadRelayBeaconViewTests
    {
        private GameObject root;
        private PayloadMissionController mission;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Relay beacon test");
            var player = Child("Player");
            var payload = Child("Payload");
            mission = Child("Mission").AddComponent<PayloadMissionController>();
            mission.Configure(
                false,
                4704,
                new Rect(-28f, -50f, 56f, 100f),
                player.transform,
                payload.transform,
                null,
                null,
                1f,
                0f,
                4.5f);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(root);
        }

        [Test]
        public void OnlyNextRelayIsActive()
        {
            var first = NewRelay(0, false);
            var second = NewRelay(1, false);

            Apply(PayloadMissionPhase.Escorting, 0);

            Assert.That(first.View.VisualState, Is.EqualTo(PayloadRelayVisualState.Active));
            Assert.That(first.Beam.activeSelf, Is.True);
            Assert.That(first.Ring.activeSelf, Is.True);
            Assert.That(second.View.VisualState, Is.EqualTo(PayloadRelayVisualState.Future));
            Assert.That(second.Beam.activeSelf, Is.False);
            Assert.That(second.Ring.activeSelf, Is.False);
        }

        [Test]
        public void CheckpointDefenseShowsLoadingState()
        {
            var relay = NewRelay(0, false);

            Apply(PayloadMissionPhase.CheckpointDefense, 0);

            Assert.That(relay.View.VisualState, Is.EqualTo(PayloadRelayVisualState.Loading));
            Assert.That(relay.Beam.activeSelf, Is.True);
            Assert.That(relay.Ring.activeSelf, Is.True);
        }

        [Test]
        public void CompletedCheckpointHidesCacheAndShowsCollectedCargo()
        {
            var relay = NewRelay(0, false);

            Apply(PayloadMissionPhase.Escorting, 1);

            Assert.That(relay.View.VisualState, Is.EqualTo(PayloadRelayVisualState.Completed));
            Assert.That(relay.Cache.activeSelf, Is.False);
            Assert.That(relay.Cargo.activeSelf, Is.True);
            Assert.That(relay.Beam.activeSelf, Is.False);
        }

        [Test]
        public void FinalDestinationUsesDestinationState()
        {
            var relay = NewRelay(3, true);

            Apply(PayloadMissionPhase.Escorting, 3);

            Assert.That(relay.View.VisualState, Is.EqualTo(PayloadRelayVisualState.DestinationActive));
            Assert.That(relay.Beam.activeSelf, Is.True);
            Assert.That(relay.Ring.activeSelf, Is.True);
        }

        [Test]
        public void RelayWithMissingOptionalVisualsStillTracksState()
        {
            var view = Child("Sparse Relay").AddComponent<PayloadRelayBeaconView>();
            view.Configure(mission, 1, false, null, null, null, null);

            Assert.DoesNotThrow(() => Apply(PayloadMissionPhase.Escorting, 1));
            Assert.That(view.VisualState, Is.EqualTo(PayloadRelayVisualState.Active));
        }

        private RelayParts NewRelay(int checkpointIndex, bool finalDestination)
        {
            var relay = Child($"Relay {checkpointIndex}");
            var beam = Child("Beam", relay.transform);
            var ring = Child("Pulse Ring", relay.transform);
            var cache = Child("Supply Cache", relay.transform);
            var cargo = Child("Collected Cargo", relay.transform);
            var view = relay.AddComponent<PayloadRelayBeaconView>();
            view.Configure(mission, checkpointIndex, finalDestination, beam, ring, cache, cargo);
            return new RelayParts(view, beam, ring, cache, cargo);
        }

        private void Apply(PayloadMissionPhase phase, int nextCheckpointIndex)
        {
            Assert.That(mission.ApplySnapshot(new PayloadMissionSnapshot(
                phase,
                10f,
                20f,
                .25f,
                nextCheckpointIndex,
                -1,
                0,
                1,
                0,
                phase == PayloadMissionPhase.Escorting,
                phase == PayloadMissionPhase.CheckpointDefense || phase == PayloadMissionPhase.FinalDefense)), Is.True);
        }

        private GameObject Child(string name, Transform parent = null)
        {
            var value = new GameObject(name);
            value.transform.SetParent(parent == null ? root.transform : parent, false);
            return value;
        }

        private readonly struct RelayParts
        {
            public RelayParts(PayloadRelayBeaconView view, GameObject beam, GameObject ring, GameObject cache, GameObject cargo)
            {
                View = view;
                Beam = beam;
                Ring = ring;
                Cache = cache;
                Cargo = cargo;
            }

            public PayloadRelayBeaconView View { get; }
            public GameObject Beam { get; }
            public GameObject Ring { get; }
            public GameObject Cache { get; }
            public GameObject Cargo { get; }
        }
    }
}
