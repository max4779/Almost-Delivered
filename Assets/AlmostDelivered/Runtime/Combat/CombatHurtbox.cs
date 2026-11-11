using UnityEngine;

namespace AlmostDelivered.Combat
{
    /// <summary>Marks a trigger as an attack target without changing movement collision.</summary>
    [RequireComponent(typeof(Collider))]
    public sealed class CombatHurtbox : MonoBehaviour
    {
    }
}
