# Frontend and departure hub

This slice provides the first player-facing loop for Almost Delivered: a pre-game menu, a walkable industrial staging room, and a return path to the menu.

## Run the flow

1. Open the project with Unity 2021.3.45f2.
2. Open `Assets/AlmostDelivered/Scenes/Frontend.unity`.
3. Enter Play mode and select `작전 준비`.
4. Move with WASD or the arrow keys.
5. Press Escape to return to the frontend.

The fixed orthographic quarter-view camera follows the placeholder capsule player. The camera angle and room layout are deliberately simple so gameplay and character assets can replace them later without changing the scene flow.

## Scenes and build order

The scene builder keeps these scenes at the start of Build Settings:

1. `Assets/AlmostDelivered/Scenes/Frontend.unity`
2. `Assets/AlmostDelivered/Scenes/DepartureHub.unity`
3. `Assets/AlmostDelivered/Scenes/CommanderAITest.unity`

`GameSceneFlow` owns the scene names and transition methods. `FrontendMenuController` starts the hub flow or quits a player build. `HubPlayerController` handles local prototype movement and Escape return. `QuarterViewCamera` maintains a fixed offset from the player.

## Regenerate the scenes

In the Unity Editor, choose:

`Almost Delivered > Frontend > Build frontend and departure hub`

For automation, start Unity with the project path and execute:

`AlmostDelivered.Editor.FrontendSceneBuilder.BuildFromCommandLine`

The builder recreates the two generated scenes and their shared materials, then normalizes Build Settings. Repeated generation is covered by EditMode tests.

## Departure hub hierarchy

`Departure Hub` separates room responsibilities into three roots:

- `Collision`: floor and boundary colliders.
- `Layout`: visible architecture, lighting, floor markings, and industrial dressing.
- `Stations`: mission terminal, equipment bay, deployment gate, and crew-access dressing.

The player and main camera remain separate scene roots so future character prefabs and camera systems can replace the placeholders cleanly.

## Scope boundary

This is a greybox staging room, not a complete lobby system. The stations are not interactive yet. The slice intentionally excludes networking, matchmaking, character selection, equipment management, inventory, combat, mission launch logic, and production art.

## Verification

EditMode scene-builder tests verify menu actions, responsive canvas setup, the background camera, room hierarchy, player/camera wiring, deterministic regeneration, and build order.

PlayMode integration tests verify that:

- the `작전 준비` button loads the departure hub;
- the spawned player has a working `CharacterController` and walkable floor;
- scripted movement changes the player position;
- the quarter-view camera tracks the player at its configured offset;
- returning from the hub loads an interactable frontend menu.

The complete Unity Test Runner suites currently contain 140 passing EditMode tests and 100 passing PlayMode tests.
