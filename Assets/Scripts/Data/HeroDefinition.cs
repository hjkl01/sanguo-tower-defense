using UnityEngine;

namespace SanguoTowerDefense.Data
{
    public enum HeroFaction { Wei, Shu, Wu, Qun, Jin }
    public enum HeroRarity { Common, Rare, Epic, Legendary }

    /// <summary>Static configuration for a hero type; create one asset per hero in Unity.</summary>
    [CreateAssetMenu(fileName = "HeroDefinition", menuName = "Sanguo/Hero Definition")]
    public sealed class HeroDefinition : ScriptableObject
    {
        [SerializeField] private string heroId;
        [SerializeField] private string displayName;
        [SerializeField] private HeroFaction faction;
        [SerializeField] private HeroRarity rarity;
        [SerializeField, Min(1)] private int baseAttack = 10;
        [SerializeField, Min(0)] private int baseHealth = 100;
        [SerializeField, Min(0.1f)] private float attackInterval = 1f;
        [SerializeField, Min(0f)] private float attackRange = 2f;
        [SerializeField, TextArea] private string description;

        public string HeroId => heroId;
        public string DisplayName => displayName;
        public HeroFaction Faction => faction;
        public HeroRarity Rarity => rarity;
        public int BaseAttack => baseAttack;
        public int BaseHealth => baseHealth;
        public float AttackInterval => attackInterval;
        public float AttackRange => attackRange;
        public string Description => description;

        public static HeroDefinition CreateRuntime(string id, string name, HeroFaction heroFaction,
            HeroRarity heroRarity, int attack, int health, float interval, float range, string details)
        {
            var definition = CreateInstance<HeroDefinition>();
            definition.heroId = id;
            definition.displayName = name;
            definition.faction = heroFaction;
            definition.rarity = heroRarity;
            definition.baseAttack = Mathf.Max(1, attack);
            definition.baseHealth = Mathf.Max(0, health);
            definition.attackInterval = Mathf.Max(0.1f, interval);
            definition.attackRange = Mathf.Max(0f, range);
            definition.description = details ?? string.Empty;
            return definition;
        }

        private void OnValidate()
        {
            heroId = heroId == null ? string.Empty : heroId.Trim();
            displayName = displayName == null ? string.Empty : displayName.Trim();
            baseAttack = Mathf.Max(1, baseAttack);
            baseHealth = Mathf.Max(0, baseHealth);
            attackInterval = Mathf.Max(0.1f, attackInterval);
            attackRange = Mathf.Max(0f, attackRange);
        }
    }
}
