# Payload Escort Mission Design

## Intent

Build the first complete mission type for Almost Delivered: the squad teleports into a procedurally assembled battlefield beside a payload, escorts it along a generated route, clears several checkpoint objectives, and wins when the payload survives the final defense. The mission should establish a reusable 20–30 minute structure without coupling the prototype to a networking package before multiplayer transport is selected.

## Delivery order

1. **Phase 1 — state-driven vertical slice:** generated route, payload escort rules, checkpoint waves, more regular enemies, world health bars, minimap, and a server-authority-ready state boundary.
2. **Phase 2 — commander AI integration:** replace simple wave tactics with commander-led squads that select formations and reactions around the payload and checkpoints.
3. **Phase 3 — presentation pass:** richer teleport arrival, payload start/stop feedback, checkpoint announcements, route dressing, audio hooks, and mission-end presentation.

This implementation cycle covers Phase 1. Phases 2 and 3 must extend the Phase 1 contracts instead of replacing them.

## Goals

- Start the mission with the player and payload arriving together at the generated route origin.
- Generate the payload route from the same deterministic mission seed as the map layout.
- Move the payload only while at least one living player is in escort range and no living enemy is contesting that range.
- Stop at three intermediate checkpoints and create a combat wave at each stop.
- Run a final defense at the destination, then complete the mission.
- Target a normal-play duration of 20–30 minutes while keeping automated tests and developer iteration fast.
- Increase enemy presence without allowing unlimited simultaneous actors.
- Show a readable world-space health bar above every regular enemy and boss.
- Generate a minimap from mission data rather than a second render camera.
- Keep the authoritative mission simulation deterministic and suitable for future multiplayer replication.

## Non-goals for Phase 1

- No real online connection, lobby, matchmaking, client prediction, rollback, or network transport dependency.
- No commander AI control of the new regular enemies yet; that is Phase 2.
- No final-quality VFX, sound, cinematic camera, or polished mission result screen; that is Phase 3.
- No infinite maps, NavMesh rebaking, or arbitrary branching payload paths.
- No long-term rewards or return-to-base reward flow.

## Constraints

- Unity `2021.3.45f2` and the built-in render pipeline remain the target.
- Reuse generated low-poly Player, Payload, Ranged, Shield, Charger, Commander, and ObjectiveBeacon visuals.
- Reuse the existing deterministic `ProceduralMapPlanner` and Industrial Dock asset catalog.
- The user-owned untracked `Assets/Scenes*` and `Assets/temp.mat*` files remain untouched.
- No GitHub push without a later explicit request.
- Screen control requires a separate user confirmation before visual verification.

## Approaches considered

### 1. State-driven vertical slice — selected

A pure mission state machine owns route progress, checkpoint state, timer state, and completion. Unity-facing components provide presence counts, spawning, transforms, UI, and presentation. This is the best first step because it gives deterministic tests and a clean future network authority boundary.

### 2. Commander AI integration immediately

Wave enemies would join commander squads from the first implementation. This creates richer combat, but combines two large debugging surfaces: mission progression and tactical coordination. It is deferred to Phase 2 and will consume the wave-spawn and mission-state events created in Phase 1.

### 3. Presentation-first scripted mission

A timeline could fake arrival, movement, stops, and enemy waves quickly. It would look convincing but would have to be replaced for multiplayer and procedural routes. It is deferred to Phase 3, where presentation will subscribe to real Phase 1 state transitions.

## Mission flow

The authoritative state sequence is:

`Deploying -> Escorting -> CheckpointDefense -> Escorting` repeated for three checkpoints, then `FinalDefense -> Completed`.

`Failed` is reserved for a later payload-destruction or team-wipe rule. Phase 1 exposes the state but does not add a punitive failure timer.

### Deployment

- The mission seed generates the route and map before actors become active.
- Player spawn and payload spawn are placed at the route origin.
- A short configurable deployment lock represents teleport arrival. Default normal duration: 8 seconds. Test configuration may use zero.
- The payload does not move during deployment.

### Escorting

- Escort radius: 4.5 metres.
- At least one living `Player` team actor inside the radius enables forward movement.
- Any living `Enemy` team actor inside the radius contests and stops movement.
- Extra players do not increase speed in Phase 1.
- The authoritative state machine advances a normalized route distance; the Unity payload view samples the generated polyline from that distance.
- Payload movement and checkpoint transitions occur only on the authority instance.

### Checkpoint and final defenses

- Checkpoints are placed at 25%, 50%, and 75% of the route length.
- Reaching a checkpoint stops the payload and requests one enemy wave.
- The state returns to escort only after every enemy owned by that wave is dead or removed.
- Reaching the route end requests a final wave that includes the two existing boss archetypes.
- Clearing the final wave changes the state to `Completed`.

## Duration model

The normal configuration targets 20–30 minutes through encounter pacing rather than an artificially slow cart:

- Generated route length target: 120–160 metres.
- Payload movement speed: 0.35 metres per second, producing roughly 6–8 minutes of uninterrupted travel.
- Three checkpoint waves target 3–4 minutes each.
- Final defense targets 3–5 minutes.
- Contests, repositioning, and imperfect escort uptime provide the remaining variation.

All durations, speeds, route size, and wave counts are configuration data. Tests call the pure state machine directly with accelerated values; they never wait real minutes.

## Route and map generation

`PayloadRoutePlanner` is a pure deterministic service. It consumes a seed and rectangular mission bounds and returns:

- a polyline from the south deployment edge to the north destination edge;
- cumulative segment distances;
- three checkpoint samples;
- a destination sample;
- rectangular route-corridor exclusion zones.

