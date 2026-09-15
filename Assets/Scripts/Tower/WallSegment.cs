using UnityEngine;

namespace TowerDefense
{
    public class WallSegment : Wall
    {
        
        [Header("Wall Visual")]
        [Tooltip("16 Sprites entsprechend der 4-Bit-Nachbarschaft.")]
        [SerializeField]
        private Sprite[] wallSprites = new Sprite[16];

        [Tooltip("Material für eine noch nicht gebaute Wall.")]
        [SerializeField]
        private Material unbuiltMaterial;

        [Tooltip("Material für eine gebaute Wall.")]
        [SerializeField]
        private Material builtMaterial;

        [Header("Sorting")]
        [SerializeField]
        private int sortingOrder = 100;

        [SerializeField]
        private string sortingLayerName = "Details";

        private WallGroup wallGroup;
        private Vector3Int cell;

        private SpriteRenderer spriteRenderer;

        public WallGroup WallGroup => wallGroup;

        public Vector3Int Cell => cell;

        public bool IsBuilt =>
            wallGroup != null &&
            wallGroup.IsBuilt;

        // =========================================================
        // INIT
        // =========================================================

        public void Initialize(
            WallGroup wallGroup,
            Vector3Int cell)
        {
            this.wallGroup = wallGroup;
            this.cell = cell;

            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponent<SpriteRenderer>();

                if (spriteRenderer == null)
                {
                    spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
                }
            }

            spriteRenderer.sortingOrder = sortingOrder;

            spriteRenderer.sortingLayerName = sortingLayerName;

            SetUnbuiltVisual();
        }

        private void Awake()
        {
            base.Awake();
            spriteRenderer =
                GetComponent<SpriteRenderer>();

            if (spriteRenderer == null)
            {
                spriteRenderer =
                    gameObject.AddComponent<SpriteRenderer>();
            }

            spriteRenderer.sortingOrder =
                sortingOrder;

            spriteRenderer.sortingLayerName =
                sortingLayerName;

            if (wallSprites == null ||
                wallSprites.Length != 16)
            {
                Debug.LogError(
                    $"WallSegment '{name}': " +
                    "Es müssen genau 16 Wall-Sprites zugewiesen werden!",
                    this
                );
            }

            if (unbuiltMaterial == null)
            {
                Debug.LogError(
                    $"WallSegment '{name}': " +
                    "Kein Unbuilt Material zugewiesen!",
                    this
                );
            }
            
        }

        // =========================================================
        // BUILD STATE
        // =========================================================

        public void SetBuilt()
        {
            SetBuiltVisual();
        }

        // =========================================================
        // MATERIAL
        // =========================================================

        private void SetBuiltVisual()
        {
            if (spriteRenderer == null)
                return;

            if (builtMaterial != null)
            {
                spriteRenderer.material = builtMaterial;
            }
        }

        public void SetUnbuiltVisual()
        {
            if (spriteRenderer == null)
                return;

            if (unbuiltMaterial != null)
            {
                spriteRenderer.material = unbuiltMaterial;
            }
        }

        // =========================================================
        // VISUAL
        // =========================================================

        public void RefreshVisual()
        {
            if (spriteRenderer == null)
                return;

            if (wallSprites == null ||
                wallSprites.Length != 16)
            {
                return;
            }
            
            int mask = CalculateNeighbourMask();

            Sprite sprite =
                wallSprites[mask];

            if (sprite != null)
            {
                spriteRenderer.sprite = sprite;
            }
        }

        // =========================================================
        // NEIGHBOURS
        // =========================================================
        /*
              0 = ·        1 = ↑        2 = →        3 = ↑→

              4 = ↓        5 = ↑↓       6 = →↓       7 = ↑→↓

              8 = ←        9 = ↑←      10 = ←→      11 = ↑←→

             12 = ↓←      13 = ↑↓←     14 = →↓←     15 = ↑→↓←
         */
        /// <summary>
        /// Bit 0 = oben
        /// Bit 1 = rechts
        /// Bit 2 = unten
        /// Bit 3 = links
        /// </summary>
        private int CalculateNeighbourMask()
        {
            if (wallGroup == null)
                return 0;

            int mask = 0;

            // Oben
            if (wallGroup.ContainsCell(
                    cell + Vector3Int.up))
            {
                mask |= 1;
            }

            // Rechts
            if (wallGroup.ContainsCell(
                    cell + Vector3Int.right))
            {
                mask |= 2;
            }

            // Unten
            if (wallGroup.ContainsCell(
                    cell + Vector3Int.down))
            {
                mask |= 4;
            }

            // Links
            if (wallGroup.ContainsCell(
                    cell + Vector3Int.left))
            {
                mask |= 8;
            }

            return mask;
        }


        // =========================================================
        // DESTROY
        // =========================================================

        protected override void DestroyTower()
        {
            TowerHeroManager.instance.UnRegisterTower(this.gameObject);
            //TowerHeroManager.instance.DeselectTower();
            
        }

        public override void TakeDamage(int damage)
        {
            if (wallGroup == null)
                return;

            wallGroup.TakeDamage(damage);
        }

        /*private void OnDestroy()
        {
            
            if (wallGroup != null)
            {
                wallGroup.RemoveWallSegment(cell);
            }
        }*/
    }
}