# Commander AI CPU baseline

Measured on 2026-11-02 KST using the final full PlayMode run, `TestResults/task8-playmode-verified.xml` and `Logs/task8-playmode-verified.log`. These are local ignored test artifacts. This is an Editor CPU baseline, not a platform performance target, FPS promise, or production player benchmark.

## Environment and workload

| Context | Recorded value |
| --- | --- |
| Unity | 2021.3.45f2, revision 88f88f591b2e |
| Test framework | Existing Unity Test Framework 1.1.33; no added packages |
| Host | AMD Ryzen 7 5800X, 8 physical / 16 logical cores |
| RAM | 85,782,876,160 bytes (approximately 79.9 GiB) |
| OS | Windows 11 Home, 10.0.26200, 64 bit |
| GPU | NVIDIA GeForce RTX 3070; headless `-nographics`, so GPU performance was not measured |
| Runtime | Windows Unity Editor batchmode, Editor/Mono execution, no Deep Profile; power mode/background load were not controlled |
| Baseline actors | 1 squad, 8 ordinary members, 1 commander, 3 observable players/dummies: 12 actors |
| Stress actors | 5 independent squads, 40 ordinary members, 5 commanders, same 3 players/dummies: 48 actors |
| Baseline command settings | Normal: 3-second cycle, 1-second telegraph, 6-second hold, 0.1 switch margin |
| Stress command settings | Hard: 1.5-second cycle, 0.5-second telegraph, 3-second hold, 0.05 switch margin |
| Memory | Serialized ten-second lifetime, identical across difficulty; decay occurs only on command cycles |
| Test interval | 301 deterministic command ticks per squad, inclusive simulated interval [0,15] at 0.05-second steps |

The stress fixture loads the actual baked arena and clones four squads with their own members, anchors, brain, knowledge, and allocator. Clones are offset two units along Z per squad, deliberately putting real agents in a crowded shared navigation area. It directly completes the initial alert to isolate steady-state cost; separate alert tests validate the interruptible signal. All eight members sense every fourth simulated step (76 batches of eight per squad), using real overlap/raycast sight. Member capsules use test-only layer 8 so they do not randomly occlude one another in the arena's layer-0 environment obstruction mask; environment LOS and NavMesh avoidance remain active. The standard overlay is disabled during collection.

Command time starts at an integer ceiling of engine unscaled time, preserving exact 3/1.5-second boundaries. Perception timestamps and agent motion use actual engine time. The accelerated 15-second command simulation therefore runs in less than one real second; toward its end it intentionally ages observations to zero and exercises fallback reassignment. It is not fifteen seconds of real-time crowd movement. The separate eight-real-second arena arrival test covers baseline arrival. Do not infer sustained avoidance quality from this short stress capture.

## Captured CPU samples

`ProfilerRecorder` collects named Scripts-category markers into a 512-frame buffer with samples summed per engine frame. Tests stop and read the complete buffer once, asserting exactly 301/1505 command-tick blocks so truncation or missing samples fails. Values below are total milliseconds / invocation count / largest summed frame milliseconds. Marker scope includes the code named, not all engine work caused asynchronously by that code.

| Marker (`CommanderAI.` prefix) | One squad: total ms / blocks / max frame ms | Five squads: total ms / blocks / max frame ms |
| --- | ---: | ---: |
| CommandTick | 0.836000 / 301 / 0.117100 | 22.115400 / 1505 / 11.646600 |
| PerceptionBatch | 12.076400 / 608 / 0.263400 | 64.467300 / 3040 / 1.361500 |
| KnowledgeMerge | 1.003700 / 1824 / 0.021400 | 4.806800 / 9120 / 0.173000 |
| KnowledgeDecay | 0.011600 / 6 / 0.003200 | 0.869400 / 55 / 0.797800 |
| UtilityEvaluation | 0.094700 / 18 / 0.017100 | 3.220300 / 165 / 2.744400 |
| TacticalAllocation | 0.061100 / 8 / 0.061100 | 1.695700 / 40 / 1.695700 |
| OrderReassignment | 0.007800 / 24 / 0.002700 | 0.081800 / 200 / 0.025400 |
| PlannerReachability | 0.196300 / 79 / 0.032700 | 1.852200 / 670 / 0.474100 |
| MovementPathRequest | 3.398000 / 1760 / 0.024200 | 13.983100 / 8000 / 0.294500 |

