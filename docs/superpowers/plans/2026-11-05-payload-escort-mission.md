# Payload Escort Mission Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the first complete, deterministic payload-escort mission with generated routes, checkpoint waves, regular enemies, health bars, a minimap, and a future multiplayer authority boundary.

**Architecture:** Pure C# planners and state machines own route and mission truth. Unity MonoBehaviours adapt that truth to physics presence queries, payload transforms, wave spawning, HUD/minimap presentation, and the generated mission scene. The mission controller is explicitly authority or replica so a future networking adapter can replicate snapshots without replacing the mission rules.

**Tech Stack:** Unity 2021.3.45f2, C#, NUnit/Unity Test Framework, built-in render pipeline, uGUI, existing AlmostDelivered runtime/editor assemblies.

**Spec:** `docs/superpowers/specs/2026-11-05-payload-escort-mission-design.md`

## Global Constraints

- Preserve `Frontend -> DepartureHub -> MissionPrototype` and existing player movement/ranged combat.
- Reuse generated low-poly visuals and Industrial Dock assets; add no external dependencies.
- Normal mission configuration targets 20–30 minutes, while tests use accelerated direct ticks.
- Phase 1 adds no networking SDK, but only the authority controller may advance mission state or request waves.
- Leave user-owned untracked `Assets/Scenes*` and `Assets/temp.mat*` untouched.
- Do not push GitHub without explicit permission.
- Do not control the Unity screen until the user separately permits it.

## Review Focus

- Degenerate/invalid bounds or route lengths must return a stationary valid plan rather than NaN positions — covered in Task 1 tests.
- Large delta times must stop exactly at checkpoints instead of skipping defenses — covered in Task 2 tests.
- Duplicate/out-of-wave enemy removal reports must not complete a wave early — covered in Task 2 tests.
- Queued enemies above the active cap must remain part of wave completion and spawn as slots open — covered in Task 4 tests.
- Replica controllers must not move payloads, request waves, or mutate progress locally — covered in Task 5 tests.

---

### Task 1: Deterministic payload route plan

**Files:**
- Create: `Assets/AlmostDelivered/Runtime/Gameplay/Payload/PayloadRoutePlan.cs`
- Create: `Assets/AlmostDelivered/Runtime/Gameplay/Payload/PayloadRoutePlanner.cs`
- Create: matching Unity `.meta` files
- Create: `Assets/AlmostDelivered/Tests/EditMode/PayloadRoutePlannerTests.cs`

**Interfaces:**
- Produces: `PayloadRoutePlan PayloadRoutePlanner.Generate(int seed, Rect bounds, int interiorPointCount, float corridorWidth)`.
- `PayloadRoutePlan` exposes `Points`, `CumulativeDistances`, `TotalLength`, `CheckpointDistances`, `Destination`, `EvaluateDistance(float)`, and `CorridorExclusions`.

- [ ] **Step 1: Write failing route tests**

Add tests proving identical seeds match, different seeds vary, Z is monotonic, every point stays in bounds, normal bounds produce a 120–160 metre target route, checkpoints are exactly 25/50/75 percent, distance sampling reaches endpoints, corridor exclusions cover every segment, and invalid bounds return a finite zero-length plan.

- [ ] **Step 2: Run focused EditMode tests and verify RED**

Run `PayloadRoutePlannerTests`; expect compile failures for the missing planner types.

- [ ] **Step 3: Implement the pure route planner**

Use seven ordered points (five seeded interior points), monotonic Z, constrained alternating lateral offsets, cumulative distance sampling, and axis-aligned inflated segment exclusions.

- [ ] **Step 4: Verify GREEN and the complete EditMode suite**

Run focused tests, then all EditMode tests.

- [ ] **Step 5: Commit Task 1**

Commit exact route source, tests, and generated metas with `feat: plan deterministic payload routes`.

### Task 2: Authoritative payload mission state machine

