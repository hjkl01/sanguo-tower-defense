#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using SanguoTowerDefense.Data;

namespace SanguoTowerDefense.Editor
{
    [Serializable]
    internal sealed class HeroCatalogFile { public int version; public HeroCatalogEntry[] heroes; }

    [Serializable]
    internal sealed class HeroCatalogEntry
    {
        public string heroId;
        public string displayName;
        public string faction;
        public string rarity;
        public int baseAttack;
        public int baseHealth;
        public float attackInterval;
        public float attackRange;
        public string description;
    }

    /// <summary>Imports the JSON hero catalog into editable ScriptableObject assets.</summary>
    public static class HeroCatalogImporter
    {
        private const string SourcePath = "Assets/Resources/Data/hero_catalog.json";
        private const string OutputPath = "Assets/Resources/Heroes";

        [MenuItem("Sanguo/Import Hero Catalog")]
        public static void Import()
        {
            if (!File.Exists(SourcePath))
            {
                Debug.LogError("Hero catalog not found: " + SourcePath);
                return;
            }

            var catalog = JsonUtility.FromJson<HeroCatalogFile>(File.ReadAllText(SourcePath));
            if (catalog == null || catalog.heroes == null || catalog.heroes.Length == 0)
            {
                Debug.LogError("Hero catalog is empty or invalid.");
                return;
            }

            Directory.CreateDirectory(OutputPath);
            var createdOrUpdated = 0;
            foreach (var entry in catalog.heroes)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.heroId) ||
                    string.IsNullOrWhiteSpace(entry.displayName))
                {
                    Debug.LogWarning("Skipped hero entry with missing ID or name.");
                    continue;
                }

                var path = OutputPath + "/" + entry.heroId + ".asset";
                var asset = AssetDatabase.LoadAssetAtPath<HeroDefinition>(path);
                if (asset == null)
                {
                    asset = ScriptableObject.CreateInstance<HeroDefinition>();
                    AssetDatabase.CreateAsset(asset, path);
                }

                var serialized = new SerializedObject(asset);
                serialized.FindProperty("heroId").stringValue = entry.heroId;
                serialized.FindProperty("displayName").stringValue = entry.displayName;
                if (!Enum.TryParse(entry.faction, true, out HeroFaction faction)) faction = HeroFaction.Qun;
                if (!Enum.TryParse(entry.rarity, true, out HeroRarity rarity)) rarity = HeroRarity.Common;
                serialized.FindProperty("faction").enumValueIndex = (int)faction;
                serialized.FindProperty("rarity").enumValueIndex = (int)rarity;
                serialized.FindProperty("baseAttack").intValue = Mathf.Max(1, entry.baseAttack);
                serialized.FindProperty("baseHealth").intValue = Mathf.Max(0, entry.baseHealth);
                serialized.FindProperty("attackInterval").floatValue = Mathf.Max(0.1f, entry.attackInterval);
                serialized.FindProperty("attackRange").floatValue = Mathf.Max(0f, entry.attackRange);
                serialized.FindProperty("description").stringValue = entry.description ?? string.Empty;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(asset);
                createdOrUpdated++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Hero catalog import complete: {createdOrUpdated} heroes.");
        }
    }
}
#endif
