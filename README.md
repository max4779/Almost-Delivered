# Almost Delivered

Almost Delivered is a quarter-view co-op PvE defense game prototype built with **Unity 2021.3.45f2**. The current player-facing flow starts at a pre-game menu and enters an industrial departure hub inspired by mission-staging spaces.

Open `Assets/AlmostDelivered/Scenes/Frontend.unity` and enter Play mode:

- Select `작전 준비` to enter the departure hub.
- Move the placeholder player with WASD or the arrow keys.
- Press Escape to return to the frontend.
- `설정 · 준비 중` is intentionally disabled while the settings flow is out of scope.

The mission terminal, equipment bay, deployment gate, and room dressing are visual layout placeholders. Character selection, inventory, matchmaking, combat, and mission launch interactions are not implemented in this slice. See the [departure hub guide](docs/frontend/departure-hub.md) for scene generation, hierarchy, controls, and verification.

## Commander AI prototype

A local Unity prototype where a visible, killable commander turns squad observations into explainable Utility AI tactics and role-based orders. Killing the commander permanently breaks squad coordination while survivors retain basic combat behavior.

Open with **Unity 2021.3.45f2** and run [CommanderAITest.unity](Assets/AlmostDelivered/Scenes/CommanderAITest.unity). The arena contains one controllable player, two instrumented dummies, one commander, and eight ordinary monsters. WASD/arrows move, click deals damage, and Space stuns nearby monsters. The overlay exposes formations, observed support tags, difficulty, commander death, score components, telegraphs, and member orders.

To open a fresh clone, add the repository root in Unity Hub and select **Unity 2021.3.45f2**. Let Unity restore the packages pinned in `Packages/manifest.json` and `Packages/packages-lock.json`, then open `Assets/AlmostDelivered/Scenes/Frontend.unity` as the first scene and enter Play mode. `ProjectSettings` is tracked; Unity creates `Library` and local editor settings on first open.

Shader Graph and its Render Pipeline Core dependency are pinned to **12.1.15**, matching this Unity 2021.3 editor. The generated phase-1 visuals use Built-in `Standard` and `Unlit/Color` shaders.

The locally imported SimpleNaturePack, BrokenVector FreeLowPolyPack, and PixPlays elemental effects packs are optional and excluded from this public repository while redistribution permission remains unverified. Phase-1 generated visuals require no external art package. See the [third-party notice](Assets/ThirdParty/NOTICE.md) for the inclusion rule.

- [Approved design spec](docs/superpowers/specs/2026-11-01-commander-ai-design.md)
- [Implementation plan](docs/superpowers/plans/2026-11-01-commander-ai-implementation.md)
- [Architecture and later authority boundary](docs/commander-ai/architecture.md)
- [Test and arena generation commands](docs/commander-ai/tests.md)
- [Measured one-squad and five-squad CPU evidence](docs/commander-ai/profiling.md)
- [Deterministic portfolio demonstration](docs/commander-ai/portfolio-demo.md)

The initial tactics are Objective Blockade, Pincer, and Focus Support. Shield, Ranged, Charger, and Support currently provide tactical positioning and simple attacks. Multiplayer and full role abilities are future work, alongside cargo/control-point gameplay, progression, production presentation, and coordinated multi-squad strategy.

Automated scene, logic, lifecycle, navigation, and stress verification is recorded in the linked evidence. The frontend-to-hub flow has also been manually verified in the Unity Game view.

- [Phase-1 low-poly visual set and rebuild instructions](docs/art/low-poly-visual-set.md)
