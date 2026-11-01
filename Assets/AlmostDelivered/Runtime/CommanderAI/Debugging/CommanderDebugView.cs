using System.Collections.Generic;
using System.Text;
using AlmostDelivered.CommanderAI.Domain;
using UnityEngine;

namespace AlmostDelivered.CommanderAI.Debugging
{
    [RequireComponent(typeof(CommanderArena))]
    public sealed class CommanderDebugView : MonoBehaviour
    {
        private CommanderArena arena;
        private Vector2 scroll;
        private void Awake() => arena = GetComponent<CommanderArena>();
        public string Describe(float now)
        {
            if (arena == null) arena = GetComponent<CommanderArena>();
            if (arena.Allocator == null) return "Arena initializing";
            var text = new StringBuilder();
            text.AppendLine("COMMANDER AI | " + arena.Squad.State + " | " + arena.Profile.name);
            if (arena.Squad.State == SquadAlertState.Confused)
                text.AppendLine("PERMANENT CONFUSION — commander dead; coordination cannot recover.");
            text.AppendLine("Next command: " + (float.IsNegativeInfinity(arena.NextCommandTime) ? "after completed alert" : Mathf.Max(0, arena.NextCommandTime - now).ToString("F2") + "s") +
                " | cycle " + arena.Profile.CommandCycleSeconds.ToString("F1") + "s | hold " + arena.Profile.MinimumHoldTime.ToString("F1") + "s");
            text.AppendLine("Active: " + (arena.Brain.ActiveEvaluation?.TacticId ?? "none") + " | Pending: " + (arena.Brain.PendingEvaluation?.TacticId ?? "none"));
            text.AppendLine("Decision: " + arena.Brain.DecisionReason);
            var cue = arena.Brain.Telegraph;
            text.AppendLine(cue != null && cue.IsVisible ? "TELEGRAPH " + cue.TacticId + " | " +
                Mathf.Clamp01(1 - (cue.CompletesAt - now) / Mathf.Max(.01f, arena.Profile.TelegraphDuration)).ToString("P0") +
                " | " + Mathf.Max(0, cue.CompletesAt - now).ToString("F2") + "s remaining" : "Telegraph: idle");
            text.AppendLine("SHARED KNOWLEDGE (observed facts only)");
            foreach (var memory in arena.Squad.Knowledge.Snapshot)
                text.AppendLine("Actor " + memory.ActorId + " last " + memory.LastKnownPosition.ToString("F1") + " confidence " + memory.Confidence.ToString("F2") + " tags " + memory.Tags);
            if (arena.Squad.Knowledge.Snapshot.Count == 0) text.AppendLine("No player knowledge. Complete an uninterrupted alert first.");
            text.AppendLine("LATEST TACTIC SCORES (all components)");
            var latest = new Dictionary<string, TacticEvaluation>();
            foreach (var evaluation in arena.Brain.EvaluationHistory) latest[evaluation.TacticId] = evaluation;
            foreach (var tactic in arena.Tactics)
            {
                if (!latest.TryGetValue(tactic.TacticId, out var evaluation))
                { text.AppendLine(tactic.TacticId + ": not evaluated / unavailable at this difficulty"); continue; }
                var reason = !evaluation.IsValid ? evaluation.RejectionReason : evaluation.TotalScore <= tactic.MinimumActivationScore
                    ? "below activation threshold" : arena.Brain.PendingEvaluation?.TacticId == evaluation.TacticId ? "selected; telegraph before orders"
                    : arena.Brain.ActiveEvaluation?.TacticId == evaluation.TacticId ? "active" : "not selected; " + arena.Brain.DecisionReason;
                text.AppendLine(evaluation.TacticId + " = " + evaluation.TotalScore.ToString("F3") + " | " + reason);
                foreach (var score in evaluation.ComponentScores) text.AppendLine("  " + score.Key + "=" + score.Value.ToString("F2"));
            }
            text.AppendLine("RESERVATIONS " + arena.Allocator.ReservationCount + " | green available / cyan reserved / red unreachable / yellow selected");
            foreach (var member in arena.Squad.LivingMembers)
            {
                var order = member.Executor.CurrentOrder;
                text.AppendLine(member.Id + " " + member.Role + " -> " + order.Type + (order.Anchor == null ? "" : " @ " + order.Anchor.name) +
                    (order.TargetActorId == 0 ? "" : " target " + order.TargetActorId) + (arena.Squad.State == SquadAlertState.Confused ? " / " + member.Executor.Tendency : ""));
            }
            foreach (var failure in arena.Brain.AllocationFailures) text.AppendLine("ALLOCATION FAILED: " + failure);
            return text.ToString();
        }

        private void OnGUI()
        {
            if (arena == null || arena.Allocator == null) return;
            GUILayout.BeginArea(new Rect(10, 10, Mathf.Min(560, Screen.width * .48f), Screen.height - 20), GUI.skin.box);
            GUILayout.Label("WASD / arrows move | click commander: 25 damage | Space: nearby stun");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Spread")) arena.Arrange(ArenaScenario.Blockade);
            if (GUILayout.Button("Cluster")) arena.Arrange(ArenaScenario.Pincer);
            if (GUILayout.Button("Reveal support")) arena.Arrange(ArenaScenario.FocusSupport);
            if (GUILayout.Button("Kill commander")) arena.Commander.ApplyDamage(1000);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            for (var i = 0; i < arena.Profiles.Length; i++) if (GUILayout.Button(arena.Profiles[i].name)) arena.SelectDifficulty(i);
            GUILayout.EndHorizontal();
            GUILayout.Label("Support tags persist once observed. Restart Play mode for a fresh scenario.");
            scroll = GUILayout.BeginScrollView(scroll);
            GUILayout.Label(Describe(Time.unscaledTime));
            GUILayout.EndScrollView(); GUILayout.EndArea();
        }
    }
}
