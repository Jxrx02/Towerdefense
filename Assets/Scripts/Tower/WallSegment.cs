using UnityEngine;

namespace TowerDefense
{
    public class WallSegment : Wall
    {
        [Header("Wall Visual")]
        [Tooltip("16 Sprites entsprechend der 4-Bit-Nachbarschaft.")]
        [SerializeField]
        private Sprite[] wallSprites = new Sprite[16];

        [SerializeField]
        private Material unbuiltMaterial;

        [SerializeField]
        private Material builtMaterial;

        private WallGroup wallGroup;
        private Vector3Int cell;
        private SpriteRenderer spriteRenderer;

        public WallGroup WallGroup => wallGroup;
        public Vector3Int Cell => cell;

        public bool IsBuilt =>
            wallGroup != null &&
            wallGroup.IsBuilt;

        protected override void Awake()
        {
            base.Awake();

            spriteRenderer = GetComponent<SpriteRenderer>();

            if (spriteRenderer == null)
                spriteRenderer = gameObject.AddComponent<SpriteRenderer>();

            ValidateWallSprites();
        }

        public void Initialize(
            WallGroup group,
            Vector3Int cell)
        {
            wallGroup = group;
            wallGroup.WallUpgradePath = upgradePaths[0];
            this.cell = cell;

            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponent<SpriteRenderer>();

                if (spriteRenderer == null)
                    spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
            }

            UpdateSorting();
            SetUnbuiltVisual();
        }

        private void UpdateSorting()
        {
            if (ysort == null || wallGroup == null)
                return;

            ysort.UpdateSorting(
                wallGroup.transform.position.y +
                transform.localPosition.y
            );
        }

        // =========================================================
        // UPGRADE
        // =========================================================

        public override void UpgradePath(int pathIndex)
        {
            if (pathIndex != 0)
                return;

            if (wallGroup == null)
            {
                Debug.LogWarning(
                    $"WallSegment '{name}' besitzt keine WallGroup.",
                    this
                );

                return;
            }

            wallGroup?.UpgradeWall();
        }

        // =========================================================
        // WALL SPRITES
        // =========================================================

        public void SetWallSprites(Sprite[] newSprites)
        {
            if (newSprites == null || newSprites.Length != 16)
            {
                Debug.LogWarning(
                    $"WallSegment '{name}': " +
                    "Es müssen genau 16 Wall-Sprites vorhanden sein.",
                    this
                );

                return;
            }

            wallSprites = newSprites;

            RefreshVisual();
        }

        public void RefreshVisual()
        {
            if (spriteRenderer == null)
                return;

            if (wallSprites == null || wallSprites.Length != 16)
                return;

            int mask = CalculateNeighbourMask();

            Sprite sprite = wallSprites[mask];

            if (sprite != null)
                spriteRenderer.sprite = sprite;

            UpdateSorting();
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
        // BUILD
        // =========================================================

        public void SetBuilt()
        {
            SetBuiltVisual();
            UpdateSorting();
        }

        private void SetBuiltVisual()
        {
            if (spriteRenderer != null && builtMaterial != null)
                spriteRenderer.material = builtMaterial;
        }

        public void SetUnbuiltVisual()
        {
            if (spriteRenderer != null && unbuiltMaterial != null)
                spriteRenderer.material = unbuiltMaterial;
        }

        // =========================================================
        // DAMAGE
        // =========================================================

        public override void TakeDamage(int damage)
        {
            if (wallGroup == null)
                return;

            wallGroup.TakeDamage(damage);
        }

        // =========================================================
        // DESTROY
        // =========================================================

        protected override void DestroyTower()
        {
            TowerHeroManager.instance.UnRegisterTower(gameObject);
        }

        // =========================================================
        // VALIDATION
        // =========================================================

        private void ValidateWallSprites()
        {
            if (wallSprites == null || wallSprites.Length != 16)
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
    }
}