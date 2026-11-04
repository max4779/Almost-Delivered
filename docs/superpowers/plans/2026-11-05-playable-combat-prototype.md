# Playable Combat Prototype Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a complete frontend-to-hub-to-combat playable slice with a visible quarter-view player, WASD movement, mouse-aimed ranged attacks, and two killable prototype bosses.

**Architecture:** Keep staging and scene flow in `Frontend`, add reusable combat components under `Runtime/Combat`, and generate a dedicated `MissionPrototype` scene through the existing frontend editor assembly. Player input delegates to a generic ranged-attack module; both boss modes reuse the same health, team, and projectile contracts without importing commander debug behavior.

**Tech Stack:** Unity 2021.3.45f2, C#, legacy Unity Input API, CharacterController, Unity UI, NUnit, Unity Test Framework.

**Spec:** `docs/superpowers/specs/2026-11-05-playable-combat-prototype-design.md`

## Global Constraints

- Preserve `Frontend -> DepartureHub`; add `DepartureHub -> MissionPrototype` through the deployment gate.
- Keep the approved 45-degree camera, `(0,16,-16)` offset, `(0,0,3)` focus offset, and orthographic size `9`.
- Use existing low-poly visuals and add no packages or downloaded assets.
- Keep movement, health, team, and attack boundaries ready for a later melee module.
- Keep the commander debug arena out of production gameplay.
- Preserve user-owned untracked `Assets/Scenes*` and `Assets/temp.mat*`.
- Use RED/GREEN cycles and finish with the complete EditMode and PlayMode suites.
- Do not push to GitHub without separate user approval.

## Review Focus

- High-speed projectile tunneling: Task 2 adds a real thin-collider PlayMode test.
- Owner/ally friendly fire: Task 2 adds owner and same-team mutation tests.
- Dead actors continuing to move or attack: Task 3 adds death-shutdown tests.
- Duplicate scene roots/build entries after rebuilding: Task 4 adds a repeated-build test.
- Hub player still obscured or backed by the grey outside strip: Task 1 adds persisted cutaway/floor assertions.

---

### Task 1: Scene Flow, Hub Gate, and Player Visibility

**Files:**
- Modify: `Assets/AlmostDelivered/Runtime/Frontend/GameSceneFlow.cs`
- Create: `Assets/AlmostDelivered/Runtime/Frontend/HubDeploymentGate.cs`
- Modify: `Assets/AlmostDelivered/Editor/Frontend/FrontendSceneBuilder.cs`
- Modify: `Assets/AlmostDelivered/Scenes/DepartureHub.unity`
- Modify: `Assets/AlmostDelivered/Tests/EditMode/GameSceneFlowTests.cs`
- Modify: `Assets/AlmostDelivered/Tests/EditMode/FrontendSceneBuilderTests.cs`
- Modify: `Assets/AlmostDelivered/Tests/PlayMode/FrontendFlowIntegrationTests.cs`

**Interfaces:**
- Produces: `GameSceneFlow.MissionPrototypeScene`, `GameSceneFlow.LoadMissionPrototype()`, `HubDeploymentGate.CanLaunch`, `HubDeploymentGate.RequestLaunch()`.
- Consumes: existing `HubPlayerController`, `FrontendSceneBuilder`, and `GameSceneFlow.LoadScene(string)`.

- [ ] **Step 1: Write failing EditMode tests**

Add assertions that `MissionPrototypeScene == "MissionPrototype"`, it is declared, the persisted hub has exactly one gate with a trigger, the tall south-wall visual is absent/disabled while south collision remains, and the floor visual covers beyond `z = -14`.

- [ ] **Step 2: Verify RED**

Run Unity Test Runner filters `GameSceneFlowTests` and `FrontendSceneBuilderTests`. Expected: failures for the missing mission contract, gate, cutaway, and extended floor.

- [ ] **Step 3: Implement scene flow and gate**

Add `LoadMissionPrototype()`. Implement a gate that tracks a `HubPlayerController` in its trigger, shows a serialized `E  출발` prompt while launch is available, responds to E, and exposes `RequestLaunch()` for real scene integration tests.

- [ ] **Step 4: Update the hub builder and persisted scene**

Keep `South Wall Collision`, remove the tall south-wall renderer, extend only the floor visual southward, add one deployment trigger/prompt, and retain all approved camera values.

- [ ] **Step 5: Write failing PlayMode gate-presence behavior**

Add `DeploymentGateTracksPlayerAndPrompt`: load the hub, move the real player into and out of the trigger, advance physics frames, and assert `CanLaunch` plus prompt visibility. The actual scene transition waits for Task 5, after the mission scene exists.

- [ ] **Step 6: Verify GREEN and commit**

Run focused Task 1 tests, then commit only Task 1 files with `feat: add hub deployment gate`.

### Task 2: Reusable Ranged Combat Core

