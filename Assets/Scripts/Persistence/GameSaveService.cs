using System;
using System.Collections.Generic;
using System.IO;
using SanguoTowerDefense.Domain;
using UnityEngine;

namespace SanguoTowerDefense.Persistence
{
    /// <summary>
    /// Local JSON save with versioned migration and atomic write (temp file + replace),
    /// so a crash mid-write cannot corrupt the previous save.
    /// </summary>
    public sealed class GameSaveService
    {
        private readonly string savePath;

        public GameSaveService(string savePath = null)
        {
            this.savePath = string.IsNullOrEmpty(savePath)
                ? Path.Combine(Application.persistentDataPath, "sanguo_save.json")
                : savePath;
        }

        public string SavePath => savePath;
        public bool SaveExists => File.Exists(savePath);

        public void Save(SaveData data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            data.version = SaveData.CurrentVersion;
            var json = JsonUtility.ToJson(data, true);
            var tempPath = savePath + ".tmp";
            File.WriteAllText(tempPath, json);
            if (File.Exists(savePath)) File.Delete(savePath);
            File.Move(tempPath, savePath);
        }

        public SaveData Load()
        {
            if (!File.Exists(savePath)) return null;
            try
            {
                var json = File.ReadAllText(savePath);
                var data = JsonUtility.FromJson<SaveData>(json);
                if (data == null) return null;
                Migrate(data);
                return data;
            }
            catch (Exception ex)
            {
                // Never lose gameplay to a broken file: move it aside and start clean.
                Debug.LogError("Save load failed: " + ex.Message);
                try { File.Move(savePath, savePath + ".corrupt-" + DateTime.Now.Ticks); }
                catch (Exception moveEx) { Debug.LogError("Could not quarantine corrupt save: " + moveEx.Message); }
                return null;
            }
        }

        public void DeleteSave()
        {
            if (File.Exists(savePath)) File.Delete(savePath);
        }

        /// <summary>Restores hero copies into the roster. Returns false when no save exists.</summary>
        public bool TryRestore(HeroRoster roster)
        {
            var data = Load();
            if (data == null) return false;
            foreach (var entry in data.heroes)
            {
                if (entry == null || string.IsNullOrEmpty(entry.instanceId) || string.IsNullOrEmpty(entry.heroId)) continue;
                if (roster.Contains(entry.instanceId)) continue;
                try { roster.Add(new HeroInstance(entry.heroId, Math.Max(1, entry.level), entry.instanceId)); }
                catch (Exception ex) { Debug.LogWarning("Skipped bad save hero entry: " + ex.Message); }
            }
            return true;
        }

        public static SaveData FromState(int coins, int highestWaveCleared, HeroRoster roster,
            IReadOnlyList<string> formationSlots)
        {
            var data = SaveData.CreateDefault();
            data.coins = Math.Max(0, coins);
            data.highestWaveCleared = Math.Max(0, highestWaveCleared);
            foreach (var hero in roster.All)
                data.heroes.Add(new HeroSaveEntry
                {
                    instanceId = hero.InstanceId,
                    heroId = hero.HeroId,
                    level = hero.Level
                });
            if (formationSlots != null)
                foreach (var slot in formationSlots)
                    data.formationSlots.Add(slot ?? string.Empty);
            return data;
        }

        private static void Migrate(SaveData data)
        {
            // Future schema changes bump CurrentVersion and add case-by-case upgrades here.
            if (data.version < 2)
            {
                if (data.formationSlots == null) data.formationSlots = new List<string>();
                if (data.highestWaveCleared < 0) data.highestWaveCleared = 0;
            }
            data.version = SaveData.CurrentVersion;
        }
    }
}
