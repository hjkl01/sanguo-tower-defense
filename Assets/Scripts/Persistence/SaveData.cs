using System;
using System.Collections.Generic;

namespace SanguoTowerDefense.Persistence
{
    /// <summary>Versioned, serializable snapshot of all persistent player state.</summary>
    [Serializable]
    public sealed class SaveData
    {
        public const int CurrentVersion = 2;

        public int version = CurrentVersion;
        public int coins;
        public int highestWaveCleared;
        public List<HeroSaveEntry> heroes = new List<HeroSaveEntry>();
        public List<string> formationSlots = new List<string>();

        public static SaveData CreateDefault() => new SaveData { version = CurrentVersion };
    }

    [Serializable]
    public sealed class HeroSaveEntry
    {
        public string instanceId;
        public string heroId;
        public int level;
    }
}
