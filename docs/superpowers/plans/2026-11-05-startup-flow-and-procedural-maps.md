# Startup Flow and Procedural Maps Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Deliver a continuous frontend-to-hub-to-mission flow using generated character models and a reproducible random combat-map generator backed by reusable theme assets.

**Architecture:** Keep the existing three-scene chain. Editor builders create persisted scenes and low-poly theme assets, while runtime map generation uses a pure deterministic planner plus a small instantiation component.

**Tech Stack:** Unity 2021.3.45f2, C#, NUnit/Unity Test Framework, built-in render pipeline, existing `LowPolyAssetFactory`.

**Spec:** `docs/superpowers/specs/2026-11-05-startup-flow-and-procedural-maps-design.md`

## Global Constraints

- Do not modify or stage `Assets/Scenes.meta`, `Assets/Scenes/`, `Assets/temp.mat`, or `Assets/temp.mat.meta`.
- Do not push to GitHub.
- Keep `Frontend`, `DepartureHub`, and `MissionPrototype` as the public scene names.
- New generated visuals remain presentation-only; collision and gameplay stay on actor roots.
- Map layout must be deterministic for an explicit seed and independent of Unity's global random state.

## Review Focus

- Pressing Play while a non-frontend scene is open must still enter `Frontend`; cover in Task 1.
- Missing generated visual prefabs must fail scene building with a clear error instead of silently restoring capsules; cover in Task 1.
- Impossible or crowded placement requests must terminate within the attempt budget; cover in Task 2.
- Same seed/theme must produce identical placement ordering while different seeds vary; cover in Task 2.
- Regeneration must remove old pieces and preserve protected spawn/corridor regions; cover in Task 3.

---

### Task 1: Continuous startup flow and generated actor visuals

**Files:**
- Modify: `Assets/AlmostDelivered/Editor/Frontend/AlmostDelivered.Frontend.Editor.asmdef`
- Modify: `Assets/AlmostDelivered/Editor/Frontend/FrontendSceneBuilder.cs`
- Modify: `Assets/AlmostDelivered/Editor/Frontend/MissionPrototypeSceneBuilder.cs`
- Modify: `Assets/AlmostDelivered/Tests/EditMode/FrontendSceneBuilderTests.cs`
- Modify: `Assets/AlmostDelivered/Tests/EditMode/MissionPrototypeSceneBuilderTests.cs`
- Modify generated scenes: `Assets/AlmostDelivered/Scenes/Frontend.unity`, `DepartureHub.unity`, `MissionPrototype.unity`

**Interfaces:**
- Consumes: `LowPolyAssetFactory.InstantiateVisual(GameObject, VisualArchetype)`.
- Produces: persisted scenes whose gameplay roots contain exactly one matching `LowPolyVisual`, and `EditorSceneManager.playModeStartScene == Frontend`.

- [ ] Add failing EditMode tests `BuildSetsFrontendAsPlayModeStartScene`, `HubPlayerUsesGeneratedPlayerVisual`, and `MissionActorsUseGeneratedArchetypes`.
- [ ] Run the focused EditMode tests and confirm failures are caused by the missing start-scene assignment and capsule visuals.
- [ ] Reference `AlmostDelivered.Presentation.Editor`, instantiate Player/Commander/Charger visuals on renderer-free actor roots, and set the editor Play start scene in `RegisterBuildSettings`.
- [ ] Rebuild all three scenes and rerun focused EditMode plus existing frontend PlayMode integration tests.
- [ ] Commit exact Task 1 files with `feat: connect frontend flow with generated models`.

### Task 2: Deterministic map layout planner

**Files:**
- Create: `Assets/AlmostDelivered/Runtime/MapGeneration/MapPieceSpec.cs`
- Create: `Assets/AlmostDelivered/Runtime/MapGeneration/MapPlacement.cs`
- Create: `Assets/AlmostDelivered/Runtime/MapGeneration/MapExclusionZone.cs`
- Create: `Assets/AlmostDelivered/Runtime/MapGeneration/ProceduralMapPlanner.cs`
- Create: matching `.meta` files through Unity import
- Create: `Assets/AlmostDelivered/Tests/EditMode/ProceduralMapPlannerTests.cs`

