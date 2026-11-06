# Continuous Escort Combat and Relay Beacons Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Keep players fighting near a moving payload, hide the full route from the field, and explain checkpoint stops with visible supply relay beacons.

**Architecture:** Keep `PayloadMissionStateMachine` authoritative for mission phases and add a deterministic `PayloadEscortDirector` that requests small travel encounters only during escorting. Extend the existing spawner for overlapping travel groups and checkpoint waves, then replace the world route view with presentation-only relay beacon components while retaining and fixing the minimap route.

**Tech Stack:** Unity 2021.3.45f2, C#, NUnit EditMode tests, Unity Test Framework PlayMode tests, Built-in Render Pipeline, existing scene builders and generated low-poly assets.

**Spec:** `docs/superpowers/specs/2026-11-06-continuous-escort-and-relay-beacons-design.md`

## Global Constraints

- The complete route remains visible on the minimap and is not drawn across the world.
- Payload movement stays authority-only, requires at least one living player inside the 4.5 m escort radius, and stops while a living enemy contests that radius.
- Travel encounters are deterministic from the mission seed and encounter sequence.
- First travel assault uses a 5–8 second delay; later pressure intervals use 12–20 seconds.
- Travel groups contain 3–6 enemies and never intentionally exceed 12 active travel-pressure enemies.
- Checkpoint waves stay at 10, 14, and 18 enemies; final defense stays at 22 with an 18-active cap.
- No production networking, progression, final art/audio, or full commander-AI integration is introduced.
- Do not use `-quit` with Unity `-runTests`; the test runner exits by itself.

## Review Focus

- A large `deltaTime` crossing deployment into escort must not retroactively create multiple travel encounters; Task 2 adds `EnteringEscortStartsOneInitialCountdown`.
- A travel group that would exceed the pressure cap must be clamped or skipped; Task 2 adds `EncounterNeverExceedsRemainingPressureCapacity`.
- Checkpoint spawning while travel enemies are alive must not throw or register travel IDs as checkpoint enemies; Task 3 adds `CheckpointWaveCanJoinLivingTravelEnemies`.
- Degenerate route direction and boundary-adjacent payload positions must still produce finite, in-bounds spawn positions outside the escort radius; Task 3 adds `TravelSpawnSanitizesDirectionAndBounds`.
- Missing relay visual references must not break mission progress; Task 4 adds `RelayWithMissingOptionalVisualsStillTracksState`.

---

### Task 1: Correct and Improve the Minimap Route

**Files:**
- Modify: `Assets/AlmostDelivered/Runtime/Gameplay/Payload/MissionMinimap.cs`
- Modify: `Assets/AlmostDelivered/Tests/PlayMode/MissionMinimapTests.cs`

**Interfaces:**
- Consumes: `MissionMinimap.Configure(Rect, PayloadRoutePlan, Transform, Transform, PayloadMissionController, PayloadWaveSpawner)`.
- Produces: pivot-safe minimap children and phase-aware route/checkpoint colors without changing the public configure signature.

- [ ] **Step 1: Verify the existing top-right pivot regression is red**

Run the focused PlayMode test `AlmostDelivered.Tests.PlayMode.MissionMinimapTests.TopRightPivotKeepsRouteInsidePanel`.
Expected: FAIL because route segment `localPosition` falls outside `panel.rect`.

- [ ] **Step 2: Anchor every generated minimap child to the panel pivot**

In `CreateIcon`, set `anchorMin` and `anchorMax` to `panel.pivot` so `ProjectWorld` coordinates and child anchor coordinates share the same origin.

- [ ] **Step 3: Verify the pivot regression is green**

Run the same focused PlayMode test.
Expected: PASS, 1 test.

- [ ] **Step 4: Add failing route-state presentation tests**

Add `CurrentLegAndCheckpointAreBrighterThanFutureRoute` and `CompletedCheckpointUsesCompletedColor`. Assert `Image.color` through the real generated child views after applying mission snapshots.

