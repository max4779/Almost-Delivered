using AlmostDelivered.Combat;
using AlmostDelivered.CommanderAI.Commander;
using AlmostDelivered.CommanderAI.Domain;
using AlmostDelivered.CommanderAI.Execution;
using AlmostDelivered.CommanderAI.Perception;
using AlmostDelivered.CommanderAI.Tactics;
using UnityEngine;

namespace AlmostDelivered.CommanderAI.Debugging
{
    public enum ArenaScenario { Blockade, Pincer, FocusSupport }

    // Scene-only composition root. Tactical decisions still read SquadKnowledge exclusively.
    public sealed class CommanderArena : MonoBehaviour
    {
        public SquadController Squad;
        public CommanderBrain Brain;
        public ActorHealth Commander;
        public Transform Objective;
        public ObservableActor[] Players;
        public MonsterOrderExecutor[] Members;
        public TacticalAnchor[] Anchors;
        public UtilityTactic[] Tactics;
        public CommanderDifficultyProfile[] Profiles;
        [SerializeField] private int difficulty = 1;
        [SerializeField] private ArenaScenario initialScenario;
        public TacticalSlotAllocator Allocator { get; private set; }
        public CommanderDifficultyProfile Profile => Profiles[difficulty];
        public float NextCommandTime => Brain.NextCommandTime;
        private bool initialized;

        private void Start()
        {
            if (initialized) return;
            initialized = true;
            Arrange(initialScenario);
            for (var i = 0; i < Members.Length; i++)
            {
                var member = Members[i];
                Squad.RegisterMember(i + 1, (MonsterRole)(i / 2), member.GetComponent<ActorHealth>(), member);
                member.GetComponent<MonsterPerception>().Configure(Squad, i + 1);
                member.GetComponent<AlertSignal>().Configure(Squad, i + 1, 1f);
            }
            SelectDifficulty(difficulty);
            Brain.SearchOnLostContact = true;
        }

        public void SelectDifficulty(int index)
        {
            if (index < 0 || index >= Profiles.Length) throw new System.ArgumentOutOfRangeException(nameof(index));
            difficulty = index;
            Allocator?.ReleaseAll();
            Allocator = new TacticalSlotAllocator(Anchors, new NavMeshTacticalReachability());
            Brain.Configure(Squad, Commander, Profile, Tactics, Anchors, Allocator, Objective.position);
        }

        public void Arrange(ArenaScenario scenario)
        {
            // Tags are observed facts and intentionally cannot be erased by changing formations.
            var positions = scenario == ArenaScenario.Blockade
                ? new[] { new Vector3(0, 1, 0), new Vector3(30, 1, 0), new Vector3(-30, 1, 0) }
                : new[] { new Vector3(30, 1, 0), new Vector3(31, 1, 2), new Vector3(32, 1, 0) };
            for (var i = 0; i < Players.Length; i++)
            {
                var controller = Players[i].GetComponent<CharacterController>();
                if (controller != null) controller.enabled = false;
                Players[i].transform.position = positions[i];
                if (controller != null) controller.enabled = true;
            }
            Players[1].RevealTag(ObservedCombatTag.RangedWeapon);
            Players[2].RevealTag(ObservedCombatTag.Mobility);
            if (scenario == ArenaScenario.FocusSupport) Players[2].RevealTag(ObservedCombatTag.Healing);
        }
    }
}
