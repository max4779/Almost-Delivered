using System;
using System.Collections;
using AlmostDelivered.Combat;
using AlmostDelivered.CommanderAI.Commander;
using AlmostDelivered.CommanderAI.Domain;
using AlmostDelivered.CommanderAI.Perception;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.LowLevel;
using UnityEngine.TestTools;

namespace AlmostDelivered.Tests.PlayMode
{
    public sealed class AlertFlowTests
    {
        private GameObject root;
        private SquadController squad;
        private ActorHealth health;
        private StunState stun;
        private MonsterPerception perception;
        private AlertSignal signal;
        private ObservableActor target;
        private const int ReporterId = 17;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Alert test squad");
            squad = root.AddComponent<SquadController>();
            var member = new GameObject("Reporter");
            member.transform.SetParent(root.transform);
            health = member.AddComponent<ActorHealth>();
            stun = member.AddComponent<StunState>();
            perception = member.AddComponent<MonsterPerception>();
            signal = member.AddComponent<AlertSignal>();
            squad.RegisterMember(ReporterId, health);
            perception.SensingInterval = 100f;
            perception.Configure(squad, ReporterId);
            signal.Configure(squad, ReporterId, 0.15f);

            var seen = new GameObject("Visible target");
            seen.transform.SetParent(root.transform);
            seen.transform.position = Vector3.forward * 3f;
            target = seen.AddComponent<ObservableActor>();
            seen.AddComponent<SphereCollider>();
            Physics.SyncTransforms();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            UnityEngine.Object.Destroy(root);
            yield return null;
        }

        [UnityTest]
        public IEnumerator AlertSignal_AfterConfiguredDuration_AlertsAndSharesFirstObservation()
        {
            signal.Begin(target);
            yield return new WaitForSecondsRealtime(0.05f);
            Assert.That(squad.State, Is.EqualTo(SquadAlertState.Unaware));
            Assert.That(squad.Knowledge.Snapshot, Is.Empty);
            yield return new WaitForSecondsRealtime(0.2f);
            Assert.That(squad.State, Is.EqualTo(SquadAlertState.Alerted));
            Assert.That(squad.Knowledge.TryGet(target.ActorId, out var memory), Is.True);
            Assert.That(memory.LastKnownPosition, Is.EqualTo(Vector3.forward * 3f));
            Assert.That(signal.IsSignaling, Is.False);
        }

        [UnityTest]
        public IEnumerator AlertSignal_Stun_Cancels()
        {
            signal.Begin(target);
            stun.Apply(1f, Time.unscaledTime);
            yield return new WaitForSecondsRealtime(0.25f);
            Assert.That(squad.State, Is.EqualTo(SquadAlertState.Unaware));
            Assert.That(signal.IsSignaling, Is.False);
        }

        [UnityTest]
        public IEnumerator AlertSignal_ReporterDeath_Cancels()
        {
            signal.Begin(target);
            health.ApplyDamage(100f);
            yield return new WaitForSecondsRealtime(0.25f);
            Assert.That(squad.State, Is.EqualTo(SquadAlertState.Unaware));
            Assert.That(signal.IsSignaling, Is.False);
        }

        [UnityTest]
        public IEnumerator AlertSignal_TargetOutOfSight_Cancels()
        {
            signal.Begin(target);
            target.transform.position = Vector3.forward * 100f;
            yield return new WaitForSecondsRealtime(0.25f);
            Assert.That(squad.State, Is.EqualTo(SquadAlertState.Unaware));
            Assert.That(signal.IsSignaling, Is.False);
        }

        [UnityTest]
        public IEnumerator AlertSignal_CancelOnCompletionFrame_RemainsUnaware()
        {
            var canceller = signal.gameObject.AddComponent<CompletionFrameCanceller>();
            canceller.Signal = signal;
            canceller.Deadline = Time.unscaledTime + 0.15f;
            signal.Begin(target);
            yield return new WaitForSecondsRealtime(0.25f);
            Assert.That(canceller.DidCancel, Is.True);
            Assert.That(squad.State, Is.EqualTo(SquadAlertState.Unaware));
            Assert.That(squad.Knowledge.Snapshot, Is.Empty);
        }

