# Startup Flow and Procedural Maps Design

## Intent

Almost Delivered must play as one continuous prototype: the player starts at the frontend, enters the departure hub, walks to the deployment gate, and launches into the combat map. The low-poly models already generated for the project must replace placeholder capsules. After that flow is stable, the combat map must be assembled from theme-specific terrain details and blocking obstacle assets with a new random seed on each run.

## Goals

- Make both editor Play and player builds begin at `Frontend`.
- Preserve the existing `Frontend -> DepartureHub -> MissionPrototype` scene chain.
- Use the generated Player visual in the hub and mission, Commander for the ranged warden, and Charger for the charging boss.
- Keep gameplay colliders and scripts on clean actor roots while visual prefabs remain presentation-only children.
- Generate a different combat layout per mission load while allowing an explicit seed to reproduce a layout.
- Keep player spawn, boss spawns, central travel corridor, and map boundaries clear.
- Create reusable low-poly terrain-detail and obstacle prefabs grouped by a map theme.

## Non-goals

- Mission completion, rewards, extraction, or returning to the hub.
- Network synchronization beyond deterministic seed compatibility.
- Runtime NavMesh baking or sophisticated pathfinding.
- Infinite terrain, streaming chunks, or multiple combat scenes in this iteration.

## Constraints

- Unity `2021.3.45f2` and the existing built-in render pipeline.
- Existing user-created untracked assets under `Assets/Scenes*` and `Assets/temp.mat*` stay untouched.
- No GitHub push without a later explicit request.
- Generated map pieces use primitive low-poly geometry and project-owned materials so the project has no new external asset dependency.
- Random layout calculation must be independent of frame timing and Unity's global random state.

## Approaches considered

1. Keep the three existing scenes and strengthen their handoffs. This is selected because it matches the current code, keeps each scene independently testable, and is appropriate for the portfolio prototype.
2. Add a persistent bootstrap scene. This would help global save/network services later, but adds lifetime management before those services exist.
3. Load the hub and mission additively around persistent objects. This helps seamless transitions, but increases scene ownership and cleanup complexity without improving the current prototype.

For map generation, a deterministic planner is separated from Unity instantiation. A monolithic MonoBehaviour using `Physics.OverlapBox` was rejected because results would depend on scene state and be harder to reproduce in multiplayer. A hand-authored list of random spawn points was also rejected because it would not demonstrate procedural placement or scale to future themes.

## Scene flow

- `Frontend` remains build index zero.
- The editor's Play Mode start scene is set to `Frontend`, so pressing Play from any open scene begins at the menu.
- `작전 준비` loads `DepartureHub`.
- The hub player walks to the deployment gate and presses `E` to load `MissionPrototype`.
- Existing scene names and public flow methods remain stable.

## Visual mapping

- Hub player: `VisualArchetype.Player`.
- Mission player: `VisualArchetype.Player`.
- Warden boss: `VisualArchetype.Commander`.
- Charger boss: `VisualArchetype.Charger`.

Each gameplay actor is a root with movement, health, team, and collision components. `LowPolyAssetFactory.InstantiateVisual` adds exactly one `VisualRoot` child and binds procedural pose animation to the gameplay root. The root itself has no visible primitive renderer.

## Procedural map architecture

`ProceduralMapPlanner` is a pure deterministic layout service. It consumes a seed, map bounds, requested piece count, piece footprints, protected rectangles/circles, and an attempt budget. It returns piece indices, positions, and 90-degree rotations. It rejects candidates outside bounds, inside protected areas, or overlapping earlier blocking placements.

`MissionMapTheme` is a ScriptableObject catalog of terrain-detail and blocking obstacle prefabs plus their footprints. The initial `Industrial Dock` theme contains flat floor plates/hazard markings and blocking cargo crates, barricades, generators, and pipe stacks. Assets live under dedicated map-theme material and prefab folders.

`MissionMapGenerator` owns runtime instantiation. On Start it chooses a fresh seed when randomization is enabled, asks the planner for terrain and obstacle placements, and creates them under a single `Generated Map` root. Tests and future network code can call `Generate(seed)` explicitly to reproduce the same map.

The mission uses a 40 x 32 floor with generation bounds inset from the walls. Protected regions cover the player spawn, both boss spawns, and a central deployment corridor. The first target is 14 blocking obstacles and 24 terrain details; the planner may return fewer only when the attempt budget is exhausted.

## Failure behavior

- A missing/empty theme produces an empty generated root without throwing during play.
- Invalid counts are clamped to zero.
- Invalid footprints are ignored.
- The generator replaces its previous `Generated Map` contents before regenerating.
- Reusing the same seed and theme produces the same placement sequence.

