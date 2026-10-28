using System;
using System.Collections.Generic;
using SanguoTowerDefense.Data;
using SanguoTowerDefense.Domain;
using UnityEngine;

namespace SanguoTowerDefense.Services
{
    public enum HeroTier { Basic, Intermediate, Advanced }
    public enum GachaCardTier { Basic, Intermediate, Advanced }

    [Serializable]
    public sealed class GachaEntry
    {
        public HeroDefinition Hero;
        public HeroTier Tier;
    }

    /// <summary>
    /// Three coin-funded pools. Tier probabilities are normalized per pool, then divided
    /// evenly among heroes in each eligible hero tier so catalog size does not skew balance.
    /// </summary>
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

        public static int GetCost(GachaCardTier cardTier)
        {
            switch (cardTier)
            {
                case GachaCardTier.Basic: return 20;
                case GachaCardTier.Intermediate: return 50;
                case GachaCardTier.Advanced: return 100;
                default: throw new ArgumentOutOfRangeException(nameof(cardTier));
            }
        }

        /// <summary>Ten-pull discount: 10% off ten single draws of the given card tier (e.g. basic 200 -> 180).</summary>
        public static int GetTenPullCost(GachaCardTier cardTier, int count = 10)
        {
            if (count < 1) throw new ArgumentOutOfRangeException(nameof(count));
            var full = checked(GetCost(cardTier) * count);
            return full - full / 10;
        }

        public HeroInstance Draw() => Draw(GachaCardTier.Basic);

        public HeroInstance Draw(GachaCardTier cardTier)
        {
            var candidates = new List<GachaEntry>();
            var counts = new Dictionary<HeroTier, int>();
            foreach (var entry in entries)
            {
                if (entry == null || entry.Hero == null || !IsEligible(cardTier, entry.Tier)) continue;
                candidates.Add(entry);
                counts[entry.Tier] = counts.TryGetValue(entry.Tier, out var count) ? count + 1 : 1;
            }

            if (candidates.Count == 0)
                throw new InvalidOperationException("该抽卡池没有可抽取的武将，请检查武将目录的 tier 配置。");

            var total = 0;
            var weights = new List<int>(candidates.Count);
            foreach (var entry in candidates)
            {
                var weight = GetTierProbability(cardTier, entry.Tier) / counts[entry.Tier];
                weights.Add(weight);
                checked { total += weight; }
            }

            if (total <= 0) throw new InvalidOperationException("抽卡池没有有效权重。");
            var roll = random.Next(total);
            for (var i = 0; i < candidates.Count; i++)
            {
                if (roll < weights[i])
                {
                    var copy = new HeroInstance(candidates[i].Hero.HeroId);
                    roster.Add(copy);
                    return copy;
                }
                roll -= weights[i];
            }

            throw new InvalidOperationException("抽卡权重计算异常。");
        }

        public IReadOnlyList<HeroInstance> DrawMany(int count, GachaCardTier cardTier)
        {
            if (count < 1) throw new ArgumentOutOfRangeException(nameof(count));
            var result = new List<HeroInstance>(count);
            for (var i = 0; i < count; i++) result.Add(Draw(cardTier));
            return result;
        }

        public IReadOnlyList<HeroInstance> DrawMany(int count) => DrawMany(count, GachaCardTier.Basic);

        private static bool IsEligible(GachaCardTier cardTier, HeroTier heroTier)
        {
            switch (cardTier)
            {
                case GachaCardTier.Basic:
                    return heroTier == HeroTier.Basic || heroTier == HeroTier.Intermediate;
                case GachaCardTier.Intermediate:
                    return true;
                case GachaCardTier.Advanced:
                    return heroTier == HeroTier.Intermediate || heroTier == HeroTier.Advanced;
                default:
                    return false;
            }
        }

        // Integer weights keep System.Random deterministic and avoid floating-point boundaries.
        // Basic card: 75% Basic / 25% Intermediate.
        // Intermediate card: 55% Basic / 30% Intermediate / 15% Advanced.
        // Advanced card: 60% Intermediate / 40% Advanced.
        private static int GetTierProbability(GachaCardTier cardTier, HeroTier heroTier)
        {
            switch (cardTier)
            {
                case GachaCardTier.Basic:
                    return heroTier == HeroTier.Basic ? 7500 : heroTier == HeroTier.Intermediate ? 2500 : 0;
                case GachaCardTier.Intermediate:
                    return heroTier == HeroTier.Basic ? 5500 :
                        heroTier == HeroTier.Intermediate ? 3000 : 1500;
                case GachaCardTier.Advanced:
                    return heroTier == HeroTier.Intermediate ? 6000 :
                        heroTier == HeroTier.Advanced ? 4000 : 0;
                default:
                    return 0;
            }
        }
    }
}
