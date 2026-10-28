using System;

namespace SanguoTowerDefense.Domain
{
    /// <summary>A single owned copy of a hero with its own level and instance ID.</summary>
    [Serializable]
    public sealed class HeroInstance
    {
        public string InstanceId { get; private set; }
        public string HeroId { get; private set; }
        public int Level { get; private set; }

        public HeroInstance(string heroId, int level = 1, string instanceId = null)
        {
            if (string.IsNullOrWhiteSpace(heroId))
                throw new ArgumentException("heroId cannot be empty.", nameof(heroId));
            if (level < 1) throw new ArgumentOutOfRangeException(nameof(level));
            InstanceId = string.IsNullOrWhiteSpace(instanceId) ? Guid.NewGuid().ToString("N") : instanceId;
            HeroId = heroId;
            Level = level;
        }

        public void IncreaseLevel(int amount = 1)
        {
            if (amount < 1) throw new ArgumentOutOfRangeException(nameof(amount));
            checked { Level += amount; }
        }
    }
}
