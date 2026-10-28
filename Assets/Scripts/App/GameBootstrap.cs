using System;
using System.Collections.Generic;
using System.Linq;
using SanguoTowerDefense.Battle;
using SanguoTowerDefense.Data;
using SanguoTowerDefense.Domain;
using SanguoTowerDefense.Persistence;
using SanguoTowerDefense.Services;
using UnityEngine;
using UnityEngine.UI;

namespace SanguoTowerDefense.App
{
    /// <summary>Prototype UI bootstrap: gacha, roster paging, synthesis guard, formation, battle and saves.</summary>
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

        private const int RosterPageSize = 10;

        private HeroRoster roster;
        private GachaService gacha;
        private HeroUpgradeService upgrades;
        private FormationService formation;
        private GameSaveService saveService;
        private readonly List<HeroDefinition> definitions = new List<HeroDefinition>();
        private readonly Dictionary<string, HeroDefinition> definitionById = new Dictionary<string, HeroDefinition>(StringComparer.Ordinal);
        private readonly List<GachaEntry> gachaEntries = new List<GachaEntry>();
        private Text statusText;
        private Text rosterText;
        private Text formationText;
        private Button battleButton;
        private BattleController battleController;
        private int currency = BattleTuning.StartingCoins;
        private int highestWaveCleared;
        private int selectedIndex;
        private int rosterPage;
        private bool battleRunning;
        private bool hasSave;
        private string lastMessage = "";

        private void Start()
        {
            Application.targetFrameRate = 60;
            LoadCatalog();
            roster = new HeroRoster();
            upgrades = new HeroUpgradeService(roster, 2, 100);
            formation = new FormationService(roster, BattleTuning.SlotCount);
            gacha = new GachaService(roster, gachaEntries);
            saveService = new GameSaveService();

            var data = saveService.Load();
            if (data != null)
            {
                hasSave = true;
                currency = data.coins;
                highestWaveCleared = data.highestWaveCleared;
                foreach (var entry in data.heroes)
                {
                    if (entry == null || string.IsNullOrEmpty(entry.instanceId) || string.IsNullOrEmpty(entry.heroId)) continue;
                    if (!definitionById.ContainsKey(entry.heroId) || roster.Contains(entry.instanceId)) continue;
                    try { roster.Add(new HeroInstance(entry.heroId, Math.Max(1, entry.level), entry.instanceId)); }
                    catch (Exception ex) { Debug.LogWarning("Skipped bad save hero entry: " + ex.Message); }
                }
                for (var i = 0; i < Math.Min(formation.SlotCount, data.formationSlots.Count); i++)
                {
                    var id = data.formationSlots[i];
                    if (!string.IsNullOrEmpty(id) && roster.Contains(id))
                        formation.TrySetHero(i, id, out _);
                }
            }
            else
            {
                // A small starter collection lets new players try the prototype without first drawing.
                for (var i = 0; i < Mathf.Min(BattleTuning.SlotCount, definitions.Count); i++)
                    roster.Add(new HeroInstance(definitions[i].HeroId));
            }

            BuildUi();

            battleController = new GameObject("BattleController").AddComponent<BattleController>();
            battleController.Initialize(
                amount => { currency += amount; RefreshUi(null); },
                health => RefreshUi(null),
                message =>
                {
                    battleRunning = false;
                    SyncBattleHeroes();
                    UpdateBattleButton();
                    SaveGame();
                    RefreshUi(message);
                },
                message => RefreshUi(message));
            RefreshUi(hasSave ? "欢迎回来，进度已从本地存档恢复。" : "欢迎来到三国烽火。抽卡、升级武将并部署到固定点位。");
        }

        private void OnApplicationPause(bool paused) { if (paused && hasSave) SaveGame(); }
        private void OnApplicationQuit() { SaveGame(); }

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
                if (definitionById.ContainsKey(item.heroId))
                {
                    Debug.LogWarning("Duplicate heroId in catalog skipped: " + item.heroId);
                    continue;
                }
                if (!Enum.TryParse(item.faction, true, out HeroFaction faction)) faction = HeroFaction.Qun;
                if (!Enum.TryParse(item.rarity, true, out HeroRarity rarity)) rarity = HeroRarity.Common;
                var definition = HeroDefinition.CreateRuntime(item.heroId, item.displayName, faction, rarity,
                    item.baseAttack, item.baseHealth, item.attackInterval, item.attackRange, item.description);
                definitions.Add(definition);
                definitionById[definition.HeroId] = definition;
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
            SetRect(header.rectTransform, new Vector2(0.04f, 0.86f), new Vector2(0.96f, 0.97f));

