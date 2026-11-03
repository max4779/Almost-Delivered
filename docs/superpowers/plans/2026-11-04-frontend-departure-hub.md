# Frontend and Departure Hub Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a basic pre-game UI and a separate walkable quarter-view departure hub made from project-owned placeholder geometry.

**Architecture:** Runtime components own scene transitions, placeholder player movement, and fixed-angle camera following. A new editor-only deterministic builder creates shared frontend materials, the two scenes, and build-settings entries; tests inspect generated scenes and exercise movement/flow contracts.

**Tech Stack:** Unity 2021.3.45f2, C#, uGUI, CharacterController, legacy Input Manager, Unity Test Framework.

**Spec:** `docs/superpowers/specs/2026-11-04-frontend-departure-hub-design.md`

## Global Constraints

- Create content only under `Assets/AlmostDelivered`; do not modify or stage user-owned `Assets/Scenes*` or `Assets/temp.mat*`.
- Do not add third-party assets or production character visuals.
- Frontend and DepartureHub are separate scenes; Frontend is build index 0.
- Hub stations are visual placeholders only; no inventory, mission, shop, or deployment behavior.
- Use a placeholder capsule, WASD movement, fixed quarter-view follow camera, and Escape-to-menu.
- Remote GitHub changes remain approval-gated.

## Review Focus

- Repeated builds must not duplicate roots, cameras, EventSystems, or build-settings entries — covered by `FrontendSceneBuilderTests.RepeatedBuildKeepsStableRequiredRootsAndBuildSettings`.
- Starting from a non-16:9 aspect ratio must keep primary buttons reachable — covered by anchor/layout assertions in `FrontendSceneBuilderTests.FrontendCanvasUsesResponsiveAnchorsAndScaler`.
- Missing or disabled CharacterController must not produce invalid movement — covered by `HubPlayerControllerTests.MoveRequiresEnabledControllerAndFiniteInput`.
- Scene flow must reject empty scene names and load only the declared frontend/hub names — covered by `GameSceneFlowTests.SceneNamesAndValidationAreStable`.
- Hub visuals must remain presentation-only and walls must keep collision on gameplay roots — covered by `FrontendSceneBuilderTests.HubSeparatesLayoutVisualsFromCollisionRoots`.

---

### Task 1: Runtime frontend and hub contracts

**Files:**
- Modify: `Assets/AlmostDelivered/Runtime/AlmostDelivered.Runtime.asmdef`
- Create: `Assets/AlmostDelivered/Runtime/Frontend/GameSceneFlow.cs`
- Create: `Assets/AlmostDelivered/Runtime/Frontend/FrontendMenuController.cs`
- Create: `Assets/AlmostDelivered/Runtime/Frontend/HubPlayerController.cs`
- Create: `Assets/AlmostDelivered/Runtime/Frontend/QuarterViewCamera.cs`
- Create: `Assets/AlmostDelivered/Tests/EditMode/GameSceneFlowTests.cs`
- Create: `Assets/AlmostDelivered/Tests/PlayMode/HubPlayerControllerTests.cs`

**Interfaces:**
- Produces: `GameSceneFlow.FrontendScene`, `GameSceneFlow.DepartureHubScene`, `LoadDepartureHub()`, `LoadFrontend()`, `QuitGame()`.
- Produces: `HubPlayerController.Move(Vector2 input, float deltaTime)` for deterministic movement tests; Update reads legacy `Horizontal`, `Vertical`, and Escape.
- Produces: `QuarterViewCamera.Target`, `Offset`, and `SnapToTarget()`.

- [ ] **Step 1: Write failing EditMode tests for stable scene names and invalid-name rejection.**
- [ ] **Step 2: Run the focused EditMode tests and confirm RED because the frontend contracts do not exist.**
- [ ] **Step 3: Write failing PlayMode tests for CharacterController movement, disabled-controller safety, non-finite input rejection, and fixed camera offset.**
- [ ] **Step 4: Run the focused PlayMode tests and confirm RED because the hub runtime components do not exist.**
- [ ] **Step 5: Add the minimum runtime components and uGUI assembly reference needed to pass the tests.**
- [ ] **Step 6: Run focused EditMode and PlayMode tests and confirm GREEN.**
- [ ] **Step 7: Commit the runtime contracts and tests locally.**

