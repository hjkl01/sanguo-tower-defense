using System;
using System.Collections.Generic;
using System.Linq;
using SanguoTowerDefense.Battle;
using SanguoTowerDefense.Data;
using SanguoTowerDefense.Domain;
using SanguoTowerDefense.Services;
using UnityEngine;
using UnityEngine.UI;

namespace SanguoTowerDefense.App
{
    public sealed class GameBootstrap : MonoBehaviour
    {
        [Serializable] private sealed class CatalogFile { public int version; public CatalogHero[] heroes; }
        [Serializable] private sealed class CatalogHero
        {
            public string heroId;
            public string displayName;
            public string faction;
            public string rarity;
            public string tier;
            public int baseAttack;
            public int baseHealth;
            public float attackInterval;
            public float attackRange;
            public string description;
        }

        private HeroRoster roster;
        private GachaService gacha;
        private HeroUpgradeService upgrades;
        private FormationService formation;
        private readonly List<HeroDefinition> definitions = new List<HeroDefinition>();
        private readonly List<GachaEntry> gachaEntries = new List<GachaEntry>();
        private Text statusText;
        private Text rosterText;
        private Text formationText;
        private Button battleButton;
        private BattleController battleController;
        private int currency = 0;
        private int selectedIndex;
        private bool battleRunning;
        private int wave;
        private int baseHealth = 20;

        private void Start()
        {
            Application.targetFrameRate = 60;
            LoadCatalog();
            roster = new HeroRoster();
            upgrades = new HeroUpgradeService(roster, 2, 100);
            formation = new FormationService(roster, 5);
            gacha = new GachaService(roster, gachaEntries);
            BuildUi();
            // A small starter collection lets players try the prototype without first drawing.
            for (var i = 0; i < Mathf.Min(5, definitions.Count); i++)
                roster.Add(new HeroInstance(definitions[i].HeroId));

            battleController = new GameObject("BattleController").AddComponent<BattleController>();
            battleController.Initialize(
                amount => { currency += amount; RefreshUi("击败敌人，获得 " + amount + " 金币。"); },
                health => { baseHealth = health; },
                message => { battleRunning = false; RefreshUi(message); UpdateBattleButton(); },
                message => RefreshUi(message));
            RefreshUi("欢迎来到三国烽火。抽卡、升级武将并部署到固定点位。");
        }

        private void LoadCatalog()
        {
            var catalogAsset = Resources.Load<TextAsset>("Data/hero_catalog");
            if (catalogAsset == null)
            {
                Debug.LogError("Missing Resources/Data/hero_catalog.json");
                return;
            }

            var catalog = JsonUtility.FromJson<CatalogFile>(catalogAsset.text);
            if (catalog == null || catalog.heroes == null)
            {
                Debug.LogError("Hero catalog JSON is invalid.");
                return;
            }

            foreach (var item in catalog.heroes)
            {
                if (item == null || string.IsNullOrWhiteSpace(item.heroId)) continue;
                if (!Enum.TryParse(item.faction, true, out HeroFaction faction)) faction = HeroFaction.Qun;
                if (!Enum.TryParse(item.rarity, true, out HeroRarity rarity)) rarity = HeroRarity.Common;
                var definition = HeroDefinition.CreateRuntime(item.heroId, item.displayName, faction, rarity,
                    item.baseAttack, item.baseHealth, item.attackInterval, item.attackRange, item.description);
                definitions.Add(definition);
                var tier = HeroTier.Basic;
                if (!Enum.TryParse(item.tier, true, out tier))
                    tier = rarity == HeroRarity.Legendary ? HeroTier.Advanced :
                        rarity == HeroRarity.Epic ? HeroTier.Intermediate : HeroTier.Basic;
                gachaEntries.Add(new GachaEntry { Hero = definition, Tier = tier });
            }

            if (definitions.Count == 0) Debug.LogError("Hero catalog contains no usable heroes.");
        }

