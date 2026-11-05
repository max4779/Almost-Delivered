using System.Collections.Generic;
using AlmostDelivered.Combat;
using UnityEngine;

namespace AlmostDelivered.Gameplay.Payload
{
    public sealed class PayloadPresenceSensor
    {
        private readonly Collider[] hits;
        private readonly HashSet<int> countedActors = new HashSet<int>();

        public PayloadPresenceSensor(int capacity = 128)
        {
            hits = new Collider[Mathf.Max(1, capacity)];
        }

        public void Sample(Vector3 center, float radius, out int players, out int enemies)
        {
            players = 0;
            enemies = 0;
            countedActors.Clear();
            if (radius <= 0f || float.IsNaN(radius) || float.IsInfinity(radius))
            {
                return;
            }

            var count = Physics.OverlapSphereNonAlloc(center, radius, hits, ~0, QueryTriggerInteraction.Collide);
            for (var index = 0; index < count; index++)
            {
                var hit = hits[index];
                if (hit == null)
                {
                    continue;
                }

                var health = hit.GetComponentInParent<ActorHealth>();
                var team = hit.GetComponentInParent<CombatTeam>();
                if (health == null || health.IsDead || team == null || !countedActors.Add(health.GetInstanceID()))
                {
                    continue;
                }

                if (team.Team == CombatTeamId.Player)
                {
                    players++;
                }
                else
                {
                    enemies++;
                }
            }
        }
    }
}