**Files:**
- Create: `Assets/AlmostDelivered/Runtime/Combat/CombatTeam.cs`
- Create: `Assets/AlmostDelivered/Runtime/Combat/CombatProjectile.cs`
- Create: `Assets/AlmostDelivered/Runtime/Combat/RangedAttackController.cs`
- Create: `Assets/AlmostDelivered/Tests/EditMode/RangedCombatContractTests.cs`
- Create: `Assets/AlmostDelivered/Tests/PlayMode/RangedCombatIntegrationTests.cs`

**Interfaces:**
- Produces: `CombatTeamId`, `CombatTeam.Team`, `CombatProjectile.Configure(...)`, `RangedAttackController.Configure(...)`, `RangedAttackController.TryFire(Vector3, float)`.
- Consumes: `ActorHealth.ApplyDamage(float)` and Unity physics.

- [ ] **Step 1: Write failing ranged-attack contract tests**

Assert that a valid shot creates one active projectile along a hand-derived planar direction; cooldown blocks the second shot; non-finite/zero-length aim and a dead owner are rejected; firing after cooldown succeeds.

- [ ] **Step 2: Verify RED**

Run `RangedCombatContractTests`. Expected: missing combat types/behavior.

- [ ] **Step 3: Implement team and attack controller**

Define `CombatTeamId { Player, Enemy }`. `RangedAttackController.Configure` accepts a projectile template, muzzle, damage, cooldown, speed, and lifetime. `TryFire` validates owner health/aim/cooldown, activates a clone, and rotates the owner on XZ.

- [ ] **Step 4: Write failing projectile integration tests**

Using real colliders, assert a high-speed projectile hits one enemy once, ignores owner/allies, dies on world geometry, and expires after its lifetime.

- [ ] **Step 5: Verify RED**

Run `RangedCombatIntegrationTests`. Expected: missing sweep movement, filtering, damage, and expiry.

- [ ] **Step 6: Implement projectile behavior**

`CombatProjectile.Configure(GameObject owner, CombatTeamId team, Vector3 direction, float speed, float damage, float lifetime)` normalizes direction, sweeps current-to-next position, resolves the nearest valid hit, ignores owner hierarchy/allies, applies one enemy hit, and destroys itself on enemy/world/lifetime.

- [ ] **Step 7: Verify GREEN and commit**

Run both Task 2 suites and commit with `feat: add reusable ranged combat`.

### Task 3: Mission Player and Boss Behaviors

**Files:**
- Create: `Assets/AlmostDelivered/Runtime/Gameplay/MissionPlayerController.cs`
- Create: `Assets/AlmostDelivered/Runtime/Gameplay/PrototypeBossController.cs`
- Create: `Assets/AlmostDelivered/Tests/PlayMode/MissionPlayerControllerTests.cs`
- Create: `Assets/AlmostDelivered/Tests/PlayMode/PrototypeBossControllerTests.cs`

**Interfaces:**
- Produces: `MissionPlayerController.Move(Vector2,float)`, `MissionPlayerController.AimAndFire(Vector3,float)`, `PrototypeBossMode`, `PrototypeBossController.Configure(...)`, `PrototypeBossController.Tick(float,float)`, `PrototypeBossController.IsDefeated`.
- Consumes: Task 2 combat components, `CharacterController`, and `GameSceneFlow.LoadDepartureHub()`.

- [ ] **Step 1: Write failing mission-player tests**

Assert normalized XZ movement, configured speed, facing movement direction, real ranged delegation, and rejection of dead/disabled/non-finite movement or fire.

- [ ] **Step 2: Verify RED**

Run `MissionPlayerControllerTests`. Expected: missing controller.

- [ ] **Step 3: Implement mission player**

`Update` reads WASD, intersects the mouse ray with a horizontal plane at player Y, fires while left mouse is held, and loads the hub on Escape. Public entry points contain the testable behavior.

- [ ] **Step 4: Write failing boss tests**

Assert Warden facing/cooldown projectiles, Charger approach/contact cooldown damage, safe null/dead target idle, and immediate movement/attack/collider shutdown after boss death.

- [ ] **Step 5: Verify RED**

Run `PrototypeBossControllerTests`. Expected: missing boss modes and behavior.

- [ ] **Step 6: Implement boss controller**

Define `PrototypeBossMode { RangedWarden, IronCharger }`. `Configure` receives mode, target, optional ranged attack, move speed, contact range/damage, and cooldown. `Tick` keeps the two prototype branches small and subscribes to `ActorHealth.Died` for shutdown.

- [ ] **Step 7: Verify GREEN and commit**

Run Task 3 suites plus existing `HubPlayerControllerTests`; commit with `feat: add mission player and prototype bosses`.

### Task 4: Mission Builder, HUD, and Production Scene

