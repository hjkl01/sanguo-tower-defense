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

        // Enemy curve: hp = BaseEnemyHealth + Wave * EnemyHealthPerWave,
        // speed = BaseEnemySpeed + Wave * EnemySpeedPerWave, reward = BaseEnemyReward + Wave.
        public const int BaseEnemyHealth = 10;
        public const int EnemyHealthPerWave = 6;
        public const float BaseEnemySpeed = 0.9f;
        public const float EnemySpeedPerWave = 0.12f;
        public const int BaseEnemyReward = 4;
        public const int MaxSpawnInterval = 11;   // x0.1 seconds, keeps spacing integers below.

        /// <summary>Enemies in a wave: 5 on wave 1, growing by one per wave.</summary>
        public static int EnemiesInWave(int wave) => 4 + Mathf.Max(1, wave);

        public static int EnemyHealthAtWave(int wave) => BaseEnemyHealth + wave * EnemyHealthPerWave;

        public static float EnemySpeedAtWave(int wave) => BaseEnemySpeed + wave * EnemySpeedPerWave;

        public static int EnemyRewardAtWave(int wave) => BaseEnemyReward + wave;

        /// <summary>Seconds between spawns within a wave (tightens as waves progress).</summary>
        public static float SpawnIntervalAtWave(int wave) =>
            Mathf.Min(MaxSpawnInterval, 13 - wave) * 0.1f;

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
