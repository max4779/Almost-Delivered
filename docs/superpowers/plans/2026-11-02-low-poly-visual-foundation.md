# Low-Poly Visual Foundation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Create a repository-safe, deterministic low-poly visual set for the player, four monster roles, commander, payload, and objective beacon, then replace the primitive renderers in the existing Commander AI arena without changing gameplay behavior.

**Architecture:** Gameplay roots retain colliders, controllers, NavMeshAgents, health, perception, and AI. A generated `VisualRoot` child owns renderers and a small procedural pose component. An Editor-only factory creates shared Built-in materials and prefab assets deterministically, while the existing arena builder instantiates those prefabs beneath its actors.

**Tech Stack:** Unity 2021.3.45f2, C# 9-compatible Unity scripts, Built-in Render Pipeline `Standard`/`Unlit` shaders, UnityEditor `PrefabUtility`/`AssetDatabase`, NUnit EditMode and Unity PlayMode tests.

**Spec:** `docs/superpowers/specs/2026-11-02-basic-game-asset-design.md`

## Global Constraints

- Keep Unity `2021.3.45f2` and the Built-in Render Pipeline; do not add URP/HDRP packages or Shader Graph assets.
- Do not commit or redistribute `Assets/SimpleNaturePack`, `Assets/BrokenVector`, or `Assets/PixPlays` until their licenses explicitly permit it.
- Keep Collider, CharacterController, NavMeshAgent, combat, perception, and Commander AI components on gameplay roots, never under `VisualRoot`.
- Generated visual prefabs may use only shared materials; runtime code must not call `Renderer.material` or create per-unit material instances.
- Existing Commander AI input boundaries and permanent-confusion behavior must remain unchanged.
- Existing final baseline is EditMode 105/105 and PlayMode 84/84.
- This plan implements design phase 1 only. Payload movement, control-point rules, AI-generated icons, production VFX, external animation, and audio are out of scope.

## File Structure

- `Assets/AlmostDelivered/Runtime/Presentation/VisualArchetype.cs` — stable visual identity enum for player, four monster roles, commander, payload, beacon, and dummy.
- `Assets/AlmostDelivered/Runtime/Presentation/LowPolyVisual.cs` — prefab contract exposing archetype, `VisualRoot`, renderers, pose pivots, and shared-material validation.
- `Assets/AlmostDelivered/Runtime/Presentation/ProceduralPoseAnimator.cs` — simple transform-based Idle/Move/Attack/Hit pose driver without a Humanoid rig.
- `Assets/AlmostDelivered/Editor/Presentation/LowPolyAssetFactory.cs` — deterministic material and prefab authoring.
- `Assets/AlmostDelivered/Editor/Presentation/LowPolyAssetBuilder.cs` — menu and command-line entrypoints around the factory.
- `Assets/AlmostDelivered/Editor/Presentation/AlmostDelivered.Presentation.Editor.asmdef` — Editor-only presentation authoring assembly.
- `Assets/AlmostDelivered/Tests/EditMode/LowPolyAssetBuilderTests.cs` — generated asset, hierarchy, material, collider, and GUID contracts.
- `Assets/AlmostDelivered/Tests/PlayMode/ProceduralPoseAnimatorTests.cs` — transform-only animation and gameplay-root isolation.
- `Assets/AlmostDelivered/Tests/EditMode/CommanderAITestSceneBuilderTests.cs` — arena integration and repeated-build coverage.
- `Assets/AlmostDelivered/Editor/CommanderAI/CommanderAITestSceneBuilder.cs` — replaces root renderers with generated visual children.
- `Assets/AlmostDelivered/Art/Materials/Generated/` — palette and role material assets.
- `Assets/AlmostDelivered/Prefabs/Generated/Characters/` — player, dummy, four monster roles, commander.
- `Assets/AlmostDelivered/Prefabs/Generated/Objectives/` — payload cart and objective beacon.
- `Assets/ThirdParty/NOTICE.md` — dependency/license inventory and redistribution boundary.
- `.gitignore`, `Packages/`, `ProjectSettings/` — public repository safety and reproducibility.

## Review Focus

- Repeated asset generation must preserve prefab/material GUIDs and produce no duplicate children; Task 3 adds a two-run GUID/count test.
- Missing or unavailable Built-in shaders must fail with an actionable exception instead of generating pink materials; Task 3 tests shader lookup before asset creation.
- A generated `VisualRoot` must contain no colliders, controllers, agents, health, perception, or AI components; Tasks 2 and 3 enforce this recursively.
- Attaching a visual to an actor that already has a `VisualRoot` must replace only that visual and preserve every gameplay component; Task 4 tests component identity before and after attachment.
- Procedural animation must not move the gameplay root or accumulate transform drift after returning to Idle; Task 2 adds root-position and neutral-pose assertions.

