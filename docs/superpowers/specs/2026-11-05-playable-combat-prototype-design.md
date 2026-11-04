# Playable Combat Prototype Design

## Intent

Turn the existing frontend and departure hub into a small end-to-end playable slice. The player starts at the frontend, enters the hub, walks to the deployment gate, launches into one combat map, moves with WASD, aims with the mouse, fires a ranged basic attack, and fights two visually and mechanically distinct bosses. The slice is a foundation for later melee attacks and full commander-AI encounters, not a finished mission.

## Goals

- Preserve the existing `Frontend -> DepartureHub` flow and add `DepartureHub -> MissionPrototype` through the deployment gate.
- Keep the existing 45-degree orthographic quarter-view camera and close framing.
- Make the player fully visible at hub spawn by using a cutaway south side instead of rendering a wall between the camera and player.
- Give the mission player deterministic WASD movement, mouse-ground aiming, and left-click ranged fire.
- Build ranged combat as a reusable module so a melee module can be added later without rewriting movement or health.
- Add one industrial combat map with cover landmarks, a clear floor boundary, a player spawn, and two bosses.
- Add one ranged boss and one pursuing contact boss, both damageable and killable through the same `ActorHealth` contract.
- Show a minimal always-visible control/health HUD so the prototype is understandable without external instructions.
- Keep scenes reproducible through editor builders and register all gameplay scenes in build settings.

## Non-goals

- Multiplayer networking, matchmaking, replication, or authoritative combat.
- Final art, animation, audio, VFX, loot, traits, progression, boss phases, win rewards, or mission selection.
- Melee attacks in this slice; the architecture only preserves a clean place to add them.
- Full commander-AI squads in the combat scene. The two bosses use lightweight prototype behavior and do not replace the commander system.
- A complete victory/defeat loop. Bosses can be defeated, player health is visible, and Escape returns to the hub; mission rewards and restart UI come later.

## Constraints

- Unity 2021.3.45f2 and the legacy `Input` API already used by the project.
- No new packages or downloaded assets.
- Reuse `ActorHealth`, existing generated low-poly visuals, `QuarterViewCamera`, and existing scene-builder patterns.
- Preserve user-owned untracked `Assets/Scenes*` and `Assets/temp.mat*` files.
- All new behavior is introduced test-first and existing EditMode/PlayMode suites must stay green.
- GitHub push requires separate user approval.

## Approaches Considered

### 1. New `MissionPrototype` scene — selected

Create a clean runtime combat layer and a dedicated editor scene builder, then connect it to the hub gate. This keeps production gameplay separate from the commander debug arena, gives later melee and mission work stable boundaries, and leaves the existing tests/debug tools intact.

### 2. Convert `CommanderAITest`

This would reuse more enemies immediately, but that scene is intentionally deterministic and carries debug controls, stress-test assumptions, three observation targets, and broad AI instrumentation. Turning it into production gameplay would couple the first playable loop to test infrastructure.

### 3. Add combat to `DepartureHub`

This is the smallest scene count but mixes preparation and combat roles, undermines the Deep Rock/Duckov-style staging-room intent, and makes later mission loading and cleanup harder.

## Scene Flow

1. `Frontend`: the existing Start Operation button loads `DepartureHub`.
2. `DepartureHub`: the player moves with WASD. Entering the deployment-gate trigger displays `E 출발`; pressing E loads `MissionPrototype`.
3. `MissionPrototype`: the player fights both bosses. Escape returns to `DepartureHub`.

`GameSceneFlow` owns all three declared names and rejects undeclared names. The frontend builder registers the three production scenes first, while retaining unrelated existing build-setting entries after them.

## Hub Visibility Fix

The current player spawn is at `(0, 1, -7.2)` while the camera is behind the south wall. After the camera focus was moved three metres into the room, the sight line from camera to player crosses the tall south-wall visual, which hides most of the character.

The hub becomes a deliberate cutaway room:

- Keep the south collision boundary so the player cannot leave the staging area.
- Do not render the tall south-wall visual.
- Extend the floor visual far enough south to fill the camera frame and remove the current grey outside strip.
- Keep all other walls and the existing camera values unchanged.

This fixes the root cause without moving the player away from the departure side or changing the approved camera scale.

## Player Runtime Structure

### `MissionPlayerController`

