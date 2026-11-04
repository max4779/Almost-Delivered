# Departure Hub Camera Framing Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Give the departure hub a closer 45-degree action framing with an approximately 11%-screen-height player positioned below center.

**Architecture:** Extend `QuarterViewCamera` with a serialized world-space focus offset while preserving its existing follow behavior. Configure the hub scene builder for orthographic size `9.0` and a `+3.0 Z` focus offset, then keep the persisted scene synchronized with those builder values.

**Tech Stack:** Unity 2021.3.45f2, C#, NUnit, Unity Test Framework

**Spec:** `docs/superpowers/specs/2026-11-05-hub-camera-framing-design.md`

## Global Constraints

- Keep the camera orthographic at a 45-degree downward angle.
- Set hub orthographic size to exactly `9.0`.
- Set hub focus offset to exactly `(0, 0, 3)` while the runtime default remains `(0, 0, 0)`.
- Do not change player scale, movement, UI, combat, or room geometry.
- Do not commit unrelated Unity-regenerated assets or the user's untracked `Assets/Scenes` and `Assets/temp.mat` files.
- Ask the user immediately before any Unity screen control.
- Do not push to GitHub without explicit permission.

## Review Focus

- Persisted-scene drift: opening `DepartureHub.unity` without rebuilding must expose the same `9.0` zoom and `(0,0,3)` focus offset.
- Common aspect ratios: 4:3, 16:9, and 21:9 must keep the player below center with the same vertical size.
- Moving target: after the player moves, camera and focus point must translate by the same delta without composition drift.
- Null target: `SnapToTarget` must leave the camera transform unchanged.
- Future scenes: a newly added `QuarterViewCamera` must default to zero focus offset.

---

### Task 1: Close Action Framing

**Files:**
- Modify: `Assets/AlmostDelivered/Runtime/Frontend/QuarterViewCamera.cs`
- Modify: `Assets/AlmostDelivered/Editor/Frontend/FrontendSceneBuilder.cs:204-225`
- Modify: `Assets/AlmostDelivered/Scenes/DepartureHub.unity`
- Modify: `Assets/AlmostDelivered/Tests/EditMode/FrontendSceneBuilderTests.cs`

**Interfaces:**
- Consumes: existing `QuarterViewCamera.Target`, `Offset`, and `SnapToTarget()`.
- Produces: `QuarterViewCamera.FocusOffset : Vector3`, applied to both camera position and look target.

- [ ] **Step 1: Write failing EditMode tests**

Add tests that:

- open the persisted hub scene and assert orthographic size `9.0` plus focus offset `(0,0,3)`;
- build the hub and assert the two-unit player occupies `0.10` to `0.12` of vertical viewport height and its viewport Y is `0.35` to `0.45` at 4:3, 16:9, and 21:9;
- move a real target, call `SnapToTarget()`, and assert camera translation matches target translation;
- assert a new camera has zero focus offset and a null target does not move its transform.

- [ ] **Step 2: Run the focused tests and verify RED**

Run: Unity Test Runner, EditMode, filter the new hub-camera framing tests.

Expected: FAIL because the hub camera is still size `13.5` and `QuarterViewCamera.FocusOffset` does not exist.

- [ ] **Step 3: Implement the minimal framing behavior**

Add serialized `Vector3 focusOffset = Vector3.zero` and public `FocusOffset` accessors. In `SnapToTarget()`, compute `focusPoint = target.position + focusOffset`, place the camera at `focusPoint + offset`, and look at `focusPoint`. Configure the builder camera with size `9.0` and focus offset `(0,0,3)`.

- [ ] **Step 4: Run focused tests and verify GREEN**

Run: Unity Test Runner, EditMode, filter the new hub-camera framing tests.

Expected: all filtered tests PASS.

- [ ] **Step 5: Synchronize the persisted scene with minimal YAML changes**

Update only the camera size, serialized focus offset, and camera transform values required to match builder output; do not retain regenerated object-ID churn.

- [ ] **Step 6: Run full verification**

Run: Unity Test Runner EditMode and PlayMode suites, followed by `git diff --check`.

Expected: EditMode and PlayMode have zero failures; diff check exits `0`; only planned tracked files and plan/spec documents differ.

- [ ] **Step 7: Perform the approved visual check and commit**

Ask immediately before Unity screen control, then verify the player is visibly larger, below center, and the room ahead dominates the frame. Commit only the planned files with message `fix: tighten departure hub camera framing` and do not push.
