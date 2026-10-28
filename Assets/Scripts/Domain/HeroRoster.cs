using System;
using System.Collections.Generic;
using System.Linq;

namespace SanguoTowerDefense.Domain
{
    /// <summary>Stores hero copies; there is no fixed hero-count limit.</summary>
    public sealed class HeroRoster
    {
        private readonly Dictionary<string, HeroInstance> heroes =
            new Dictionary<string, HeroInstance>(StringComparer.Ordinal);

        public IReadOnlyCollection<HeroInstance> All => heroes.Values.ToArray();
        public int Count => heroes.Count;
        public bool Contains(string instanceId) => heroes.ContainsKey(instanceId);

        public HeroInstance Find(string instanceId)
        {
            if (instanceId == null) return null;
            heroes.TryGetValue(instanceId, out var instance);
            return instance;
        }

        public void Add(HeroInstance instance)
        {
            if (instance == null) throw new ArgumentNullException(nameof(instance));
            if (heroes.ContainsKey(instance.InstanceId))
                throw new InvalidOperationException("Duplicate hero instance ID: " + instance.InstanceId);
            heroes.Add(instance.InstanceId, instance);
        }

        public bool Remove(string instanceId) => heroes.Remove(instanceId);

        public int CountCopies(string heroId, int level) =>
            heroes.Values.Count(h => h.HeroId == heroId && h.Level == level);
    }
}
