using UnityEngine;

namespace AlmostDelivered.Combat
{
    public enum CombatTeamId
    {
        Player,
        Enemy
    }

    public sealed class CombatTeam : MonoBehaviour
    {
        [SerializeField] private CombatTeamId team;

        public CombatTeamId Team
        {
            get => team;
            set => team = value;
        }
    }
}
