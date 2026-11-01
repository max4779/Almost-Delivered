# Almost Delivered Commander AI Design

Date: 2026-11-01
Status: Approved

## Intent

Almost Delivered is a quarter-view cooperative PvE objective game centered on moving cargo, defending control points, and protecting objectives from monster groups. Its combat should reward team positioning, role synergy, and long-term class specialization more than precision aiming.

The portfolio-defining system is a visible, killable commander embedded in each monster squad. Ordinary monsters make only local, simple combat decisions. The commander receives observations from squad members, scores possible tactics with Utility AI, and executes the selected tactic through authored tactical playbooks. Killing the commander permanently removes organized tactics from that squad and leaves its survivors in a readable confusion state.

The first milestone proves the complete information-to-order loop. It does not attempt to build the full game.

## Goals

- Make an alerted squad visibly more coordinated while its commander is alive.
- Make commander decisions depend only on information observed by the squad, never hidden omniscient game state.
- Keep ordinary monster logic cheap, predictable, and reusable.
- Let players understand and counter enemy intent through readable signals and commander assassination.
- Make every tactical decision inspectable in a portfolio-quality debug view.
- Establish data-driven extension points for new monster roles, tactics, maps, and difficulty levels.

## Non-goals for the first milestone

- Online multiplayer and network replication.
- Cargo, control-point, long-term progression, or class-tree implementation.
- Reinforcement learning, generative AI, or an external AI service.
- Procedurally generating tactics.
- Production character art, animation, audio, or final UI.
- More than one squad acting in concert.
- Dynamic promotion of a replacement commander.

## Anti-goals

- An AI that reads player loadouts or exact positions without observing them.
- Ordinary monsters independently running the full tactical planner.
- Instant, untelegraphed formation changes that feel unfair.
- Difficulty based primarily on inflated health or damage.
- A commander that becomes irrelevant once combat starts.
- A system whose choices cannot be explained during debugging or a portfolio interview.

## Project constraints

- Existing project: Unity 2021.3.45f2.
- Existing gameplay code: none; imported environment and VFX assets are present.
- Navigation: Unity NavMesh and `NavMeshAgent` are available.
- The first slice should run without third-party AI packages.
- The architecture must allow a later server-authoritative multiplayer implementation, but the first slice is local-only.

## Approaches considered

### Fixed finite-state tactics

The commander would advance through a fixed chain such as alert, defend, flank, and retreat. This is inexpensive and easy to control, but it would not convincingly combine observations, squad composition, and objective state. It was rejected as the main planner.

### Utility AI plus tactical playbooks — selected

The commander scores every currently valid tactic from observed facts. The winning tactic activates an authored playbook that creates role-specific orders and assigns available monsters to tactical positions. This approach is explainable, tunable, deterministic enough to test, and expressive enough to appear intelligent.

### Reinforcement-learning commander

A learned policy could select tactics after training. It was rejected for the first milestone because reward design, training infrastructure, nondeterministic behavior, and poor explainability would expand the portfolio scope without first proving better gameplay.

## First playable scope

The prototype contains one controllable player, two instrumented player test dummies, one squad, one killable commander, and eight ordinary monsters. The dummies expose different observed role and ability tags so clustering and target-priority tactics can be validated without implementing multiplayer. Placeholder meshes are acceptable.

Initial monster roles:

- Shield: blocks a route and protects allies.
- Ranged: attacks from a safer rear or elevated position.
- Charger: pressures a flank or isolated target.
- Support: stays behind the formation and assists the commander or damaged elites.

Initial tactics:

- Objective Blockade: shields occupy approach lanes, ranged units take rear positions, support stays protected, and chargers contest exposed gaps.
- Pincer: the squad splits pressure between two approach directions when players are clustered and both flank routes are usable.
- Focus Support: the squad prioritizes an observed healing, shielding, or support-oriented player and assigns chargers and ranged units to pressure that target.

The first map is a compact test arena containing a fixed objective marker, two flank routes, cover, and authored tactical anchors. The marker has no capture, cargo movement, or progress simulation in this milestone.

## Prototype combat boundary

The milestone includes only the combat contracts required to exercise commander behavior:

- health, damage, death, and stun events;
- an observed weapon tag event;
- an observed ability or trait tag event;
- interruptible alert and command telegraphs;
- a simple basic attack used to prove that monsters remain dangerous.

Shield, Ranged, Charger, and Support are tactical assignment roles in this milestone. They must move to appropriate positions and expose their active order in the debug view, but Shield does not need full projectile blocking, Charger does not need a production charge attack, and Support does not need full healing logic. Those combat abilities are deferred until the commander loop is proven.

## Core gameplay rules

### Before alert

- Each ordinary monster owns local sight and hearing only.
- The commander has no player record and cannot choose player-specific tactics.
- A monster that sees a player begins a readable alert action such as shouting or blowing a horn.
- The alert action can be interrupted by death, stun, or loss of the required condition.
- The squad becomes alerted only after one monster completes the alert action.

### After alert

- All current observations made by living squad members are copied immediately into the squad knowledge store.
- Immediate information sharing does not mean immediate reaction. The commander evaluates tactics only on its configured command cycle.
- Weapon and trait tags become known only after a squad member visibly observes the relevant weapon or ability in use.
- Lost targets retain a last-known position. Knowledge confidence decays over time rather than tracking an unseen target.
- The commander emits a readable command signal before new orders take effect.

### Commander death

- The active playbook is cancelled.
- Reserved tactical positions are released.
- Every surviving member of that squad permanently enters Confused for the rest of its lifetime.
- No replacement commander is promoted in the first milestone.
- Confused monsters keep basic combat competence but no longer form coordinated lines, focus targets as a group, or accept squad orders.

## Runtime architecture

### `SquadController`

Owns squad membership, alert state, commander reference, shared knowledge, active tactic, and tactical reservations. It is the stable boundary for a later server-authoritative implementation.

### `MonsterPerception`

Per ordinary monster, performs budgeted sight and hearing checks and emits observation events. It does not query or modify commander decisions.

### `AlertSignal`

Runs the interruptible initial shout or horn action. Successful completion changes the squad from Unaware to Alerted.

### `SquadKnowledge`

Stores only observed facts:

- player identifier;
- last-known position and movement direction;
- observation timestamp;
- confidence;
- observed weapon tags;
- observed ability or trait tags;
- reporting monster identifier.

Merging observations refreshes newer facts. Confidence decay turns stale exact positions into search information.

### `CommanderBrain`

On each command cycle, builds a read-only `TacticalContext`, asks every valid tactic to score it, applies switching rules, and selects at most one active playbook in the first milestone.

The brain never reads player components directly. Its only player-specific input is `SquadKnowledge`.

### `UtilityTactic`

A ScriptableObject definition containing:

- required squad roles and minimum counts;
- score considerations and curves;
- minimum activation score;
- minimum hold duration;
- switch penalty;
- referenced tactical playbook.

Each consideration returns a normalized score. The combined score and every component score are retained for debugging. Invalid tactics are reported with an explicit rejection reason.

### `TacticalPlaybook`

A ScriptableObject that translates the selected tactic into role requirements, tactical-slot queries, and simple monster orders. The playbook does not choose whether it should run; the Utility AI does that.

### `TacticalAnchor`

An authored scene component marking useful positions with tags such as ObjectiveFront, CoverRear, LeftFlank, RightFlank, CommanderSafe, and SupportRear. It exposes capacity, allowed roles, facing direction, and optional objective association.

Authored anchors are selected for the first milestone because they are deterministic, easy to debug, and let the prototype validate tactical reasoning before adding dynamic NavMesh sampling.

### `TacticalSlotAllocator`

Filters anchors by playbook query, NavMesh reachability, occupancy, distance, and exposure. It reserves the best valid slot for each requested role and releases reservations when orders end, members die, or the commander dies.

### `MonsterOrderExecutor`

Runs one simple order at a time:

- MoveToSlot;
- HoldSlot;
- AttackObservedTarget;
- ProtectUnit;
- SearchLastKnownPosition;
- ConfusedCombat.

It owns movement and local attack execution but does not select squad tactics.

### `CommanderDebugView`