**Files:**
- Create: `Assets/AlmostDelivered/Runtime/Gameplay/Payload/PayloadMissionState.cs`
- Create: `Assets/AlmostDelivered/Runtime/Gameplay/Payload/PayloadMissionStateMachine.cs`
- Create: matching Unity `.meta` files
- Create: `Assets/AlmostDelivered/Tests/EditMode/PayloadMissionStateMachineTests.cs`

**Interfaces:**
- Consumes: route length and checkpoint distances from `PayloadRoutePlan`.
- Produces: `PayloadMissionSnapshot`, `PayloadMissionPhase`, `PayloadMissionStateMachine.Tick(float deltaTime, int playersInRange, int enemiesInRange)`, `RegisterWave(int waveId, IReadOnlyCollection<int> enemyIds)`, and `ReportEnemyRemoved(int waveId, int enemyId)`.
- Emits: `WaveRequested(PayloadWaveRequest)` when a checkpoint/final wave must be created.

- [ ] **Step 1: Write failing phase and movement tests**

Cover deployment lock, player-required movement, enemy contest stop, extra-player no speed boost, exact checkpoint clamping under large delta, three checkpoint cycles, final defense/completion, empty-wave continuation, duplicate removal, foreign wave IDs, invalid delta, and immutable snapshots.

- [ ] **Step 2: Run focused tests and verify RED**

Run `PayloadMissionStateMachineTests`; expect missing-type failures.

- [ ] **Step 3: Implement minimal pure state logic**

Keep enemy identifiers in a `HashSet<int>`, clamp movement to the next checkpoint/destination, and change phase only through explicit wave registration/removal.

- [ ] **Step 4: Verify GREEN and all EditMode tests**

- [ ] **Step 5: Commit Task 2**

Use `feat: add payload mission state machine`.

### Task 3: Health change contract and world health bars

**Files:**
- Modify: `Assets/AlmostDelivered/Runtime/Combat/ActorHealth.cs`
- Create: `Assets/AlmostDelivered/Runtime/Gameplay/WorldHealthBar.cs`
- Create: matching Unity `.meta` file
- Modify: `Assets/AlmostDelivered/Tests/EditMode/CombatContractTests.cs`
- Create: `Assets/AlmostDelivered/Tests/PlayMode/WorldHealthBarTests.cs`

**Interfaces:**
- `ActorHealth` produces `HealthChanged(float current, float maximum)` and `ConfigureMaximum(float maximum, bool refill = true)`.
- `WorldHealthBar.Attach(GameObject actor, Vector3 localOffset)` returns a bound health bar with `FillAmount` for verification.

- [ ] **Step 1: Write failing health contract tests**

Prove maximum configuration refills/clamps, damage emits one change event, death emits final zero before `Died`, invalid maximum values throw, and existing damage validation remains unchanged.

- [ ] **Step 2: Verify RED, then implement the health contract**

- [ ] **Step 3: Write failing PlayMode health-bar tests**

Prove attach creates one world-space bar, damage changes fill ratio, the bar faces the camera, repeated attach is idempotent, and death hides the bar.

- [ ] **Step 4: Verify RED, implement `WorldHealthBar`, and verify GREEN**

Build a compact world-space uGUI canvas with background/fill images and event subscriptions; unsubscribe on destroy.

- [ ] **Step 5: Run complete EditMode and PlayMode suites, then commit**

Use `feat: show enemy world health bars`.

### Task 4: Regular mobs and capped deterministic waves

**Files:**
- Create: `Assets/AlmostDelivered/Runtime/Gameplay/Payload/MissionMobController.cs`
- Create: `Assets/AlmostDelivered/Runtime/Gameplay/Payload/PayloadWaveSpawner.cs`
- Create: matching Unity `.meta` files
- Create: `Assets/AlmostDelivered/Tests/PlayMode/MissionMobControllerTests.cs`
- Create: `Assets/AlmostDelivered/Tests/PlayMode/PayloadWaveSpawnerTests.cs`

