# Commander AI Foundation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a local Unity prototype in which a killable squad commander turns shared monster observations into explainable Utility AI tactics and role-based battlefield placement, while commander death permanently removes squad coordination.

**Architecture:** Pure C# domain objects hold observations, tactical context, scores, and switching policy; thin MonoBehaviours adapt those objects to Unity perception, NavMesh movement, health, and scene objects. The commander chooses one data-driven tactical playbook per command cycle, allocates authored tactical anchors, telegraphs the order, and sends simple orders to members. The first slice is local-only but keeps all squad decisions behind `SquadController` so a later authoritative server can own the same boundary.

**Tech Stack:** Unity 2021.3.45f2, C# 9-compatible Unity scripting, Unity Test Framework 1.1.33, NUnit, Unity NavMesh/`NavMeshAgent`, ScriptableObject configuration, uGUI/OnGUI plus Gizmos for debug output.

**Spec:** `docs/superpowers/specs/2026-11-01-commander-ai-design.md`

## Global Constraints

- Use Unity 2021.3.45f2 and the packages already present in `Packages/manifest.json`.
- Add no third-party AI, networking, or testing packages.
- The first slice is local-only and contains one controllable player, two instrumented player test dummies, one squad, one commander, and eight ordinary monsters.
- Player-specific commander input must come only from `SquadKnowledge`; the commander must never inspect player components directly.
- Initial roles are Shield, Ranged, Charger, and Support; initial tactics are Objective Blockade, Pincer, and Focus Support.
- Information sharing is immediate only after the interruptible initial alert completes; reactions still wait for command cycle and telegraph.
- Commander death permanently puts surviving squad members into Confused with no promotion or recovery.
- Role-specific production abilities, multiplayer, moving cargo, capture progress, progression, reinforcement learning, and generative AI remain out of scope.
- Exact score weights, radii, and timing values live in configuration assets or serializable settings, never scattered magic constants.
- Keep runtime code under `Assets/AlmostDelivered`; do not modify imported third-party asset code.

## File Structure

Create these focused areas:

- `Assets/AlmostDelivered/Runtime/AlmostDelivered.Runtime.asmdef` — runtime assembly boundary.
- `Assets/AlmostDelivered/Runtime/Combat/` — minimal health, stun, and observable combat-tag contracts.
- `Assets/AlmostDelivered/Runtime/CommanderAI/Domain/` — enums, observations, memory, tactical context, scores, orders, and pure policies.
- `Assets/AlmostDelivered/Runtime/CommanderAI/Perception/` — local sensing and interruptible alert signal.
- `Assets/AlmostDelivered/Runtime/CommanderAI/Tactics/` — tactic assets, playbooks, anchors, slot queries, and allocation.
- `Assets/AlmostDelivered/Runtime/CommanderAI/Execution/` — order execution and NavMesh movement.
- `Assets/AlmostDelivered/Runtime/CommanderAI/Commander/` — squad lifecycle and commander orchestration.
- `Assets/AlmostDelivered/Runtime/CommanderAI/Debugging/` — screen diagnostics and world Gizmos.
- `Assets/AlmostDelivered/Editor/CommanderAI/` — deterministic prototype-scene builder and asset factory.
- `Assets/AlmostDelivered/Tests/EditMode/` — pure logic and allocation tests.
- `Assets/AlmostDelivered/Tests/PlayMode/` — alert, commander lifecycle, and end-to-end scenario tests.
- `Assets/AlmostDelivered/Scenes/CommanderAITest.unity` — generated test arena.
- `Assets/AlmostDelivered/Configs/CommanderAI/` — generated difficulty, tactic, and playbook assets.

## Review Focus

- Out-of-order observations: an older report must never overwrite a newer position or reduce already observed tags; covered by Task 2 knowledge tests.
- Alert completion race: stun/death/cancel in the completion frame must win over completion and keep the squad Unaware; covered by Task 3 PlayMode tests.
- Tactic thrashing: close or alternating scores must respect minimum hold time and switch margin; covered by Task 5 switching tests.
- Invalid tactical slots: destroyed, occupied, or newly unreachable anchors must be released and produce one reassignment before fallback; covered by Task 4 allocator and executor tests.
- Commander death during telegraph: the pending tactic must never activate and all living members must become permanently Confused; covered by Task 6 lifecycle tests.

---

### Task 1: Repository and Unity test foundation

