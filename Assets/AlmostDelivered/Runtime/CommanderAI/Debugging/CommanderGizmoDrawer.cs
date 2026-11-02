using AlmostDelivered.CommanderAI.Tactics;
using UnityEngine;

namespace AlmostDelivered.CommanderAI.Debugging
{
    public enum AnchorDebugState { Available, Reserved, Unreachable, Selected }
    [RequireComponent(typeof(CommanderArena))]
    public sealed class CommanderGizmoDrawer : MonoBehaviour
    {
        public AnchorDebugState Classify(TacticalAnchor anchor)
        {
            var arena = GetComponent<CommanderArena>();
            if (anchor == null || !anchor.isActiveAndEnabled) return AnchorDebugState.Unreachable;
            if (arena.Allocator != null && arena.Allocator.IsReserved(anchor)) return AnchorDebugState.Reserved;
            if (arena.Allocator != null)
            {
                var reachable = false;
                foreach (var member in arena.Squad.LivingMembers)
                    reachable |= arena.Allocator.CanUseAnchor(member.Id, member.Role, anchor);
                if (!reachable) return AnchorDebugState.Unreachable;
            }
            return AnchorDebugState.Available;
        }
        private void OnDrawGizmos()
        {
            var arena = GetComponent<CommanderArena>();
            if (arena.Anchors == null) return;
            foreach (var anchor in arena.Anchors)
            {
                if (anchor == null) continue;
                var state = Classify(anchor);
#if UNITY_EDITOR
                if (UnityEditor.Selection.activeGameObject == anchor.gameObject) state = AnchorDebugState.Selected;
#endif
                Gizmos.color = state == AnchorDebugState.Reserved ? Color.cyan : state == AnchorDebugState.Unreachable ? Color.red :
                    state == AnchorDebugState.Selected ? Color.yellow : Color.green;
                Gizmos.DrawWireSphere(anchor.transform.position, .7f);
#if UNITY_EDITOR
                UnityEditor.Handles.Label(anchor.transform.position + Vector3.up, anchor.name + " [" + state + "]");
#endif
            }
            if (arena.Squad == null) return;
            foreach (var member in arena.Squad.LivingMembers)
            {
                var order = member.Executor.CurrentOrder;
                Gizmos.color = arena.Squad.State == Domain.SquadAlertState.Confused ? Color.magenta : Color.cyan;
                if (order.Anchor != null) Gizmos.DrawLine(member.Health.transform.position, order.Anchor.transform.position);
#if UNITY_EDITOR
                UnityEditor.Handles.Label(member.Health.transform.position + Vector3.up * 2,
                    member.Role + " " + member.Id + " | " + order.Type + (arena.Squad.State == Domain.SquadAlertState.Confused ? " " + member.Executor.Tendency : ""));
#endif
            }
        }
    }
}
