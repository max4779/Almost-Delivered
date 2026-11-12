using System;
using System.Collections.Generic;
using UnityEngine;

namespace AlmostDelivered.Frontend
{
    [Serializable]
    public sealed class PlayerTraitRank
    {
        public string id;
        public int rank;
    }

    [Serializable]
    public sealed class PlayerTraitProgress
    {
        public int level = 1;
        public List<PlayerTraitRank> ranks = new List<PlayerTraitRank>();

        public int SpentPoints
        {
            get
            {
                var sum = 0;
                foreach (var definition in PlayerTraitCatalog.All) sum += Rank(definition.Id);
                return sum;
            }
        }

        public int AvailablePoints => Mathf.Max(0,
            Mathf.Min(PlayerTraitCatalog.TotalPointLimit, Mathf.Max(1, level)) - SpentPoints);

        public int Rank(string id)
        {
            if (ranks == null || PlayerTraitCatalog.Find(id) == null) return 0;
            foreach (var entry in ranks)
                if (entry != null && string.Equals(entry.id, id, StringComparison.Ordinal))
                    return Mathf.Clamp(entry.rank, 0, PlayerTraitCatalog.MaximumRank);
            return 0;
        }

        public int CategoryPoints(PlayerTraitCategory category)
        {
            var sum = 0;
            foreach (var definition in PlayerTraitCatalog.All)
                if (definition.Category == category) sum += Rank(definition.Id);
            return sum;
        }

        public bool CanInvest(string id)
        {
            var definition = PlayerTraitCatalog.Find(id);
            return definition != null && Rank(id) < PlayerTraitCatalog.MaximumRank &&
                   (definition.RequiredTraitId == null || Rank(definition.RequiredTraitId) >= definition.RequiredRank) &&
                   CategoryPoints(definition.Category) < PlayerTraitCatalog.CategoryPointLimit &&
                   AvailablePoints > 0;
        }

        public bool TryInvest(string id)
        {
            if (!CanInvest(id)) return false;
            if (ranks == null) ranks = new List<PlayerTraitRank>();
            foreach (var entry in ranks)
            {
                if (entry == null || !string.Equals(entry.id, id, StringComparison.Ordinal)) continue;
                entry.rank++;
                return true;
            }
            ranks.Add(new PlayerTraitRank { id = id, rank = 1 });
            return true;
        }

        public bool GrantLevel()
        {
            if (level >= PlayerTraitCatalog.TotalPointLimit) return false;
            level = Mathf.Max(1, level) + 1;
            return true;
        }

        public void ResetRanks() => ranks = new List<PlayerTraitRank>();

        public PlayerTraitProgress Copy() => JsonUtility.FromJson<PlayerTraitProgress>(JsonUtility.ToJson(this));
    }
}
