# Environment Terrain Design

## Intent

The payload mission map should feel like a place rather than a flat test arena. Add deterministic, low-poly hills and grass that fit open parts of the generated map while keeping the payload route, checkpoints, deployment areas, and enemy spawn lanes readable and reliable.

## Goals

- Add low, smooth hills that the player and enemies can walk over.
- Add collider-free grass clusters with a small rendering footprint.
- Place both from the existing seeded map generation so multiplayer peers can reproduce the same layout.
- Keep hills, grass, and blocking obstacles out of payload corridor and protected start/end regions.
- Prevent blocking obstacles and grass from intersecting generated hills.
- Preserve the existing flat terrain-detail and blocking-obstacle APIs.

## Non-goals

- Unity Terrain, terrain deformation, NavMesh baking, foliage animation, or biome streaming.
- Hills on the payload route or vertical payload movement.
- Network transport implementation.

## Constraints

- Unity 2021.3.45f2 and the built-in Standard shader.
- Generated assets remain low-poly and are produced by `MissionMapAssetFactory`.
- Grass has no colliders; hills use static mesh colliders.
- Generation remains deterministic for a given seed.
- Existing user-owned scene and temporary material files are not modified.

## Approaches

1. Treat hills as ordinary blocking obstacles. This is the smallest change, but makes them walls instead of terrain and does not satisfy walkable elevation.
2. Extend the map theme with traversable-terrain and foliage categories. This keeps placement rules explicit, preserves deterministic generation, and lets hills and grass use different collision and exclusion behavior. **Selected.**
3. Replace the arena floor with Unity Terrain. This offers richer landscapes but adds authoring, runtime, navigation, and multiplayer synchronization complexity that is disproportionate to the current prototype.

## Design

`MissionMapTheme` gains `TraversableTerrain` and `Foliage` catalogs while retaining the old two-catalog configuration overload. `MissionMapGenerator` gains separate counts and an extended configuration overload. It generates flat details first, then hills, then blockers, then foliage. Hills, blockers, and foliage avoid protected and payload-corridor exclusions; blockers and foliage additionally avoid hill footprints.

The Industrial Dock asset factory creates two radial low-poly mound meshes and two single-mesh grass-cluster variants. Large hills are approximately 5.5 x 4.5 metres and 1.6 metres high; small mounds are approximately 3.8 x 3.2 metres and 1.1 metres high, keeping their slopes below the player's 45-degree controller limit. Grass clusters use double-sided crossed blades, shared materials, and no colliders.

The mission prototype requests six hills and forty-two grass clusters across its 56 x 100 metre arena. Their placement is seeded with the mission seed, so clients can reproduce it without synchronizing each decoration.

## Verification

- EditMode asset tests verify valid catalogs, hill mesh colliders, useful hill elevation, and collider-free foliage.
- PlayMode generator tests verify deterministic generation, category counts, route exclusions, and cross-category non-overlap.
- Existing full EditMode and PlayMode suites must remain green.
