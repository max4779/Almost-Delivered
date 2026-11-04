# Departure Hub Camera Framing Design

## Intent

Make the departure hub feel closer and more readable, with character presence comparable to the middle ground between *Eternal Return* and *The Spell Brigade*, while preserving enough surrounding space for tactical navigation.

## Goals

- Keep the approved 45-degree orthographic quarter view.
- Reduce the hub camera orthographic size from `13.5` to `9.0` so the 2-unit-tall player occupies roughly 11% of screen height.
- Frame the player below screen center by looking 3 world units farther into the room on positive Z.
- Preserve player following, movement controls, scene flow, and the existing hub layout.

## Non-goals

- Do not switch to a perspective camera.
- Do not change player scale, movement speed, UI, combat, or room geometry.
- Do not add runtime zoom controls in this pass.

## Constraints

- Unity `2021.3.45f2` and the existing built-in rendering/runtime APIs only.
- The camera remains orthographic and follows the player every `LateUpdate`.
- The framing offset is serialized and configurable; the runtime default remains zero so other future scenes are not shifted implicitly.
- Persisted `DepartureHub.unity` values must match the scene builder output without committing unrelated regenerated scene churn.

## Approaches

1. **Hybrid framing (selected):** 45-degree pitch, orthographic size `9.0`, and a `+3.0 Z` framing offset. This balances character presence with tactical visibility and avoids centering the near-wall spawn over empty space.
2. **Eternal Return leaning:** about 50 degrees with orthographic size `9.5`. This improves tactical visibility but makes the character smaller and returns toward the view the user found too high.
3. **The Spell Brigade leaning:** about 42 degrees with orthographic size `8.0` to `8.5`. This increases spectacle but reduces the space available for commander-AI and horde awareness.

## Verification

- An EditMode integration test builds the real hub scene and verifies a 45-degree camera, orthographic size `9.0`, approximately 11% player screen height, and a player viewport Y below center.
- Run the complete EditMode and PlayMode suites.
- Perform a final Unity Game-view check only after asking the user immediately before screen control.