            statusText = CreateText(background, "Status", "", 18, TextAnchor.MiddleLeft);
            SetRect(statusText.rectTransform, new Vector2(0.04f, 0.78f), new Vector2(0.96f, 0.855f));

            var left = CreatePanel(background, "RosterPanel", new Vector2(0.04f, 0.16f), new Vector2(0.57f, 0.765f),
                new Color(0.12f, 0.15f, 0.2f, 0.76f));
            rosterText = CreateText(left, "Roster", "", 17, TextAnchor.UpperLeft);
            SetRect(rosterText.rectTransform, new Vector2(0.04f, 0.03f), new Vector2(0.96f, 0.97f));

            var right = CreatePanel(background, "FormationPanel", new Vector2(0.60f, 0.16f), new Vector2(0.96f, 0.765f),
                new Color(0.12f, 0.15f, 0.2f, 1));
            formationText = CreateText(right, "Formation", "", 17, TextAnchor.UpperLeft);
            SetRect(formationText.rectTransform, new Vector2(0.05f, 0.03f), new Vector2(0.95f, 0.97f));

            // Row 1: gacha, paging and save actions.
            CreateButton(background, "初级卡 20金", new Vector2(0.025f, 0.095f), new Vector2(0.155f, 0.15f), () => DrawHero(GachaCardTier.Basic));
            CreateButton(background, "中级卡 50金", new Vector2(0.165f, 0.095f), new Vector2(0.295f, 0.15f), () => DrawHero(GachaCardTier.Intermediate));
            CreateButton(background, "高级卡 100金", new Vector2(0.305f, 0.095f), new Vector2(0.44f, 0.15f), () => DrawHero(GachaCardTier.Advanced));
            CreateButton(background, "十连抽 180金", new Vector2(0.45f, 0.095f), new Vector2(0.585f, 0.15f), () => DrawTenTimes());
            CreateButton(background, "上一页", new Vector2(0.595f, 0.095f), new Vector2(0.675f, 0.15f), () => PageRoster(-1));
            CreateButton(background, "下一页", new Vector2(0.685f, 0.095f), new Vector2(0.765f, 0.15f), () => PageRoster(1));
            CreateButton(background, "存档", new Vector2(0.775f, 0.095f), new Vector2(0.84f, 0.15f), () => SaveGame(true));
            CreateButton(background, "重置存档", new Vector2(0.85f, 0.095f), new Vector2(0.975f, 0.15f), () => ResetSave());

