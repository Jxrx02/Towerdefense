using System.Collections.Generic;
using ScriptableObjects;
using UnityEngine;

namespace TowerDefense
{
    /// <summary>Was ein Gegner aktiv angreifen darf. Die Reihenfolge in EnemyConfig.targetPriority ist die Priorität.</summary>
    public enum TargetCategory
    {
        Wall,
        Tower,
        Hero
    }

    /// <summary>
    /// Wählt das Aggro-Ziel eines Gegners: höchste Priorität innerhalb der Aggro-Reichweite, bei Gleichstand das nächste.
    /// Der Weg dorthin kommt aus PathfindingManager.FindPathToTarget.
    /// Mauern, die den Weg zum Levelende versperren, werden unabhängig davon vom PathfindingManager gemeldet.
    /// </summary>
    public static class EnemyTargeting
    {
        /// <param name="ignoreUntil">Ziele, die vor kurzem nicht erreichbar waren. Sie werden übersprungen,
        /// solange sie nicht in Angriffsreichweite sind (Fernkämpfer können über Mauern schießen).</param>
        public static Tower FindAggroTarget(Vector3 origin, EnemyConfig cfg, Dictionary<Tower, float> ignoreUntil = null)
        {
            if (cfg == null || cfg.targetPriority == null || cfg.targetPriority.Length == 0)
                return null;

            Tower best = null;
            int bestRank = int.MaxValue;
            float bestSqrDist = float.MaxValue;

            float aggroSqr = cfg.aggroRange * cfg.aggroRange;
            float attackSqr = cfg.attackRange * cfg.attackRange;
            float now = Time.time;

            var all = Tower.Active;
            for (int i = 0; i < all.Count; i++)
            {
                Tower t = all[i];
                if (t == null) continue;

                // Günstige Prüfungen zuerst (es gibt viele Mauersegmente in der Liste)
                int rank = cfg.GetPriorityRank(t.Category);
                if (rank < 0) continue;

                float sqr = ((Vector2)(t.transform.position - origin)).sqrMagnitude;
                if (sqr > aggroSqr) continue;

                if (!t.IsTargetable) continue;

                if (sqr > attackSqr && ignoreUntil != null &&
                    ignoreUntil.TryGetValue(t, out float until) && now < until)
                    continue;

                if (rank < bestRank || (rank == bestRank && sqr < bestSqrDist))
                {
                    best = t;
                    bestRank = rank;
                    bestSqrDist = sqr;
                }
            }

            return best;
        }
    }
}