- [ ] **Step 5: Track route distances and refresh route/checkpoint colors**

Store the mission reference, cumulative route segment endpoints, and route/checkpoint `Image` references. In `Refresh`, use `Snapshot.RouteDistance` and `Snapshot.NextCheckpointIndex` to render completed segments muted green, the current leg bright cyan, future legs dim cyan, the active checkpoint amber, and completed checkpoints green.

- [ ] **Step 6: Run all `MissionMinimapTests`**

Expected: every test in `AlmostDelivered.Tests.PlayMode.MissionMinimapTests` passes.

- [ ] **Step 7: Commit**

Commit message: `fix: keep payload route inside minimap`

### Task 2: Build the Deterministic Escort Director

**Files:**
- Create: `Assets/AlmostDelivered/Runtime/Gameplay/Payload/PayloadEscortDirector.cs`
- Create: `Assets/AlmostDelivered/Runtime/Gameplay/Payload/PayloadEscortEncounter.cs`
- Create: `Assets/AlmostDelivered/Tests/EditMode/PayloadEscortDirectorTests.cs`

**Interfaces:**
- Consumes: `PayloadMissionPhase` and current active enemy count.
- Produces: `PayloadEscortDirector(int seed, int pressureCap = 12)`, `bool Tick(float deltaTime, PayloadMissionPhase phase, int activeEnemyCount, out PayloadEscortEncounter encounter)`, and immutable `PayloadEscortEncounter` properties `SequenceId`, `GroupSize`, and `Pattern`.
- Produces: enum `PayloadAssaultPattern` with `ForwardBlockade`, `LeftAmbush`, `RightAmbush`, and `RearPursuit`.

- [ ] **Step 1: Write failing pure-logic tests**

Add tests named `FirstEncounterArrivesWithinEightEscortSeconds`, `SubsequentEncounterWaitsAtLeastTwelveSeconds`, `NonEscortPhasesPauseRequests`, `SameSeedProducesSameEncounterSequence`, `EnteringEscortStartsOneInitialCountdown`, and `EncounterNeverExceedsRemainingPressureCapacity`.

- [ ] **Step 2: Run focused EditMode tests and verify red**

Expected: compilation failure because the director contracts do not exist.

- [ ] **Step 3: Implement encounter value types and countdown logic**

Use one `System.Random` seeded in the constructor. On entering `Escorting`, roll one 5–8 second initial delay. After a successful request, roll a 12–20 second delay. Generate group size 3–6, clamp to `pressureCap - activeEnemyCount`, rotate deterministic patterns, and return false for non-finite/negative time or non-escort phases.

- [ ] **Step 4: Run `PayloadEscortDirectorTests`**

Expected: all focused EditMode tests pass.

- [ ] **Step 5: Commit**

Commit message: `feat: add deterministic escort combat director`

### Task 3: Spawn Travel Groups and Wire Them to the Mission

**Files:**
- Modify: `Assets/AlmostDelivered/Runtime/Gameplay/Payload/PayloadWaveSpawner.cs`
- Modify: `Assets/AlmostDelivered/Runtime/Gameplay/Payload/PayloadMissionController.cs`
- Modify: `Assets/AlmostDelivered/Tests/PlayMode/PayloadWaveSpawnerTests.cs`
- Modify: `Assets/AlmostDelivered/Tests/PlayMode/PayloadMissionControllerTests.cs`

**Interfaces:**
- Consumes: `PayloadEscortDirector.Tick(...)` and `PayloadEscortEncounter` from Task 2.
- Produces: `PayloadWaveSpawner.SpawnTravelGroup(PayloadEscortEncounter encounter, Vector3 payloadPosition, Vector3 routeDirection, Rect missionBounds, float exclusionRadius)`.
- Produces: travel enemies with stable negative IDs, leaving positive checkpoint wave IDs and `WaveStarted` registration unchanged.