---

### Task 1: Public Repository and Third-Party Boundary

**Files:**
- Modify: `.gitignore`
- Create: `Assets/ThirdParty/NOTICE.md`
- Add: `Packages/manifest.json`
- Add: `Packages/packages-lock.json`
- Add: `ProjectSettings/**`
- Modify: `README.md`

**Interfaces:**
- Consumes: the existing public repository and local imported asset directories.
- Produces: a reproducible Unity project boundary where first-party assets are tracked and named third-party source directories remain local-only.

- [ ] **Step 1: Record the current reproducibility and asset boundary**

Add explicit root-relative ignores for `/Assets/SimpleNaturePack/`, `/Assets/BrokenVector/`, `/Assets/PixPlays/` and their matching root `.meta` files. Ignore `/Assets/ThirdParty/*` with an exception for `/Assets/ThirdParty/NOTICE.md`. Do not ignore `Assets/Scenes` or arbitrary `.mat` files.

- [ ] **Step 2: Create `Assets/ThirdParty/NOTICE.md`**

Record each local pack name, current purpose, redistribution status as `Not verified — excluded from public repository`, and a rule that files move into the public project only after recording source URL, license, version, and redistribution permission.

- [ ] **Step 3: Audit Unity reproducibility files**

Review `Packages/manifest.json`, `Packages/packages-lock.json`, and `ProjectSettings` for absolute paths, credentials, machine-only data, or unrelated package changes. Stage the manifest, lock file, and safe project settings required to open Unity 2021.3.45f2; exclude user settings and generated caches already covered by `.gitignore`.

- [ ] **Step 4: Verify the public file boundary**

Run:

```powershell
git check-ignore -v Assets/SimpleNaturePack/Prefabs/Tree_01.prefab Assets/BrokenVector/FreeLowPolyPack/readme.txt Assets/PixPlays/ElementalShields/README_Shields.md
git status --short
git diff --check
```

Expected: all three third-party paths show explicit ignore rules; `Packages`/`ProjectSettings` safe files are visible for staging; no third-party source file is staged.

- [ ] **Step 5: Document clone/open requirements in `README.md`**

State Unity version, first scene, that third-party packs are optional and excluded, and that phase-1 generated visuals require no external art package.

- [ ] **Step 6: Commit**

```powershell
git add .gitignore Assets/ThirdParty/NOTICE.md Packages ProjectSettings README.md
git diff --cached --check
git commit -m "chore: define Unity project asset boundary"
```

### Task 2: Runtime Visual Contract and Procedural Poses

**Files:**
- Create: `Assets/AlmostDelivered/Runtime/Presentation/VisualArchetype.cs`
- Create: `Assets/AlmostDelivered/Runtime/Presentation/LowPolyVisual.cs`
- Create: `Assets/AlmostDelivered/Runtime/Presentation/ProceduralPoseAnimator.cs`
- Create: `Assets/AlmostDelivered/Tests/EditMode/LowPolyVisualTests.cs`
- Create: `Assets/AlmostDelivered/Tests/PlayMode/ProceduralPoseAnimatorTests.cs`

**Interfaces:**
- Consumes: Unity `Transform`, `Renderer`, and shared `Material` references only.
- Produces: `VisualArchetype`; `LowPolyVisual.Configure(VisualArchetype, Transform, Renderer[], Transform[])`; `LowPolyVisual.IsGameplayFree(out string)`; `ProceduralPoseAnimator.Configure(Transform body, Transform leftArm, Transform rightArm)`; `BindMotionSource(Transform)`; `PlayAttack()`; `PlayHit()`.

- [ ] **Step 1: Write failing EditMode contract tests**

Create tests named:

- `Configure_RequiresVisualRootAndSharedRenderers`
- `IsGameplayFree_RejectsColliderOrNavMeshAgentAnywhereBelowVisualRoot`
- `IsGameplayFree_RejectsHealthPerceptionOrCommanderAIComponentsBelowVisualRoot`
- `SharedMaterials_DoesNotInstantiateRendererMaterials`

Assert the exact archetype and references returned by `LowPolyVisual`, recursive rejection messages containing the offending component type, and identity equality for renderer `sharedMaterial` before and after validation.

- [ ] **Step 2: Run focused EditMode tests and confirm RED**

Run Unity with `-runTests -testPlatform EditMode -testFilter AlmostDelivered.Tests.EditMode.LowPolyVisualTests`.

