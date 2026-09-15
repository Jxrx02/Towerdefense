using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace TowerDefense.GridMovement
{
    /*
     GridManager
    │
    ├── kennt WallTilemap
    ├── kennt WallSegment-Prefab
    └── erstellt WallGroup

    WallGroup
        │
        ├── ist nur Container
        ├── kennt ihre WallSegments
        ├── kennt die gemeinsamen Wall-Zellen
        └── verwaltet Build/Unbuild der Gruppe

    WallSegment : Wall : Tower
        │
        ├── eigenes Sprite
        ├── eigener SpriteRenderer
        ├── eigenes Built-Material
        ├── eigenes Unbuilt-Material
        ├── eigene Sorting-Einstellungen
        ├── eigener Collider
        ├── eigene Tower-Stats
        └── kennt seine WallGroup + Cell

     */

    public class GridManager : MonoBehaviour
    {
        public static GridManager Instance;

        [Header("Tilemaps")] [SerializeField] private Tilemap groundTilemap;
        [SerializeField] private Tilemap obstacleTilemap;

        [Tooltip(
            "Tilemap, auf der im Editor die Positionen der geplanten Walls eingezeichnet werden."
        )]
        [SerializeField]
        private Tilemap wallTilemap;

        [Header("Wall")] [SerializeField] private WallSegment wallSegmentPrefab;

        private Dictionary<Vector3Int, GridNode> nodes = new();

        // =========================================================
        // UNITY
        // =========================================================

        private void Awake()
        {
            Instance = this;

            BuildGrid();
            CacheNeighbours();
        }

        private void Start()
        {
            CreateWallGroupFromTilemap();
        }

        // =========================================================
        // SNAP
        // =========================================================

        public Vector3 SnapToGrid(Vector3 worldPosition)
        {
            Vector3Int cell =
                groundTilemap.WorldToCell(worldPosition);

            return groundTilemap.GetCellCenterWorld(cell);
        }

        // =========================================================
        // WALL
        // =========================================================

        /// <summary>
        /// Prüft, ob eine Wall grundsätzlich auf dieser
        /// Grid-Zelle platziert werden kann.
        ///
        /// Wichtig:
        /// Die Prüfung erfolgt ausschließlich über den GridNode.
        /// Die ObstacleTilemap wird hier nicht verändert.
        /// </summary>
        public bool CanPlaceWall(Vector3 worldPosition)
        {
            Vector3Int cell =
                groundTilemap.WorldToCell(worldPosition);

            return CanPlaceWall(cell);
        }

        public bool CanPlaceWall(Vector3Int cell)
        {
            if (!nodes.TryGetValue(
                    cell,
                    out GridNode node))
            {
                return false;
            }

            return node.walkable;
        }

        /// <summary>
        /// Belegt eine Grid-Zelle mit einer Wall.
        ///
        /// Die Zelle wird im Node als nicht begehbar markiert.
        ///
        /// Es wird KEIN Tile auf die ObstacleTilemap geschrieben.
        /// </summary>
        public bool PlaceWall(Vector3 worldPosition)
        {
            Vector3Int cell =
                groundTilemap.WorldToCell(worldPosition);

            return PlaceWall(cell);
        }

        public bool PlaceWall(Vector3Int cell)
        {
            if (!nodes.TryGetValue(
                    cell,
                    out GridNode node))
            {
                Debug.LogWarning(
                    $"PlaceWall: Zelle {cell} existiert nicht im Grid."
                );

                return false;
            }

            // Bereits blockiert.
            if (!node.walkable)
            {
                return false;
            }

            node.walkable = false;

            return true;
        }

        /// <summary>
        /// Entfernt die Wall-Blockierung von einer Grid-Zelle.
        ///
        /// Auch hier wird die ObstacleTilemap NICHT verändert.
        /// </summary>
        public bool RemoveWall(Vector3 worldPosition)
        {
            Vector3Int cell =
                groundTilemap.WorldToCell(worldPosition);

            return RemoveWall(cell);
        }

        public bool RemoveWall(Vector3Int cell)
        {
            if (!nodes.TryGetValue(
                    cell,
                    out GridNode node))
            {
                return false;
            }

            node.walkable = true;

            return true;
        }

        /// <summary>
        /// Belegt mehrere Grid-Zellen mit einer Wall.
        ///
        /// Wird für eine komplette WallGroup verwendet.
        /// </summary>
        public void PlaceWallGroup(
            IEnumerable<Vector3Int> cells)
        {
            if (cells == null)
                return;

            bool changed = false;

            foreach (Vector3Int cell in cells)
            {
                if (!nodes.TryGetValue(
                        cell,
                        out GridNode node))
                {
                    continue;
                }

                if (node.walkable)
                {
                    node.walkable = false;
                    changed = true;
                }
            }

            if (changed)
            {
                NotifyGridChanged();
            }
        }

        /// <summary>
        /// Entfernt die Blockierung einer kompletten WallGroup.
        /// </summary>
        public void RemoveWallGroup(
            IEnumerable<Vector3Int> cells)
        {
            if (cells == null)
                return;

            bool changed = false;

            foreach (Vector3Int cell in cells)
            {
                if (!nodes.TryGetValue(
                        cell,
                        out GridNode node))
                {
                    continue;
                }

                if (!node.walkable)
                {
                    node.walkable = true;
                    changed = true;
                }
            }

            if (changed)
            {
                NotifyGridChanged();
            }
        }

        // =========================================================
        // WALL GROUP
        // =========================================================
        public bool CanPlaceWallGroup(
            IEnumerable<Vector3Int> cells)
        {
            if (cells == null)
                return false;

            foreach (Vector3Int cell in cells)
            {
                if (!nodes.TryGetValue(
                        cell,
                        out GridNode node))
                {
                    return false;
                }

                if (!node.walkable)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Liest beim Start alle Tiles aus der WallTilemap
        /// und erzeugt daraus eine einzige WallGroup.
        ///
        /// Die WallTilemap dient ausschließlich als Editor-
        /// Definition für die geplanten Wall-Positionen.
        /// </summary>
        private void CreateWallGroupFromTilemap()
        {
            if (wallTilemap == null)
            {
                Debug.LogError(
                    "GridManager: Keine WallTilemap zugewiesen!",
                    this
                );

                return;
            }

            if (wallSegmentPrefab == null)
            {
                Debug.LogError(
                    "GridManager: Kein WallSegment-Prefab zugewiesen!",
                    this
                );

                return;
            }

            // =========================================================
            // ALLE WALL-ZELLEN SAMMELN
            // =========================================================

            HashSet<Vector3Int> unprocessedCells =
                new HashSet<Vector3Int>();

            BoundsInt bounds =
                wallTilemap.cellBounds;

            foreach (Vector3Int cell in bounds.allPositionsWithin)
            {
                if (!wallTilemap.HasTile(cell))
                    continue;

                if (!nodes.ContainsKey(cell))
                {
                    Debug.LogWarning(
                        $"WallTilemap-Zelle {cell} liegt nicht auf dem GroundGrid.",
                        this
                    );

                    continue;
                }

                unprocessedCells.Add(cell);
            }

            if (unprocessedCells.Count == 0)
            {
                Debug.Log(
                    "GridManager: Keine Wall-Zellen auf der WallTilemap gefunden."
                );

                return;
            }

            // =========================================================
            // ZUSAMMENHÄNGENDE WALLGROUPS ERMITTELN
            // =========================================================

            int groupIndex = 0;

            while (unprocessedCells.Count > 0)
            {
                // Beliebige noch nicht verarbeitete Zelle auswählen
                Vector3Int startCell = GetFirstCell(unprocessedCells);

                // Alle zusammenhängenden Zellen dieser Gruppe finden
                List<Vector3Int> groupCells =
                    FindConnectedWallCells(
                        startCell,
                        unprocessedCells
                    );

                if (groupCells.Count == 0)
                    break;

                // =====================================================
                // WALLGROUP ERSTELLEN
                // =====================================================

                Vector3 groupCenter =
                    CalculateWallGroupCenter(groupCells);

                GameObject groupObject =
                    new GameObject(
                        $"WallGroup_{groupIndex}"
                    );

                groupObject.transform.position =
                    groupCenter;

                WallGroup wallGroup =
                    groupObject.AddComponent<WallGroup>();

                wallGroup.Initialize(
                    groupCells,
                    groundTilemap,
                    wallSegmentPrefab
                );

                groupIndex++;
            }

            // =========================================================
            // WALLTILEMAP NUR ALS EDITOR-VORLAGE
            // =========================================================

            wallTilemap.gameObject.SetActive(false);

            Debug.Log(
                $"GridManager: {groupIndex} WallGroups erstellt."
            );
        }
        private Vector3Int GetFirstCell(
            HashSet<Vector3Int> cells)
        {
            foreach (Vector3Int cell in cells)
            {
                return cell;
            }

            return default;
        }
        private List<Vector3Int> FindConnectedWallCells(
            Vector3Int startCell,
            HashSet<Vector3Int> unprocessedCells)
        {
            List<Vector3Int> connectedCells =
                new List<Vector3Int>();

            Queue<Vector3Int> queue =
                new Queue<Vector3Int>();

            queue.Enqueue(startCell);
            unprocessedCells.Remove(startCell);

            // Nur horizontal + vertikal
            Vector3Int[] directions =
            {
                Vector3Int.up,
                Vector3Int.down,
                Vector3Int.left,
                Vector3Int.right
            };

            while (queue.Count > 0)
            {
                Vector3Int currentCell =
                    queue.Dequeue();

                connectedCells.Add(currentCell);

                foreach (Vector3Int direction in directions)
                {
                    Vector3Int neighbour =
                        currentCell + direction;

                    if (!unprocessedCells.Contains(neighbour))
                        continue;

                    unprocessedCells.Remove(neighbour);
                    queue.Enqueue(neighbour);
                }
            }

            return connectedCells;
        }
        private Vector3 CalculateWallGroupCenter(
            List<Vector3Int> cells)
        {
            Vector3 center = Vector3.zero;

            foreach (Vector3Int cell in cells)
            {
                center +=
                    groundTilemap.GetCellCenterWorld(cell);
            }

            return center / cells.Count;
        }
        // =========================================================
        // GRID
        // =========================================================

        private void BuildGrid()
        {
            nodes.Clear();

            BoundsInt bounds =
                groundTilemap.cellBounds;

            foreach (
                Vector3Int cell
                in bounds.allPositionsWithin)
            {
                if (!groundTilemap.HasTile(cell))
                    continue;

                GridNode node =
                    new GridNode(
                        cell,
                        groundTilemap.GetCellCenterWorld(cell)
                    );

                // Die normale ObstacleTilemap bestimmt nur
                // die bereits vorhandenen statischen Hindernisse.
                node.walkable =
                    !obstacleTilemap.HasTile(cell);

                nodes.Add(
                    cell,
                    node
                );
            }
        }

        private void CacheNeighbours()
        {
            foreach (GridNode node in nodes.Values)
            {
                node.neighbours.Clear();

                for (int x = -1; x <= 1; x++)
                {
                    for (int y = -1; y <= 1; y++)
                    {
                        if (x == 0 && y == 0)
                            continue;

                        Vector3Int neighbourCell =
                            node.cell +
                            new Vector3Int(
                                x,
                                y,
                                0
                            );

                        if (nodes.TryGetValue(
                                neighbourCell,
                                out GridNode neighbour))
                        {
                            node.neighbours.Add(
                                neighbour
                            );
                        }
                    }
                }
            }
        }

        // =========================================================
        // NODE ACCESS
        // =========================================================

        public GridNode GetNode(
            Vector3 worldPosition)
        {
            Vector3Int cell =
                groundTilemap.WorldToCell(
                    worldPosition
                );

            nodes.TryGetValue(
                cell,
                out GridNode node
            );

            return node;
        }

        public GridNode GetNode(
            Vector3Int cell)
        {
            nodes.TryGetValue(
                cell,
                out GridNode node
            );

            return node;
        }

        public IEnumerable<GridNode> GetAllNodes()
        {
            return nodes.Values;
        }

        // =========================================================
        // GRID UPDATE
        // =========================================================

        public void NotifyGridChanged()
        {
            foreach (GridNode node in nodes.Values)
            {
                node.neighbours.Clear();
            }

            CacheNeighbours();

            Actions.onGridChanged?.Invoke();
        }
    }
}