- Requires `CharacterController`, `ActorHealth`, and `CombatTeam`.
- Reads horizontal/vertical axes and moves on the XZ plane at a serialized speed.
- Clamps diagonal input and rotates toward non-zero movement.
- Reads mouse position through the main camera, intersects a horizontal ground plane, and supplies that world point to the ranged attack.
- Fires while left mouse is pressed, subject to the weapon cooldown.
- Escape loads `DepartureHub`.
- Exposes explicit `Move(Vector2, float)` and `AimAndFire(Vector3, float)` entry points so real behavior can be tested without synthesizing OS input.

### `PlayerRangedAttack`

- Owns damage, cooldown, projectile speed, projectile lifetime, muzzle offset, and projectile prefab reference.
- `TryFire(Vector3 worldTarget, float now)` rejects non-finite targets, zero-length aim, dead owners, and cooldown attempts.
- Spawns one projectile, sets the owner team, and rotates the player toward the planar aim direction.
- Returns whether a shot was created; this will let a future loadout/controller select ranged or melee modules without embedding attacks in movement code.

### `CombatProjectile`

- Moves every frame in a normalized direction at configured speed.
- Ignores triggers, the firing root, and same-team combatants.
- Applies damage once to the first opposing `ActorHealth`, then destroys itself.
- Destroys itself at its configured lifetime or when it hits solid world geometry.

### `CombatTeam`

- Declares `Player` or `Enemy` on a combatant root.
- Provides the minimal friendly-fire rule shared by player and boss projectiles.

## Bosses

Both bosses use `ActorHealth`, `CombatTeam.Enemy`, a collider/character controller, and existing low-poly generated visuals.

### Ranged Warden

- Visual archetype: Commander.
- Higher maximum health, slower rotation, stationary for this slice.
- Faces the player and fires slower enemy projectiles on a visible cadence.
- Projectiles use the same projectile component and team filtering as the player.

### Iron Charger

- Visual archetype: Charger.
- Moves toward the living player with a `CharacterController`.
- Stops within contact range and applies contact damage on a cooldown.
- Never moves or attacks after its health reaches zero.

`PrototypeBossController` owns the two behavior modes through a serialized enum. This is intentionally smaller than commander AI; replacing it later does not change projectile, health, team, player, or scene-flow contracts.

On death, each boss disables its controller/collider, plays the existing procedural hit/death-compatible visual response where available, and removes its gameplay object after a short delay. No loot or phase transition is included.

## Mission Map

The scene is a medium rectangular industrial delivery yard with:

- A large walkable floor and four collision boundaries.
- Player spawn near the south side with the camera focused several metres ahead.
- Two separated boss pads in the northern half so both attacks are readable.
- Low cover/crate landmarks that establish scale but do not require a navigation bake.
- Lighting and materials aligned with the cyan/amber/dark hub palette.
- A screen-space HUD displaying controls, player HP, and remaining bosses.

The player and Charger use `CharacterController`, so this first map does not require NavMesh data. The Warden is stationary.

## Camera

The mission uses `QuarterViewCamera` with the same approved 45-degree pitch, `(0, 16, -16)` distance, `(0, 0, 3)` focus offset, and orthographic size `9`. The target remains the mission player, so movement preserves the same framing as the hub.

## Failure and Edge Behavior

- Dead player input cannot move or fire; enemy attacks stop selecting a dead player.
- Repeated fire input cannot bypass cooldown.
- Projectiles cannot damage their owner or allies and cannot damage the same target twice.
- Missing/null targets cause bosses to idle rather than throw exceptions.
- Scene builders remain repeatable and leave exactly one required root/player/camera/boss pair.
- The hub player remains visible from the persisted spawn composition after rebuilding scenes.

## Testing and Verification

- EditMode contract tests for declared scene names, team filtering, firing validation/cooldown, projectile configuration, scene roots, build settings, hub cutaway geometry, boss modes, HUD, and camera wiring.
- PlayMode tests for mission movement, one-shot projectile damage, friendly-fire rejection, projectile lifetime, Warden firing, Charger contact damage, death shutdown, hub gate transition, mission Escape return, and full frontend-to-hub compatibility.
- Full EditMode and PlayMode suites in the open Unity Test Runner.
- Manual visual verification of hub spawn visibility, gate prompt/transition, mission movement, aiming, firing, both bosses taking damage, and the camera following the player.

## Builder Self-check

- The existing project already supplies health, visuals, scene flow, camera following, and scene-builder conventions; the design reuses all five.
- The selected scene boundary keeps commander test infrastructure out of production gameplay.
- The ranged module has a public behavior boundary that a future melee/loadout module can sit beside.
- The hub fix addresses the actual render obstruction while preserving collision and the approved camera.
- The smallest user-visible complete slice requires both a scene transition and a minimal HUD; omitting either would make the new map difficult to discover or understand.

