# Continuous Escort Combat and Supply Relay Beacons

## Intent

Make the payload mission fun during movement, not only at checkpoints. Players must stay near the payload and fight continuously while it travels. The field should not reveal the complete route. Instead, the minimap shows the route and the world clearly communicates only the next destination through a supply relay beacon.

The payload's story is that it is an automated delivery vehicle collecting additional supplies, fuel, and replacement parts from relay caches before making its final delivery. Reaching a checkpoint therefore has a visible purpose: the vehicle stops to load cargo while the team defends it.

## Goals

- Keep combat active throughout every escort segment.
- Make staying near the payload the center of play.
- Remove the complete world-space route line while retaining the complete minimap route.
- Make the next checkpoint readable from a distance and explain why the payload travels there.
- Make checkpoint defense feel different from movement combat.
- Preserve deterministic mission generation and authority-first multiplayer boundaries.
- Fix the minimap pivot bug that moves route graphics outside the panel.

## Non-goals

- Implement production networking or matchmaking in this change.
- Complete the long-term progression or trait-selection system.
- Replace all enemy combat AI or finish the full commander-AI integration.
- Produce final-quality art, VFX, sound, or narrative dialogue.

## Anti-goals

- Long enemy-free walking sections.
- A constant undifferentiated stream of enemies with no breathing rhythm.
- A glowing line across the entire field that makes exploration and navigation trivial.
- Checkpoints that are only small ground markers or unexplained combat arenas.
- Spawning enemies directly in the camera view or on top of players.

## Approaches Considered

### 1. Full world-space route line

Keep the current route spline visible and enlarge checkpoint markers. This is the cheapest implementation, but it exposes the complete route and makes the environment feel like a debug prototype. Rejected because it conflicts with the desired field presentation.

### 2. Supply relay beacon network — selected

Hide the world-space route spline. Show the complete route only on the minimap. In the field, activate only the next supply relay with a vertical light column, pulsing base ring, floating objective marker, and a directional signal on the payload. At arrival, stored supplies visibly transfer onto the payload. This gives the route and checkpoint stops an understandable fictional purpose without drawing the whole path.

### 3. Guide drone

Have a drone fly ahead and lead the payload. This provides strong local navigation but requires reliable path-following, obstruction handling, and additional animation. It also explains where to go less clearly at long range. Deferred as a possible later polish layer.

## Mission Flow

1. Deployment finishes and the first relay beacon activates.
2. The payload begins moving only while at least one living player is inside its escort radius and no contesting enemy is inside that radius.
3. An escort director generates small assault groups throughout travel.
4. The director chooses a readable entry pattern: forward blockade, side ambush, or rear pursuit.
5. Players fight while maintaining proximity to the payload. Leaving the radius stops the vehicle but does not instantly remove the current threat.
6. On reaching the relay, the payload stops and begins a visible loading operation.
7. A larger checkpoint defense starts. The team must finish the defense and loading operation before travel resumes.
8. The completed relay dims, the next relay activates, and the payload points toward it.
9. The final beacon represents the delivery destination rather than another supply cache.

## Escort Combat Director

Add an authority-only escort director separate from the mission state machine. The state machine remains the authoritative source of mission phase and payload progress; the director decides when and where travel encounters are requested.

Initial prototype tuning:

- First travel assault begins 5–8 seconds after deployment.
- Travel groups contain 3–6 enemies.
- The next group is considered after a 12–20 second pressure interval.
- Travel encounters have an active cap of 12 enemies; a new group is not requested if it would exceed the cap.
- The director pauses new travel requests during checkpoint and final defense phases.
- Existing travel enemies remain valid when a checkpoint begins; they join the checkpoint pressure rather than disappearing.
- Spawn positions are derived from the current route tangent and payload position, stay within mission bounds, avoid the escort radius, and must be outside the camera view when possible.
- Deterministic random selection uses the mission seed plus an encounter sequence number.

The first implementation reuses the existing enemy archetypes and wave-spawn infrastructure. A commander archetype may appear in a travel group, but deeper squad command behavior remains a later integration step.