**Files:**
- Create: `.gitignore`
- Create: `.gitattributes`
- Create: `Assets/AlmostDelivered/Runtime/AlmostDelivered.Runtime.asmdef`
- Create: `Assets/AlmostDelivered/Tests/EditMode/AlmostDelivered.CommanderAI.EditModeTests.asmdef`
- Create: `Assets/AlmostDelivered/Tests/PlayMode/AlmostDelivered.CommanderAI.PlayModeTests.asmdef`
- Create: `Assets/AlmostDelivered/Tests/EditMode/AssemblySmokeTests.cs`

**Interfaces:**
- Consumes: Unity project version and existing Test Framework package.
- Produces: `AlmostDelivered.Runtime` assembly and test assemblies referencing it.

- [ ] **Step 1: Initialize source control before adding generated files**

Run from the project root:

```powershell
git init -b main
```

Expected: an empty repository on branch `main`; do not add `Library`, `Temp`, `Logs`, `UserSettings`, generated solution files, or IDE state.

- [ ] **Step 2: Add Unity ignore and text-normalization rules**

Create a standard Unity `.gitignore` covering `/Library/`, `/Temp/`, `/Obj/`, `/Build*/`, `/Logs/`, `/UserSettings/`, `*.csproj`, `*.sln`, and IDE folders. Add `.gitattributes` with `* text=auto` and Unity YAML files (`*.meta`, `*.unity`, `*.prefab`, `*.asset`) normalized as text.

- [ ] **Step 3: Create runtime and test assembly definitions**

`AlmostDelivered.Runtime.asmdef` must use namespace root `AlmostDelivered`. Both test assemblies reference `AlmostDelivered.Runtime` and set `optionalUnityReferences` to `TestAssemblies`; let the installed Unity Test Framework resolve its own runner assemblies.

- [ ] **Step 4: Write the smoke test**

Create `AssemblySmokeTests.RuntimeAssembly_IsLoadable()` and assert `typeof(AlmostDelivered.RuntimeAssemblyMarker).Assembly.GetName().Name == "AlmostDelivered.Runtime"`. Add the marker as a sealed empty type in `Assets/AlmostDelivered/Runtime/RuntimeAssemblyMarker.cs`.

- [ ] **Step 5: Run EditMode tests**

