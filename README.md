# Almost Delivered: Commander AI prototype

A local Unity prototype where a visible, killable commander turns squad observations into explainable Utility AI tactics and role-based orders. Killing the commander permanently breaks squad coordination while survivors retain basic combat behavior.

Open with **Unity 2021.3.45f2** and run [CommanderAITest.unity](Assets/AlmostDelivered/Scenes/CommanderAITest.unity). The arena contains one controllable player, two instrumented dummies, one commander, and eight ordinary monsters. WASD/arrows move, click deals damage, and Space stuns nearby monsters. The overlay exposes formations, observed support tags, difficulty, commander death, score components, telegraphs, and member orders.

- [Approved design spec](docs/superpowers/specs/2026-11-01-commander-ai-design.md)
- [Implementation plan](docs/superpowers/plans/2026-11-01-commander-ai-implementation.md)
- [Architecture and later authority boundary](docs/commander-ai/architecture.md)
- [Test and arena generation commands](docs/commander-ai/tests.md)
- [Measured one-squad and five-squad CPU evidence](docs/commander-ai/profiling.md)
- [Deterministic portfolio demonstration](docs/commander-ai/portfolio-demo.md)

The initial tactics are Objective Blockade, Pincer, and Focus Support. Shield, Ranged, Charger, and Support currently provide tactical positioning and simple attacks. Multiplayer and full role abilities are future work, alongside cargo/control-point gameplay, progression, production presentation, and coordinated multi-squad strategy.

Automated scene, logic, lifecycle, navigation, and stress verification is recorded in the linked evidence. Manual screenshot/layout review and a human portfolio playthrough remain pending; no visual sign-off is claimed.