The disjoint top-level instrumented subtotal (`CommandTick + PerceptionBatch + MovementPathRequest`) is 16.310400 ms for one squad and 100.565800 ms for five. Wall elapsed for the 301-frame fixture is 114.136 ms and 393.009 ms respectively, including assertions, coroutine scheduling, and other engine/test work. Neither number is a total-game CPU budget. We did not capture full PlayerLoop/native-worker CPU, rendering, animation, GPU, GC allocation bytes, or networking. Those require a representative player profiling session.

Nested scopes overlap: PerceptionBatch includes KnowledgeMerge; CommandTick includes context creation, KnowledgeDecay, UtilityEvaluation, allocation/assignment, and planning path checks. Do not add every row. OrderReassignment measures `MonsterOrderExecutor.Assign`, including its local stop/release work; it does not claim to time all asynchronous navigation. Five squads run first and include first-use/JIT costs, visible in the first command-tick maximum. The one-squad and five-squad profiles differ, so these captures are not a controlled scaling ratio or statistical distribution.

## Navigation and invariants

| Observation | One squad | Five squads |
| --- | ---: | ---: |
| Actual command cycles per squad | 6 | 11, 11, 11, 11, 11 |
| Synchronous planner path-check calls | 79 | 670 |
| Movement path-request blocks | 1760 | 8000 |
| Agent/frame samples with nonzero movement | 1946 | 11158 |
| Agent/frame samples with path pending | 5 | 27 |
| Sampled existing paths not complete | 0 | 0 |
| Peak intended ordinary members | 8 | 40 |

A movement marker wraps the executor's `CalculatePath` and, when complete, `SetDestination`; the block count is attempted movement-path work rather than a native asynchronous path-job count. Planner reachability wraps `NavMesh.CalculatePath` and path completeness. Default agent avoidance stays enabled and agents demonstrably move. Finite positions/scores, reservation capacity and allocator/order agreement, no excess command cycles, and nonzero samples for all markers passed. The runner reported no exceptions or unexpected error logs. No machine-specific millisecond threshold is asserted.

## Bottlenecks and follow-up

- Perception overlap/raycast batches cost more than utility scoring in this capture. Overlap/raycast arrays, snapshots, context dictionaries, and score/history objects allocate; GC bytes were not measured.
- `MonsterOrderExecutor.Move` recalculates a complete path and reissues `SetDestination` every moving frame. Cache destinations/paths or rate-limit requests only after a representative player capture confirms the benefit.
- `BuildContext` repeatedly obtains sorted living-member lists and computes reachability; the spec's proposed per-cycle path cache is still absent. Allocation and cleanup also scan collections.
- Evaluation history grows without a retention limit. Long-session memory behavior remains unmeasured.
- Native NavMesh avoidance cost and crowd congestion/arrival time at forty members were not isolated. The stress run is too short in actual engine time to validate sustained crowd behavior.
- Production art/animation, full role abilities, networking/replication, a player build, and a declared target platform remain future profiling workloads.
- Task 7's deferred Minor remains diagnostic polish: `CommanderGizmoDrawer` can briefly show an invalid released anchor as Reserved while paused. It is deliberately unchanged. Gizmo classification also recomputes member/reachability queries; its diagnostic cost is outside this headless baseline.

Final verification: 104 EditMode tests and 60 PlayMode tests passed with Unity exit 0. Arena regeneration and semantic comparison are recorded in the Task 8 report. Manual screenshot/layout review and the [portfolio playthrough](portfolio-demo.md) remain pending; automated evidence does not replace that sign-off. Reproduce using [test commands](tests.md).