Expected: test assembly compilation fails because presentation contracts do not exist.

- [ ] **Step 3: Implement `VisualArchetype` and `LowPolyVisual`**

Use enum values `Player`, `Dummy`, `Shield`, `Ranged`, `Charger`, `Support`, `Commander`, `Payload`, `ObjectiveBeacon`. Validation walks `VisualRoot.GetComponentsInChildren<Component>(true)` and rejects gameplay/physics types without mutating the hierarchy.

- [ ] **Step 4: Run focused EditMode tests and confirm GREEN**

Expected: all `LowPolyVisualTests` pass with Unity exit 0.

- [ ] **Step 5: Write failing PlayMode pose tests**

Create tests named:

- `Moving_AlternatesArmPivotsWithoutMovingGameplayRoot`
- `Idle_ReturnsAllConfiguredPivotsToRecordedNeutralPose`
- `AttackAndHit_AreOneShotAndReturnToNeutralWithoutDrift`
- `Disable_RestoresNeutralPose`

Use real frames and move the bound motion source externally. Assert the animator never changes gameplay-root position/rotation, arm rotations change while the source moves, and every pivot returns within `0.1` degrees/`0.001` units of its captured local pose.

- [ ] **Step 6: Run focused PlayMode tests and confirm RED**

Run Unity with `-runTests -testPlatform PlayMode -testFilter AlmostDelivered.Tests.PlayMode.ProceduralPoseAnimatorTests`.

Expected: compilation fails because `ProceduralPoseAnimator` does not exist.

- [ ] **Step 7: Implement `ProceduralPoseAnimator`**

Capture neutral local transforms once in `Configure`. `BindMotionSource` stores a transform and samples its planar displacement without modifying it. Drive only configured child pivots in `LateUpdate`: movement uses a sine phase, attack and hit use bounded one-shot timers, idle damps to neutral, and `OnDisable` restores exact captured values.

- [ ] **Step 8: Run focused and full presentation tests**

Expected: focused EditMode and PlayMode tests pass with Unity exit 0.

- [ ] **Step 9: Commit**

```powershell
git add Assets/AlmostDelivered/Runtime/Presentation Assets/AlmostDelivered/Tests/EditMode/LowPolyVisualTests.cs* Assets/AlmostDelivered/Tests/PlayMode/ProceduralPoseAnimatorTests.cs*
git diff --cached --check
git commit -m "feat: add low-poly presentation contracts"
```

### Task 3: Deterministic Low-Poly Asset Factory

**Files:**
- Create: `Assets/AlmostDelivered/Editor/Presentation/LowPolyAssetFactory.cs`
- Create: `Assets/AlmostDelivered/Editor/Presentation/LowPolyAssetBuilder.cs`
- Create: `Assets/AlmostDelivered/Editor/Presentation/AlmostDelivered.Presentation.Editor.asmdef`
- Modify: `Assets/AlmostDelivered/Editor/CommanderAI/AlmostDelivered.CommanderAI.Editor.asmdef`
- Modify: `Assets/AlmostDelivered/Tests/EditMode/AlmostDelivered.CommanderAI.EditModeTests.asmdef`
- Create: `Assets/AlmostDelivered/Tests/EditMode/LowPolyAssetBuilderTests.cs`
- Generate: `Assets/AlmostDelivered/Art/Materials/Generated/*.mat`
- Generate: `Assets/AlmostDelivered/Prefabs/Generated/Characters/*.prefab`
- Generate: `Assets/AlmostDelivered/Prefabs/Generated/Objectives/*.prefab`

**Interfaces:**
- Consumes: Task 2 `VisualArchetype`, `LowPolyVisual`, and `ProceduralPoseAnimator`.
- Produces: `LowPolyAssetFactory.CreateOrUpdateAll(Func<string, Shader> shaderResolver = null)`; `GetPrefabPath(VisualArchetype) -> string`; `LoadPrefab(VisualArchetype) -> GameObject`; `InstantiateVisual(GameObject root, VisualArchetype) -> LowPolyVisual`; command-line marker `LOW_POLY_ASSETS_BUILT`.

- [ ] **Step 1: Write failing factory tests**

Create tests named:

- `CreateOrUpdateAll_CreatesEveryArchetypeWithBuiltInShader`
- `GeneratedPrefabs_HaveOneVisualRootAndNoGameplayComponents`
- `GeneratedPrefabs_UseOnlyGeneratedSharedMaterials`
- `GeneratedRoleSilhouettes_HaveRequiredNamedPartsAndDistinctBounds`
- `CreateOrUpdateAll_RepeatedRunPreservesGuidsAndChildCounts`
- `InstantiateVisual_ReplacesExistingVisualOnlyAndPreservesRootComponents`
- `MissingShader_ThrowsBeforeWritingAnyAsset`

