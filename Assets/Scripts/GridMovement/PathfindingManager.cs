using System.Collections.Generic;
using UnityEngine;

namespace TowerDefense.GridMovement
{
    public class PathfindingManager : MonoBehaviour
    {
        public static PathfindingManager Instance;

        [Header("Crowding")]
        [Tooltip("Aufschlag pro Gegner, der ein Angriffsfeld schon belegt. Wird auf die Weg-Kosten (gCost) addiert.")]
        [SerializeField] private int crowdPenalty = 30;

        private readonly Dictionary<GridNode, int> slotOccupancy = new();

        // Wiederverwendete Puffer (kein Garbage pro Suche). Nicht reentrant, aber alles läuft im Main-Thread.
        private readonly Dictionary<GridNode, GameObject> goalToTarget = new();
        private readonly HashSet<GridNode> closed = new();
        private readonly List<HeapEntry> heap = new();
        private readonly List<GameObject> singleTarget = new(1);

        private struct HeapEntry
        {
            public GridNode node;
            public int priority;
        }

        private void Awake()
        {
            Instance = this;
        }

        // =====================================================
        // SLOTS (Angriffsfelder)
        // =====================================================

        public void ReserveSlot(GridNode node)
        {
            if (node == null) return;
            slotOccupancy.TryGetValue(node, out int n);
            slotOccupancy[node] = n + 1;
        }

        public void ReleaseSlot(GridNode node)
        {
            if (node == null) return;
            if (!slotOccupancy.TryGetValue(node, out int n)) return;

            if (n <= 1) slotOccupancy.Remove(node);
            else slotOccupancy[node] = n - 1;
        }

        private int CrowdCost(GridNode node, GridNode ownSlot)
        {
            if (node == null) return 0;

            slotOccupancy.TryGetValue(node, out int n);

            // Den eigenen Platz nicht mitzählen, sonst wechselt der Gegner ständig
            if (node == ownSlot) n--;

            return Mathf.Max(0, n) * crowdPenalty;
        }

        // =====================================================
        // PUBLIC API
        // =====================================================

        /// <summary>
        /// Weg zum Ziel. Ist er frei, wird er zurückgegeben (A*). Ist er versperrt, wird der günstigste
        /// angreifbare Mauerabschnitt gesucht (attackTarget = Mauer).
        /// </summary>
        public PathResult FindPath(Vector3 startWorld, Vector3 targetWorld, GridNode ownSlot = null)
        {
            GridNode startNode = GridManager.Instance.GetNode(startWorld);
            GridNode targetNode = GridManager.Instance.GetNode(targetWorld);

            if (startNode == null || targetNode == null)
                return null;

            PathResult normalPath = FindPathInternal(startNode, targetNode);
            if (normalPath != null)
                return normalPath;

            return FindBestAttackPath(startNode, AttackableWalls(), ownSlot);
        }

        /// <summary>
        /// Weg zu einem bestimmten Ziel (Turm, Held, Mauer): zum günstigsten freien Feld daneben.
        /// attackTarget = das Ziel. Null, wenn kein Angriffsfeld erreichbar ist.
        /// </summary>
        public PathResult FindPathToTarget(Vector3 startWorld, GameObject target, GridNode ownSlot = null)
        {
            if (target == null) return null;

            GridNode startNode = GridManager.Instance.GetNode(startWorld);
            if (startNode == null) return null;

            singleTarget.Clear();
            singleTarget.Add(target);
            return FindBestAttackPath(startNode, singleTarget, ownSlot);
        }

        // =====================================================
        // ANGRIFFSPFAD (eine Suche für beliebig viele Ziele)
        // =====================================================

        private IEnumerable<GameObject> AttackableWalls()
        {
            foreach (GameObject wall in TowerHeroManager.instance.walls)
            {
                if (wall != null && wall.TryGetComponent(out Tower tower) && tower.IsTargetable)
                    yield return wall;
            }
        }

        /// <summary>
        /// Dijkstra vom Start aus über das ganze Grid, bricht ab, sobald kein besseres Angriffsfeld mehr möglich ist.
        /// Ersetzt "pro Mauer und pro Feld ein eigener A*" und skaliert dadurch auch mit vielen Zielen (Türme, Held, Mauern).
        /// Kosten = Weglänge (gCost) + Crowd-Aufschlag des Angriffsfelds.
        /// </summary>
        private PathResult FindBestAttackPath(GridNode startNode, IEnumerable<GameObject> targets, GridNode ownSlot)
        {
            // 1) Angriffsfelder sammeln: Feld -> Ziel
            goalToTarget.Clear();

            foreach (GameObject target in targets)
            {
                if (target == null) continue;

                GridNode targetNode = GridManager.Instance.GetNode(target.transform.position);
                if (targetNode == null) continue;

                foreach (GridNode position in GetAttackPositions(targetNode))
                {
                    if (!goalToTarget.ContainsKey(position))
                        goalToTarget[position] = target;
                }
            }

            if (goalToTarget.Count == 0)
                return null;

            // 2) Suche
            ResetNodes();
            closed.Clear();
            heap.Clear();

            startNode.gCost = 0;
            HeapPush(startNode, 0);

            GridNode bestGoal = null;
            int bestTotal = int.MaxValue;

            while (heap.Count > 0)
            {
                GridNode current = HeapPop().node;

                if (closed.Contains(current))
                    continue;                       // veralteter Eintrag

                if (current.gCost >= bestTotal)
                    break;                          // alles Weitere ist mindestens so teuer

                closed.Add(current);

                if (goalToTarget.ContainsKey(current))
                {
                    int total = current.gCost + CrowdCost(current, ownSlot);
                    if (total < bestTotal)
                    {
                        bestTotal = total;
                        bestGoal = current;
                    }
                }

                foreach (GridNode neighbour in current.neighbours)
                {
                    if (!neighbour.walkable || closed.Contains(neighbour))
                        continue;

                    if (!CanMoveDiagonal(current, neighbour))
                        continue;

                    int cost = current.gCost + GetDistance(current, neighbour) + neighbour.movementCost;

                    if (cost < neighbour.gCost)
                    {
                        neighbour.gCost = cost;
                        neighbour.parent = current;
                        HeapPush(neighbour, cost);
                    }
                }
            }

            if (bestGoal == null)
                return null;

            return new PathResult
            {
                path = RetracePath(startNode, bestGoal),
                targetNode = bestGoal,
                attackTarget = goalToTarget[bestGoal]
            };
        }