## Payload Proximity

- Keep a clearly readable escort radius around the payload using a restrained local ring, vehicle lights, or both; this indicator must not resemble the removed route line.
- At least one living player in range enables payload movement.
- A living enemy in range contests and stops movement.
- HUD feedback distinguishes `MOVING`, `WAITING FOR ESCORT`, `CONTESTED`, and `LOADING SUPPLIES`.
- Multiplayer movement speed does not scale with player count in this prototype, avoiding a solo/co-op balance split before real networking exists.

## Relay Beacon Presentation

Each checkpoint becomes a supply relay installation with:

- a physical cache platform containing crates, fuel cells, or replacement components;
- a vertical emissive light column readable above terrain and obstacles;
- a pulsing ground ring localized to the checkpoint;
- a floating checkpoint number and distance indicator;
- three visual states: future/offline, active/amber-cyan pulse, completed/steady green;
- an arrival state in which the signal locks onto the payload and the cache opens;
- a loading state in which supply props move, appear, or attach to cargo sockets on the payload;
- a completion state in which the cache is visibly depleted and the payload carries more cargo.

The payload gets a small antenna or forward lamp that rotates toward the active relay. This provides local direction even when the beacon is temporarily hidden by geometry.

## Route and Minimap Presentation

- Disable the world-space `PayloadRouteView` line renderer in the generated mission scene.
- Replace its tiny world checkpoint markers with relay beacon views.
- Keep the complete route, all checkpoints, player, payload, and currently visible enemies on the minimap.
- Highlight the current route leg and active checkpoint more strongly than completed or future legs.
- Correct minimap child anchoring so projected route graphics remain inside panels with a top-right pivot.
- Add a regression test that evaluates actual child local positions against the real top-right-pivot panel rectangle.

## State and Data Boundaries

- `PayloadMissionStateMachine` continues to own deployment, escort, checkpoint defense, final defense, and completion.
- `PayloadEscortDirector` observes authoritative snapshots and emits deterministic travel encounter requests.
- `PayloadWaveSpawner` gains a travel-group request path without allowing overlapping checkpoint wave registration.
- `PayloadRelayBeaconView` observes checkpoint index and mission phase to update presentation; it does not advance gameplay state.
- The scene builder generates relay objects from route checkpoint distances and wires the active-beacon presentation.
- All gameplay-changing decisions remain authority-only so later networking can replicate snapshots and spawn results.

## Failure Handling

- If no valid off-camera spawn position is found, use the farthest valid in-bounds candidate and delay the spawn rather than placing enemies inside the escort radius.
- If a relay visual is missing, mission progression still works and the HUD/minimap remain usable.
- If no wave spawner is configured, the director issues no requests and does not throw.
- Reconfiguration or scene teardown unbinds director, spawner, and beacon events to prevent duplicate encounters.

## Verification

- EditMode tests cover deterministic director timing, group size, pressure cap, phase pausing, and encounter-pattern selection.
- PlayMode tests cover continuous travel spawns, no spawn inside the escort radius, payload contest behavior, relay visual-state transitions, and loading completion.
- Minimap tests cover the real top-right pivot and route containment.
- Scene-builder tests verify the absence of the world route line and the presence of one relay beacon per checkpoint plus the final destination.
- The accelerated scene-level escort test verifies travel enemies during `Escorting`, minimap-only route visibility, relay loading, and the first collected-cargo prop.
- Full verification on 2026-11-07 passed 221/221 EditMode tests and 179/179 PlayMode tests with zero failures or skipped tests.
- Final relay art, animation timing, audio feedback, and full commander-AI squad integration remain future work.

## Acceptance Criteria

- A player who escorts normally encounters enemies before the first checkpoint.
- No normal escort interval contains the current roughly 98-second enemy-free opening.
- The payload moves only while escorted and uncontested.
- The full route is visible on the minimap but not drawn across the world.
- The next relay is visible and understandable from the field without opening the minimap.
- Arrival visibly loads supplies onto the payload and explains the checkpoint defense.
- Completed relays and collected cargo visibly communicate mission progress.