        [UnityTest]
        public IEnumerator AlertSignal_CancelAfterLateUpdateOnCompletionFrame_RemainsUnaware() =>
            InterruptAfterLateUpdate(() => signal.Cancel());

        [UnityTest]
        public IEnumerator AlertSignal_StunAfterLateUpdateOnCompletionFrame_RemainsUnaware() =>
            InterruptAfterLateUpdate(() => stun.Apply(1f, Time.unscaledTime));

        [UnityTest]
        public IEnumerator AlertSignal_DeathAfterLateUpdateOnCompletionFrame_RemainsUnaware() =>
            InterruptAfterLateUpdate(() => health.ApplyDamage(100f));

        [UnityTest]
        public IEnumerator AlertSignal_TargetLossAfterLateUpdateOnCompletionFrame_RemainsUnaware() =>
            InterruptAfterLateUpdate(() => target.transform.position = Vector3.forward * 100f);

        private IEnumerator InterruptAfterLateUpdate(Action interrupt)
        {
            var observations = 0;
            perception.ObservationProduced += _ => observations++;
            using (var interruption = new AfterLateUpdateAction(Time.unscaledTime + 0.15f, interrupt))
            {
                signal.Begin(target);
                yield return new WaitForSecondsRealtime(0.25f);
                Assert.That(interruption.DidInvoke, Is.True, "The due frame must reach PostLateUpdate.");
                Assert.That(squad.State, Is.EqualTo(SquadAlertState.Unaware));
                Assert.That(squad.Knowledge.Snapshot, Is.Empty);
                Assert.That(observations, Is.Zero);
                Assert.That(signal.IsSignaling, Is.False);
                Assert.That(signal.GetComponentInChildren<TextMesh>(true).text, Does.Contain("cancelled"));
            }
        }

        [UnityTest]
        public IEnumerator AlertSignal_DueFrame_PublishesOnlyAfterFrameHasPassed()
        {
            var stateOnDueFrame = SquadAlertState.Confused;
            var memoriesOnDueFrame = -1;
            var observations = 0;
            var observationsOnDueFrame = -1;
            perception.ObservationProduced += _ => observations++;
            using (var inspection = new AfterLateUpdateAction(Time.unscaledTime + 0.15f, () =>
                   {
                       stateOnDueFrame = squad.State;
                       memoriesOnDueFrame = squad.Knowledge.Snapshot.Count;
                       observationsOnDueFrame = observations;
                   }))
            {
                signal.Begin(target);
                yield return new WaitForSecondsRealtime(0.25f);
                Assert.That(inspection.DidInvoke, Is.True);
                Assert.That(stateOnDueFrame, Is.EqualTo(SquadAlertState.Unaware));
                Assert.That(memoriesOnDueFrame, Is.Zero);
                Assert.That(observationsOnDueFrame, Is.Zero);
                Assert.That(squad.State, Is.EqualTo(SquadAlertState.Alerted));
                Assert.That(squad.Knowledge.TryGet(target.ActorId, out _), Is.True);
                Assert.That(observations, Is.EqualTo(1));
            }
        }

        [UnityTest]
        public IEnumerator AlertSignal_ConfusedDuringSignal_DoesNotAlert()
        {
            signal.Begin(target);
            squad.EnterConfused();
            yield return new WaitForSecondsRealtime(0.25f);
            Assert.That(squad.State, Is.EqualTo(SquadAlertState.Confused));
            Assert.That(signal.IsSignaling, Is.False);
            Assert.That(squad.Knowledge.Snapshot, Is.Empty);
        }

        [UnityTest]
        public IEnumerator AlertSignal_TargetDisabled_Cancels()
        {
            signal.Begin(target);
            target.gameObject.SetActive(false);
            yield return new WaitForSecondsRealtime(0.25f);
            Assert.That(squad.State, Is.EqualTo(SquadAlertState.Unaware));
        }

