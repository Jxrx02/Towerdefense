using System.Collections.Generic;
using UnityEngine;

namespace TowerDefense.GridMovement
{
    public class PathfindingManager : MonoBehaviour
    {
        public static PathfindingManager Instance;


        private void Awake()
        {
            Instance = this;
        }
        [Header("Crowding")]
        [Tooltip("Aufschlag pro Gegner, der ein Angriffsfeld schon belegt. 10 = eine Zelle Umweg.")]
        [SerializeField] private int crowdPenalty = 30;

        private readonly Dictionary<GridNode, int> slotOccupancy = new();

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
        
        public PathResult FindPath(Vector3 startWorld, Vector3 targetWorld, GridNode ownSlot = null)
        {
            GridNode startNode = GridManager.Instance.GetNode(startWorld);
            GridNode targetNode = GridManager.Instance.GetNode(targetWorld);

            if (startNode == null || targetNode == null)
                return null;
            // normaler Weg
            PathResult normalPath = FindPathInternal(startNode, targetNode);

            if (normalPath != null)
                return normalPath;
            
            // blockiert -> beste Wall suchen
            WallPathResult wallResult = FindBestWallPath(startNode, ownSlot);

            return wallResult?.pathResult;
        }


        // =====================================================
        // WALL SEARCH
        // =====================================================

        private WallPathResult FindBestWallPath(GridNode startNode, GridNode ownSlot)
        {
            WallPathResult best = null;

            foreach (GameObject wall in TowerHeroManager.instance.walls)
            {
                PathResult result = FindPathToWall(startNode, wall, ownSlot);

                if (result == null)
                    continue;

                int cost = CalculatePathCost(result.path)
                           + CrowdCost(result.targetNode, ownSlot);

                if (best == null || cost < best.cost)
                {
                    best = new WallPathResult { cost = cost, pathResult = result };
                }
            }

            return best;
        }

        private PathResult FindPathToWall(GridNode startNode, GameObject wall, GridNode ownSlot)
        {
            GridNode wallNode =
                GridManager.Instance.GetNode(
                    wall.transform.position
                );

            if(wallNode == null)
                return null;

            
            List<GridNode> attackPositions = GetAttackPositions(wallNode);
            
            PathResult best = null;

            int bestCost = int.MaxValue;
            
            foreach (GridNode position in attackPositions)
            {
                PathResult result = FindPathInternal(startNode, position);

                if (result == null)
                    continue;

                int cost = CalculatePathCost(result.path) + CrowdCost(position, ownSlot);

                if (cost < bestCost)
                {
                    bestCost = cost;
                    best = result;
                }
            }

            if(best != null)
            {
                best.attackTarget = wall;
            }

            return best;
        }



        private List<GridNode> GetAttackPositions(GridNode wallNode)
        {
            List<GridNode> result = new();


            foreach(GridNode neighbour in wallNode.neighbours)
            {
                if(neighbour.walkable)
                    result.Add(neighbour);
            }


            return result;
        }



        // =====================================================
        // A STAR
        // =====================================================

        private PathResult FindPathInternal(
            GridNode startNode,
            GridNode targetNode)
        {
            List<GridNode> open = new();
            HashSet<GridNode> closed = new();


            foreach(GridNode node in GridManager.Instance.GetAllNodes())
            {
                node.gCost = int.MaxValue;
                node.hCost = 0;
                node.parent = null;
            }



            startNode.gCost = 0;
            startNode.hCost =
                GetDistance(startNode,targetNode);


            open.Add(startNode);



            while(open.Count > 0)
            {
                GridNode current = open[0];


                for(int i=1;i<open.Count;i++)
                {
                    if(open[i].fCost < current.fCost)
                        current = open[i];
                }


                open.Remove(current);
                closed.Add(current);



                if(current == targetNode)
                {
                    return new PathResult
                    {
                        path =
                        RetracePath(
                            startNode,
                            targetNode
                        ),

                        targetNode = targetNode
                    };
                }



                foreach(GridNode neighbour in current.neighbours)
                {
                    if(!neighbour.walkable)
                        continue;


                    if(closed.Contains(neighbour))
                        continue;


                    if(!CanMoveDiagonal(current,neighbour))
                        continue;



                    int cost =
                        current.gCost +
                        GetDistance(current,neighbour) +
                        neighbour.movementCost;



                    if(cost < neighbour.gCost)
                    {
                        neighbour.gCost = cost;

                        neighbour.hCost =
                            GetDistance(
                                neighbour,
                                targetNode
                            );


                        neighbour.parent=current;


                        if(!open.Contains(neighbour))
                            open.Add(neighbour);
                    }
                }
            }


            return null;
        }



        // =====================================================
        // UTIL
        // =====================================================


        private int CalculatePathCost(List<Vector3> path)
        {
            int cost=0;


            foreach(Vector3 pos in path)
            {
                GridNode node =
                    GridManager.Instance.GetNode(pos);


                if(node!=null)
                    cost += node.movementCost;
            }


            return cost;
        }



        private List<Vector3> RetracePath(
            GridNode start,
            GridNode end)
        {
            List<Vector3> path = new();


            GridNode current=end;


            while(current != start)
            {
                path.Add(current.worldPosition);
                current=current.parent;
            }


            path.Add(start.worldPosition);


            path.Reverse();


            return path;
        }



        private int GetDistance(
            GridNode a,
            GridNode b)
        {
            int x =
                Mathf.Abs(
                    a.cell.x-b.cell.x
                );

            int y =
                Mathf.Abs(
                    a.cell.y-b.cell.y
                );


            return Mathf.Min(x,y)*14 +
                   Mathf.Abs(x-y)*10;
        }



        private bool CanMoveDiagonal(
            GridNode current,
            GridNode next)
        {
            int dx =
                next.cell.x-current.cell.x;

            int dy =
                next.cell.y-current.cell.y;



            if(Mathf.Abs(dx)!=1 ||
               Mathf.Abs(dy)!=1)
                return true;



            GridNode h =
                GridManager.Instance.GetNode(
                    current.cell +
                    new Vector3Int(dx,0,0)
                );


            GridNode v =
                GridManager.Instance.GetNode(
                    current.cell +
                    new Vector3Int(0,dy,0)
                );



            return h != null &&
                   v != null &&
                   h.walkable &&
                   v.walkable;
        }
    }



    public class PathResult
    {
        public List<Vector3> path;
        public GridNode targetNode;
        public GameObject attackTarget;
    }



    public class WallPathResult
    {
        public int cost;
        public PathResult pathResult;
    }
}