Run:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\2021.3.45f2\Editor\Unity.exe' -batchmode -nographics -projectPath 'C:\Users\max47\Desktop\Coding\Almost Delivered' -runTests -testPlatform EditMode -testResults 'TestResults\editmode.xml' -logFile 'Logs\commander-ai-editmode.log' -quit
```

Expected: process exit code `0`, smoke test PASS, and no compile errors.

- [ ] **Step 6: Commit**

```powershell
git add .gitignore .gitattributes Assets/AlmostDelivered
git commit -m "chore: establish commander AI test foundation"
```

### Task 2: Observation memory and minimal combat contracts

**Files:**
- Create: `Assets/AlmostDelivered/Runtime/Combat/ActorHealth.cs`
- Create: `Assets/AlmostDelivered/Runtime/Combat/StunState.cs`
- Create: `Assets/AlmostDelivered/Runtime/Combat/ObservableActor.cs`
- Create: `Assets/AlmostDelivered/Runtime/CommanderAI/Domain/MonsterRole.cs`
- Create: `Assets/AlmostDelivered/Runtime/CommanderAI/Domain/ObservedCombatTag.cs`
- Create: `Assets/AlmostDelivered/Runtime/CommanderAI/Domain/ObservationSample.cs`
- Create: `Assets/AlmostDelivered/Runtime/CommanderAI/Domain/ObservedTargetMemory.cs`
- Create: `Assets/AlmostDelivered/Runtime/CommanderAI/Domain/SquadKnowledge.cs`
- Test: `Assets/AlmostDelivered/Tests/EditMode/SquadKnowledgeTests.cs`
- Test: `Assets/AlmostDelivered/Tests/EditMode/CombatContractTests.cs`

**Interfaces:**
- Consumes: `UnityEngine.Vector3` and Unity lifecycle only at MonoBehaviour adapters.
- Produces: `ObservationSample`, `SquadKnowledge.Merge(in ObservationSample)`, `SquadKnowledge.Decay(float now, float secondsToZero)`, `SquadKnowledge.TryGet(int actorId, out ObservedTargetMemory)`, `ActorHealth.Died`, `StunState.StunChanged`, and `ObservableActor.TagObserved`.

- [ ] **Step 1: Write failing knowledge tests**

Cover these named cases with explicit assertions:

- `Merge_NewTarget_CreatesMemoryWithPositionTimestampAndTags`.
- `Merge_OlderSample_DoesNotReplaceNewerPositionOrTimestamp`.
- `Merge_OlderSample_UnionsNewlyObservedTags`.
- `Decay_StaleMemory_ReachesZeroConfidenceWithoutMovingPosition`.
- `TryGet_UnknownActor_ReturnsFalse`.

Use actor IDs as positive `int` values, timestamps as unscaled seconds, confidence clamped to `[0,1]`, and `[Flags] ObservedCombatTag` values `None`, `RangedWeapon`, `Shield`, `Healing`, and `Mobility`.

- [ ] **Step 2: Run the targeted test and verify failure**

Run the Task 1 EditMode command with `-testFilter AlmostDelivered.Tests.EditMode.SquadKnowledgeTests`.

Expected: FAIL because domain types do not exist.

- [ ] **Step 3: Implement immutable observation data and `SquadKnowledge`**

Use exact public entry points:

```csharp
public bool Merge(in ObservationSample sample);
public void Decay(float now, float secondsToZero);
public bool TryGet(int actorId, out ObservedTargetMemory memory);
public IReadOnlyCollection<ObservedTargetMemory> Snapshot { get; }
public void Clear();
```

`Merge` returns `true` only when stored knowledge changes. Reject non-positive actor IDs, negative timestamps, NaN coordinates, and confidence outside `[0,1]` with `ArgumentOutOfRangeException` or `ArgumentException`.

- [ ] **Step 4: Write failing combat contract tests**

Test `ActorHealth.ApplyDamage` clamps health, raises `Died` once, and ignores later damage; `StunState.Apply(float duration, float now)` extends but never shortens an active stun; `ObservableActor.RevealTag(ObservedCombatTag)` raises only newly revealed tags.

- [ ] **Step 5: Implement minimal combat adapters**

Expose health/death, stun timing, stable actor ID, velocity, and observed tags. Do not implement production weapons, shielding, charging, or healing.

- [ ] **Step 6: Run EditMode tests**

Expected: all Task 2 tests PASS and no pre-existing tests fail.

- [ ] **Step 7: Commit**

```powershell
git add Assets/AlmostDelivered/Runtime Assets/AlmostDelivered/Tests/EditMode
git commit -m "feat: add observed actor memory and combat contracts"
```

### Task 3: Squad alert and immediate post-alert information sharing

**Files:**
- Create: `Assets/AlmostDelivered/Runtime/CommanderAI/Domain/SquadAlertState.cs`
- Create: `Assets/AlmostDelivered/Runtime/CommanderAI/Commander/SquadController.cs`
- Create: `Assets/AlmostDelivered/Runtime/CommanderAI/Perception/MonsterPerception.cs`
- Create: `Assets/AlmostDelivered/Runtime/CommanderAI/Perception/AlertSignal.cs`
- Test: `Assets/AlmostDelivered/Tests/EditMode/SquadControllerTests.cs`
- Test: `Assets/AlmostDelivered/Tests/PlayMode/AlertFlowTests.cs`

**Interfaces:**
- Consumes: `ObservationSample`, `SquadKnowledge`, `ActorHealth`, `StunState`, and `ObservableActor` from Task 2.
- Produces: `SquadController.State`, `SquadController.TryCompleteAlert(int reporterId)`, `SquadController.Report(in ObservationSample)`, `AlertSignal.Begin(ObservableActor target)`, `AlertSignal.Cancel()`, and `MonsterPerception.ObservationProduced`.

- [ ] **Step 1: Write failing squad-state tests**

Assert Unaware is the initial state; reports are ignored while Unaware; a valid completed alert changes state once; after Alerted, reports immediately merge into knowledge; Confused rejects both alerts and reports.

- [ ] **Step 2: Run tests and verify failure**

Run the EditMode test command filtered to `SquadControllerTests`.

Expected: FAIL because squad lifecycle types do not exist.

- [ ] **Step 3: Implement `SquadController` as the knowledge boundary**

Use:

```csharp
public SquadAlertState State { get; }
public SquadKnowledge Knowledge { get; }
public bool TryCompleteAlert(int reporterId);
public bool Report(in ObservationSample sample);
public void EnterConfused();
```

Validate that reporter IDs belong to living registered squad members before alert or report acceptance.

- [ ] **Step 4: Write PlayMode tests for interruptible alert timing**

Cover success after configured duration, cancel on stun, cancel on reporter death, cancel on target loss, and the race `AlertSignal_CancelOnCompletionFrame_RemainsUnaware`.

- [ ] **Step 5: Implement local perception and `AlertSignal`**

`MonsterPerception` owns sight radius, field-of-view angle, obstruction mask, and staggered sensing interval. Before alert it may start `AlertSignal`; after alert it emits observations immediately to `SquadController.Report`. It never calls commander tactic APIs.

- [ ] **Step 6: Run EditMode and PlayMode alert tests**

Run the standard Unity command once for EditMode and once with `-testPlatform PlayMode -testFilter AlmostDelivered.Tests.PlayMode.AlertFlowTests -testResults 'TestResults\playmode.xml'`.

Expected: all alert and squad tests PASS.

- [ ] **Step 7: Commit**

```powershell
git add Assets/AlmostDelivered
git commit -m "feat: add interruptible squad alert and shared observations"
```

### Task 4: Tactical anchors, slot allocation, and simple orders

**Files:**
- Create: `Assets/AlmostDelivered/Runtime/CommanderAI/Domain/MonsterOrder.cs`
- Create: `Assets/AlmostDelivered/Runtime/CommanderAI/Domain/MonsterOrderType.cs`
- Create: `Assets/AlmostDelivered/Runtime/CommanderAI/Tactics/TacticalAnchorTag.cs`
- Create: `Assets/AlmostDelivered/Runtime/CommanderAI/Tactics/TacticalAnchor.cs`
- Create: `Assets/AlmostDelivered/Runtime/CommanderAI/Tactics/TacticalSlotRequest.cs`
- Create: `Assets/AlmostDelivered/Runtime/CommanderAI/Tactics/TacticalSlotAllocator.cs`
- Create: `Assets/AlmostDelivered/Runtime/CommanderAI/Execution/MonsterOrderExecutor.cs`
- Test: `Assets/AlmostDelivered/Tests/EditMode/TacticalSlotAllocatorTests.cs`
- Test: `Assets/AlmostDelivered/Tests/PlayMode/MonsterOrderExecutorTests.cs`

**Interfaces:**
- Consumes: registered squad-member role, current position, living state, and Unity NavMesh reachability adapter.
- Produces: `TacticalSlotAllocator.TryAllocate(int memberId, MonsterRole role, in TacticalSlotRequest request, out TacticalAnchor anchor)`, `Release(int memberId)`, `ReleaseAll()`, and `MonsterOrderExecutor.Assign(in MonsterOrder order)`.

- [ ] **Step 1: Write failing allocator tests**

Test role filtering, tag filtering, capacity, deterministic nearest-score tie break by anchor instance ID, duplicate reservation prevention, destroyed-anchor cleanup, partial allocation, and release by member.

- [ ] **Step 2: Run allocator tests and verify failure**

Expected: FAIL because allocator and anchor types do not exist.

- [ ] **Step 3: Implement anchors and deterministic allocation**

Initial tags are `ObjectiveFront`, `CoverRear`, `LeftFlank`, `RightFlank`, `CommanderSafe`, and `SupportRear`. Keep NavMesh reachability behind `ITacticalReachability.CanReach(Vector3 from, Vector3 to)` so EditMode tests use a fake.

- [ ] **Step 4: Write failing order-executor PlayMode tests**

Cover MoveToSlot then HoldSlot, AttackObservedTarget using only memory target ID, ProtectUnit, SearchLastKnownPosition, invalid anchor causing exactly one reassignment request, and fallback after that request fails.

- [ ] **Step 5: Implement `MonsterOrderExecutor`**

Expose:

```csharp
public MonsterOrder CurrentOrder { get; }
public void Assign(in MonsterOrder order);
public void Cancel();
public event Action<int> ReassignmentRequested;
```

Movement uses `NavMeshAgent`; local attacks remain a short-range cooldown event sufficient for test damage.

- [ ] **Step 6: Run Task 4 EditMode and PlayMode tests**

Expected: all PASS with no leaked anchor reservations after object destruction.

- [ ] **Step 7: Commit**

```powershell
git add Assets/AlmostDelivered
git commit -m "feat: add tactical slots and monster order execution"
```

### Task 5: Utility tactics, playbooks, switching policy, and difficulty

**Files:**
- Create: `Assets/AlmostDelivered/Runtime/CommanderAI/Domain/TacticalContext.cs`
- Create: `Assets/AlmostDelivered/Runtime/CommanderAI/Domain/TacticEvaluation.cs`
- Create: `Assets/AlmostDelivered/Runtime/CommanderAI/Tactics/UtilityConsideration.cs`
- Create: `Assets/AlmostDelivered/Runtime/CommanderAI/Tactics/UtilityTactic.cs`
- Create: `Assets/AlmostDelivered/Runtime/CommanderAI/Tactics/TacticalPlaybook.cs`
- Create: `Assets/AlmostDelivered/Runtime/CommanderAI/Tactics/TacticSwitchPolicy.cs`
- Create: `Assets/AlmostDelivered/Runtime/CommanderAI/Commander/CommanderDifficultyProfile.cs`
- Test: `Assets/AlmostDelivered/Tests/EditMode/UtilityTacticTests.cs`
- Test: `Assets/AlmostDelivered/Tests/EditMode/TacticSwitchPolicyTests.cs`
- Test: `Assets/AlmostDelivered/Tests/EditMode/InitialTacticScenarioTests.cs`

**Interfaces:**
- Consumes: a read-only snapshot of squad knowledge, living role counts, objective marker position, anchor availability, squad health ratio, current tactic, and tactic age.
- Produces: `UtilityTactic.Evaluate(in TacticalContext) -> TacticEvaluation`, `TacticSwitchPolicy.ShouldSwitch(...)`, playbook slot requests, and Easy/Normal/Hard command settings.

- [ ] **Step 1: Write failing utility evaluation tests**

Assert every consideration score is named and clamped to `[0,1]`; invalid required roles return an evaluation with `IsValid == false` and a reason; identical context produces identical component and total scores; NaN consideration output invalidates the tactic rather than winning.

- [ ] **Step 2: Implement `TacticalContext`, considerations, and `UtilityTactic`**

Use:

```csharp
public TacticEvaluation Evaluate(in TacticalContext context);
```

`TacticEvaluation` contains tactic ID, validity, total score, rejection reason, and immutable named component scores. Combine normalized considerations with a data-driven weighted geometric mean so one critical zero can meaningfully suppress a tactic.

- [ ] **Step 3: Write failing switch-policy tests**

Test threshold rejection, minimum hold duration, switch margin, exact-score tie retaining current tactic, and alternating near-equal scores never thrashing.

- [ ] **Step 4: Implement `TacticSwitchPolicy` and difficulty profiles**

Defaults from the spec: Easy cycle `5.0f`, Normal `3.0f`, Hard `1.5f`. Profiles also expose telegraph duration, available tactic IDs, minimum hold time, and switch margin; information-sharing behavior is not configurable by difficulty.

- [ ] **Step 5: Write the three controlled-scenario tests**

Build contexts where Objective Blockade, Pincer, and Focus Support respectively win. Assertions must name the winning tactic and at least one decisive score component, including observed `Healing` or `Shield` tag for Focus Support.

- [ ] **Step 6: Implement the initial playbook definitions**

Playbooks produce role and anchor-tag requests only; they do not move units. Objective Blockade requests ObjectiveFront shields and CoverRear ranged units; Pincer requests LeftFlank and RightFlank chargers; Focus Support assigns chargers and ranged units to the observed support target while keeping Support at SupportRear.

- [ ] **Step 7: Run all EditMode tests and commit**

Expected: all PASS.

```powershell
git add Assets/AlmostDelivered
git commit -m "feat: add utility tactics and tactical playbooks"
```

### Task 6: Commander orchestration, telegraph, and permanent confusion

**Files:**
- Create: `Assets/AlmostDelivered/Runtime/CommanderAI/Commander/CommanderBrain.cs`
- Create: `Assets/AlmostDelivered/Runtime/CommanderAI/Commander/CommandTelegraph.cs`
- Create: `Assets/AlmostDelivered/Runtime/CommanderAI/Domain/ConfusionTendency.cs`
- Modify: `Assets/AlmostDelivered/Runtime/CommanderAI/Commander/SquadController.cs`
- Modify: `Assets/AlmostDelivered/Runtime/CommanderAI/Execution/MonsterOrderExecutor.cs`
- Test: `Assets/AlmostDelivered/Tests/EditMode/CommanderBrainTests.cs`
- Test: `Assets/AlmostDelivered/Tests/PlayMode/CommanderLifecycleTests.cs`

**Interfaces:**
- Consumes: Task 5 evaluation and playbooks, Task 4 allocation and executors, squad knowledge, difficulty profile, and commander `ActorHealth.Died`.
- Produces: `CommanderBrain.Tick(float now)`, `CommanderBrain.PendingEvaluation`, `CommanderBrain.ActiveEvaluation`, `SquadController.EnterConfused()`, and stable role/seed-based `ConfusionTendency` assignment.

- [ ] **Step 1: Write failing commander-cycle tests**

Verify no evaluation before Alerted, one evaluation per configured cycle, the brain never receives an `ObservableActor` reference, fallback when no tactic is valid, telegraph delay before assignment, and switch policy retention.

- [ ] **Step 2: Implement `CommanderBrain` orchestration**

Use explicit time supplied to `Tick(float now)` for deterministic tests. The brain builds context from squad-owned data, evaluates tactics, starts or retains a telegraph, activates the playbook after telegraph completion, and records all evaluations for debug inspection.

- [ ] **Step 3: Write failing lifecycle PlayMode tests**

Cover commander death while idle, while a tactic is active, and during telegraph. Assert pending and active tactics clear, every reservation releases, every living executor receives `ConfusedCombat`, knowledge stops accepting reports, and no later tick restores organization.

- [ ] **Step 4: Implement permanent confusion**

Choose a stable `ConfusionTendency` from role and member ID: nearest visible target, hold area, retreat when wounded, or search last known position. Do not use frame-order-dependent random state.

- [ ] **Step 5: Add the partial-playbook fallback test and implementation**

When some slots allocate and others fail, allocated members execute their orders and unallocated members receive fallback HoldSlot/AttackObservedTarget. Record each failure reason for debugging.

- [ ] **Step 6: Run all EditMode and commander PlayMode tests**

Expected: all PASS, especially `CommanderDiesDuringTelegraph_PendingTacticNeverActivates`.

- [ ] **Step 7: Commit**

```powershell
git add Assets/AlmostDelivered
git commit -m "feat: orchestrate commander tactics and squad confusion"
```

### Task 7: Portfolio debug view and deterministic prototype arena

**Files:**
- Create: `Assets/AlmostDelivered/Runtime/CommanderAI/Debugging/CommanderDebugView.cs`
- Create: `Assets/AlmostDelivered/Runtime/CommanderAI/Debugging/CommanderGizmoDrawer.cs`
- Create: `Assets/AlmostDelivered/Editor/CommanderAI/CommanderAIAssetFactory.cs`
- Create: `Assets/AlmostDelivered/Editor/CommanderAI/CommanderAITestSceneBuilder.cs`
- Test: `Assets/AlmostDelivered/Tests/EditMode/CommanderAITestSceneBuilderTests.cs`
- Create: `Assets/AlmostDelivered/Scenes/CommanderAITest.unity`
- Create: `Assets/AlmostDelivered/Configs/CommanderAI/Easy.asset`
- Create: `Assets/AlmostDelivered/Configs/CommanderAI/Normal.asset`
- Create: `Assets/AlmostDelivered/Configs/CommanderAI/Hard.asset`
- Create: tactic and playbook assets under `Assets/AlmostDelivered/Configs/CommanderAI/Tactics/`
- Test: `Assets/AlmostDelivered/Tests/PlayMode/CommanderScenarioTests.cs`

**Interfaces:**
- Consumes: all commander public snapshots, evaluations, knowledge, reservations, and current member orders.
- Produces: a reproducible arena builder menu/API, visible diagnostics, and end-to-end scenario tests.

- [ ] **Step 1: Write a failing scene-builder EditMode test**

`CommanderAITestSceneBuilderTests.Build_CreatesRequiredActorsAnchorsAndConfigs` asserts one controllable player, two tagged dummies, one commander, two monsters for each of four roles, a fixed objective marker, both flank tags, rear cover, commander-safe/support anchors, baked navigation source geometry, and all three difficulty assets.

- [ ] **Step 2: Implement the asset factory and scene builder**

Expose:

```csharp
public static void CreateOrUpdateDefaultConfigs();
public static Scene BuildOrUpdateTestScene(string scenePath);
```

Make repeated runs idempotent by stable object names and asset paths. Use primitives and existing safe placeholder materials; do not depend on imported demo scenes.

- [ ] **Step 3: Implement the debug view and Gizmos**

Display alert state, difficulty, next command time, last-known player position/confidence/tags, every tactic's component scores and rejection reason, pending/active tactic, telegraph progress, anchor reservations, allocation failures, and each member's current order. Gizmos distinguish available, reserved, unreachable, and selected anchors.

- [ ] **Step 4: Write end-to-end PlayMode scenarios**

Tests must prove:

- no player-specific tactic before completed alert;
- interrupted alert leaves Unaware;
- post-alert observation immediately updates knowledge but waits for next cycle and telegraph before reassignment;
- each of the three controlled arena configurations activates its expected tactic;
- commander death visibly and permanently removes coordination;
- Easy/Normal/Hard change cycles and allowed complexity without changing knowledge acceptance.

- [ ] **Step 5: Generate the scene and run all tests**

Invoke the editor builder in batch mode with `-executeMethod AlmostDelivered.Editor.CommanderAI.CommanderAITestSceneBuilder.BuildFromCommandLine`, then run all EditMode and PlayMode tests.

Expected: process exit code `0`, generated assets exist at fixed paths, and every test PASS.

- [ ] **Step 6: Open the arena for a manual portfolio check**

Verify the debug overlay explains why the tactic changed, command cues precede repositioning, role-colored primitives occupy expected anchors, and killing the commander creates an obvious coordination contrast. Record tuning issues rather than silently changing spec behavior.

- [ ] **Step 7: Commit**

```powershell
git add Assets/AlmostDelivered
git commit -m "feat: add commander AI test arena and diagnostics"
```

### Task 8: Performance evidence and documentation handoff

**Files:**
- Create: `Assets/AlmostDelivered/Tests/PlayMode/CommanderAIStressTests.cs`
- Create: `docs/commander-ai/architecture.md`
- Create: `docs/commander-ai/portfolio-demo.md`
- Create: `docs/commander-ai/profiling.md`
- Create: `README.md`

**Interfaces:**
- Consumes: completed prototype, debug snapshots, deterministic scene builder, and Unity Profiler markers.
- Produces: reproducible one-squad and five-squad stress runs, architecture explanation, demo script, and recorded baseline results.

- [ ] **Step 1: Add Profiler markers around the actual cost centers**

Add named markers for perception batches, knowledge merge/decay, utility evaluation, tactical allocation, and order reassignment. Do not claim performance from planner timing alone; track NavMesh path requests and avoidance observations in the written results.

- [ ] **Step 2: Write the stress PlayMode test**

Build one baseline squad and a five-squad/forty-member stress arrangement. Assert no exceptions, no NaN positions or scores, no duplicate reservations, and no command-cycle count above profile expectations during a fixed simulated interval. Performance values are recorded, not asserted against machine-specific millisecond thresholds.

- [ ] **Step 3: Run all automated verification**

Run the full EditMode and PlayMode commands.

Expected: exit code `0`, all tests PASS, and XML results saved under `TestResults/`.

- [ ] **Step 4: Capture and document profiling evidence**

Record Unity version, machine context, actor counts, command-cycle settings, total and per-system CPU samples, NavMesh observations, and known bottlenecks in `profiling.md`. Clearly label baseline measurements rather than promising a platform target not yet defined.

- [ ] **Step 5: Write architecture and portfolio demo documentation**

`architecture.md` explains perception-to-knowledge-to-utility-to-playbook-to-order flow and the later server-authoritative boundary. `portfolio-demo.md` gives a short deterministic demonstration: interrupt alert, allow alert, show score change, trigger each tactic, kill commander, and compare organized versus confused behavior.

- [ ] **Step 6: Update `README.md`**

Link the approved spec, implementation plan, architecture, test commands, arena scene, and portfolio demo. State that multiplayer and full role abilities are future work.

- [ ] **Step 7: Commit**

```powershell
git add Assets/AlmostDelivered docs README.md
git commit -m "docs: record commander AI architecture and evidence"
```

## Final Verification Gate

- [ ] Run all EditMode tests and confirm exit code `0`.
- [ ] Run all PlayMode tests and confirm exit code `0`.
- [ ] Regenerate the test arena and confirm the working tree changes only when intended.
- [ ] Launch `CommanderAITest.unity` and manually execute the portfolio demo script.
- [ ] Inspect `git status --short` and ensure only intentional, documented files remain.
- [ ] Review every acceptance criterion in the approved spec against a passing test or manual demo step.
