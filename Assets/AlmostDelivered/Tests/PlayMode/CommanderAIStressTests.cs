using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AlmostDelivered.Combat;
using AlmostDelivered.CommanderAI.Commander;
using AlmostDelivered.CommanderAI.Debugging;
using AlmostDelivered.CommanderAI.Domain;
using AlmostDelivered.CommanderAI.Execution;
using AlmostDelivered.CommanderAI.Perception;
using AlmostDelivered.CommanderAI.Tactics;
using NUnit.Framework;
using Unity.Profiling;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace AlmostDelivered.Tests.PlayMode
{
    public sealed class CommanderAIStressTests
    {
        private readonly List<Rig> rigs = new List<Rig>();
        private readonly List<GameObject> created = new List<GameObject>();
        private CommanderArena arena;
        private static readonly string[] MarkerNames = {
            "CommanderAI.CommandTick",
            "CommanderAI.PerceptionBatch", "CommanderAI.KnowledgeMerge", "CommanderAI.KnowledgeDecay",
            "CommanderAI.UtilityEvaluation", "CommanderAI.TacticalAllocation", "CommanderAI.OrderReassignment",
            "CommanderAI.PlannerReachability", "CommanderAI.MovementPathRequest"
        };

        [UnitySetUp] public IEnumerator Load()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(
                "Assets/AlmostDelivered/Scenes/CommanderAITest.unity", new LoadSceneParameters(LoadSceneMode.Single));
            arena = Object.FindObjectOfType<CommanderArena>();
            Assert.That(arena, Is.Not.Null);
            arena.Brain.enabled = false;
            arena.GetComponent<CommanderDebugView>().enabled = false;
            foreach (var member in arena.Members)
            {
                // Keep dense member capsules out of the arena's layer-0 sight obstruction mask.
                // Real environment LOS and NavMesh avoidance remain active.
                member.gameObject.layer = 8;
                member.GetComponent<MonsterPerception>().enabled = false;
                member.GetComponent<AlertSignal>().Cancel();
            }
        }

        [UnityTearDown] public IEnumerator Cleanup()
        {
            foreach (var item in created) if (item != null) Object.Destroy(item);
            created.Clear(); rigs.Clear();
            var empty = SceneManager.CreateScene("Stress cleanup");
            SceneManager.SetActiveScene(empty);
            yield return SceneManager.UnloadSceneAsync(arena.gameObject.scene);
        }

        // Removing cycle gating, allowing duplicate reservations, or emitting non-finite state breaks these tests.
        [UnityTest] public IEnumerator OneSquad_NormalProfile_PreservesInvariantsAndRecordsCost()
        { yield return Run(1, 1, 6); }

        [UnityTest] public IEnumerator FiveSquads_FortyMembers_HardProfile_PreservesInvariantsAndRecordsCost()
        { yield return Run(5, 2, 11); }

        private IEnumerator Run(int squadCount, int difficulty, int maximumCycles)
        {
            arena.SelectDifficulty(difficulty);
            rigs.Add(new Rig(arena.Squad, arena.Brain, arena.Allocator, arena.Members));
            for (var index = 1; index < squadCount; index++) AddSquad(index);
            foreach (var rig in rigs)
            {
                Assert.That(rig.Squad.LivingMembers.Count, Is.EqualTo(8));
                Assert.That(rig.Squad.TryCompleteAlert(1), Is.True);
                foreach (var member in rig.Members)
                {
                    var sense = member.GetComponent<MonsterPerception>();
                    sense.enabled = true;
                    sense.SensingInterval = 1000;
                    sense.Configure(rig.Squad, Array.IndexOf(rig.Members, member) + 1);
                }
            }
            var samplers = MarkerNames.Select(name => new Samples(name)).ToArray();
            // Integer origin keeps exact cycle boundaries while aligning observation and command clocks.
            var origin = Mathf.Ceil(Time.unscaledTime);
            var wall = System.Diagnostics.Stopwatch.StartNew();
            var movingSamples = 0; var pendingSamples = 0; var invalidSamples = 0;
            try
            {
                // Fixed commander interval [0,15] at 0.05s; navigation uses real engine time.
                for (var step = 0; step <= 300; step++)
                {
                    Physics.SyncTransforms();
                    foreach (var rig in rigs)
                    {
                        if (step % 4 == 0)
                            foreach (var member in rig.Members) member.GetComponent<MonsterPerception>().Sense();
                        var previous = rig.Brain.EvaluationHistory.Count;
                        rig.Brain.Tick(origin + step * .05f);
                        rig.Activated |= rig.Brain.ActiveEvaluation != null;
                        var delta = rig.Brain.EvaluationHistory.Count - previous;
                        Assert.That(delta, Is.EqualTo(0).Or.EqualTo(3), "one batch at most per tick");
                        if (delta > 0) rig.Cycles++;
                        Assert.That(rig.Cycles, Is.LessThanOrEqualTo(maximumCycles));
                        AssertSafe(rig);
                        foreach (var member in rig.Members)
                        {
                            var agent = member.GetComponent<NavMeshAgent>();
                            if (agent.velocity.sqrMagnitude > .01f) movingSamples++;
                            if (agent.pathPending) pendingSamples++;
                            if (agent.hasPath && agent.pathStatus != NavMeshPathStatus.PathComplete) invalidSamples++;
                        }
                    }
                    yield return null;
                }
                wall.Stop();
                foreach (var sample in samplers) sample.Capture();
                foreach (var rig in rigs)
                {
                    Debug.Log($"STRESS_RIG cycles={rig.Cycles} knowledge={rig.Squad.Knowledge.Snapshot.Count} active={rig.Brain.ActiveEvaluation?.TacticId} reason={rig.Brain.DecisionReason} latest={string.Join(",", rig.Brain.EvaluationHistory.Skip(Math.Max(0, rig.Brain.EvaluationHistory.Count - 3)).Select(e => e.TacticId + ":" + e.TotalScore + ":" + e.RejectionReason))}");
                    Assert.That(rig.Cycles, Is.EqualTo(maximumCycles));
                    Assert.That(rig.Squad.Knowledge.Snapshot, Is.Not.Empty, "sight must populate knowledge");
                    Assert.That(rig.Activated, Is.True, "playbook must execute during the interval");
                }
                Debug.Log($"COMMANDER_STRESS squads={squadCount} ordinaryMembers={squadCount * 8} commanders={squadCount} players=3 profile={arena.Profile.name} cycleSeconds={arena.Profile.CommandCycleSeconds} simulatedSeconds=15 frames=301 cycles={string.Join(",", rigs.Select(rig => rig.Cycles))} wallMs={wall.Elapsed.TotalMilliseconds:F3} movingAgentFrameSamples={movingSamples} pathPendingAgentFrameSamples={pendingSamples} nonCompletePathAgentFrameSamples={invalidSamples}");
                foreach (var sample in samplers)
                {
                    Debug.Log(sample.Describe());
                    Assert.That(sample.Blocks, Is.GreaterThan(0), "Missing CPU evidence for " + sample.Name);
                }
                Assert.That(samplers[0].Blocks, Is.EqualTo(301L * squadCount), "capture must include every command tick");
                Assert.That(movingSamples, Is.GreaterThan(0), "navigation must run");
                Assert.That(invalidSamples, Is.Zero);
            }
            finally { foreach (var sample in samplers) sample.Dispose(); }
        }

        private void AddSquad(int index)
        {
            var root = new GameObject("Stress squad " + index); created.Add(root);
            var squad = root.AddComponent<SquadController>();
            var commander = root.AddComponent<ActorHealth>();
            var brain = root.AddComponent<CommanderBrain>(); brain.enabled = false;
            var offset = new Vector3(0, 0, index * 2);
            var anchors = arena.Anchors.Select(original => {
                var clone = Object.Instantiate(original, root.transform);
                clone.transform.position = original.transform.position + offset;
                return clone;
            }).ToArray();
            var members = arena.Members.Select((original, memberIndex) => {
                var clone = Object.Instantiate(original, root.transform);
                var agent = clone.GetComponent<NavMeshAgent>();
                Assert.That(agent.Warp(original.transform.position + offset), Is.True);
                clone.GetComponent<AlertSignal>().Cancel();
                Assert.That(squad.RegisterMember(memberIndex + 1, (MonsterRole)(memberIndex / 2),
                    clone.GetComponent<ActorHealth>(), clone), Is.True);
                return clone;
            }).ToArray();
            var allocator = new TacticalSlotAllocator(anchors, new NavMeshTacticalReachability());
            brain.Configure(squad, commander, arena.Profile, arena.Tactics, anchors, allocator, arena.Objective.position);
            rigs.Add(new Rig(squad, brain, allocator, members));
        }

        private static void AssertSafe(Rig rig)
        {
            var occupancy = new Dictionary<TacticalAnchor, int>();
            foreach (var member in rig.Members)
            {
                Assert.That(Finite(member.transform.position.x) && Finite(member.transform.position.y) && Finite(member.transform.position.z), Is.True);
                var anchor = member.CurrentOrder.Anchor;
                if (anchor == null) continue;
                occupancy.TryGetValue(anchor, out var count); occupancy[anchor] = count + 1;
                Assert.That(occupancy[anchor], Is.LessThanOrEqualTo(anchor.Capacity), "duplicate reservation");
            }
            Assert.That(rig.Allocator.ReservationCount, Is.EqualTo(occupancy.Values.Sum()));
            foreach (var evaluation in rig.Brain.EvaluationHistory)
            {
                Assert.That(Finite(evaluation.TotalScore), Is.True);
                foreach (var score in evaluation.ComponentScores.Values) Assert.That(Finite(score), Is.True);
            }
            foreach (var memory in rig.Squad.Knowledge.Snapshot)
                Assert.That(Finite(memory.Confidence), Is.True);
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private sealed class Rig
        {
            public readonly SquadController Squad; public readonly CommanderBrain Brain;
            public readonly TacticalSlotAllocator Allocator; public readonly MonsterOrderExecutor[] Members;
            public int Cycles;
            public bool Activated;
            public Rig(SquadController squad, CommanderBrain brain, TacticalSlotAllocator allocator, MonsterOrderExecutor[] members)
            { Squad = squad; Brain = brain; Allocator = allocator; Members = members; }
        }

        private sealed class Samples : IDisposable
        {
            public readonly string Name;
            private ProfilerRecorder recorder;
            public long Nanoseconds, Blocks, MaximumFrameNanoseconds;
            public Samples(string name)
            {
                Name = name;
                recorder = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, name, 512,
                    ProfilerRecorderOptions.Default | ProfilerRecorderOptions.SumAllSamplesInFrame);
            }
            public void Capture()
            {
                if (!recorder.Valid || recorder.Count == 0) return;
                recorder.Stop();
                for (var index = 0; index < recorder.Count; index++)
                {
                    var sample = recorder.GetSample(index);
                    Nanoseconds += sample.Value; Blocks += sample.Count;
                    MaximumFrameNanoseconds = Math.Max(MaximumFrameNanoseconds, sample.Value);
                }
            }
            public string Describe() => $"COMMANDER_CPU marker={Name} blocks={Blocks} totalMs={Nanoseconds / 1000000d:F6} meanBlockUs={(Blocks == 0 ? 0 : Nanoseconds / (1000d * Blocks)):F3} maxFrameMs={MaximumFrameNanoseconds / 1000000d:F6}";
            public void Dispose() { recorder.Dispose(); }
        }
    }
}