        private List<GridNode> GetAttackPositions(GridNode targetNode)
        {
            List<GridNode> result = new();

            foreach (GridNode neighbour in targetNode.neighbours)
            {
                if (neighbour.walkable)
                    result.Add(neighbour);
            }

            return result;
        }

        // =====================================================
        // A* (freier Weg zum Ziel)
        // =====================================================

        private PathResult FindPathInternal(GridNode startNode, GridNode targetNode)
        {
            List<GridNode> open = new();
            HashSet<GridNode> closedSet = new();

            ResetNodes();

            startNode.gCost = 0;
            startNode.hCost = GetDistance(startNode, targetNode);
            open.Add(startNode);

            while (open.Count > 0)
            {
                GridNode current = open[0];

                for (int i = 1; i < open.Count; i++)
                {
                    if (open[i].fCost < current.fCost)
                        current = open[i];
                }

                open.Remove(current);
                closedSet.Add(current);

                if (current == targetNode)
                {
                    return new PathResult
                    {
                        path = RetracePath(startNode, targetNode),
                        targetNode = targetNode
                    };
                }

                foreach (GridNode neighbour in current.neighbours)
                {
                    if (!neighbour.walkable || closedSet.Contains(neighbour))
                        continue;

                    if (!CanMoveDiagonal(current, neighbour))
                        continue;

                    int cost = current.gCost + GetDistance(current, neighbour) + neighbour.movementCost;

                    if (cost < neighbour.gCost)
                    {
                        neighbour.gCost = cost;
                        neighbour.hCost = GetDistance(neighbour, targetNode);
                        neighbour.parent = current;

                        if (!open.Contains(neighbour))
                            open.Add(neighbour);
                    }
                }
            }

            return null;
        }

        // =====================================================
        // BINARY HEAP (Min-Heap nach priority, lazy deletion)
        // =====================================================

        private void HeapPush(GridNode node, int priority)
        {
            heap.Add(new HeapEntry { node = node, priority = priority });

            int i = heap.Count - 1;
            while (i > 0)
            {
                int parent = (i - 1) / 2;
                if (heap[parent].priority <= heap[i].priority) break;

                (heap[parent], heap[i]) = (heap[i], heap[parent]);
                i = parent;
            }
        }

        private HeapEntry HeapPop()
        {
            HeapEntry top = heap[0];

            int last = heap.Count - 1;
            heap[0] = heap[last];
            heap.RemoveAt(last);

            int i = 0;
            int count = heap.Count;

            while (true)
            {
                int left = 2 * i + 1;
                int right = left + 1;
                int smallest = i;

                if (left < count && heap[left].priority < heap[smallest].priority) smallest = left;
                if (right < count && heap[right].priority < heap[smallest].priority) smallest = right;

                if (smallest == i) break;

                (heap[i], heap[smallest]) = (heap[smallest], heap[i]);
                i = smallest;
            }

            return top;
        }

        // =====================================================
        // UTIL
        // =====================================================

        private static void ResetNodes()
        {
            foreach (GridNode node in GridManager.Instance.GetAllNodes())
            {
                node.gCost = int.MaxValue;
                node.hCost = 0;
                node.parent = null;
            }
        }

        private List<Vector3> RetracePath(GridNode start, GridNode end)
        {
            List<Vector3> path = new();

            GridNode current = end;

            while (current != start)
            {
                path.Add(current.worldPosition);
                current = current.parent;
            }

            path.Add(start.worldPosition);
            path.Reverse();

            return path;
        }

        private int GetDistance(GridNode a, GridNode b)
        {
            int x = Mathf.Abs(a.cell.x - b.cell.x);
            int y = Mathf.Abs(a.cell.y - b.cell.y);

            return Mathf.Min(x, y) * 14 + Mathf.Abs(x - y) * 10;
        }

        private bool CanMoveDiagonal(GridNode current, GridNode next)
        {
            int dx = next.cell.x - current.cell.x;
            int dy = next.cell.y - current.cell.y;

            if (Mathf.Abs(dx) != 1 || Mathf.Abs(dy) != 1)
                return true;

            GridNode h = GridManager.Instance.GetNode(current.cell + new Vector3Int(dx, 0, 0));
            GridNode v = GridManager.Instance.GetNode(current.cell + new Vector3Int(0, dy, 0));

            return h != null && v != null && h.walkable && v.walkable;
        }
    }

    public class PathResult
    {
        public List<Vector3> path;
        public GridNode targetNode;
        public GameObject attackTarget;
    }
}