**Files:**
- Create: `Assets/AlmostDelivered/Runtime/Gameplay/MissionHud.cs`
- Create: `Assets/AlmostDelivered/Editor/Frontend/MissionPrototypeSceneBuilder.cs`
- Modify: `Assets/AlmostDelivered/Editor/Frontend/FrontendSceneBuilder.cs`
- Modify: `Assets/AlmostDelivered/Editor/Frontend/AlmostDelivered.Frontend.Editor.asmdef`
- Create: `Assets/AlmostDelivered/Scenes/MissionPrototype.unity`
- Create: `Assets/AlmostDelivered/Tests/EditMode/MissionPrototypeSceneBuilderTests.cs`
- Modify: `Assets/AlmostDelivered/Tests/EditMode/FrontendSceneBuilderTests.cs`

**Interfaces:**
- Produces: `MissionPrototypeSceneBuilder.ScenePath`, `MissionPrototypeSceneBuilder.BuildOrUpdate()`, `MissionHud.Configure(ActorHealth, PrototypeBossController[])`.
- Consumes: Tasks 1-3, `LowPolyAssetFactory`, `VisualArchetype`, `QuarterViewCamera`, Unity UI.

- [ ] **Step 1: Write failing builder tests**

Build/open the real scene and assert exactly one root/player/camera/Warden/Charger/HUD/template; required player and boss components/teams; distinct boss modes; approved camera values; spawn-safe floor/cover; bound HUD; production build-setting order; and stable counts after two builds.

- [ ] **Step 2: Verify RED**

Run `MissionPrototypeSceneBuilderTests`. Expected: missing builder, scene, HUD, and objects.

- [ ] **Step 3: Implement HUD**

`MissionHud.Configure` binds player and bosses. Serialized `Text` fields display `WASD 이동  |  마우스 조준  |  좌클릭 발사  |  ESC 귀환`, player HP, and remaining boss count.

- [ ] **Step 4: Implement mission builder**

Create an industrial yard around `40 x 32`, player at `(0,1,-8)`, separated northern boss pads, non-overlapping crate cover, inactive visible projectile template, existing Player/Commander/Charger visuals, approved camera, lighting, and screen-space HUD. Configure boss max health through serialized scene data and wire all references.

- [ ] **Step 5: Integrate the builder**

Call the mission builder from `BuildOrUpdateAll`, reference `AlmostDelivered.Presentation.Editor`, and register Frontend, DepartureHub, and MissionPrototype once each before retained unrelated scenes.

- [ ] **Step 6: Verify GREEN**

Run Task 4 tests. Restore unrelated generated material/config/prefab/CommanderAI/Frontend churn and retain only intentional mission/hub/runtime/editor/test assets.

- [ ] **Step 7: Commit**

Commit Task 4 files with `feat: build mission prototype scene`.

### Task 5: End-to-End Flow and Final Verification

**Files:**
- Modify: `Assets/AlmostDelivered/Tests/PlayMode/FrontendFlowIntegrationTests.cs`
- Create: `Assets/AlmostDelivered/Tests/PlayMode/MissionPrototypeIntegrationTests.cs`
- Modify only when a RED integration test proves it necessary: Task 1-4 production files.

**Interfaces:**
- Consumes all prior interfaces; adds no production API unless a failing integration test proves a missing boundary.

- [ ] **Step 1: Write failing end-to-end tests**

Assert Frontend Start still loads a visible walkable hub; `DeploymentGateRequestLoadsMissionPrototype` uses the persisted gate to load a mission with one living player/two living bosses; player movement shifts camera equally; real player projectiles damage bosses but not player; both boss modes damage player; killing both updates HUD/remaining count; Escape returns to hub.

- [ ] **Step 2: Verify RED and apply only proven wiring fixes**

Run `FrontendFlowIntegrationTests` and `MissionPrototypeIntegrationTests`. Fix serialized references, spawn placement, colliders/layers, or scene flow one observed failure at a time.

- [ ] **Step 3: Verify focused GREEN**

Both integration suites pass without unexpected logs.

- [ ] **Step 4: Run complete suites**

Run every EditMode test, then every PlayMode test in the open Unity Test Runner and record exact totals. Investigate or report every named failure.

- [ ] **Step 5: Perform approved visual/input verification**

Immediately before Unity screen control, ask the user. Verify full hub player visibility/no grey strip, WASD and E gate flow, mission WASD/mouse/left-click, both boss behaviors/deaths, camera follow, and HUD legibility.

- [ ] **Step 6: Clean and review**

Restore generated churn, run `git diff --check`, preserve user untracked assets, and self-review plan alignment, projectile edge cases, death shutdown, scene idempotence, and test quality. Record why no reviewer subagent was used under the active session policy.

- [ ] **Step 7: Commit and hand off without pushing**

Commit integration tests and any test-proven fixes with `test: verify playable mission flow`. Report controls, exact test totals, commits, and known prototype limits; leave GitHub untouched.