            // Row 2: management and battle actions.
            CreateButton(background, "升级选中", new Vector2(0.025f, 0.02f), new Vector2(0.155f, 0.075f), () => UpgradeSelected());
            CreateButton(background, "上阵/换将", new Vector2(0.165f, 0.02f), new Vector2(0.295f, 0.075f), () => DeploySelected());
            CreateButton(background, "下阵选中", new Vector2(0.305f, 0.02f), new Vector2(0.44f, 0.075f), () => UndeploySelected());
            CreateButton(background, "选择下一名", new Vector2(0.45f, 0.02f), new Vector2(0.585f, 0.075f), () => SelectNext());
            battleButton = CreateButton(background, "开始战斗", new Vector2(0.595f, 0.02f), new Vector2(0.72f, 0.075f), () => ToggleBattle());
            CreateButton(background, "暂停/继续", new Vector2(0.73f, 0.02f), new Vector2(0.85f, 0.075f), () => battleController.TogglePause());
            CreateButton(background, "清空阵容", new Vector2(0.86f, 0.02f), new Vector2(0.975f, 0.075f), () => ClearFormation());
        }

        private void DrawHero(GachaCardTier cardTier)
        {
            if (battleRunning) { RefreshUi("战斗中不能抽卡。"); return; }
            var cost = GachaService.GetCost(cardTier);
            if (currency < cost) { RefreshUi("金币不足：该卡需要 " + cost + " 金币，请通过战斗击败敌人获取金币。"); return; }
            currency -= cost;
            try
            {
                var hero = gacha.Draw(cardTier);
                SelectInstance(hero.InstanceId);
                SaveGame();
                RefreshUi(CardTierName(cardTier) + "招募获得：" + GetName(hero.HeroId) + "。");
            }
            catch (Exception ex) { currency += cost; RefreshUi("抽卡失败：" + ex.Message); }
        }

        private void DrawTenTimes()
        {
            if (battleRunning) { RefreshUi("战斗中不能抽卡。"); return; }
            const int count = 10;
            var cost = GachaService.GetTenPullCost(GachaCardTier.Basic, count);
            if (currency < cost) { RefreshUi("金币不足：十连抽需要 " + cost + " 金币。"); return; }
            currency -= cost;
            try
            {
                var heroes = gacha.DrawMany(count, GachaCardTier.Basic);
                var names = heroes.Select(h => GetName(h.HeroId));
                SelectInstance(heroes[heroes.Count - 1].InstanceId);
                SaveGame();
                RefreshUi("十连抽招募：" + string.Join("、", names));
            }
            catch (Exception ex) { currency += cost; RefreshUi("十连抽失败：" + ex.Message); }
        }

        private void UpgradeSelected()
        {
            if (battleRunning) { RefreshUi("战斗中禁止合成，请先结束战斗。"); return; }
            var selected = GetSelected();
            if (selected == null) { RefreshUi("请先选择武将。"); return; }
            if (IsDeployed(selected.InstanceId)) { RefreshUi("已上阵的武将不能作为合成目标，请先下阵。"); return; }
            var materials = roster.All.Where(h => h.InstanceId != selected.InstanceId &&
                h.HeroId == selected.HeroId && h.Level == selected.Level && !IsDeployed(h.InstanceId))
                .Take(upgrades.MaterialsRequired).Select(h => h.InstanceId).ToArray();
            if (materials.Length != upgrades.MaterialsRequired)
            {
                RefreshUi("材料不足：需要 " + upgrades.MaterialsRequired + " 个同名、同等级且未上阵的副本。");
                return;
            }

            if (upgrades.TryUpgrade(selected.InstanceId, materials, out var error))
            {
                SaveGame();
                RefreshUi(GetName(selected.HeroId) + " 升至 " + selected.Level + " 级（攻击 " +
                    BattleTuning.AttackAtLevel(BaseAttack(selected.HeroId), selected.Level) + "）。");
            }
            else RefreshUi(error);
        }

        private void DeploySelected()
        {
            var selected = GetSelected();
            if (selected == null) { RefreshUi("请先选择武将。"); return; }
            var slots = formation.GetSnapshot();
            var index = Array.FindIndex(slots.ToArray(), string.IsNullOrEmpty);
            if (index < 0) { RefreshUi("所有点位已满，请先“下阵选中”或“清空阵容”。"); return; }
            if (formation.TrySwitchHero(index, selected.InstanceId, out var error))
            {
                if (battleRunning) SyncBattleHeroes();
                SaveGame();
                RefreshUi(GetName(selected.HeroId) + " 已部署到点位 " + (index + 1) + "。");
            }
            else RefreshUi(error);
        }

        private void UndeploySelected()
        {
            var selected = GetSelected();
            if (selected == null) { RefreshUi("请先选择武将。"); return; }
            var slots = formation.GetSnapshot();
            var index = Array.FindIndex(slots.ToArray(), id => id == selected.InstanceId);
            if (index < 0) { RefreshUi("该武将没有在任何点位上。"); return; }
            formation.TrySetHero(index, null, out _);
            if (battleRunning) SyncBattleHeroes();
            SaveGame();
            RefreshUi(GetName(selected.HeroId) + " 已从点位 " + (index + 1) + " 下阵。");
        }

        private void ClearFormation()
        {
            for (var i = 0; i < formation.SlotCount; i++) formation.TrySetHero(i, null, out _);
            if (battleRunning) SyncBattleHeroes();
            SaveGame();
            RefreshUi("阵容已清空。");
        }

        private void ToggleBattle()
        {
            if (battleRunning)
            {
                battleController.StopBattle();
                battleRunning = false;
                SyncBattleHeroes();
                RefreshUi("已结束当前战斗，阵容保持不变。");
            }
            else
            {
                var deployed = formation.GetSnapshot().Count(id => !string.IsNullOrEmpty(id));
                if (deployed == 0) { RefreshUi("至少部署一名武将才能开始战斗。"); return; }
                SyncBattleHeroes();
                battleRunning = true;
                battleController.StartBattle();
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
                if (!definitionById.TryGetValue(instance.HeroId, out var definition)) continue;
                configs.Add(new BattleHeroConfig
                {
                    SlotIndex = i,
                    DisplayName = definition.DisplayName,
                    Attack = BattleTuning.AttackAtLevel(definition.BaseAttack, instance.Level),
                    AttackInterval = definition.AttackInterval,
                    AttackRange = BattleTuning.RangeAtLevel(definition.AttackRange, instance.Level),
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
            var list = OrderedRoster();
            if (list.Count == 0) { RefreshUi("收藏为空。"); return; }
            selectedIndex = (selectedIndex + 1) % list.Count;
            rosterPage = selectedIndex / RosterPageSize;
            RefreshUi("当前选中：" + GetName(list[selectedIndex].HeroId) + "。");
        }

        private void PageRoster(int delta)
        {
            var list = OrderedRoster();
            var pages = Math.Max(1, (list.Count + RosterPageSize - 1) / RosterPageSize);
            rosterPage = Mathf.Clamp(rosterPage + delta, 0, pages - 1);
            selectedIndex = Mathf.Clamp(rosterPage * RosterPageSize, 0, Math.Max(0, list.Count - 1));
            RefreshUi(null);
        }

        private void SelectInstance(string instanceId)
        {
            var list = OrderedRoster();
            var index = list.FindIndex(x => x.InstanceId == instanceId);
            if (index < 0) return;
            selectedIndex = index;
            rosterPage = index / RosterPageSize;
        }

        private HeroInstance GetSelected()
        {
            var list = OrderedRoster();
            if (list.Count == 0) return null;
            selectedIndex = Mathf.Clamp(selectedIndex, 0, list.Count - 1);
            return list[selectedIndex];
        }

        /// <summary>Stable roster ordering so paging and selection survive dictionary iteration changes.</summary>
        private List<HeroInstance> OrderedRoster()
        {
            return roster.All
                .OrderBy(h => h.HeroId, StringComparer.Ordinal)
                .ThenByDescending(h => h.Level)
                .ThenBy(h => h.InstanceId, StringComparer.Ordinal)
                .ToList();
        }

        private bool IsDeployed(string instanceId) => formation.GetSnapshot().Contains(instanceId);

        private int BaseAttack(string heroId) =>
            definitionById.TryGetValue(heroId, out var d) ? d.BaseAttack : 1;

        private void SaveGame(bool showFeedback = false)
        {
            hasSave = true;
            saveService.Save(GameSaveService.FromState(currency, highestWaveCleared, roster, formation.GetSnapshot()));
            if (showFeedback) RefreshUi("已保存到本地存档：" + saveService.SavePath);
        }

        private void ResetSave()
        {
            if (battleRunning) { RefreshUi("战斗中不能重置存档。"); return; }
            saveService.DeleteSave();
            selectedIndex = 0;
            rosterPage = 0;
            battleRunning = false;
            UpdateBattleButton();
            StartNewGame();
            RefreshUi("存档已重置，开始新的游戏。");
        }

        private void StartNewGame()
        {
            roster = new HeroRoster();
            upgrades = new HeroUpgradeService(roster, 2, 100);
            formation = new FormationService(roster, BattleTuning.SlotCount);
            gacha = new GachaService(roster, gachaEntries);
            currency = BattleTuning.StartingCoins;
            highestWaveCleared = 0;
            for (var i = 0; i < Mathf.Min(BattleTuning.SlotCount, definitions.Count); i++)
                roster.Add(new HeroInstance(definitions[i].HeroId));
            SyncBattleHeroes();
            SaveGame();
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

        private string GetName(string heroId) =>
            definitionById.TryGetValue(heroId, out var hero) ? hero.DisplayName : heroId;

        private void RefreshUi(string message)
        {
            if (!string.IsNullOrEmpty(message)) lastMessage = FirstLine(message);
            if (statusText != null)
                statusText.text = lastMessage + "   金币：" + currency + (battleRunning ? "   战斗中" : "");
            if (rosterText != null)
            {
                var list = OrderedRoster();
                var pages = Math.Max(1, (list.Count + RosterPageSize - 1) / RosterPageSize);
                var lines = new List<string>
                {
                    "武将收藏（" + list.Count + " 份副本）  第 " + (rosterPage + 1) + "/" + pages + " 页", ""
                };
                var start = rosterPage * RosterPageSize;
                for (var i = start; i < Mathf.Min(start + RosterPageSize, list.Count); i++)
                {
                    var hero = list[i];
                    var definition = definitionById.TryGetValue(hero.HeroId, out var d) ? d : null;
                    var deployed = IsDeployed(hero.InstanceId);
                    lines.Add((i == selectedIndex ? "> " : "  ") + (i + 1) + ". " +
                        (definition != null ? definition.DisplayName : hero.HeroId) +
                        " Lv." + hero.Level +
                        (definition != null ? " 攻" + BattleTuning.AttackAtLevel(definition.BaseAttack, hero.Level) : "") +
                        (deployed ? " [上阵]" : ""));
                }
                if (list.Count == 0) lines.Add("（收藏为空）");
                rosterText.text = string.Join("\n", lines);
            }

            if (formationText != null)
            {
                var lines = new List<string> { "固定防守点位", "" };
                var slots = formation.GetSnapshot();
                for (var i = 0; i < slots.Count; i++)
                {
                    var instance = string.IsNullOrEmpty(slots[i]) ? null : roster.Find(slots[i]);
                    lines.Add("点位 " + (i + 1) + "：" + (instance == null ? "（空）"
                        : GetName(instance.HeroId) + " Lv." + instance.Level +
                          " 攻" + BattleTuning.AttackAtLevel(BaseAttack(instance.HeroId), instance.Level)));
                }
                lines.Add("");
                lines.Add("武将总数：" + definitions.Count);
                lines.Add("升级材料：" + upgrades.MaterialsRequired + " 个同名同等级副本");
                lines.Add("抽卡：初级 20 / 中级 50 / 高级 100 金币，十连 180");
                lines.Add("基地生命：" + (battleController != null ? battleController.BaseHealth : BattleTuning.StartingBaseHealth) +
                    " / " + BattleTuning.StartingBaseHealth);
                if (battleController != null && battleRunning)
                    lines.Add("战斗波次：" + battleController.CurrentWave + " / " + battleController.MaxWaveCount +
                        "，场上敌人：" + battleController.ActiveEnemyCount +
                        (battleController.IsPaused ? "（已暂停）" : ""));
                lines.Add("最高通关波次：" + highestWaveCleared);
                formationText.text = string.Join("\n", lines);
            }
        }

        private static string FirstLine(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            var index = value.IndexOf('\n');
            return index < 0 ? value : value.Substring(0, index);
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
            text.text = value;
            text.font = EnsureChineseFont();
            text.fontSize = size; text.color = new Color(0.93f, 0.93f, 0.9f, 1);
            text.alignment = anchor; text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        // Arial.ttf cannot render CJK glyphs on most platforms; prefer a system font that can.
        private static Font cachedFont;
        private static Font EnsureChineseFont()
        {
            if (cachedFont != null) return cachedFont;
            var candidates = new[] { "Microsoft YaHei", "PingFang SC", "Hiragino Sans GB", "Noto Sans CJK SC", "SimHei" };
            foreach (var name in candidates)
            {
                try
                {
                    var font = Font.CreateDynamicFontFromOSFont(name, 16);
                    if (font != null) { cachedFont = font; return cachedFont; }
                }
                catch (Exception) { /* font unavailable on this platform */ }
            }
            cachedFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
            return cachedFont;
        }

        private static Button CreateButton(Transform parent, string label, Vector2 min, Vector2 max, Action action)
        {
            var obj = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
            obj.transform.SetParent(parent, false);
            var rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
            obj.GetComponent<Image>().color = new Color(0.32f, 0.24f, 0.16f, 1);
            obj.GetComponent<Button>().onClick.AddListener(() => action());
            var text = CreateText(obj.transform, "Label", label, 15, TextAnchor.MiddleCenter);
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