Displays squad alert state, observed player facts and confidence, all tactic scores, invalidity reasons, the selected tactic, command-cycle timing, tactical anchor reservations, and member orders. World-space gizmos draw anchors, assignments, and last-known positions.

## Utility evaluation

Every tactic score is composed from named, inspectable considerations. Initial examples:

- availability of required roles;
- player clustering;
- distance from players to the objective;
- observed support abilities;
- left and right flank reachability;
- number of safe ranged or shield anchors;
- squad health and surviving-member ratio;
- current tactic age.

The selected score must exceed the tactic threshold. A new tactic must beat the current tactic by the configured switch margin and the current tactic must have passed its minimum hold time. These rules prevent thrashing when scores are close.

The planner is deterministic for identical context data. Random variation may be added later only after deterministic behavior is covered by tests.

## Initial difficulty profiles

Information-sharing rules stay identical across all difficulties. Difficulty changes command capacity rather than hidden knowledge.

- Easy: 5-second command cycle, one simple tactic, long telegraph, conservative switching.
- Normal: 3-second command cycle, the three initial tactics, standard telegraph and switch margin.
- Hard: 1.5-second command cycle, shorter telegraph, lower switch resistance, and later support for layered orders.

The exact timings are starting values and must remain data-driven.

## Confused behavior

On commander death, each member receives a deterministic confusion tendency chosen from its role and stable instance seed. Possible tendencies are attacking the nearest visible target, holding the current area, retreating when wounded, or searching the last-known position. Confusion is intentionally less coordinated, not harmless.

Confused members do not share new observations, reserve tactical anchors, focus a commander-designated target, or return to organized state.

## Performance boundaries

- Perception checks are staggered across frames.
- Utility evaluation runs once per commander command cycle, never per frame.
- Ordinary monsters execute simple state machines and NavMesh movement.
- Tactical anchors and reachability results are cached within a planning cycle.
- The architecture targets a later scenario of roughly five squads and forty active ordinary monsters, but the first milestone validates one squad before scaling.
- Profiling must focus on NavMesh path requests, avoidance, animation, and eventual network replication as well as planner cost.

## Multiplayer boundary

Multiplayer is not implemented in this milestone. The later authoritative host or server will own `SquadController`, knowledge merging, utility evaluation, slot allocation, and order issuance. Clients will receive replicated alert cues, commander signals, selected tactic presentation, member orders, and movement state. No client will author commander knowledge or tactical decisions.

## Failure handling

- If no tactic is valid or no score reaches threshold, the squad uses a simple fallback hold-and-attack order.
- If only part of a playbook can allocate positions, allocated members execute while unallocated members use the fallback order; the failure is visible in debug output.
- If a reserved anchor becomes unreachable, the executor requests one reassignment before falling back.
- If the commander dies during a command telegraph, the pending playbook is cancelled and confusion begins immediately.

## Validation and acceptance criteria

The first milestone is accepted when all of the following are demonstrated in the test arena:

1. Before a successful alert signal, the commander has no player knowledge and issues no player-specific tactic.
2. Interrupting the alert prevents squad alert.
3. After alert, observations from any living member immediately update shared knowledge, while tactical changes wait for the next command cycle and telegraph.
4. Controlled scenarios cause Objective Blockade, Pincer, and Focus Support to win for explainable score reasons.
5. Monsters of each role move to appropriate reserved anchors and execute simple orders.
6. Minimum hold time and switching margin prevent rapid tactic oscillation.
7. Killing the commander cancels organized orders, releases anchors, and permanently puts surviving members into Confused.
8. Changing difficulty modifies command timing and available complexity without giving the commander unobserved information.
9. The debug view explains every chosen and rejected tactic.
10. A profiling capture records the cost of one squad and a stress setup before additional squad types are added.

## Builder decisions intentionally left open

- Concrete class and folder names may change to match normal Unity conventions while preserving responsibilities.
- Placeholder visuals and audio cues may use existing assets or primitives.
- Exact score curves, weights, radii, and timings are tuning values.
- Unit-test framework details and scene-test harness organization are implementation-plan decisions.
- Final network library selection is deferred until the local tactical loop is proven.
