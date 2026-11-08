# Mob Death Presentation and Environment Themes

## Intent

Improve combat readability by removing visually static defeated enemies, and make the mission ground feel cohesive while preserving a readable payload road.

## Decisions

- Defeated mobs immediately lose combat, collision, and wave-tracking authority.
- Their visual model falls backward, sinks into the ground, and is destroyed after roughly one second.
- Environment colors are supplied by a selectable `MissionEnvironmentTheme` asset.
- Each theme independently selects the whole-map ground, road center, and road shoulder materials.
- Each environment theme also selects its map-piece catalog so obstacles and foliage match the palette.
- The default prototype theme is grassland: grass ground, earth road, moss shoulder.
- The grassland catalog adds large and small trees, fallen logs, boulder clusters, thickets, bushes, and grass.
- The existing industrial catalog and prefabs remain available as a separate option.
- No square terrain-detail tiles are restored.

## Implementation

1. Add deterministic death-presentation state to `MissionMobController` and cover it with PlayMode tests.
2. Add environment theme and applier runtime types.
3. Generate a grassland material palette and natural obstacle catalog from the map asset factory.
4. Build the mission floor, road, and generated map from the selected theme and serialize the selection into the mission scene.
5. Run targeted tests, rebuild the generated scene, then run the complete EditMode and PlayMode suites.
6. Copy verified changed files into the original Unity project without committing or pushing again.

## Alternatives considered

- Immediate removal: clearest and cheapest, but weakens impact.
- Shrink or particle disappearance: readable, but too arcade-like for the current tone.
- Ragdoll: expressive, but heavier and less deterministic than the low-poly presentation needs.
- Hard-coded floor colors: quick, but blocks future wasteland, snow, and industrial palettes.
