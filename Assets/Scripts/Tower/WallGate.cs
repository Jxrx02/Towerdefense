using ScriptableObjects;
using TowerDefense;
using UnityEngine;

namespace Tower
{

    public enum WallGateOrientation
    {
        Horizontal,
        Vertical
    }
    public class WallGate : WallSegment
    {
        private bool isOpen;
        private bool isHorizontal;

        [SerializeField]private Sprite horizontalClosed;
        [SerializeField]private Sprite horizontalOpen;
        [SerializeField]private Sprite verticalClosed;
        [SerializeField]private Sprite verticalOpen;

        private SpriteRenderer spriteRenderer;

        protected override void Awake()
        {
            base.Awake();

            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        public void InitializeGate(bool horizontal)
        {
            isHorizontal = horizontal;
            isOpen = false;

            UpdateGateSprite();
        }
        
        public void SetGateSprites(Sprite gateHorizontalClosed, Sprite gateHorizontalOpen,  Sprite gateVerticalClosed, Sprite gateVerticalOpen)
        {
            this.horizontalClosed = gateHorizontalClosed;
            this.horizontalOpen = gateHorizontalOpen;
            this.verticalClosed = gateVerticalClosed;
            this.verticalOpen = gateVerticalOpen;

            UpdateGateSprite();
        }
        public void SetGateOpen(bool _isOpen)
        {
            if (!_isOpen)
            {
                isOpen = false;
            }
            else
            {
                isOpen = true;
            }
            UpdateGateSprite();


        }


        public bool IsOpen => isOpen;

        private void UpdateGateSprite()
        {
            if (spriteRenderer == null)
                return;

            if (isHorizontal)
            {
                spriteRenderer.sprite = isOpen
                    ? horizontalOpen
                    : horizontalClosed;
            }
            else
            {
                spriteRenderer.sprite = isOpen
                    ? verticalOpen
                    : verticalClosed;
            }
        }
    }
}