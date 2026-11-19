using System;
using System.Collections.Generic;
using AlmostDelivered.Frontend;

namespace AlmostDelivered.Gameplay.Payload
{
    public static class MissionCommanderAssaultPlanner
    {
        public static MissionMobArchetype[] Plan(MissionScoutCombatProfile profile,
            int groupSize, bool includeCargoSaboteur, int seed)
        {
            if (profile == null || !profile.HasObservedWeapon || groupSize <= 0)
                return Array.Empty<MissionMobArchetype>();

            var plan = new MissionMobArchetype[groupSize];
            var offset = includeCargoSaboteur ? 1 : 0;
            if (includeCargoSaboteur) plan[0] = MissionMobArchetype.CargoSaboteur;
            var available = groupSize - offset;
            var choices = new List<MissionMobArchetype>(available);
            var primaryCount = (available + 1) / 2;
            MissionMobArchetype primary;
            MissionMobArchetype secondary;
            MissionMobArchetype tertiary;
            switch (profile.LastObservedWeapon)
            {
                case PlayerWeaponType.Melee:
                    primary = MissionMobArchetype.Ranged;
                    secondary = MissionMobArchetype.Shield;
                    tertiary = MissionMobArchetype.Melee;
                    break;
                case PlayerWeaponType.Revolver:
                    primary = MissionMobArchetype.Melee;
                    secondary = MissionMobArchetype.Ranged;
                    tertiary = MissionMobArchetype.Shield;
                    break;
                default:
                    primary = MissionMobArchetype.Shield;
                    secondary = MissionMobArchetype.Melee;
                    tertiary = MissionMobArchetype.Ranged;
                    break;
            }

            for (var index = 0; index < primaryCount; index++) choices.Add(primary);
            for (var index = primaryCount; index < available; index++)
                choices.Add((index - primaryCount) % 2 == 0 ? secondary : tertiary);
            var random = new Random(seed);
            for (var index = choices.Count - 1; index > 0; index--)
            {
                var other = random.Next(index + 1);
                var temp = choices[index];
                choices[index] = choices[other];
                choices[other] = temp;
            }
            for (var index = 0; index < choices.Count; index++) plan[offset + index] = choices[index];
            return plan;
        }
    }
}
