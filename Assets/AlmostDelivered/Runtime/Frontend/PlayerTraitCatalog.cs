using System;
using System.Collections.Generic;

namespace AlmostDelivered.Frontend
{
    public enum PlayerTraitCategory { Combat, Survival, Escort }

    public sealed class PlayerTraitDefinition
    {
        public readonly string Id;
        public readonly PlayerTraitCategory Category;
        public readonly string Name;
        public readonly string Description;
        public readonly float FirstRankBonus;
        public readonly float LaterRankBonus;
        public readonly string RequiredTraitId;
        public readonly int RequiredRank;

        public PlayerTraitDefinition(string id, PlayerTraitCategory category, string name, string description,
            float firstRankBonus, float laterRankBonus, string requiredTraitId = null, int requiredRank = 0)
        {
            Id = id;
            Category = category;
            Name = name;
            Description = description;
            FirstRankBonus = firstRankBonus;
            LaterRankBonus = laterRankBonus;
            RequiredTraitId = requiredTraitId;
            RequiredRank = requiredRank;
        }
    }

    /// <summary>Stable IDs preserve saved ranks when provisional names and effects change.</summary>
    public static class PlayerTraitCatalog
    {
        public const int MaximumRank = 5;
        public const int CategoryPointLimit = 10;
        public const int TotalPointLimit = 20;

        public static readonly IReadOnlyList<PlayerTraitDefinition> All = new[]
        {
            new PlayerTraitDefinition("combat.cargo_fire", PlayerTraitCategory.Combat,
                "화물 사수", "공격 피해 +10%, 이후 랭크마다 +3%p", .10f, .03f),
            new PlayerTraitDefinition("combat.counter", PlayerTraitCategory.Combat,
                "반격 태세", "공격 간격 -8%, 이후 랭크마다 -3%p", .08f, .03f,
                "combat.cargo_fire", 3),
            new PlayerTraitDefinition("survival.armor", PlayerTraitCategory.Survival,
                "강화 외골격", "최대 체력 +20%, 이후 랭크마다 +5%p", .20f, .05f),
            new PlayerTraitDefinition("survival.mobility", PlayerTraitCategory.Survival,
                "기동 전문가", "스태미나 회복 +20%, 이후 랭크마다 +5%p", .20f, .05f,
                "survival.armor", 3),
            new PlayerTraitDefinition("escort.lead", PlayerTraitCategory.Escort,
                "선도 요원", "화물 이동 속도 +12%, 이후 랭크마다 +4%p", .12f, .04f),
            new PlayerTraitDefinition("escort.repair", PlayerTraitCategory.Escort,
                "현장 정비", "화물 수리량 +20%, 이후 랭크마다 +5%p", .20f, .05f,
                "escort.lead", 3)
        };

        public static PlayerTraitDefinition Find(string id)
        {
            foreach (var definition in All)
                if (string.Equals(definition.Id, id, StringComparison.Ordinal)) return definition;
            return null;
        }
    }
}
