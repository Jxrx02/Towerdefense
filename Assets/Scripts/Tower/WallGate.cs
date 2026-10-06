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
        
        public bool IsOpen
        {
            get => isOpen;
        }

        [SerializeField]private Sprite horizontalClosed;
        [SerializeField]private Sprite horizontalOpen;
        [SerializeField]private Sprite verticalClosed;
        [SerializeField]private Sprite verticalOpen;

        private BoxCollider2D boxCollider2D;
        protected override void Awake()
        {
            base.Awake();

            sr = GetComponent<SpriteRenderer>();
        }

        public void InitializeGate(bool horizontal)
        {
            isHorizontal = horizontal;
            isOpen = false;
            boxCollider2D = GetComponent<BoxCollider2D>();
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
                boxCollider2D.enabled = true;

            }
            else
            {
                isOpen = true;
                boxCollider2D.enabled = false;

            }
            UpdateGateSprite();


        }



        private void UpdateGateSprite()
        {
            if (sr == null)
                return;

            if (isHorizontal)
            {
                sr.sprite = isOpen
                    ? horizontalOpen
                    : horizontalClosed;
            }
            else
            {
                sr.sprite = isOpen
                    ? verticalOpen
                    : verticalClosed;
            }
        }
    }
}