        [UnityTest]
        public IEnumerator AlertSignal_RepeatedBegin_DoesNotRestartDuration()
        {
            signal.Begin(target);
            var until = Time.unscaledTime + 0.25f;
            while (Time.unscaledTime < until)
            {
                signal.Begin(target);
                yield return null;
            }
            Assert.That(squad.State, Is.EqualTo(SquadAlertState.Alerted));
        }

        [Test]
        public void Perception_UnseenUseThenSighting_DoesNotLearnAbilityHistory()
        {
            squad.TryCompleteAlert(ReporterId);
            target.transform.position = Vector3.back * 3;
            target.RevealTag(ObservedCombatTag.Healing);
            target.transform.position = Vector3.forward * 3;
            Physics.SyncTransforms(); perception.Sense();
            Assert.That(squad.Knowledge.TryGet(target.ActorId, out var memory), Is.True);
            Assert.That(memory.Tags, Is.EqualTo(ObservedCombatTag.None));
        }

        [Test]
        public void Perception_RepeatedVisibleUse_IsLearnedOnlyByLivingVisibleObservers()
        {
            squad.TryCompleteAlert(ReporterId);
            target.transform.position = Vector3.back * 3;
            target.RevealTag(ObservedCombatTag.Healing);
            target.transform.position = Vector3.forward * 3;
            Physics.SyncTransforms(); perception.Sense();
            Assert.That(squad.Knowledge.TryGet(target.ActorId, out var before), Is.True);
            Assert.That(before.Tags, Is.EqualTo(ObservedCombatTag.None));
            target.RevealTag(ObservedCombatTag.Healing);
            Assert.That(squad.Knowledge.TryGet(target.ActorId, out var memory), Is.True);
            Assert.That(memory.Tags, Is.EqualTo(ObservedCombatTag.Healing));
            health.ApplyDamage(100);
            target.RevealTag(ObservedCombatTag.Shield);
            Assert.That(squad.Knowledge.TryGet(target.ActorId, out memory), Is.True);
            Assert.That(memory.Tags, Is.EqualTo(ObservedCombatTag.Healing));
        }

        [Test]
        public void Perception_IndependentSquad_DoesNotInheritAnotherObserversTags()
        {
            squad.TryCompleteAlert(ReporterId);
            var other = new GameObject("Independent observer"); other.transform.SetParent(root.transform);
            other.transform.position = Vector3.back * 100;
            var otherSquad = other.AddComponent<SquadController>();
            var otherHealth = other.AddComponent<ActorHealth>();
            var otherPerception = other.AddComponent<MonsterPerception>();
            otherSquad.RegisterMember(1, otherHealth); otherPerception.Configure(otherSquad, 1);
            otherSquad.TryCompleteAlert(1);
            target.RevealTag(ObservedCombatTag.Healing);
            other.transform.position = Vector3.zero;
            Physics.SyncTransforms(); otherPerception.Sense();
            Assert.That(otherSquad.Knowledge.TryGet(target.ActorId, out var memory), Is.True);
            Assert.That(memory.Tags, Is.EqualTo(ObservedCombatTag.None));
            target.RevealTag(ObservedCombatTag.Healing);
            otherPerception.Sense();
            Assert.That(otherSquad.Knowledge.TryGet(target.ActorId, out memory), Is.True);
            Assert.That(memory.Tags, Is.EqualTo(ObservedCombatTag.Healing));
        }

        [UnityTest]
        public IEnumerator AlertCue_StartProgressCompletionAndExpiry()
        {
            signal.Begin(target);
            var cue = signal.GetComponentInChildren<TextMesh>(true);
            Assert.That(cue, Is.Not.Null, "The reporting member needs a runtime world-space cue.");
            Assert.That(cue.font, Is.Not.Null, "A cue must have a glyph font, not just lifecycle state.");
            Assert.That(cue.GetComponent<MeshRenderer>().sharedMaterial, Is.EqualTo(cue.font.material));
            Assert.That(cue.gameObject.activeSelf, Is.True);
            var start = cue.text;
            yield return new WaitForSecondsRealtime(.08f);
            Assert.That(cue.text, Is.Not.EqualTo(start));
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That(cue.text, Does.Contain("alerted"));
            yield return new WaitForSecondsRealtime(.5f);
            Assert.That(cue.gameObject.activeSelf, Is.False);
        }