- [ ] **Step 1: Write failing spawner tests**

Add `TravelGroupUsesRequestedCountAndStableNegativeIds`, `TravelGroupsCanOverlapLivingEnemies`, `CheckpointWaveCanJoinLivingTravelEnemies`, and `TravelSpawnSanitizesDirectionAndBounds`. Assert deterministic archetypes/positions, finite coordinates, arena containment, and distance greater than the escort exclusion radius.

- [ ] **Step 2: Run focused `PayloadWaveSpawnerTests` and verify red**

Expected: compilation failure because `SpawnTravelGroup` does not exist.

- [ ] **Step 3: Implement travel-group queuing and spawn patterns**

Use payload-local forward/right vectors, pattern-specific centers 18–24 m from the payload, and seeded 3–5 m scatter. Clamp candidates to mission bounds with a 1 m inset and push any candidate inside `exclusionRadius + 2 m` outward before the final clamp. Travel requests never invoke `WaveStarted`; `EnemyRemoved` continues to report IDs so the mission state machine safely ignores negative travel IDs.

- [ ] **Step 4: Permit checkpoint waves alongside living travel enemies**

Replace the blanket `living.Count > 0` rejection with a guard that rejects only a second pending/registered checkpoint wave. Checkpoint IDs are queued behind any active population and fill available slots up to the existing active cap.

- [ ] **Step 5: Run focused spawner tests and verify green**

Expected: all `PayloadWaveSpawnerTests` pass.

- [ ] **Step 6: Write failing controller integration tests**

Add `AuthoritySpawnsTravelEnemiesBeforeFirstCheckpoint`, `ReplicaNeverRunsEscortDirector`, and `CheckpointRegistrationIgnoresTravelEnemyIds`. Use controlled `Tick` calls and real `PayloadWaveSpawner` instances.

- [ ] **Step 7: Wire the director in `PayloadMissionController`**

Create the director in `Initialize` only for authority instances with a spawner. During `Tick`, after the authoritative state update, call the director with the current phase and spawner count. For a request, calculate the current route tangent from `EvaluateDistance(distance ± 1 m)` and call `SpawnTravelGroup` with configured bounds and escort radius.

- [ ] **Step 8: Run focused controller tests and complete EditMode tests**

Expected: `PayloadMissionControllerTests` and all EditMode tests pass.

- [ ] **Step 9: Commit**

Commit message: `feat: fight continuously while escorting payload`

### Task 4: Replace the Field Route with Supply Relay Beacons

**Files:**
- Create: `Assets/AlmostDelivered/Runtime/Gameplay/Payload/PayloadRelayBeaconView.cs`
- Create: `Assets/AlmostDelivered/Runtime/Gameplay/Payload/PayloadNavigationIndicator.cs`
- Create: `Assets/AlmostDelivered/Tests/PlayMode/PayloadRelayBeaconViewTests.cs`
- Modify: `Assets/AlmostDelivered/Editor/Frontend/MissionPrototypeSceneBuilder.cs`
- Modify: `Assets/AlmostDelivered/Tests/EditMode/MissionPrototypeSceneBuilderTests.cs`
- Modify: `Assets/AlmostDelivered/Tests/PlayMode/MissionPrototypeIntegrationTests.cs`
- Modify: `Assets/AlmostDelivered/Scenes/MissionPrototype.unity`

**Interfaces:**
- Consumes: `PayloadMissionController.Snapshot`, `SnapshotChanged`, route checkpoint positions, and payload transform.
- Produces: `PayloadRelayBeaconView.Configure(PayloadMissionController mission, int checkpointIndex, bool finalDestination, GameObject beam, GameObject pulseRing, GameObject supplyCache, GameObject collectedCargo)`.
- Produces: enum `PayloadRelayVisualState` with `Future`, `Active`, `Loading`, `Completed`, and `DestinationActive`.
- Produces: `PayloadNavigationIndicator.Configure(PayloadMissionController mission, IReadOnlyList<Transform> relayTargets)`; its child antenna points only toward the active relay.

