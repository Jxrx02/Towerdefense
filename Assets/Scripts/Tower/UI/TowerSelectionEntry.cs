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

            Sprite icon = null;
            SpriteRenderer sr = tower.GetComponent<SpriteRenderer>();
            if (sr != null)
                icon = sr.sprite;

            InitializeCustom(index, tower.towerName, icon);
        }

        /// <summary>Für Upgrade-Pfade (Name + Icon frei wählbar).</summary>
        public void InitializeCustom(int index, string displayName, Sprite icon)
        {
            this.index = index;

            if (towerName != null)
                towerName.text = displayName;

            if (towerIcon != null)
            {
                towerIcon.sprite = icon;
                towerIcon.enabled = icon != null;
            }

            SetSelected(false);
        }
        public void SetLocked(bool locked)
        {
            CanvasGroup group = GetComponent<CanvasGroup>();

            if (group == null)
                group = gameObject.AddComponent<CanvasGroup>();

            group.alpha = locked ? 0.35f : 1f;
        }
        public void SetSelected(bool selected)
        {
            if (selectedFrame != null)
                selectedFrame.SetActive(selected);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (InteractionPopup.instance != null)
                InteractionPopup.instance.SelectFromMouse(index);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (InteractionPopup.instance != null)
                InteractionPopup.instance.SelectFromMouse(index);
        }
    }
}