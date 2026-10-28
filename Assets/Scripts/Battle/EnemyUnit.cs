using System;
using UnityEngine;

namespace SanguoTowerDefense.Battle
{
    /// <summary>Runtime enemy that follows a configured path and reports death or base arrival once.</summary>
    public sealed class EnemyUnit : MonoBehaviour
    {
        private Vector3[] path;
        private float health;
        private float speed;
        private int reward;
        private int pathIndex;
        private Action<EnemyUnit, int> killed;
        private Action<EnemyUnit> reachedBase;
        private bool resolved;

        public bool IsAlive => !resolved && health > 0f;
        public float Health => Mathf.Max(0f, health);

        /// <summary>Normalized 0..1 progress along the path; used for "furthest along" targeting.</summary>
        public float PathProgress
        {
            get
            {
                if (path == null || path.Length < 2) return 0f;
                var total = 0f;
                var done = 0f;
                for (var i = 1; i < path.Length; i++)
                {
                    var segment = Vector3.Distance(path[i - 1], path[i]);
                    total += segment;
                    if (i < pathIndex) done += segment;
                    else if (i == pathIndex)
                        done += Mathf.Clamp01(segment - Vector3.Distance(transform.position, path[i])) ;
                }
                return total <= 0f ? 0f : Mathf.Clamp01(done / total);
            }
        }

        public void Initialize(Vector3[] route, float maxHealth, float moveSpeed, int coinReward,
            Action<EnemyUnit, int> onKilled, Action<EnemyUnit> onReachedBase)
        {
            path = route;
            health = Mathf.Max(1f, maxHealth);
            speed = Mathf.Max(0.1f, moveSpeed);
            reward = Mathf.Max(0, coinReward);
            killed = onKilled;
            reachedBase = onReachedBase;
            pathIndex = 1;
        }

        private void Update()
        {
            if (!IsAlive || path == null || path.Length == 0) return;
            if (pathIndex >= path.Length)
            {
                resolved = true;
                reachedBase?.Invoke(this);
                Destroy(gameObject);
                return;
            }

            transform.position = Vector3.MoveTowards(transform.position, path[pathIndex], speed * Time.deltaTime);
            if (Vector3.Distance(transform.position, path[pathIndex]) < 0.03f) pathIndex++;
        }

        public void TakeDamage(int damage)
        {
            if (!IsAlive || damage <= 0) return;
            health -= damage;
            if (health <= 0f)
            {
                resolved = true;
                killed?.Invoke(this, reward);
                Destroy(gameObject);
            }
        }
    }
}