**Interfaces:**
- Produces: `IReadOnlyList<MapPlacement> ProceduralMapPlanner.Generate(int seed, Vector2 halfExtents, IReadOnlyList<MapPieceSpec> pieces, int requestedCount, IReadOnlyList<MapExclusionZone> exclusions, int maxAttempts)`.
- `MapPlacement` exposes `PieceIndex`, `Position`, `QuarterTurns`, and rotated `Footprint`.

- [ ] Add failing tests for identical seeds, different seeds, bounds, exclusions, non-overlap, invalid footprints, zero count, and attempt-budget termination.
- [ ] Run `ProceduralMapPlannerTests` and verify compile/behavior failures identify the missing planner API.
- [ ] Implement the minimal pure planner using `System.Random`, axis-aligned footprints, and quarter-turn rotations.
- [ ] Rerun focused tests and the complete EditMode suite.
- [ ] Commit exact Task 2 files with `feat: add deterministic map layout planner`.

### Task 3: Theme assets and runtime mission-map generation

**Files:**
- Create: `Assets/AlmostDelivered/Runtime/MapGeneration/MissionMapTheme.cs`
- Create: `Assets/AlmostDelivered/Runtime/MapGeneration/MissionMapGenerator.cs`
- Create: `Assets/AlmostDelivered/Editor/Frontend/MissionMapAssetFactory.cs`
- Modify: `Assets/AlmostDelivered/Editor/Frontend/MissionPrototypeSceneBuilder.cs`
- Create: `Assets/AlmostDelivered/Tests/EditMode/MissionMapAssetFactoryTests.cs`
- Create: `Assets/AlmostDelivered/Tests/PlayMode/MissionMapGeneratorTests.cs`
- Generate: `Assets/AlmostDelivered/Art/Materials/MapThemes/IndustrialDock/*`
- Generate: `Assets/AlmostDelivered/Prefabs/MapThemes/IndustrialDock/*`
- Generate: `Assets/AlmostDelivered/Configs/MapThemes/IndustrialDock.asset`
- Modify generated scene: `Assets/AlmostDelivered/Scenes/MissionPrototype.unity`

**Interfaces:**
- Consumes: Task 2 planner and the persisted actor spawn positions from Task 1.
- Produces: `MissionMapGenerator.Generate(int seed)` and `MissionMapGenerator.LastSeed`, plus a reusable Industrial Dock theme asset.

- [ ] Add failing EditMode tests that the asset factory creates terrain details without colliders, blocking obstacles with colliders, and a valid theme catalog.
- [ ] Add failing PlayMode tests that explicit seeds reproduce layouts, different seeds vary them, old generated children are replaced, and protected mission regions stay clear.
- [ ] Implement `MissionMapTheme`, `MissionMapAssetFactory`, and the initial low-poly Industrial Dock assets.
- [ ] Implement `MissionMapGenerator`, configure 24 terrain details and 14 blockers, and replace the mission's fixed cover cubes with the configured generator.
- [ ] Rebuild the mission scene, run focused tests, then run complete EditMode and PlayMode suites.
- [ ] Commit exact Task 3 files with `feat: generate themed mission layouts`.

### Task 4: End-to-end visual verification and cleanup

**Files:**
- Modify only if a visual/runtime defect is reproduced by a new failing test.
- Update: `.superpowers/sdd/2026-11-05-startup-flow-and-procedural-maps/progress.md` (git-ignored ledger).

**Interfaces:**
- Consumes: completed scene chain, generated visuals, and mission-map generator.
- Produces: verified frontend -> hub -> gate -> generated mission flow.

- [ ] Start Play from a non-frontend open scene and verify the frontend appears first.
- [ ] Traverse to the hub, confirm the generated Player visual moves with the camera, enter the gate, and launch the mission.
- [ ] Confirm the mission uses Player/Commander/Charger visuals, randomized terrain/obstacles appear clear of protected spawns, and ranged combat still works.
- [ ] Stop Play Mode, restore unrelated Unity serialization churn, and run `git diff --check`.
- [ ] Run complete EditMode and PlayMode suites once more and record exact totals.
- [ ] Perform whole-branch review, fix Important/Critical findings through RED-GREEN tests, and leave GitHub unpushed.