        [UnityTest] public IEnumerator AlertCue_CancelLifetime() => CueCancellation(() => signal.Cancel());
        [UnityTest] public IEnumerator AlertCue_StunLifetime() => CueCancellation(() => stun.Apply(1, Time.unscaledTime));
        [UnityTest] public IEnumerator AlertCue_DeathLifetime() => CueCancellation(() => health.ApplyDamage(100));
        [UnityTest] public IEnumerator AlertCue_TargetLossLifetime() => CueCancellation(() => target.transform.position = Vector3.back * 100);
        [UnityTest] public IEnumerator AlertCue_TargetDisabledLifetime() => CueCancellation(() => target.enabled = false);
        [UnityTest] public IEnumerator AlertCue_TargetDestroyedLifetime() => CueCancellation(() => UnityEngine.Object.Destroy(target.gameObject));
        [UnityTest] public IEnumerator AlertCue_TargetDeathLifetime() => CueCancellation(() => target.gameObject.AddComponent<ActorHealth>().ApplyDamage(100));
        [UnityTest] public IEnumerator AlertCue_ReporterUnregisteredLifetime() => CueCancellation(() => squad.UnregisterMember(ReporterId));
        [UnityTest] public IEnumerator AlertCue_ReconfigurationLifetime() => CueCancellation(() => signal.Configure(squad, ReporterId, .15f));
        [UnityTest] public IEnumerator AlertCue_CompetingCompletionLifetime() => CueCancellation(() => squad.TryCompleteAlert(ReporterId), expectedAlerted: true);
        [UnityTest] public IEnumerator AlertCue_PerceptionDisabledLifetime() => CueCancellation(() => perception.enabled = false);
        [UnityTest] public IEnumerator AlertCue_ConfusionLifetime() => CueCancellation(() => squad.EnterConfused());
        [UnityTest] public IEnumerator AlertCue_SignalDisabledHidesImmediately() => CueCancellation(() => signal.enabled = false, true);
        [UnityTest] public IEnumerator AlertCue_ReporterDisabledHidesImmediately() => CueCancellation(() => signal.gameObject.SetActive(false), true);

        private IEnumerator CueCancellation(Action cancel, bool disabled = false, bool expectedAlerted = false)
        {
            signal.Begin(target);
            var cue = signal.GetComponentInChildren<TextMesh>(true);
            Assert.That(cue, Is.Not.Null);
            cancel(); yield return null; yield return null;
            Assert.That(signal.IsSignaling, Is.False);
            if (!disabled) Assert.That(cue.text, Does.Contain("cancelled"));
            else Assert.That(cue.gameObject.activeSelf, Is.False);
            yield return new WaitForSecondsRealtime(.6f);
            Assert.That(cue.gameObject.activeSelf, Is.False);
            Assert.That(squad.State == SquadAlertState.Alerted, Is.EqualTo(expectedAlerted));
        }

        [Test]
        public void Perception_AfterAlert_SharesObservedFactsImmediately()
        {
            squad.TryCompleteAlert(ReporterId);
            target.RevealTag(ObservedCombatTag.RangedWeapon);
            var events = 0;
            ObservationSample observed = default;
            perception.ObservationProduced += sample => { events++; observed = sample; };
            perception.Sense();
            Assert.That(events, Is.EqualTo(1));
            Assert.That(observed.ActorId, Is.EqualTo(target.ActorId));
            Assert.That(observed.ReporterId, Is.EqualTo(ReporterId));
            Assert.That(squad.Knowledge.TryGet(target.ActorId, out var memory), Is.True);
            Assert.That(memory.Tags, Is.EqualTo(ObservedCombatTag.RangedWeapon));
        }