Required named parts: Player `Body`, `Head`, `Weapon`; Shield adds `Shield`; Ranged adds `RangedWeapon`; Charger adds `Horns`; Support adds `Totem`; Commander adds `Cape`, `Banner`, `WarHorn`; Payload adds `Cart`, four uniquely named `Wheel*` children, `RuneCore`; ObjectiveBeacon adds `Beacon`, `RuneRing`.

Distinct-bounds assertions: Shield width exceeds Ranged by at least 20%; Commander height exceeds every ordinary role; Charger forward depth exceeds Support; Payload has nonzero bounds and exactly four wheels.

- [ ] **Step 2: Run focused EditMode tests and confirm RED**

Run Unity with `-runTests -testPlatform EditMode -testFilter AlmostDelivered.Tests.EditMode.LowPolyAssetBuilderTests`.

Expected: compilation fails because the Editor factory does not exist.

- [ ] **Step 3: Add Editor assembly boundaries**

Create `AlmostDelivered.Presentation.Editor` referencing `AlmostDelivered.Runtime`. Add that reference to `AlmostDelivered.CommanderAI.Editor` and the EditMode test assembly so the arena builder and direct factory tests can consume the presentation factory without reflection or a circular dependency.

- [ ] **Step 4: Implement material authoring**

Create/reuse exact material assets through `AssetDatabase.LoadAssetAtPath` and `CreateAsset`; the optional resolver defaults to `Shader.Find` and allows the missing-shader test to return null before any write. Use `Standard` for lit surfaces and `Unlit/Color` for rings/markers. Palette assets are Player teal/blue, Payload gold/teal, enemy rust, Shield green, Ranged orange, Charger red, Support lime, Commander purple/gold, neutral dummy gray.

- [ ] **Step 5: Implement prefab authoring**

Build temporary prefab roots with exactly one `VisualRoot`, compose primitive meshes as child parts, remove all generated primitive colliders immediately, configure shared materials and Task 2 components, save through `PrefabUtility.SaveAsPrefabAsset`, and destroy temporary objects in `finally` blocks.

- [ ] **Step 6: Implement visual attachment**

`InstantiateVisual` deletes only an existing direct child named `VisualRoot`, instantiates the requested prefab as a direct child, resets local position/rotation/scale, binds its `ProceduralPoseAnimator` motion source to the gameplay root, validates `IsGameplayFree`, and returns its `LowPolyVisual`. It must not remove root Renderer/Collider components; callers decide whether a legacy root Renderer is disabled.

- [ ] **Step 7: Add menu and command-line entrypoints**

Menu: `Almost Delivered/Art/Build low-poly visual set`. Command line calls `CreateOrUpdateAll`, logs `LOW_POLY_ASSETS_BUILT`, saves assets, and exits 0; errors log and exit 1.

- [ ] **Step 8: Run focused tests twice and inspect generated scope**

Expected: all factory tests pass on both runs; `git status --short` lists only intended generated assets/code/meta files; no third-party directory appears.

- [ ] **Step 9: Commit**

```powershell
git add Assets/AlmostDelivered/Editor/Presentation Assets/AlmostDelivered/Editor/CommanderAI/AlmostDelivered.CommanderAI.Editor.asmdef Assets/AlmostDelivered/Tests/EditMode/AlmostDelivered.CommanderAI.EditModeTests.asmdef Assets/AlmostDelivered/Tests/EditMode/LowPolyAssetBuilderTests.cs* Assets/AlmostDelivered/Art/Materials/Generated Assets/AlmostDelivered/Prefabs/Generated
git diff --cached --check
git commit -m "feat: generate low-poly game visual set"
```

### Task 4: Commander Arena Visual Integration and Phase-1 Verification

**Files:**
- Modify: `Assets/AlmostDelivered/Editor/CommanderAI/CommanderAITestSceneBuilder.cs`
- Modify: `Assets/AlmostDelivered/Tests/EditMode/CommanderAITestSceneBuilderTests.cs`
- Create: `Assets/AlmostDelivered/Tests/PlayMode/CommanderVisualIntegrationTests.cs`
- Modify: `Assets/AlmostDelivered/Scenes/CommanderAITest.unity`
- Modify: `README.md`
- Create: `docs/art/low-poly-visual-set.md`

