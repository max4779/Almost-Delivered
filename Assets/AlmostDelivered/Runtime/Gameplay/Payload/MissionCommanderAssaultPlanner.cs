using System;
using System.Collections.Generic;
using AlmostDelivered.Frontend;
using UnityEngine;

namespace AlmostDelivered.Gameplay.Payload
{
    [Serializable]
    public sealed class CommanderWaveBudget
    {
        [Min(1)] public int tokensPerWave = 10;
        [Min(1)] public int meleeCost = 1;
        [Min(1)] public int rangedCost = 2;
        [Min(1)] public int shieldCost = 2;
        [Min(1)] public int cargoSaboteurCost = 1;
        [Min(1)] public int scoutCost = 2;
        [Min(1)] public int chargerBossCost = 4;
        [Min(1)] public int reconPatrolSize = 2;
        [Min(1)] public int maximumLivingScouts = 5;
        [Min(1f)] public float freshIntelSeconds = 25f;

        public int CostFor(MissionMobArchetype type)
        {
            switch (type)
            {
                case MissionMobArchetype.Ranged: return Math.Max(1, rangedCost);
                case MissionMobArchetype.Shield: return Math.Max(1, shieldCost);
                case MissionMobArchetype.CargoSaboteur: return Math.Max(1, cargoSaboteurCost);
                case MissionMobArchetype.ChargerBoss: return Math.Max(1, chargerBossCost);
                default: return Math.Max(1, meleeCost);
            }
        }
    }

    public static class MissionCommanderAssaultPlanner
    {
        // Compatibility entry point for callers that only know the group size.
        public static MissionMobArchetype[] Plan(MissionScoutCombatProfile profile,
            int groupSize, bool includeCargoSaboteur, int seed)
        {
            if (profile == null || !profile.HasObservedWeapon) return Array.Empty<MissionMobArchetype>();
            var settings = new CommanderWaveBudget();
            return PlanBudget(profile, settings, settings.tokensPerWave,
                groupSize, includeCargoSaboteur, seed);
        }

        public static MissionMobArchetype[] PlanBudget(MissionScoutCombatProfile profile,
            CommanderWaveBudget settings, int tokenBudget, int maximumUnits,
            bool includeCargoSaboteur, int seed)
        {
            settings = settings ?? new CommanderWaveBudget();
            var remaining = Math.Max(0, tokenBudget);
            var limit = Math.Max(0, maximumUnits);
            if (remaining == 0 || limit == 0) return Array.Empty<MissionMobArchetype>();

            var result = new List<MissionMobArchetype>(limit);
            if (includeCargoSaboteur)
            {
                var cost = settings.CostFor(MissionMobArchetype.CargoSaboteur);
                if (cost > remaining) return Array.Empty<MissionMobArchetype>();
                result.Add(MissionMobArchetype.CargoSaboteur);
                remaining -= cost;
            }

            var preference = Preference(profile);
            var next = 0;
            var ranged = 0;
            var shields = 0;
            while (result.Count < limit)
            {
                var selected = false;
                for (var offset = 0; offset < preference.Length; offset++)
                {
                    var index = (next + offset) % preference.Length;
                    var candidate = preference[index];
                    var cost = settings.CostFor(candidate);
                    // A shield needs ranged support; at most two shields per ranged unit.
                    if (cost > remaining ||
                        candidate == MissionMobArchetype.Shield && shields >= 2 * ranged)
                        continue;
                    result.Add(candidate);
                    remaining -= cost;
                    if (candidate == MissionMobArchetype.Ranged) ranged++;
                    if (candidate == MissionMobArchetype.Shield) shields++;
                    next = (index + 1) % preference.Length;
                    selected = true;
                    break;
                }
                if (!selected) break;
            }

            var random = new System.Random(seed);
            var fixedPrefix = includeCargoSaboteur ? 1 : 0;
            for (var index = result.Count - 1; index > fixedPrefix; index--)
            {
                var other = random.Next(fixedPrefix, index + 1);
                var temp = result[index];
                result[index] = result[other];
                result[other] = temp;
            }
            return result.ToArray();
        }

        public static int SpentTokens(IReadOnlyList<MissionMobArchetype> plan,
            CommanderWaveBudget settings)
        {
            settings = settings ?? new CommanderWaveBudget();
            var total = 0;
            if (plan != null)
                foreach (var type in plan) total += settings.CostFor(type);
            return total;
        }

        private static MissionMobArchetype[] Preference(MissionScoutCombatProfile profile)
        {
            if (profile != null && profile.HasObservedWeapon)
            {
                if (profile.LastObservedWeapon == PlayerWeaponType.Melee || profile.PrefersMelee)
                    return new[] { MissionMobArchetype.Ranged, MissionMobArchetype.Shield,
                        MissionMobArchetype.Ranged, MissionMobArchetype.Melee };
                if (profile.LastObservedWeapon == PlayerWeaponType.Revolver)
                    return new[] { MissionMobArchetype.Melee, MissionMobArchetype.Ranged,
                        MissionMobArchetype.Shield, MissionMobArchetype.Melee };
                return new[] { MissionMobArchetype.Ranged, MissionMobArchetype.Shield,
                    MissionMobArchetype.Melee, MissionMobArchetype.Shield };
            }
            return new[] { MissionMobArchetype.Ranged, MissionMobArchetype.Shield,
                MissionMobArchetype.Melee };
        }
    }
}
