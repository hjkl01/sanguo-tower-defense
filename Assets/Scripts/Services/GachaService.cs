using System;
using System.Collections.Generic;
using SanguoTowerDefense.Data;
using SanguoTowerDefense.Domain;
using UnityEngine;

namespace SanguoTowerDefense.Services
{
    [Serializable]
    public sealed class GachaEntry
    {
        public HeroDefinition Hero;
        [Min(1)] public int Weight = 1;
    }

    /// <summary>Weighted hero draw service. Add hero assets to the pool without changing code.</summary>
    public sealed class GachaService
    {
        private readonly HeroRoster roster;
        private readonly IReadOnlyList<GachaEntry> entries;
        private readonly System.Random random;

        public GachaService(HeroRoster roster, IReadOnlyList<GachaEntry> entries, int? seed = null)
        {
            this.roster = roster ?? throw new ArgumentNullException(nameof(roster));
            this.entries = entries ?? throw new ArgumentNullException(nameof(entries));
            random = seed.HasValue ? new System.Random(seed.Value) : new System.Random();
        }

        public HeroInstance Draw()
        {
            var total = 0;
            foreach (var entry in entries)
                if (entry != null && entry.Hero != null && entry.Weight > 0)
                    checked { total += entry.Weight; }

            if (total <= 0) throw new InvalidOperationException("抽卡池为空，或没有有效的正权重武将。");
            var roll = random.Next(total);

            foreach (var entry in entries)
            {
                if (entry == null || entry.Hero == null || entry.Weight <= 0) continue;
                if (roll < entry.Weight)
                {
                    var copy = new HeroInstance(entry.Hero.HeroId);
                    roster.Add(copy);
                    return copy;
                }
                roll -= entry.Weight;
            }
            throw new InvalidOperationException("抽卡权重计算异常。");
        }

        public IReadOnlyList<HeroInstance> DrawMany(int count)
        {
            if (count < 1) throw new ArgumentOutOfRangeException(nameof(count));
            var result = new List<HeroInstance>(count);
            for (var i = 0; i < count; i++) result.Add(Draw());
            return result;
        }
    }
}
