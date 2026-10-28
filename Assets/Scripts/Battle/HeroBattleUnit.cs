using System;
using System.Collections.Generic;
using UnityEngine;

namespace SanguoTowerDefense.Battle
{
    /// <summary>One fixed defense slot. It automatically targets the enemy furthest along a path within range.</summary>
    public sealed class HeroBattleUnit : MonoBehaviour
    {
        private BattleHeroConfig hero;
        private Func<IReadOnlyList<EnemyUnit>> enemyProvider;
        private SpriteRenderer spriteRenderer;
        private Sprite sprite;
        // Cooldown starts "cold" so (re)deploying a hero cannot reset-and-fire for free DPS.
        private float attackTimer;

        public void Initialize(Sprite unitSprite)
        {
            sprite = unitSprite;
            spriteRenderer = GetComponent<SpriteRenderer>();
            ClearHero();
        }

        public void Configure(BattleHeroConfig config, Func<IReadOnlyList<EnemyUnit>> getEnemies)
        {
            hero = config;
            enemyProvider = getEnemies;
            var interval = Mathf.Max(0.1f, config.AttackInterval);
            attackTimer = interval * 0.5f;
            if (spriteRenderer != null) spriteRenderer.color = new Color(0.25f, 0.78f, 0.42f, 1f);
            gameObject.name = "HeroSlot_" + (config.SlotIndex + 1) + "_" + config.DisplayName;
        }

        public void ClearHero()
        {
            hero = null;
            enemyProvider = null;
            attackTimer = 0f;
            if (spriteRenderer != null) spriteRenderer.color = new Color(0.25f, 0.48f, 0.82f, 1f);
        }

        private void Update()
        {
            if (hero == null || enemyProvider == null) return;
            attackTimer -= Time.deltaTime;
            if (attackTimer > 0f) return;

            var enemies = enemyProvider();
            EnemyUnit target = null;
            var bestProgress = -1f;
            var range = Mathf.Max(0.1f, hero.AttackRange);
            if (enemies != null)
            {
                foreach (var enemy in enemies)
                {
                    // Destroyed Unity objects do not compare equal to null as managed references;
                    // check both so a stale list entry can never throw here.
                    if (enemy == null || enemy.Equals(null) || !enemy.IsAlive) continue;
                    var distance = Vector2.Distance(transform.position, enemy.transform.position);
                    if (distance > range) continue;
                    // Prioritize the enemy closest to the base instead of the nearest one.
                    var progress = enemy.PathProgress;
                    if (progress > bestProgress)
                    {
                        bestProgress = progress;
                        target = enemy;
                    }
                }
            }

            if (target == null)
            {
                // Keep the cooldown ready to fire the moment a target appears.
                attackTimer = 0f;
                return;
            }
            target.TakeDamage(Mathf.Max(1, hero.Attack));
            attackTimer = Mathf.Max(0.1f, hero.AttackInterval);
        }
    }
}