### Task 2: Deterministic frontend and departure-hub scenes

**Files:**
- Create: `Assets/AlmostDelivered/Editor/Frontend/AlmostDelivered.Frontend.Editor.asmdef`
- Create: `Assets/AlmostDelivered/Editor/Frontend/FrontendSceneBuilder.cs`
- Modify: `Assets/AlmostDelivered/Tests/EditMode/AlmostDelivered.CommanderAI.EditModeTests.asmdef`
- Create: `Assets/AlmostDelivered/Tests/EditMode/FrontendSceneBuilderTests.cs`
- Generate: `Assets/AlmostDelivered/Art/Materials/Frontend/*.mat`
- Generate: `Assets/AlmostDelivered/Scenes/Frontend.unity`
- Generate: `Assets/AlmostDelivered/Scenes/DepartureHub.unity`
- Modify through builder: `ProjectSettings/EditorBuildSettings.asset`

**Interfaces:**
- Consumes: Task 1 runtime components and scene-name constants.
- Produces: menu item `Almost Delivered/Frontend/Build frontend and departure hub` and command-line entry `AlmostDelivered.Editor.Frontend.FrontendSceneBuilder.BuildFromCommandLine`.

- [ ] **Step 1: Write failing builder tests for frontend UI hierarchy, responsive canvas, hub zones/colliders, player/camera wiring, unique roots, and build-settings order.**
- [ ] **Step 2: Run the focused EditMode tests and confirm RED because the builder and scenes do not exist.**
- [ ] **Step 3: Implement shared-material generation and the deterministic Frontend scene.**
- [ ] **Step 4: Implement the industrial DepartureHub layout, placeholder player, fixed camera, lighting, signage, and visual-only stations.**
- [ ] **Step 5: Register Frontend, DepartureHub, and the existing CommanderAITest scene without duplicates.**
- [ ] **Step 6: Run the builder from Unity and inspect both scenes in the open editor.**
- [ ] **Step 7: Run focused builder tests twice and confirm stable GREEN results.**
- [ ] **Step 8: Commit generated scenes, materials, builder, and tests locally.**

### Task 3: End-to-end scene flow and final verification

**Files:**
- Create: `Assets/AlmostDelivered/Tests/PlayMode/FrontendFlowIntegrationTests.cs`
- Modify: `README.md`
- Create: `docs/frontend/departure-hub.md`

**Interfaces:**
- Consumes: Task 1 scene flow and Task 2 generated scenes.
- Produces: real-scene coverage for frontend-to-hub and Escape-to-frontend flow.

- [ ] **Step 1: Write failing real-scene PlayMode tests for Start-button hub loading, walkable spawn safety, fixed camera tracking, and Escape-return behavior.**
- [ ] **Step 2: Run focused PlayMode tests and confirm RED before final wiring.**
- [ ] **Step 3: Complete only the scene wiring required by the failing integration tests.**
- [ ] **Step 4: Run focused integration tests and confirm GREEN.**
- [ ] **Step 5: Play the flow in Unity, verify the menu and walkable hub visually, and record any visual-only follow-ups.**
- [ ] **Step 6: Run full EditMode and PlayMode suites plus the command-line builder.**
- [ ] **Step 7: Document rebuild steps, controls, scope boundaries, and current placeholders.**
- [ ] **Step 8: Commit the integration tests and documentation locally.**

## Self-review

- The plan covers the requested pre-game UI and walkable departure room without introducing character, combat, mission, inventory, or shop systems.
- Runtime, editor generation, and real-scene integration are separated into independently reviewable tasks.
- Every production behavior starts from a named failing test; generated assets are verified through the builder tests and real-scene tests.
- The existing user scene/material files and third-party packs remain outside the plan.