        [Test]
        public void Perception_BeforeAlert_StartsSignalWithoutSharingKnowledge()
        {
            perception.Sense();
            Assert.That(signal.IsSignaling, Is.True);
            Assert.That(squad.Knowledge.Snapshot, Is.Empty);
        }

        [Test]
        public void Perception_TargetOutsideFieldOfView_IsNotReported()
        {
            squad.TryCompleteAlert(ReporterId);
            target.transform.position = Vector3.back * 3f;
            Physics.SyncTransforms();
            perception.Sense();
            Assert.That(squad.Knowledge.Snapshot, Is.Empty);
        }

        [Test]
        public void Perception_ObstructedTarget_IsNotReported()
        {
            squad.TryCompleteAlert(ReporterId);
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.SetParent(root.transform);
            wall.transform.position = Vector3.forward * 1.5f;
            Physics.SyncTransforms();
            perception.Sense();
            Assert.That(squad.Knowledge.Snapshot, Is.Empty);
        }

        [Test]
        public void Perception_Confused_DoesNotProduceObservations()
        {
            squad.EnterConfused();
            var events = 0;
            perception.ObservationProduced += _ => events++;
            perception.Sense();
            Assert.That(events, Is.Zero);
            Assert.That(signal.IsSignaling, Is.False);
            Assert.That(squad.Knowledge.Snapshot, Is.Empty);
        }

        [UnityTest]
        public IEnumerator Perception_ScheduledSensing_DiscoversTarget()
        {
            perception.SensingInterval = 0.05f;
            perception.Configure(squad, ReporterId);
            yield return new WaitForSecondsRealtime(0.3f);
            Assert.That(squad.State, Is.EqualTo(SquadAlertState.Alerted));
            Assert.That(squad.Knowledge.TryGet(target.ActorId, out _), Is.True);
        }

        // A real PostLateUpdate callback runs after all behaviour LateUpdates, including
        // execution order 32000, without relying on rendered WaitForEndOfFrame in batch mode.
        private sealed class AfterLateUpdateAction : IDisposable
        {
            private readonly PlayerLoopSystem originalLoop;
            public bool DidInvoke { get; private set; }

            public AfterLateUpdateAction(float deadline, Action action)
            {
                originalLoop = PlayerLoop.GetCurrentPlayerLoop();
                var loop = originalLoop;
                loop.subSystemList = (PlayerLoopSystem[])originalLoop.subSystemList.Clone();
                for (var i = 0; i < loop.subSystemList.Length; i++)
                {
                    var phase = loop.subSystemList[i];
                    if (phase.type != typeof(UnityEngine.PlayerLoop.PostLateUpdate))
                        continue;
                    var systems = new PlayerLoopSystem[phase.subSystemList.Length + 1];
                    Array.Copy(phase.subSystemList, systems, phase.subSystemList.Length);
                    systems[systems.Length - 1] = new PlayerLoopSystem
                    {
                        type = typeof(AfterLateUpdateAction),
                        updateDelegate = () =>
                        {
                            if (DidInvoke || Time.unscaledTime < deadline)
                                return;
                            DidInvoke = true;
                            action();
                        }
                    };
                    phase.subSystemList = systems;
                    loop.subSystemList[i] = phase;
                    PlayerLoop.SetPlayerLoop(loop);
                    return;
                }
                throw new InvalidOperationException("PostLateUpdate player-loop phase was not found.");
            }

            public void Dispose() => PlayerLoop.SetPlayerLoop(originalLoop);
        }
    }

    // Cancellation in Update must win over the due alert's LateUpdate commit.
    [DefaultExecutionOrder(10000)]
    public sealed class CompletionFrameCanceller : MonoBehaviour
    {
        public AlertSignal Signal;
        public float Deadline;
        public bool DidCancel;

        private void Update()
        {
            if (DidCancel || Time.unscaledTime < Deadline)
                return;
            Signal.Cancel();
            DidCancel = true;
        }
    }
}
