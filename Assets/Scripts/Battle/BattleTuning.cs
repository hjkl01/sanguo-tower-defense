namespace SanguoTowerDefense.Battle
{
    /// <summary>
    /// Single source of truth for battle and economy balance values.
    /// The UI reads these constants instead of duplicating hard-coded numbers.
    /// </summary>
    public static class BattleTuning
    {
        public const int MaxWaves = 5;
        public const int StartingBaseHealth = 20;
        public const int SlotCount = 5;

        // A new save starts with enough coins for one basic draw so the very first
        // battle is not a dead-end when the starter lineup misses an enemy slot.
        public const int StartingCoins = 20;

        // Fixed coin bonus per cleared wave, on top of kill rewards.
        public const int WaveClearBonus = 10;

        // Linear level scaling used by both the battle sync and the roster UI.
        public const float LevelAttackGrowth = 0.25f;
        public const float LevelRangeGrowth = 0.03f;

        public static int AttackAtLevel(int baseAttack, int level)
        {
            var scaled = baseAttack * (1f + (level - 1) * LevelAttackGrowth);
            return scaled < 1f ? 1 : UnityEngine.Mathf.RoundToInt(scaled);
        }

        public static float RangeAtLevel(float baseRange, int level)
        {
            return baseRange * (1f + (level - 1) * LevelRangeGrowth);
        }
    }
}