        private void BuildUi()
        {
            var canvasObject = new GameObject("GameCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = 0.5f;

            var background = CreatePanel(canvas.transform, "Background", new Vector2(0, 0), new Vector2(1, 1),
                new Color(0.075f, 0.09f, 0.13f, 0.34f));
            var header = CreateText(background, "Title", "三国烽火  |  武将收集与塔防原型", 30, TextAnchor.MiddleLeft);
            SetRect(header.rectTransform, new Vector2(0.04f, 0.84f), new Vector2(0.96f, 0.97f));

            statusText = CreateText(background, "Status", "", 18, TextAnchor.MiddleLeft);
            SetRect(statusText.rectTransform, new Vector2(0.04f, 0.76f), new Vector2(0.96f, 0.84f));

            var left = CreatePanel(background, "RosterPanel", new Vector2(0.04f, 0.13f), new Vector2(0.57f, 0.74f),
                new Color(0.12f, 0.15f, 0.2f, 0.76f));
            rosterText = CreateText(left, "Roster", "", 17, TextAnchor.UpperLeft);
            SetRect(rosterText.rectTransform, new Vector2(0.04f, 0.12f), new Vector2(0.96f, 0.96f));

            var right = CreatePanel(background, "FormationPanel", new Vector2(0.60f, 0.13f), new Vector2(0.96f, 0.74f),
                new Color(0.12f, 0.15f, 0.2f, 1));
            formationText = CreateText(right, "Formation", "", 18, TextAnchor.UpperLeft);
            SetRect(formationText.rectTransform, new Vector2(0.06f, 0.25f), new Vector2(0.94f, 0.95f));

            CreateButton(background, "初级卡 20 金币", new Vector2(0.025f, 0.035f), new Vector2(0.17f, 0.105f), () => DrawHero(GachaCardTier.Basic));
            CreateButton(background, "中级卡 50 金币", new Vector2(0.18f, 0.035f), new Vector2(0.325f, 0.105f), () => DrawHero(GachaCardTier.Intermediate));
            CreateButton(background, "高级卡 100 金币", new Vector2(0.335f, 0.035f), new Vector2(0.49f, 0.105f), () => DrawHero(GachaCardTier.Advanced));
            CreateButton(background, "升级选中", new Vector2(0.50f, 0.035f), new Vector2(0.62f, 0.105f), () => UpgradeSelected());
            CreateButton(background, "上阵 / 换将", new Vector2(0.63f, 0.035f), new Vector2(0.75f, 0.105f), () => DeploySelected());
            battleButton = CreateButton(background, "开始战斗", new Vector2(0.76f, 0.035f), new Vector2(0.87f, 0.105f), () => ToggleBattle());
            CreateButton(background, "选择下一名", new Vector2(0.88f, 0.035f), new Vector2(0.975f, 0.105f), () => SelectNext());
        }

        private void DrawHero(GachaCardTier cardTier)
        {
            var cost = GachaService.GetCost(cardTier);
            if (currency < cost) { RefreshUi("金币不足：该卡需要 " + cost + " 金币，请通过战斗击败敌人获取金币。"); return; }
            currency -= cost;
            try
            {
                var hero = gacha.Draw(cardTier);
                selectedIndex = Mathf.Max(0, roster.All.ToList().FindIndex(x => x.InstanceId == hero.InstanceId));
                RefreshUi(CardTierName(cardTier) + "招募获得：" + GetName(hero.HeroId) + "。");
            }
            catch (Exception ex) { RefreshUi("抽卡失败：" + ex.Message); }
        }

        private void UpgradeSelected()
        {
            var selected = GetSelected();
            if (selected == null) { RefreshUi("请先选择武将。"); return; }
            var materials = roster.All.Where(h => h.InstanceId != selected.InstanceId &&
                h.HeroId == selected.HeroId && h.Level == selected.Level).Take(upgrades.MaterialsRequired)
                .Select(h => h.InstanceId).ToArray();
            if (materials.Length != upgrades.MaterialsRequired)
            {
                RefreshUi("材料不足：需要 " + upgrades.MaterialsRequired + " 个同名、同等级副本。");
                return;
            }

            if (upgrades.TryUpgrade(selected.InstanceId, materials, out var error))
                RefreshUi(GetName(selected.HeroId) + " 升至 " + selected.Level + " 级。");
            else RefreshUi(error);
        }

        private void DeploySelected()
        {
            var selected = GetSelected();
            if (selected == null) { RefreshUi("请先选择武将。"); return; }
            var slots = formation.GetSnapshot();
            var index = Array.FindIndex(slots.ToArray(), string.IsNullOrEmpty);
            if (index < 0) index = 0;
            if (formation.TrySwitchHero(index, selected.InstanceId, out var error))
            {
                if (battleRunning) SyncBattleHeroes();
                RefreshUi(GetName(selected.HeroId) + " 已部署到点位 " + (index + 1) + "。");
            }
            else RefreshUi(error);
        }

        private void ToggleBattle()
        {
            if (battleRunning)
            {
                battleController.StopBattle();
                battleRunning = false;
                RefreshUi("已结束当前战斗。");
            }
            else
            {
                SyncBattleHeroes();
                battleRunning = true;
                battleController.StartBattle();
                wave = 1;
                RefreshUi("战斗开始：自动攻击敌人，击杀获得金币。");
            }
            UpdateBattleButton();
        }

        private void SyncBattleHeroes()
        {
            var configs = new List<BattleHeroConfig>();
            var slots = formation.GetSnapshot();
            for (var i = 0; i < slots.Count; i++)
            {
                if (string.IsNullOrEmpty(slots[i])) continue;
                var instance = roster.Find(slots[i]);
                if (instance == null) continue;
                var definition = definitions.FirstOrDefault(x => x.HeroId == instance.HeroId);
                if (definition == null) continue;
                configs.Add(new BattleHeroConfig
                {
                    SlotIndex = i,
                    DisplayName = definition.DisplayName,
                    Attack = Mathf.Max(1, Mathf.RoundToInt(definition.BaseAttack * (1f + (instance.Level - 1) * 0.25f))),
                    AttackInterval = definition.AttackInterval,
                    AttackRange = definition.AttackRange,
                    Level = instance.Level
                });
            }
            battleController.SetHeroes(configs);
        }

        private void UpdateBattleButton()
        {
            if (battleButton == null) return;
            var label = battleButton.GetComponentInChildren<Text>();
            if (label != null) label.text = battleRunning ? "结束战斗" : "开始战斗";
        }

        private void SelectNext()
        {
            var list = roster.All.ToList();
            if (list.Count == 0) { RefreshUi("收藏为空。"); return; }
            selectedIndex = (selectedIndex + 1) % list.Count;
            var selected = list[selectedIndex];
            RefreshUi("当前选中：" + GetName(selected.HeroId) + "。");
        }

        private HeroInstance GetSelected()
        {
            var list = roster.All.ToList();
            if (list.Count == 0) return null;
            selectedIndex = Mathf.Clamp(selectedIndex, 0, list.Count - 1);
            return list[selectedIndex];
        }

        private static string CardTierName(GachaCardTier tier)
        {
            switch (tier)
            {
                case GachaCardTier.Basic: return "初级卡";
                case GachaCardTier.Intermediate: return "中级卡";
                case GachaCardTier.Advanced: return "高级卡";
                default: return "抽卡";
            }
        }

        private string GetName(string heroId)
        {
            var hero = definitions.FirstOrDefault(x => x.HeroId == heroId);
            return hero == null ? heroId : hero.DisplayName;
        }

        private void RefreshUi(string message)
        {
            if (statusText != null) statusText.text = message + "   金币：" + currency + (battleRunning ? "   战斗中" : "");
            if (rosterText != null)
            {
                var list = roster.All.ToList();
                var lines = new List<string> { "武将收藏（" + list.Count + " 份副本）", "选择序号：" + (list.Count == 0 ? "-" : (selectedIndex + 1).ToString()), "" };
                for (var i = 0; i < Mathf.Min(list.Count, 18); i++)
                {
                    var hero = list[i];
                    lines.Add((i == selectedIndex ? "▶ " : "  ") + (i + 1) + ". " + GetName(hero.HeroId) +
                        "  Lv." + hero.Level + "  " + hero.HeroId);
                }
                if (list.Count > 18) lines.Add("……还有 " + (list.Count - 18) + " 份副本");
                rosterText.text = string.Join("\n", lines);
            }

            if (formationText != null)
            {
                var lines = new List<string> { "固定防守点位", "" };
                var slots = formation.GetSnapshot();
                for (var i = 0; i < slots.Count; i++)
                {
                    var instance = string.IsNullOrEmpty(slots[i]) ? null : roster.Find(slots[i]);
                    lines.Add("点位 " + (i + 1) + "：" + (instance == null ? "（空）" : GetName(instance.HeroId) + " Lv." + instance.Level));
                }
                lines.Add("");
                lines.Add("武将总数：" + definitions.Count);
                lines.Add("升级材料：" + upgrades.MaterialsRequired + " 个同名同等级副本");
                lines.Add("抽卡：初级 20 / 中级 50 / 高级 100 金币");
                lines.Add("武将分级：普通/稀有=初级，史诗=中级，传说=高级");
                lines.Add("金币来源：战斗击败敌人获得。");
                lines.Add("基地生命：" + baseHealth + " / 20");
                if (battleController != null && battleRunning)
                    lines.Add("战斗波次：" + battleController.CurrentWave + " / 5，场上敌人：" + battleController.ActiveEnemyCount);
                formationText.text = string.Join("\n", lines);
            }
        }

        private static GameObject CreatePanel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Color color)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(Image));
            obj.transform.SetParent(parent, false);
            var rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin; rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
            obj.GetComponent<Image>().color = color;
            return obj;
        }

        private static Text CreateText(Transform parent, string name, string value, int size, TextAnchor anchor)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(Text));
            obj.transform.SetParent(parent, false);
            var text = obj.GetComponent<Text>();
            text.text = value; text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = size; text.color = new Color(0.93f, 0.93f, 0.9f, 1);
            text.alignment = anchor; text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private static Button CreateButton(Transform parent, string label, Vector2 min, Vector2 max, Action action)
        {
            var obj = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
            obj.transform.SetParent(parent, false);
            var rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
            obj.GetComponent<Image>().color = new Color(0.32f, 0.24f, 0.16f, 1);
            obj.GetComponent<Button>().onClick.AddListener(() => action());
            var text = CreateText(obj.transform, "Label", label, 16, TextAnchor.MiddleCenter);
            SetRect(text.rectTransform, Vector2.zero, Vector2.one);
            return obj.GetComponent<Button>();
        }

        private static void SetRect(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min; rect.anchorMax = max;
            rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
        }
    }
}
