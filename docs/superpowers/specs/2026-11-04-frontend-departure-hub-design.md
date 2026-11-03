# Frontend and Departure Hub Design

## Intent

Build the first player-facing flow before production characters are introduced: a basic pre-game menu followed by a walkable mission-preparation room inspired by the spatial preparation loops of Escape from Duckov and Deep Rock Galactic.

## Goals

- Launch into a dedicated frontend scene with the game title and a clear `작전 준비` action.
- Load a separate departure-hub scene after starting.
- Let a placeholder capsule walk with WASD while a fixed-angle quarter-view camera follows it.
- Make the hub read as an industrial underground operations bunker with a deployment gate, mission terminal, equipment bay, storage, and a central staging floor.
- Generate both scenes deterministically from an editor command so the prototype is reproducible and easy to revise while viewing it in Unity.

## Non-goals

- No production character models, combat, inventory, matchmaking, mission selection, loadout editing, shops, or deployment transition.
- The stations and deployment gate are visual placeholders only.
- No third-party art is added or committed.

## Selected approach

### Deterministic scene builder — selected

Create focused runtime components and an editor builder that generates the menu and hub scenes from project-owned primitives, shared materials, and uGUI. This fits the existing Commander AI scene-builder pattern, makes review and tests reliable, and still allows immediate visual inspection in the open Unity editor.

### Hand-authored scenes

Faster for a one-off composition pass, but harder to review, reproduce, and keep stable while the layout changes.

### One scene with a menu overlay

Cheaper initially, but couples frontend state to the hub and makes later boot/loading/session work harder. Rejected in favor of explicit scene boundaries.

## Player flow

1. `Frontend.unity` opens first.
2. The screen shows `ALMOST DELIVERED`, a short mission-preparation subtitle, `작전 준비`, disabled `설정 (준비 중)`, and `종료`.
3. `작전 준비` loads `DepartureHub.unity`.
4. The player walks around the hub with WASD. A quarter-view camera follows without rotation controls.
5. `Escape` returns to the frontend.

## Hub layout

- Approximate footprint: 32 m × 22 m.
- South: player spawn and crew access door.
- North: large sealed deployment gate with hazard markings.
- West: mission terminal and briefing wall.
- East: equipment/loadout bay placeholder.
- Center: open staging floor with painted lanes and a small cargo pallet.
- Corners: crates, pipes, barriers, work lights, and signage to prevent the room feeling like an empty box.

## Visual direction

- Low-poly industrial forms built from primitives.
- Dark blue-gray structure, warm amber work lights, cyan terminal accents, and yellow-black deployment markings.
- Menu uses the same palette so frontend and hub feel like one game.
- Layout and labels remain readable at 16:9 and tolerate other aspect ratios through anchors and layout components.

## Constraints

- Unity 2021.3.45f2 and the legacy Input Manager already used by the project.
- Work under `Assets/AlmostDelivered`; preserve user-owned `Assets/Scenes*` and `Assets/temp.mat*`.
- Keep gameplay code on roots and presentation-only objects below dedicated visual/layout parents.
- Local commits are allowed; GitHub push remains approval-gated.

## Acceptance

- Both scenes are in Editor Build Settings, with Frontend first.
- Starting from Frontend and pressing `작전 준비` loads the hub.
- The placeholder player moves and collides with room walls.
- The camera follows while preserving its fixed quarter-view angle.
- Escape returns to the frontend.
- Rebuilding does not duplicate required roots or build-settings entries.
- Existing EditMode and PlayMode suites remain green.
