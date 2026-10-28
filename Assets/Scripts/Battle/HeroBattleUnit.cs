using System;
using System.Collections.Generic;
using UnityEngine;

namespace SanguoTowerDefense.Battle
{
    /// <summary>One fixed defense slot. It automatically targets the nearest enemy in range.</summary>
    public sealed class HeroBattleUnit : MonoBehaviour
    {
        private BattleHeroConfig hero;
        private Func<IReadOnlyList<EnemyUnit>> enemyProvider;
        private SpriteRenderer spriteRenderer;
        private Sprite sprite;
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
            attackTimer = 0f;
            if (spriteRenderer != null) spriteRenderer.color = new Color(0.25f, 0.78f, 0.42f, 1f);
            gameObject.name = "HeroSlot_" + (config.SlotIndex + 1) + "_" + config.DisplayName;
        }

        public void ClearHero()
        {
            hero = null;
            enemyProvider = null;
            if (spriteRenderer != null) spriteRenderer.color = new Color(0.25f, 0.48f, 0.82f, 1f);
        }

        private void Update()
        {
            if (hero == null || enemyProvider == null) return;
            attackTimer -= Time.deltaTime;
            if (attackTimer > 0f) return;

            var enemies = enemyProvider();
            EnemyUnit target = null;
            var nearest = float.MaxValue;
            if (enemies != null)
            {
                foreach (var enemy in enemies)
                {
                    if (enemy == null || !enemy.IsAlive) continue;
                    var distance = Vector2.Distance(transform.position, enemy.transform.position);
                    if (distance <= Mathf.Max(0.1f, hero.AttackRange) && distance < nearest)
                    {
                        target = enemy;
                        nearest = distance;
                    }
                }
            }

            if (target == null) return;
            target.TakeDamage(Mathf.Max(1, hero.Attack));
            attackTimer = Mathf.Max(0.1f, hero.AttackInterval);
        }
    }
}
