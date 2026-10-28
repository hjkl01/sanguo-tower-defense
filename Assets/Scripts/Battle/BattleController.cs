using System;
using System.Collections.Generic;
using UnityEngine;

namespace SanguoTowerDefense.Battle
{
    public sealed class BattleHeroConfig
    {
        public int SlotIndex;
        public string DisplayName;
        public int Attack;
        public float AttackInterval;
        public float AttackRange;
        public int Level;
    }

    /// <summary>Owns path, waves, base health, tower slots, rewards, pause and battle outcome.</summary>
    public sealed class BattleController : MonoBehaviour
    {
        private readonly List<EnemyUnit> enemies = new List<EnemyUnit>();
        private readonly List<HeroBattleUnit> towers = new List<HeroBattleUnit>();
        private readonly Vector3[] path =
        {
            new Vector3(-8f, -2.4f, 0f), new Vector3(-4.8f, -2.4f, 0f),
            new Vector3(-4.8f, 0.7f, 0f), new Vector3(-1.2f, 0.7f, 0f),
            new Vector3(-1.2f, -1.5f, 0f), new Vector3(2.4f, -1.5f, 0f),
            new Vector3(2.4f, 2.1f, 0f), new Vector3(8f, 2.1f, 0f)
        };
        // Positions chosen so every point sits within the attack range of at least one slot.
        private readonly Vector3[] towerPositions =
        {
            new Vector3(-6.4f, -0.6f, 0f), new Vector3(-3.1f, 1.6f, 0f),
            new Vector3(-0.1f, -0.1f, 0f), new Vector3(1.1f, 0.2f, 0f),
            new Vector3(5.0f, 1.0f, 0f)
        };

        private Sprite squareSprite;
        private LineRenderer pathLine;
        private Action<int> onCoinsEarned;
        private Action<int> onBaseHealthChanged;
        private Action<string> onBattleEnded;
        private Action<string> onStatusChanged;
        private bool running;
        private bool paused;
        private int currentWave;
        private int spawnedThisWave;
        private int enemiesThisWave;
        private int baseHealth = BattleTuning.StartingBaseHealth;
        private float spawnTimer;
        private float intermissionTimer;
        private bool waitingForNextWave;
        private bool battleFinished;

        public bool IsRunning => running;
        public bool IsPaused => paused;
        public int CurrentWave => currentWave;
        public int MaxWaveCount => BattleTuning.MaxWaves;
        public int BaseHealth => baseHealth;
        public int ActiveEnemyCount => CountLivingEnemies();

        public void Initialize(Action<int> coinsEarned, Action<int> baseChanged,
            Action<string> battleEnded, Action<string> statusChanged)
        {
            onCoinsEarned = coinsEarned;
            onBaseHealthChanged = baseChanged;
            onBattleEnded = battleEnded;
            onStatusChanged = statusChanged;
            EnsureSceneObjects();
        }

        public void SetHeroes(IReadOnlyList<BattleHeroConfig> configs)
        {
            EnsureSceneObjects();
            for (var i = 0; i < towers.Count; i++) towers[i].ClearHero();
            if (configs == null) return;
            foreach (var config in configs)
            {
                if (config == null || config.SlotIndex < 0 || config.SlotIndex >= towers.Count) continue;
                towers[config.SlotIndex].Configure(config, () => enemies);
            }
        }

        public void StartBattle()
        {
            EnsureSceneObjects();
            ClearEnemies();
            running = true;
            paused = false;
            battleFinished = false;
            currentWave = 1;
            baseHealth = BattleTuning.StartingBaseHealth;
            onBaseHealthChanged?.Invoke(baseHealth);
            BeginWave();
            onStatusChanged?.Invoke("战斗开始：第 1/" + BattleTuning.MaxWaves + " 波。击败敌人可获得金币。");
        }

        public void StopBattle()
        {
            if (!running) return;
            running = false;
            paused = false;
            // Refund deployment so stopping mid-battle never destroys equipped copies.
            for (var i = 0; i < towers.Count; i++) towers[i].ClearHero();
            ClearEnemies();
            waitingForNextWave = false;
            onStatusChanged?.Invoke("战斗已停止，阵容已返还。");
        }

        public void TogglePause()
        {
            if (!running || battleFinished) return;
            paused = !paused;
            onStatusChanged?.Invoke(paused ? "战斗已暂停。" : "战斗继续。");
        }