**Interfaces:**
- Consumes: Task 3 `CreateOrUpdateAll`, `InstantiateVisual`, and `VisualArchetype` prefab paths.
- Produces: a regenerated Commander AI arena whose gameplay roots are unchanged and whose actors/objective use project-owned visuals.

- [ ] **Step 1: Write failing arena EditMode assertions**

Extend `Build_CreatesRequiredActorsAnchorsAndConfigs` to assert:

- Player, Dummy A/B, Commander, and all eight members have one direct `VisualRoot`.
- Role-name to `VisualArchetype` mapping is exact for both members of each role.
- Root CharacterController/NavMeshAgent/ActorHealth/Perception/Executor components remain on their existing actors.
- No gameplay or physics component exists beneath any `VisualRoot`.
- `Fixed Objective` owns an `ObjectiveBeacon` visual and a separate `Payload display` child owns a Payload visual without changing `arena.Objective` tactical position.

Extend repeated-build coverage to compare visual prefab/material GUIDs and actor/visual counts across two runs.

- [ ] **Step 2: Run focused EditMode test and confirm RED**

Expected: current primitive-only arena has no `LowPolyVisual` children.

- [ ] **Step 3: Integrate visuals in `CommanderAITestSceneBuilder`**

Call `LowPolyAssetFactory.CreateOrUpdateAll()` before scene creation. After each gameplay primitive is configured, disable its legacy root Renderer and attach the mapped visual. Keep root colliders/controllers/agents unchanged. Add the payload as a non-gameplay display child at the objective; do not add payload movement or health in this plan.

- [ ] **Step 4: Write PlayMode integration tests**

Create tests named:

- `ArenaStart_AllVisualRootsRemainGameplayFree`
- `MemberMovement_AnimatesVisualWithoutChangingNavMeshRootContract`
- `CommanderDeath_VisualsRemainAttachedWhileOrdersBecomeConfused`
- `RepeatedMembers_ShareMaterialInstancesByIdentity`

Use the real generated scene and assert existing AI state transitions in addition to visual invariants.

- [ ] **Step 5: Run focused PlayMode tests and confirm GREEN**

Expected: all new visual integration tests pass and no material instance count grows after multiple frames.

- [ ] **Step 6: Regenerate the arena by command line**

Run `AlmostDelivered.Editor.CommanderAI.CommanderAITestSceneBuilder.BuildFromCommandLine` in batch mode.

Expected: Unity exit 0 and log marker `COMMANDER_ARENA_BUILT Assets/AlmostDelivered/Scenes/CommanderAITest.unity`.

- [ ] **Step 7: Run the complete regression suite**

Run full EditMode and PlayMode suites without `-quit`.

Expected: baseline tests plus all new tests pass; zero failed, skipped, or inconclusive; Unity exits 0.

- [ ] **Step 8: Perform visual acceptance capture**

Open or render the regenerated arena from the fixed orthographic camera. Capture one formation overview and one commander/role close view. Confirm manually: all four roles are distinguishable without labels, Commander remains unique in grayscale silhouette, payload/objective outrank environment markers, and no material is pink. Record any visual-only follow-up rather than changing gameplay scope.

- [ ] **Step 9: Document the asset set**

In `docs/art/low-poly-visual-set.md`, record generated paths, palette, rebuild command, prefab replacement boundary, current procedural-animation limitation, screenshot paths, and third-party exclusions. Update README with the new visual entrypoint.

- [ ] **Step 10: Commit**

```powershell
git add Assets/AlmostDelivered/Editor/CommanderAI/CommanderAITestSceneBuilder.cs Assets/AlmostDelivered/Tests/EditMode/CommanderAITestSceneBuilderTests.cs Assets/AlmostDelivered/Tests/PlayMode/CommanderVisualIntegrationTests.cs Assets/AlmostDelivered/Scenes/CommanderAITest.unity README.md docs/art/low-poly-visual-set.md
git diff --cached --check
git commit -m "feat: apply low-poly visuals to commander arena"
```

## Final Verification

- [ ] Run `git status --short` and prove no ignored third-party source or unrelated user asset is staged.
- [ ] Run `git diff --check` and confirm no whitespace errors.
- [ ] Run full EditMode and PlayMode test suites from current HEAD and read NUnit XML counts.
- [ ] Run the low-poly asset builder and arena builder command-line entrypoints and confirm both log markers and exit 0.
- [ ] Review the complete branch diff against `docs/superpowers/specs/2026-11-02-basic-game-asset-design.md`, explicitly noting phase-2 and phase-3 items as out of scope rather than missing.
- [ ] Ask before any GitHub push; local commits require no additional approval.