**Interfaces:**
- Produces: `MissionMobArchetype`, `MissionMobDefinition`, and `PayloadWaveSpawner.SpawnWave(int waveId, int checkpointIndex, bool finalWave)`.
- `PayloadWaveSpawner` exposes `LivingEnemies`, `ActiveCount`, `PendingCount`, `WaveStarted(int waveId, IReadOnlyCollection<int> enemyIds)`, and `EnemyRemoved(int waveId, int enemyId)`.
- Consumes: player target, projectile template, generated visual prefab references, checkpoint spawn anchors, mission seed, and an active cap of 18.

- [ ] **Step 1: Write failing regular-mob behavior tests**

Cover melee approach/contact cooldown, ranged stand-off/fire, shield health/speed configuration, dead mob disablement, and null/dead target idling.

- [ ] **Step 2: Verify RED, implement `MissionMobController`, verify GREEN**

- [ ] **Step 3: Write failing wave-spawner tests**

Cover deterministic spawn order/positions, requested counts 10/14/18/22, active cap 18, queued replacement on death, stable unique IDs, final bosses, one removal report, and cleanup of disabled/destroyed enemies.

- [ ] **Step 4: Verify RED, implement `PayloadWaveSpawner`, verify GREEN**

Create runtime actor roots with `ActorHealth`, `CombatTeam`, collider, simple controller, visual child, and `WorldHealthBar`; queue definitions above the active cap.

- [ ] **Step 5: Run all PlayMode tests and commit**

Use `feat: spawn payload defense waves`.

### Task 5: Payload mission authority controller and map-route integration

**Files:**
- Create: `Assets/AlmostDelivered/Runtime/Gameplay/Payload/PayloadPresenceSensor.cs`
- Create: `Assets/AlmostDelivered/Runtime/Gameplay/Payload/PayloadMissionController.cs`
- Create: `Assets/AlmostDelivered/Runtime/Gameplay/Payload/PayloadRouteView.cs`
- Create: matching Unity `.meta` files
- Modify: `Assets/AlmostDelivered/Runtime/MapGeneration/MissionMapGenerator.cs`
- Modify: `Assets/AlmostDelivered/Tests/PlayMode/MissionMapGeneratorTests.cs`
- Create: `Assets/AlmostDelivered/Tests/PlayMode/PayloadMissionControllerTests.cs`

**Interfaces:**
- `MissionMapGenerator.Generate(int seed, IReadOnlyList<MapExclusionZone> extraBlockingExclusions)` keeps the existing overload and excludes route corridor only from blocking obstacles.
- `PayloadMissionController.Configure(...)` consumes authority flag, seed, bounds, player/payload transforms, route planner settings, map generator, wave spawner, and timing/speed values.
- Produces: `RoutePlan`, `Snapshot`, `IsAuthority`, `ApplySnapshot(PayloadMissionSnapshot)`, `SnapshotChanged`, and phase transition events.

- [ ] **Step 1: Write failing map-exclusion tests**

Prove blocking obstacles avoid supplied route zones while terrain details still generate, identical seeds remain deterministic, and the existing overload remains compatible.

- [ ] **Step 2: Verify RED, extend `MissionMapGenerator`, verify GREEN**

- [ ] **Step 3: Write failing authority controller tests**

Cover initialization from one seed, payload transform sampling, presence rules using living teams, wave request/registration wiring, death progression, authority-only mutation, replica snapshot application, and missing dependencies producing stationary safe state.

- [ ] **Step 4: Verify RED, implement sensor/controller/route view, verify GREEN**

Use `Physics.OverlapSphereNonAlloc` only in the Unity adapter; pass counts into the pure state machine. Build a world route `LineRenderer` and checkpoint beacon markers from `PayloadRoutePlan`.

- [ ] **Step 5: Run complete test suites and commit**

Use `feat: drive authoritative payload missions`.

### Task 6: Generated minimap and payload HUD

**Files:**
- Create: `Assets/AlmostDelivered/Runtime/Gameplay/Payload/MissionMinimap.cs`
- Create: matching Unity `.meta` file
- Modify: `Assets/AlmostDelivered/Runtime/Gameplay/MissionHud.cs`
- Create: `Assets/AlmostDelivered/Tests/EditMode/MissionMinimapProjectionTests.cs`
- Create: `Assets/AlmostDelivered/Tests/PlayMode/MissionMinimapTests.cs`
- Modify: `Assets/AlmostDelivered/Tests/PlayMode/MissionPrototypeIntegrationTests.cs`

