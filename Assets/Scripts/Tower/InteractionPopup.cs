using UnityEngine;
using UnityEngine.UI;

namespace TowerDefense
{
    public class InteractionPopup : MonoBehaviour
    {
        public static InteractionPopup instance;

        [Header("UI")]
        [SerializeField] private Slider progressSlider;

        [Header("Position")]
        [SerializeField] private Vector3 worldOffset = new Vector3(0f, 0.8f, 0f);

        private RectTransform rectTransform;
        private Canvas canvas;

        private void Awake()
        {
            if (instance == null)               instance = this;

            rectTransform = GetComponent<RectTransform>();
            canvas = GetComponentInParent<Canvas>();

            SetChildrenActive(false);
        }

        public void Show(Vector3 worldPosition)
        {
            Position(worldPosition);

            ResetProgress();

            SetChildrenActive(true);
        }

        public void Hide()
        {
            SetChildrenActive(false);
        }
        private void SetChildrenActive(bool active)
        {
            foreach (Transform child in transform)
            {
                child.gameObject.SetActive(active);
            }
        }
        public void SetProgress(float progress)
        {
            if (progressSlider == null)
                return;

            progressSlider.value = Mathf.Clamp01(progress) *4;
        }

        public void ResetProgress()
        {
            if (progressSlider != null)
                progressSlider.value = 0f;
        }

        private void Position(Vector3 worldPosition)
        {
            worldPosition += worldOffset;

            if (canvas == null)
                return;

            if (canvas.renderMode == RenderMode.WorldSpace)
            {
                rectTransform.position = worldPosition;
                return;
            }

            Camera cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : canvas.worldCamera;

            Vector2 screenPosition =
                RectTransformUtility.WorldToScreenPoint(
                    cam,
                    worldPosition
                );

            RectTransform canvasRect =
                canvas.transform as RectTransform;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect,
                screenPosition,
                cam,
                out Vector2 localPosition
            );

            rectTransform.anchoredPosition = localPosition;
        }
    }
}