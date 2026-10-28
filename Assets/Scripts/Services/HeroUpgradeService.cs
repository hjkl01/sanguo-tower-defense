using System;
using System.Collections.Generic;
using System.Linq;
using SanguoTowerDefense.Domain;

namespace SanguoTowerDefense.Services
{
    /// <summary>Consumes copies of the same hero at the same level to level up the target.</summary>
    public sealed class HeroUpgradeService
    {
        private readonly HeroRoster roster;
        private readonly int materialsRequired;
        private readonly int maxLevel;

        public HeroUpgradeService(HeroRoster roster, int materialsRequired = 2, int maxLevel = 100)
        {
            this.roster = roster ?? throw new ArgumentNullException(nameof(roster));
            if (materialsRequired < 1) throw new ArgumentOutOfRangeException(nameof(materialsRequired));
            if (maxLevel < 1) throw new ArgumentOutOfRangeException(nameof(maxLevel));
            this.materialsRequired = materialsRequired;
            this.maxLevel = maxLevel;
        }

        public int MaterialsRequired => materialsRequired;

        public bool TryUpgrade(string targetInstanceId, IEnumerable<string> materialInstanceIds, out string error)
        {
            error = null;
            var target = roster.Find(targetInstanceId);
            if (target == null) { error = "目标武将不存在。"; return false; }
            if (target.Level >= maxLevel) { error = "武将已达到等级上限 " + maxLevel + "。"; return false; }

            var ids = materialInstanceIds == null ? new List<string>() : materialInstanceIds.ToList();
            if (ids.Count != materialsRequired) { error = "本次升级需要恰好 " + materialsRequired + " 个材料武将。"; return false; }
            if (ids.Distinct(StringComparer.Ordinal).Count() != ids.Count) { error = "材料武将不能重复选择。"; return false; }

            foreach (var id in ids)
            {
                var material = roster.Find(id);
                if (material == null) { error = "有材料武将不存在或已被消耗。"; return false; }
                if (material.InstanceId == target.InstanceId) { error = "目标武将不能作为升级材料。"; return false; }
                if (material.HeroId != target.HeroId) { error = "只能消耗同名武将作为材料。"; return false; }
                if (material.Level != target.Level) { error = "目标武将与材料武将必须处于同一等级。"; return false; }
            }

            // All validation completes before mutation, preventing partial upgrades.
            foreach (var id in ids) roster.Remove(id);
            target.IncreaseLevel();
            return true;
        }
    }
}
