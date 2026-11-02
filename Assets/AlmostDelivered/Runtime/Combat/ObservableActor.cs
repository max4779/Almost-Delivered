using System;
using System.Threading;
using AlmostDelivered.CommanderAI.Domain;
using UnityEngine;

namespace AlmostDelivered.Combat
{
    public sealed class ObservableActor : MonoBehaviour
    {
        private static int nextActorId;
        private int actorId;
        private Vector3 previousPosition;

        // IDs are stable for this component's lifetime, including disable/enable.
        public int ActorId => actorId == 0 ? actorId = Interlocked.Increment(ref nextActorId) : actorId;
        public Vector3 Velocity { get; private set; }
        public ObservedCombatTag ObservedTags { get; private set; }
        public event Action<ObservedCombatTag> TagObserved;
        // A use is transient evidence. Observers must validate sight at this instant;
        // ObservedTags remains actor telemetry, never a source of squad knowledge.
        public static event Action<ObservableActor, ObservedCombatTag> TagUsed;

        public void RevealTag(ObservedCombatTag tag)
        {
            if (tag == ObservedCombatTag.None || !isActiveAndEnabled) return;
            TagUsed?.Invoke(this, tag);
            var newlyObserved = tag & ~ObservedTags;
            if (newlyObserved == ObservedCombatTag.None)
                return;

            ObservedTags |= newlyObserved;
            TagObserved?.Invoke(newlyObserved);
        }

        private void Awake()
        {
            // Allocate eagerly in play mode, with a lazy fallback for editor consumers.
            _ = ActorId;
        }

        private void OnEnable()
        {
            previousPosition = transform.position;
            Velocity = Vector3.zero;
        }

        private void Update()
        {
            var position = transform.position;
            Velocity = Time.unscaledDeltaTime > 0f
                ? (position - previousPosition) / Time.unscaledDeltaTime
                : Vector3.zero;
            previousPosition = position;
        }
    }
}
