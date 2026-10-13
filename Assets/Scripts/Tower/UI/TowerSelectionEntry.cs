using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TowerDefense
{
    public class TowerSelectionEntry :
        MonoBehaviour,
        IPointerEnterHandler,
        IPointerClickHandler
    {
        [Header("UI")]
        [SerializeField] private Image towerIcon;
        [SerializeField] private Text towerName;
        [SerializeField] private GameObject selectedFrame;

        private int index;

        public void Initialize(int index, Tower tower)
        {
            this.index = index;

            if (tower == null)
                return;

            if (towerName != null)
                towerName.text = tower.towerName;

            if (towerIcon != null)
            {
                SpriteRenderer sr =
                    tower.GetComponent<SpriteRenderer>();

                if (sr != null)
                    towerIcon.sprite = sr.sprite;
            }

            SetSelected(false);
        }

        public void SetSelected(bool selected)
        {
            if (selectedFrame != null)
                selectedFrame.SetActive(selected);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (InteractionUISelectTower.instance != null)
                InteractionUISelectTower.instance.SelectFromMouse(index);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (InteractionUISelectTower.instance != null)
                InteractionUISelectTower.instance.SelectFromMouse(index);
        }
    }
}