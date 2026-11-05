# Environment Terrain Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add deterministic walkable hills and collider-free grass to the payload mission's procedural map.

**Architecture:** Extend the existing map theme and generator with dedicated traversable-terrain and foliage categories. Generate low-poly mesh assets through the current editor factory, then configure the mission scene builder to request a conservative number of hills and grass clusters while honoring route exclusions.

**Tech Stack:** Unity 2021.3.45f2, C#, Unity Test Framework, built-in Standard shader

**Spec:** `docs/superpowers/specs/2026-11-06-environment-terrain-design.md`

## Global Constraints

- Hills are static walkable mesh colliders and remain outside payload corridor/protected zones.
- Grass is rendered as single-mesh clusters with no colliders.
- Same seed produces the same positions, rotations, and piece choices.
- Preserve existing two-category configuration overloads.
- Do not modify user-owned `Assets/Scenes`, `Assets/temp.mat`, or code-coverage settings.

## Review Focus

- A route exclusion covering the whole map must eliminate hills, blockers, and foliage while leaving flat terrain details available; covered by Task 1.
- Null/empty new catalogs and zero counts must generate empty category roots without exceptions; covered by Task 1.
- Blockers and foliage must not overlap hills even when placement space is constrained; covered by Task 1.
- Generated hill slopes must remain walkable for the existing 45-degree CharacterController; covered by Task 2 bounds/slope assertions.
- Grass prefabs must never gain colliders through asset regeneration; covered by Task 2.

---

### Task 1: Map categories and safe deterministic placement

**Files:**
- Modify: `Assets/AlmostDelivered/Runtime/MapGeneration/MissionMapTheme.cs`
- Modify: `Assets/AlmostDelivered/Runtime/MapGeneration/MissionMapGenerator.cs`
- Modify: `Assets/AlmostDelivered/Tests/PlayMode/MissionMapGeneratorTests.cs`

**Interfaces:**
- Produces: `MissionMapPieceCategory.TraversableTerrain`, `MissionMapPieceCategory.Foliage`
- Produces: `MissionMapTheme.TraversableTerrain`, `MissionMapTheme.Foliage`
- Produces: extended `MissionMapTheme.Configure(terrainDetails, traversableTerrain, blockingObstacles, foliage)` overload
- Produces: extended `MissionMapGenerator.Configure(theme, halfExtents, terrainCount, traversableCount, blockerCount, foliageCount, protectedZones, randomizeOnStart, maxAttempts)` overload

- [ ] **Step 1: Write failing PlayMode tests**

Add tests named `ExtendedCategoriesAreGeneratedDeterministically`, `RouteExclusionsProtectAllPhysicalAndVisualEnvironment`, `BlockersAndFoliageAvoidGeneratedHills`, and `EmptyExtendedCatalogsRemainSafe`. Assert category presence, identical same-seed snapshots, exclusion behavior, and non-overlap.

- [ ] **Step 2: Run focused PlayMode tests and confirm RED**

Run the Unity PlayMode suite filtered to `MissionMapGeneratorTests`.
Expected: compile failures for the new enum values/configuration overload.

- [ ] **Step 3: Extend theme and generator**

Retain old overloads. Generate flat details at height `.015f`; generate hills at `0f`; generate blockers at `0f`; generate foliage at `.02f`. Accumulate hill footprints as exclusions for blockers and foliage. Apply supplied route exclusions to hills, blockers, and foliage.

- [ ] **Step 4: Run focused PlayMode tests and confirm GREEN**

Expected: all `MissionMapGeneratorTests` pass.

- [ ] **Step 5: Commit Task 1**

Commit message: `feat: add procedural environment categories`

### Task 2: Low-poly hill and grass assets

**Files:**
- Modify: `Assets/AlmostDelivered/Editor/Frontend/MissionMapAssetFactory.cs`
- Modify: `Assets/AlmostDelivered/Tests/EditMode/MissionMapAssetFactoryTests.cs`
- Generate: `Assets/AlmostDelivered/Art/Meshes/MapThemes/IndustrialDock/*`
- Generate: `Assets/AlmostDelivered/Art/Materials/MapThemes/IndustrialDock/*`
- Generate: `Assets/AlmostDelivered/Prefabs/MapThemes/IndustrialDock/Landscape/*`
- Generate: `Assets/AlmostDelivered/Prefabs/MapThemes/IndustrialDock/Foliage/*`

**Interfaces:**
- Consumes: the four `MissionMapTheme` catalogs from Task 1.
- Produces: two walkable hill prefabs and two collider-free grass prefabs referenced by `IndustrialDock.asset`.

- [ ] **Step 1: Write failing EditMode asset tests**

Add `TraversableTerrainUsesElevatedMeshColliders`, `FoliageUsesSingleMeshWithoutColliders`, and catalog minimum assertions. Check hill mesh bounds, collider mesh assignment, and absence of foliage colliders.

- [ ] **Step 2: Run focused EditMode tests and confirm RED**

Expected: new catalogs are empty and asset assertions fail.

- [ ] **Step 3: Generate materials, meshes, prefabs, and theme references**

Create reusable radial-hill meshes with 12 segments and 4 rings, maximum heights 1.6m and 1.1m. Create two double-sided crossed-blade grass meshes. Keep one renderer and zero colliders per grass root; assign a static `MeshCollider` to each hill root.

- [ ] **Step 4: Run focused EditMode tests and confirm GREEN**

Expected: all `MissionMapAssetFactoryTests` pass.

- [ ] **Step 5: Commit Task 2**

Commit message: `feat: generate low poly hills and grass`

### Task 3: Mission integration and regression verification

**Files:**
- Modify: `Assets/AlmostDelivered/Editor/Frontend/MissionPrototypeSceneBuilder.cs`
- Modify: `Assets/AlmostDelivered/Tests/EditMode/MissionPrototypeSceneBuilderTests.cs`
- Regenerate: `Assets/AlmostDelivered/Scenes/MissionPrototype.unity`

**Interfaces:**
- Consumes: extended generator configuration and Industrial Dock theme assets from Tasks 1-2.
- Produces: saved mission scene configured for 40 flat details, 6 hills, 28 blockers, and 42 grass clusters.

- [ ] **Step 1: Write failing scene-builder test**

Assert the saved scene's generator exposes counts `40`, `6`, `28`, `42`, and that generated hill/foliage categories are present after mission initialization.

- [ ] **Step 2: Run focused EditMode test and confirm RED**

Expected: missing extended count properties or old scene configuration values.

- [ ] **Step 3: Configure and rebuild the mission scene**

Use the extended generator overload with counts `40, 6, 28, 42`. Rebuild through `MissionPrototypeSceneBuilder.BuildOrUpdate()` so serialized references remain valid.

- [ ] **Step 4: Run focused tests and then full suites**

Expected: focused tests pass; full EditMode and PlayMode suites have zero failures.

- [ ] **Step 5: Review generated scene and repository diff**

Confirm no missing script references, no user-owned files changed, `git diff --check` passes, and asset/meta files are present.

- [ ] **Step 6: Commit Task 3**

Commit message: `feat: populate mission with hills and grass`