**Interfaces:**
- `Vector2 MissionMinimap.ProjectWorld(Vector3 worldPosition, Rect worldBounds, Rect minimapRect)` is the pure projection seam.
- `MissionMinimap.Configure(Rect bounds, PayloadRoutePlan route, Transform player, Transform payload, PayloadMissionController mission, PayloadWaveSpawner waves)` creates static route/checkpoint UI and tracks dynamic icons.
- `MissionHud.ConfigurePayload(...)` adds phase, progress, checkpoint, escort/contested, and elapsed-time views without removing player health or controls.

- [ ] **Step 1: Write failing projection tests**

Cover all four world corners, center, out-of-bounds clamping, non-square bounds, and invalid zero-sized bounds.

- [ ] **Step 2: Verify RED, implement projection, verify GREEN**

- [ ] **Step 3: Write failing runtime minimap/HUD tests**

Prove route segments and checkpoint markers are generated once, player/payload/enemy icons track positions, dead enemy icons disappear, and snapshot text shows phase/progress/time.

- [ ] **Step 4: Verify RED, implement minimap/HUD binding, verify GREEN**

- [ ] **Step 5: Run full test suites and commit**

Use `feat: add payload mission minimap`.

### Task 7: Mission scene integration

**Files:**
- Modify: `Assets/AlmostDelivered/Editor/Frontend/MissionPrototypeSceneBuilder.cs`
- Modify: `Assets/AlmostDelivered/Tests/EditMode/MissionPrototypeSceneBuilderTests.cs`
- Modify: `Assets/AlmostDelivered/Tests/PlayMode/MissionPrototypeIntegrationTests.cs`
- Modify generated scene: `Assets/AlmostDelivered/Scenes/MissionPrototype.unity`

**Interfaces:**
- Consumes all prior Phase 1 runtime contracts.
- Produces a persisted `MissionPrototype` scene with offline authority, 56x100 Industrial Dock bounds, generated payload route/map, payload visual/root, spawn definitions, health bars, route view, minimap, and expanded HUD.

- [ ] **Step 1: Write failing scene-contract tests**

Assert one authority controller, one Payload visual, one map generator with runtime randomization disabled, no preplaced bosses, four wave definitions (10/14/18/22), active cap 18, route-aware obstacle generation, one minimap, and payload HUD views.

- [ ] **Step 2: Run focused tests and verify RED**

- [ ] **Step 3: Update the scene builder and rebuild the scene**

Expand floor/boundaries, configure the mission seed owner, create player/payload starts, provide runtime visual prefab references, and wire controller/spawner/minimap/HUD. Keep Frontend and DepartureHub unchanged.

- [ ] **Step 4: Add accelerated end-to-end PlayMode tests**

Directly tick through deployment, escort, each checkpoint wave, final defense, and completion; verify payload and HUD/minimap state without waiting real time.

- [ ] **Step 5: Run all EditMode and PlayMode tests**

Record exact totals. Restore only unrelated Unity-generated churn and preserve user untracked files.

- [ ] **Step 6: Commit Task 7**

Use `feat: build payload escort mission prototype`.

### Task 8: Final review and handoff

**Files:**
- Modify only for defects reproduced by a new failing test.
- Update the git-ignored SDD ledger for this plan.

**Interfaces:**
- Produces a reviewed Phase 1 branch ready for user-authorized visual verification.

- [ ] **Step 1: Review the full branch against every design requirement**

Pay special attention to authority leakage, route/checkpoint skipping, queued-wave deadlocks, scene lifetime leaks, and minimap stale references.

- [ ] **Step 2: Fix Important/Critical findings through RED-GREEN tests**

- [ ] **Step 3: Run `git diff --check`, full EditMode, and full PlayMode suites**

- [ ] **Step 4: Stop before screen control**

Report implementation and automated verification, then ask permission before opening/controlling Unity for visual validation.
