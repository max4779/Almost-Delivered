using UnityEngine;
using UnityEngine.AI;
using Unity.Profiling;

namespace AlmostDelivered.CommanderAI.Tactics
{
    public interface ITacticalReachability
    {
        bool CanReach(Vector3 from, Vector3 to);
    }

    public sealed class NavMeshTacticalReachability : ITacticalReachability
    {
        private static readonly ProfilerMarker ReachabilityMarker = new ProfilerMarker("CommanderAI.PlannerReachability");
        public bool CanReach(Vector3 from, Vector3 to)
        {
            using var reachabilitySample = ReachabilityMarker.Auto();
            var path = new NavMeshPath();
            return NavMesh.CalculatePath(from, to, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete;
        }
    }
}
