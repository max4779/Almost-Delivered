using UnityEngine;
using UnityEngine.AI;

namespace AlmostDelivered.CommanderAI.Tactics
{
    public interface ITacticalReachability
    {
        bool CanReach(Vector3 from, Vector3 to);
    }

    public sealed class NavMeshTacticalReachability : ITacticalReachability
    {
        public bool CanReach(Vector3 from, Vector3 to)
        {
            var path = new NavMeshPath();
            return NavMesh.CalculatePath(from, to, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete;
        }
    }
}
