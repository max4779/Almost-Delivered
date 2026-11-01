using AlmostDelivered.CommanderAI.Domain;
using UnityEngine;

namespace AlmostDelivered.CommanderAI.Tactics
{
    // Initial tuning is centralized here and copied into editable ScriptableObject definitions.
    // Callers own the returned tactics and their playbooks; no scene objects are created.
    public static class InitialTacticDefinitions
    {
        public static UtilityTactic[] Create()
        {
            var blockade = Definition(InitialTacticIds.ObjectiveBlockade, "Objective Blockade",
                new[] { new RoleRequirement(MonsterRole.Shield, 1), new RoleRequirement(MonsterRole.Ranged, 1) },
                new[] {
                    new UtilityConsideration("Objective proximity", ConsiderationKind.ObjectiveProximity, scale: 20f),
                    new UtilityConsideration("Shield anchor availability", ConsiderationKind.AnchorAvailability, anchorTag: TacticalAnchorTag.ObjectiveFront),
                    new UtilityConsideration("Ranged anchor availability", ConsiderationKind.AnchorAvailability, anchorTag: TacticalAnchorTag.CoverRear),
                    new UtilityConsideration("Squad health", ConsiderationKind.SquadHealth) },
                false, new[] {
                    new PlaybookRoleSlots(MonsterRole.Shield, false, TacticalAnchorTag.ObjectiveFront),
                    new PlaybookRoleSlots(MonsterRole.Ranged, false, TacticalAnchorTag.CoverRear),
                    new PlaybookRoleSlots(MonsterRole.Charger, false, TacticalAnchorTag.LeftFlank, TacticalAnchorTag.RightFlank),
                    new PlaybookRoleSlots(MonsterRole.Support, false, TacticalAnchorTag.SupportRear) });

            var pincer = Definition(InitialTacticIds.Pincer, "Pincer",
                new[] { new RoleRequirement(MonsterRole.Charger, 2) },
                new[] {
                    new UtilityConsideration("Player clustering", ConsiderationKind.PlayerClustering, weight: 2f, scale: 8f),
                    new UtilityConsideration("Left flank availability", ConsiderationKind.AnchorAvailability, anchorTag: TacticalAnchorTag.LeftFlank),
                    new UtilityConsideration("Right flank availability", ConsiderationKind.AnchorAvailability, anchorTag: TacticalAnchorTag.RightFlank),
                    new UtilityConsideration("Squad health", ConsiderationKind.SquadHealth) },
                false, new[] {
                    new PlaybookRoleSlots(MonsterRole.Charger, false, TacticalAnchorTag.LeftFlank, TacticalAnchorTag.RightFlank),
                    new PlaybookRoleSlots(MonsterRole.Shield, false, TacticalAnchorTag.ObjectiveFront),
                    new PlaybookRoleSlots(MonsterRole.Ranged, false, TacticalAnchorTag.CoverRear),
                    new PlaybookRoleSlots(MonsterRole.Support, false, TacticalAnchorTag.SupportRear) });

            var focus = Definition(InitialTacticIds.FocusSupport, "Focus Support",
                new[] { new RoleRequirement(MonsterRole.Charger, 1), new RoleRequirement(MonsterRole.Ranged, 1) },
                new[] {
                    new UtilityConsideration("Observed support", ConsiderationKind.ObservedSupport, weight: 2f),
                    new UtilityConsideration("Ranged anchor availability", ConsiderationKind.AnchorAvailability, anchorTag: TacticalAnchorTag.CoverRear),
                    new UtilityConsideration("Squad health", ConsiderationKind.SquadHealth) },
                true, new[] {
                    new PlaybookRoleSlots(MonsterRole.Charger, true, TacticalAnchorTag.LeftFlank, TacticalAnchorTag.RightFlank),
                    new PlaybookRoleSlots(MonsterRole.Ranged, true, TacticalAnchorTag.CoverRear),
                    new PlaybookRoleSlots(MonsterRole.Support, false, TacticalAnchorTag.SupportRear),
                    new PlaybookRoleSlots(MonsterRole.Shield, false, TacticalAnchorTag.ObjectiveFront) });
            return new[] { blockade, pincer, focus };
        }

        private static UtilityTactic Definition(string id, string name, RoleRequirement[] roles,
            UtilityConsideration[] considerations, bool requiresSupport, PlaybookRoleSlots[] slots)
        {
            var playbook = ScriptableObject.CreateInstance<TacticalPlaybook>();
            playbook.name = name + " Playbook";
            playbook.RequiresObservedSupport = requiresSupport;
            playbook.RoleSlots = slots;
            var tactic = ScriptableObject.CreateInstance<UtilityTactic>();
            tactic.name = name;
            tactic.TacticId = id;
            tactic.RequiredRoles = roles;
            tactic.Considerations = considerations;
            tactic.Playbook = playbook;
            return tactic;
        }
    }
}