- [ ] **Step 1: Write failing relay-state PlayMode tests**

Add `OnlyNextRelayIsActive`, `CheckpointDefenseShowsLoadingState`, `CompletedCheckpointHidesCacheAndShowsCollectedCargo`, `FinalDestinationUsesDestinationState`, and `RelayWithMissingOptionalVisualsStillTracksState`.

- [ ] **Step 2: Run focused relay tests and verify red**

Expected: compilation failure because relay presentation types do not exist.

- [ ] **Step 3: Implement presentation-only relay and navigation components**

Derive state solely from the mission snapshot. Subscribe/unsubscribe safely on configure, enable the beam/ring only for active/loading states, hide depleted supply cache after completion, reveal the matching payload cargo prop after completion, and rotate the navigation indicator on the horizontal plane toward the active relay.

- [ ] **Step 4: Run relay tests and verify green**

Expected: all `PayloadRelayBeaconViewTests` pass.

- [ ] **Step 5: Add failing generated-scene contracts**

Update `MissionPrototypeSceneBuilderTests` to assert zero `PayloadRouteView` components, four relay views, exactly one payload navigation indicator, one supply cache at each of the first three relays, a distinct final destination, and cargo progress props on the payload. Update the real-scene PlayMode integration test to assert the first relay is active and the route still exists on the minimap.

- [ ] **Step 6: Generate relay geometry and remove the field route view**

In `MissionPrototypeSceneBuilder`, remove construction of `Payload Route`. Build three cache relays at checkpoint distances plus a final destination relay at `route.Destination`. Use existing cyan/amber/green/dark materials, a tall local beam, a pulsing base ring, numbered `TextMesh` labels, cache crates/fuel cells, and three initially hidden cargo props parented to the payload.

- [ ] **Step 7: Rebuild the generated mission scene**

Run `AlmostDelivered.Editor.Frontend.FrontendSceneBuilder.BuildOrUpdateAll` in batch mode and save `Assets/AlmostDelivered/Scenes/MissionPrototype.unity`.

- [ ] **Step 8: Run scene-builder and mission integration tests**

Expected: the focused EditMode and PlayMode tests pass and the generated scene has no field route line.

- [ ] **Step 9: Commit**

Commit message: `feat: guide payload with supply relay beacons`

### Task 5: Verify the Complete Escort Loop

**Files:**
- Modify if needed: `Assets/AlmostDelivered/Tests/PlayMode/MissionPrototypeIntegrationTests.cs`
- Modify: `docs/superpowers/specs/2026-11-06-continuous-escort-and-relay-beacons-design.md`

**Interfaces:**
- Consumes: all completed tasks.
- Produces: end-to-end evidence for continuous travel combat, checkpoint loading presentation, and route visibility rules.

- [ ] **Step 1: Add an accelerated end-to-end travel encounter test**

Add `EscortSpawnsTravelCombatThenLoadsSuppliesAtRelay`. Assert that enemies exist while the mission is still `Escorting`, the field has no `PayloadRouteView`, the minimap retains route segments, the first relay enters `Loading`, and clearing checkpoint enemies completes the first cargo collection stage.

- [ ] **Step 2: Run the focused end-to-end test**

Expected: PASS.

- [ ] **Step 3: Run complete Unity suites**

Run all EditMode tests and all PlayMode tests without `-quit`.
Expected: zero failures or skipped tests; record exact totals from the NUnit XML files.

- [ ] **Step 4: Run repository checks**

Run `git diff --check` and inspect `git status --short`. Revert only test-runner-generated settings noise by restoring its exact previous content, preserving all user changes.

- [ ] **Step 5: Update the design verification section**

Record exact test totals and note that final visual tuning and full commander-AI integration remain future work.

- [ ] **Step 6: Commit**

Commit message: `test: verify continuous payload escort loop`