        private void Update()
        {
            if (!running || paused) return;

            for (var i = enemies.Count - 1; i >= 0; i--)
                if (enemies[i] == null || !enemies[i].IsAlive)
                    enemies.RemoveAt(i);

            if (spawnedThisWave < enemiesThisWave)
            {
                spawnTimer -= Time.deltaTime;
                if (spawnTimer <= 0f)
                {
                    SpawnEnemy();
                    spawnedThisWave++;
                    spawnTimer = Mathf.Max(0.45f, 1.1f - currentWave * 0.08f);
                }
                return;
            }

            if (CountLivingEnemies() > 0) return;

            // Wave cleared: pay the fixed bonus before checking for final victory.
            if (!waitingForNextWave && currentWave >= 1 && spawnedThisWave >= enemiesThisWave)
            {
                onCoinsEarned?.Invoke(BattleTuning.WaveClearBonus);
            }

            if (currentWave >= BattleTuning.MaxWaves)
            {
                running = false;
                battleFinished = true;
                onStatusChanged?.Invoke("第 " + BattleTuning.MaxWaves + " 波完成，获得 " + BattleTuning.WaveClearBonus + " 金币奖励。");
                onBattleEnded?.Invoke("胜利！完成全部 " + BattleTuning.MaxWaves + " 波次。");
                return;
            }

            if (!waitingForNextWave)
            {
                waitingForNextWave = true;
                intermissionTimer = 2.5f;
                onStatusChanged?.Invoke("第 " + currentWave + " 波完成，获得 " + BattleTuning.WaveClearBonus + " 金币，下一波即将开始。");
            }
            else
            {
                intermissionTimer -= Time.deltaTime;
                if (intermissionTimer <= 0f)
                {
                    currentWave++;
                    BeginWave();
                    onStatusChanged?.Invoke("第 " + currentWave + "/" + BattleTuning.MaxWaves + " 波开始。");
                }
            }
        }

        private void BeginWave()
        {
            spawnedThisWave = 0;
            enemiesThisWave = 4 + currentWave * 2;
            spawnTimer = 0.1f;
            waitingForNextWave = false;
        }

        private void SpawnEnemy()
        {
            var enemyObject = new GameObject("Enemy_W" + currentWave + "_" + (spawnedThisWave + 1));
            enemyObject.transform.position = path[0];
            var renderer = enemyObject.AddComponent<SpriteRenderer>();
            renderer.sprite = squareSprite;
            renderer.color = new Color(0.82f, 0.22f, 0.2f, 1f);
            renderer.sortingOrder = 5;
            enemyObject.transform.localScale = new Vector3(0.38f, 0.38f, 1f);
            var enemy = enemyObject.AddComponent<EnemyUnit>();
            enemy.Initialize(path, 16 + currentWave * 8, 1.05f + currentWave * 0.12f,
                3 + currentWave, HandleEnemyKilled, HandleEnemyReachedBase);
            enemies.Add(enemy);
        }

        private void HandleEnemyKilled(EnemyUnit enemy, int reward)
        {
            onCoinsEarned?.Invoke(reward);
        }

        private void HandleEnemyReachedBase(EnemyUnit enemy)
        {
            baseHealth = Mathf.Max(0, baseHealth - 1);
            onBaseHealthChanged?.Invoke(baseHealth);
            if (baseHealth <= 0)
            {
                running = false;
                battleFinished = true;
                ClearEnemies();
                onBattleEnded?.Invoke("失败：基地生命归零。");
            }
            else
            {
                onStatusChanged?.Invoke("敌人抵达终点，基地生命 -1。");
            }
        }

        private int CountLivingEnemies()
        {
            var count = 0;
            foreach (var enemy in enemies) if (enemy != null && enemy.IsAlive) count++;
            return count;
        }

        private void ClearEnemies()
        {
            foreach (var enemy in enemies) if (enemy != null) Destroy(enemy.gameObject);
            enemies.Clear();
        }

        private void EnsureSceneObjects()
        {
            if (squareSprite == null)
                squareSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f));

            var camera = Camera.main;
            if (camera == null)
            {
                var cameraObject = new GameObject("Main Camera", typeof(Camera));
                cameraObject.tag = "MainCamera";
                camera = cameraObject.GetComponent<Camera>();
            }
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            camera.transform.position = new Vector3(0f, 0f, -10f);
            camera.backgroundColor = new Color(0.08f, 0.12f, 0.11f, 1f);
            camera.clearFlags = CameraClearFlags.SolidColor;

            if (pathLine == null)
            {
                var pathObject = new GameObject("EnemyPath");
                pathLine = pathObject.AddComponent<LineRenderer>();
                pathLine.positionCount = path.Length;
                pathLine.SetPositions(path);
                pathLine.startWidth = 0.18f;
                pathLine.endWidth = 0.18f;
                pathLine.material = new Material(Shader.Find("Sprites/Default"));
                pathLine.startColor = new Color(0.55f, 0.45f, 0.28f, 1f);
                pathLine.endColor = pathLine.startColor;
                pathLine.sortingOrder = 1;
            }

            while (towers.Count < towerPositions.Length)
            {
                var index = towers.Count;
                var towerObject = new GameObject("HeroSlot_" + (index + 1));
                towerObject.transform.position = towerPositions[index];
                towerObject.transform.localScale = new Vector3(0.58f, 0.58f, 1f);
                var renderer = towerObject.AddComponent<SpriteRenderer>();
                renderer.sprite = squareSprite;
                renderer.color = new Color(0.25f, 0.48f, 0.82f, 1f);
                renderer.sortingOrder = 4;
                var tower = towerObject.AddComponent<HeroBattleUnit>();
                tower.Initialize(squareSprite);
                towers.Add(tower);
            }
        }
    }
}