The initial route uses seven ordered control points. The first and last points are fixed near opposing map edges; five interior points receive seeded lateral offsets with limits that prevent self-crossing and segments that are too sharp.

`MissionMapGenerator` accepts the route corridor as additional exclusions before placing blocking obstacles. Terrain markings may overlap the route, but blocking obstacles may not. Enemy spawn anchors are derived from checkpoint positions with lateral offsets outside the escort radius.

The mission floor expands from the compact arena to an Industrial Dock rectangle large enough for the route. World boundaries, generation bounds, camera follow, and minimap bounds share one mission-bounds value.

## Payload runtime

`PayloadMissionStateMachine` is pure C# and owns:

- mission phase;
- elapsed mission time;
- normalized and absolute route progress;
- next checkpoint index;
- active wave identifier and remaining enemy count;
- the conditions that permit movement;
- an immutable snapshot for clients and UI.

`PayloadMissionController` is the Unity authority adapter. It gathers presence information, ticks the state machine, moves the payload view, and emits transition events. It supports two modes:

- authority: advances state and emits snapshots;
- replica: never advances locally and only applies snapshots.

No networking SDK is added. A future adapter can serialize the snapshot and route seed through Netcode, Mirror, FishNet, or a custom transport.

The payload uses the existing `VisualArchetype.Payload` visual on a collider-bearing gameplay root. The Phase 1 payload is not destructible, so its health is not part of win or failure conditions yet.

## Enemy waves

Phase 1 adds regular mission enemies using a small `MissionMobController` rather than treating every unit as a boss.

- Melee grunt: Charger visual, 60 health, closes distance and deals contact damage.
- Ranged unit: Ranged visual, 45 health, stops at range and fires existing projectiles.
- Shield unit: Shield visual, 90 health, slower melee pressure.
- Final wave: regular units plus the existing Commander and Charger bosses.

Default total spawns are 10, 14, 18, and 22 for the three checkpoints and final defense. The active actor cap is 18; larger waves spawn in batches as enemies die. Spawn positions come from deterministic checkpoint anchors, and every spawned enemy receives a wave identifier so unrelated actors cannot block progression.

Phase 2 will replace simple target selection and formation-free movement with commander squad membership while keeping the same wave identifiers, spawn requests, and death reporting.

## Health bars

`WorldHealthBar` binds to `ActorHealth` and uses a small world-space canvas with background and fill images.

- Every regular enemy and boss receives one health bar.
- The fill is `CurrentHealth / MaxHealth` and updates without polling private fields.
- The bar faces the active camera, clamps its scale for the orthographic view, and hides on death.
- Player health remains in the HUD; payload health is omitted in Phase 1.

`ActorHealth` gains a non-breaking `HealthChanged(current, maximum)` event so UI does not poll every frame. Existing `Died` behavior remains intact.

## Minimap

`MissionMinimap` is a generated UI panel in the upper-right HUD. It projects world X/Z positions into a fixed `RectTransform` using the shared mission bounds.

It displays:

- the generated route polyline;
- checkpoint and destination markers;
- player marker(s);
- payload marker;
- living enemy markers;
- current payload progress.

It does not use a second camera or RenderTexture. The same deterministic route data used by the simulation creates the visual route, so authority and replica clients see the same layout.

## Multiplayer boundary

Phase 1 remains offline but follows these rules:

- Only `PayloadMissionController` in authority mode may tick mission state, request waves, or complete checkpoints.
- Route generation is determined by a shared integer seed.
- UI and presentation subscribe to immutable mission snapshots and transition events.
- Enemy death is reported by stable wave-local identifiers rather than scene searches.
- Player and enemy presence is supplied as counts/IDs to the state machine, not discovered inside pure logic.
- Scene builders wire an offline authority adapter; a future network adapter replaces only that boundary.

## Scene and UI integration

- `MissionPrototypeSceneBuilder` becomes a payload mission builder rather than a static boss arena.
- It builds the expanded floor and boundaries, configures deterministic mission generation, creates the payload root, spawn anchors, mission controller, wave spawner, minimap, and updated HUD.
- The existing player movement and ranged attack remain unchanged.
- The HUD adds phase text, payload progress, escort/contested status, checkpoint count, and elapsed time.
- The existing two bosses are spawned by the final wave instead of existing from mission start.

## Failure behavior

- Missing or invalid route configuration produces a zero-length route and keeps the payload stationary without throwing.
- A replica controller cannot advance progress or spawn waves.
- Empty waves complete on the next authority tick rather than deadlocking.
- Destroyed or disabled enemies report removal once; duplicate reports are ignored.
- Missing minimap targets remove their icons without breaking the mission.
- Invalid actor health changes remain rejected using the existing validation behavior.

## Verification

- EditMode tests cover deterministic route generation, bounds, checkpoints, corridor exclusions, distance sampling, and minimap projection.
- Pure state tests cover escort, no-player stop, enemy contest, checkpoint stop, wave clear, final completion, empty waves, duplicate death reports, and replica immutability.
- PlayMode tests cover payload transform movement, deterministic wave spawns, active cap, enemy death progression, health bar fill/hide behavior, and the generated mission scene contract.
- Existing EditMode and PlayMode suites must remain green.
- Visual verification is deferred until the user separately permits screen control.

## Builder self-check

The design keeps the mission state, route generation, Unity presentation, and future networking boundary separate. The main implementation risk is scope: route generation, waves, minimap, and UI are several connected systems. They are still one coherent Phase 1 because each consumes the same route/state contracts and none is independently useful as the requested mission without the others. Tests use accelerated values so the 20–30 minute target does not slow verification.
