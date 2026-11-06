# Screen, Health Bar, and Natural Road Design

## Goal

Remove the apparent top/left crop in the Unity preview, make enemy health loss visibly continuous, and turn the payload route into a natural part of the environment without restoring a glowing world-space route line.

## Decisions

- The preview crop is an editor viewing issue, not a shipped camera or HUD layout bug. The Unity Game view was zoomed to `1.1x`; resetting it to `1x` shows the full top and left HUD margins. No runtime camera change is required.
- `WorldHealthBar` keeps its event-driven health updates, but the visible fill rectangle scales horizontally from a left-edge pivot. This does not depend on an `Image` sprite supporting `Image.Type.Filled`.
- A new `PayloadRoadView` builds one deterministic procedural mesh from the same `PayloadRoutePlan` used by the mission.
- The road is a 5.5 metre earth ribbon with smoothly varying width and centre offset. Moss shoulder strips soften both edges.
- Grass patch prefabs are placed sparsely and deterministically along alternating road shoulders. They are decoration only and have no colliders.
- The payload route remains absent as a glowing line in the field. The minimap continues to show the navigation route.
- The route exclusion corridor grows to 8 metres in the mission scene so blocking obstacles and random foliage do not overlap the road and its shoulders.

## Structure

- `PayloadRoadView` owns generated road mesh data and optional roadside foliage instances.
- `MissionPrototypeSceneBuilder` supplies route, seed, road materials, and existing foliage prefabs, and saves the generated result into `MissionPrototype.unity`.
- Existing route planning, payload movement, minimap projection, and multiplayer authority remain unchanged.

## Verification

- A health-bar PlayMode test compares the visible fill geometry before and after damage and checks that it shrinks from the right.
- EditMode tests validate deterministic road geometry, material submeshes, width bounds, sparse foliage, and safe null/short routes.
- The mission scene builder test verifies the road component, materials, route corridor, and continued absence of `PayloadRouteView`.
- Full EditMode and PlayMode suites must pass after regenerating the mission scene.

