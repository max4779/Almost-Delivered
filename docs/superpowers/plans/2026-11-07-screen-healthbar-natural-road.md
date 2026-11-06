# Screen, Health Bar, and Natural Road Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Show the complete preview, make damage visibly reduce enemy health bars, and render the payload path as a deterministic natural dirt road.

**Architecture:** Keep the preview correction as Unity editor state, repair the existing health-bar renderer at its UI geometry boundary, and add an isolated runtime road-mesh component configured by the existing mission scene builder. The road consumes `PayloadRoutePlan` without changing payload simulation or minimap behavior.

**Tech Stack:** Unity 2021.3, C#, Unity UI, procedural `Mesh`, NUnit Unity Test Framework

**Spec:** `docs/superpowers/specs/2026-11-07-screen-healthbar-natural-road-design.md`

## Global Constraints

- Do not restore a glowing world-space route line.
- Road width is 5.5 metres and mission route exclusion width is 8 metres.
- Road generation and foliage placement are deterministic for the mission seed.
- Existing payload authority and network-ready snapshot flow remain unchanged.
- Do not push to GitHub without explicit user approval.

## Review Focus

- One-point or null routes must produce no mesh and no exception.
- Very short route segments must not create zero-length tangents or invalid vertices.
- Reconfiguration must replace prior mesh and foliage instead of accumulating objects.
- Road edges must remain wide enough to contain the exact piecewise-linear payload path at bends.
- Generated decoration must have no colliders that block the player or payload.

---

### Task 1: Visible Health Bar Fill

**Files:**
- Modify: `Assets/AlmostDelivered/Runtime/Gameplay/WorldHealthBar.cs`
- Test: `Assets/AlmostDelivered/Tests/PlayMode/WorldHealthBarTests.cs`

**Interfaces:**
- Consumes: `ActorHealth.HealthChanged(float current, float maximum)`
- Produces: `WorldHealthBar.FillAmount` plus visible left-anchored fill geometry

- [x] **Step 1: Write the failing visual-geometry test**

Add `DamageShrinksVisibleFillGeometryFromTheRightEdge`, comparing world-space fill corners before and after 25 damage.

- [x] **Step 2: Run the test and verify RED**

Run the Unity PlayMode filter for the new test; expected result is one failure because the sprite-less filled image keeps full geometry.

- [x] **Step 3: Scale the fill rectangle from a left pivot**

Set the fill pivot to `(0, .5)` and set `localScale.x` to the clamped health ratio in `Refresh(float current, float maximum)`.

- [x] **Step 4: Run all `WorldHealthBarTests` and verify GREEN**

Expected: 6 passed, 0 failed.

### Task 2: Deterministic Natural Road Mesh

**Files:**
- Create: `Assets/AlmostDelivered/Runtime/Gameplay/Payload/PayloadRoadView.cs`
- Create: `Assets/AlmostDelivered/Tests/EditMode/PayloadRoadViewTests.cs`

**Interfaces:**
- Consumes: `PayloadRoutePlan`, mission seed, width, earth/moss materials, optional grass prefabs
- Produces: `Configure(PayloadRoutePlan route, int seed, float width, Material earth, Material moss, GameObject[] grassPrefabs)` and readable `RoadMesh`, `RoadWidth`, `RoadsideFoliageCount`

- [ ] **Step 1: Write failing mesh and edge-case tests**

Test deterministic vertices, two material submeshes, 5.5 metre nominal width, sparse collider-free foliage, safe null/one-point routes, and replacement on reconfigure.

- [ ] **Step 2: Run the new EditMode fixture and verify RED**

Expected: compile failure because `PayloadRoadView` does not exist.

- [ ] **Step 3: Implement `PayloadRoadView.Configure(...)`**

Sample route distance every 1.5 metres; derive stable tangents from adjacent samples; use seed-based Perlin noise for small centre and width variation; create earth core and moss shoulder submeshes at `y=.035`; place alternating grass prefabs roughly every 7 metres outside the shoulder.

- [ ] **Step 4: Run the new EditMode fixture and verify GREEN**

Expected: all `PayloadRoadViewTests` pass.

### Task 3: Mission Scene Integration

**Files:**
- Modify: `Assets/AlmostDelivered/Editor/Frontend/MissionPrototypeSceneBuilder.cs`
- Modify: `Assets/AlmostDelivered/Tests/EditMode/MissionPrototypeSceneBuilderTests.cs`
- Regenerate: `Assets/AlmostDelivered/Scenes/MissionPrototype.unity`

**Interfaces:**
- Consumes: `PayloadRoadView.Configure(...)` from Task 2 and existing Industrial Dock earth, moss, and foliage assets
- Produces: saved `Payload Road` scene object and 8 metre protected route corridor

- [ ] **Step 1: Add failing scene integration assertions**

Assert exactly one road view, earth/moss materials, positive mesh vertex count, sparse foliage, 8 metre route exclusions, and no `PayloadRouteView`.

- [ ] **Step 2: Run the scene-builder fixture and verify RED**

Expected: road component is absent.

- [ ] **Step 3: Build and configure the road in `BuildOrUpdate()`**

Generate the mission route with an 8 metre corridor, configure the mission with the same width, and load `Earth.mat`, `Moss.mat`, `GrassPatch.prefab`, and `TallGrass.prefab` for the road view.

- [ ] **Step 4: Regenerate the mission scene and run the fixture**

Expected: all `MissionPrototypeSceneBuilderTests` pass and the saved scene contains the road.

### Task 4: Full Verification and Integration

**Files:**
- Verify all changed runtime, test, documentation, and generated scene files

**Interfaces:**
- Consumes: Tasks 1-3
- Produces: verified worktree ready for fast-forward integration into the user's original project

- [ ] **Step 1: Run full EditMode suite**

Expected: all tests pass.

- [ ] **Step 2: Run full PlayMode suite**

Expected: all tests pass.

- [ ] **Step 3: Inspect the diff and restore unrelated generated changes**

Keep only health-bar, road, tests, docs, and intended mission-scene changes.

- [ ] **Step 4: Commit locally and fast-forward the original project branch**

Use a scoped local commit; do not push to GitHub.

