# Portfolio demonstration

This is a deterministic presentation script with automated scenario equivalents. Manual execution and screenshot/layout review remain pending: Task 7's Windows screenshot tool stalled, and Task 8 does not claim a visual capture or launch another blocking editor. Record the manual check only after a person performs it.

## Preparation

Open `Assets/AlmostDelivered/Scenes/CommanderAITest.unity` in Unity 2021.3.45f2. Rebuild through **Almost Delivered > Commander AI > Build deterministic arena** if needed. Use a Game view wide enough for the scrollable overlay; enable Scene Gizmos to inspect anchor lines. Enter fresh Play mode for every independent take because observed Healing tags persist.

Player controls are WASD/arrows, click for 25 damage, and Space for nearby stun. Overlay controls are Spread, Cluster, Reveal support, Kill commander, and difficulty. Normal uses a three-second command cycle, one-second command telegraph, and six-second minimum hold. Hard uses 1.5, 0.5, and three seconds respectively. Wait for the displayed clock/state rather than relying on wall-clock narration.

## Five-minute walkthrough

| Beat | Deterministic action | Evidence to show |
| --- | --- | --- |
| Interrupted alert | Before Play, disable `MonsterPerception` on all members except Shield 1. Enter Play paused and advance initialization, then set Player position to `(-10.5, 1, -6)`, four units from Shield 1. Resume and press Space during its one-second alert. Pause immediately afterward to inspect the state. | The signal cancels, squad remains Unaware, shared knowledge and score history are empty. This take isolates one reporter so another member cannot complete an alert. |
| Successful alert | Stop Play and reload the saved scene to restore original components/positions. Enter Play normally. Leave the alert uninterrupted. | Unaware becomes Alerted only after the signal; observed IDs, positions, and tags populate the overlay. No hidden player facts are available before this. |
| Objective Blockade | In a fresh take choose Normal and Spread; allow alert and command telegraph to complete. | Objective proximity is high, Player clustering is low, Blockade wins. Cyan reservations and MoveToSlot/HoldSlot orders place shields in front, ranged in rear, support behind, and chargers on opposite flanks. |
| Score change and Pincer | Select Cluster. Inspect shared positions immediately, then wait for the displayed next cycle and minimum hold to expire. | Objective proximity drops and Player clustering rises. The previous formation persists through the hold and pending telegraph. Pincer then becomes active with both flank routes represented. |
| Focus Support | Select Reveal support, allowing sight to observe Dummy B's Healing tag. Wait for cycle/hold and telegraph. | Observed support rises; Focus Support wins and charger/ranged orders name Dummy B's observed actor ID. Support stays protected. Restart Play before repeating a no-support comparison. |
| Commander death | Once organized movement is visible, select Kill commander. In a second fresh take, kill during a pending telegraph. | Pending and active tactic clear immediately, reservations become zero, all survivors show ConfusedCombat and a tendency. The pending tactic never activates. |
| Organized versus confused | Compare the before-death role lines with the survivors after death; move into local attack range. | Survivors remain dangerous through basic combat but no longer reserve formation slots or accept commander orders. No replacement commander appears and coordination never recovers. |
| Difficulty | In a fresh take compare Easy, Normal, and Hard using the displayed cycle, hold, telegraph, and available tactic labels. | Timing/complexity changes while observation permissions and memory lifetime remain the same. |

The arena's arrangement function resets player positions on Start, so perform the interrupted-alert setup by pausing after initialization and positioning Player in the Inspector before advancing the first sensing frames. Do not save this temporary take over the generated scene.

## Automated counterpart and remaining review

Run `CommanderScenarioTests`, `AlertFlowTests`, `CommanderLifecycleTests`, and the full suites using [these commands](tests.md). They exercise the actual generated arena, alert interruption, all tactic winners, cycle/hold/telegraph gating, role arrival, and permanent confusion. The focused brain regressions verify command-cycle memory decay and immediate report refresh. Stress tests provide [measured CPU evidence](profiling.md).

Manual review still needs to confirm overlay readability/scrolling, world label visibility, command cue presentation, and the overall organized/confused comparison on screen. No screenshot or portfolio-quality visual sign-off is supplied by automated logs.
