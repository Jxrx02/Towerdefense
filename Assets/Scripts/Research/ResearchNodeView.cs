using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace TowerDefense.Research
{
    /// <summary>
    /// Ein Knoten im Skilltree (Turm-Wurzel oder Upgrade).
    /// Zustände: Locked (gesperrt, gedimmt), Available (erforschbar, pulsiert bei genug Punkten), Researched (Gold + Marker).
    /// </summary>
    public class ResearchNodeView : MonoBehaviour
    {
        public enum State { Locked, Available, Researched }

        [Header("Teile")]
        [SerializeField] private Image border;
        [SerializeField] private Image background;
        [SerializeField] private Image icon;
        [SerializeField] private GameObject lockOverlay;
        [SerializeField] private Text lockLabel;
        [SerializeField] private GameObject researchedMarker;
        [SerializeField] private GameObject selectedFrame;
        [SerializeField] private Text badge;
        [SerializeField] private Text costText;
        [SerializeField] private Button button;

        [Header("Farben")]
        [SerializeField] private Color lockedBorder = new Color(0.30f, 0.24f, 0.17f, 1f);
        [SerializeField] private Color availableBorder = new Color(0.62f, 0.40f, 0.18f, 1f);
        [SerializeField] private Color affordableBorder = new Color(1f, 0.78f, 0.29f, 1f);
        [SerializeField] private Color researchedBorder = new Color(1f, 0.78f, 0.29f, 1f);
        [SerializeField] private Color costAffordable = new Color(1f, 0.78f, 0.29f, 1f);
        [SerializeField] private Color costTooExpensive = new Color(0.90f, 0.35f, 0.30f, 1f);
        [SerializeField] private Color costLocked = new Color(0.55f, 0.50f, 0.42f, 1f);

        private State _state;
        private bool _affordable;
        private Coroutine _pop;

        public void Setup(Sprite sprite, string badgeText, Action onClick)
        {
            icon.sprite = sprite;
            icon.enabled = sprite != null;

            LocalizedText.Bind(lockLabel, () => Texts.Common.Locked.ToUpperInvariant());

            badge.text = badgeText;
            badge.gameObject.SetActive(!string.IsNullOrEmpty(badgeText));

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => onClick?.Invoke());

            SetSelected(false);
        }

        /// <param name="costLabel">Text unter dem Knoten (leer = ausblenden)</param>
        public void SetState(State state, bool affordable, string costLabel)
        {
            _state = state;
            _affordable = affordable;

            lockOverlay.SetActive(state == State.Locked);
            researchedMarker.SetActive(state == State.Researched);

            icon.color = state == State.Locked ? new Color(0.6f, 0.6f, 0.6f, 1f) : Color.white;

            Color borderColor =
                state == State.Researched ? researchedBorder :
                state == State.Available ? (affordable ? affordableBorder : availableBorder) :
                lockedBorder;

            border.color = borderColor;

            bool showCost = state != State.Researched && !string.IsNullOrEmpty(costLabel);
            costText.gameObject.SetActive(showCost);

            if (showCost)
            {
                costText.text = costLabel;
                costText.color = state == State.Locked ? costLocked : (affordable ? costAffordable : costTooExpensive);
            }
        }

        public void SetSelected(bool selected)
        {
            if (selectedFrame != null)
                selectedFrame.SetActive(selected);
        }

        /// <summary>Kurzes Aufpoppen nach dem Erforschen.</summary>
        public void Pop()
        {
            if (!gameObject.activeInHierarchy) return;

            if (_pop != null) StopCoroutine(_pop);
            _pop = StartCoroutine(PopRoutine());
        }

        private IEnumerator PopRoutine()
        {
            const float duration = 0.3f;

            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                float k = t / duration;
                float scale = 1f + 0.3f * Mathf.Sin(k * Mathf.PI);
                transform.localScale = Vector3.one * scale;
                yield return null;
            }

            transform.localScale = Vector3.one;
            _pop = null;
        }

        private void Update()
        {
            // Erforschbare Knoten mit genug Punkten pulsieren
            if (_state == State.Available && _affordable)
                border.color = Color.Lerp(availableBorder, affordableBorder, Mathf.PingPong(Time.unscaledTime * 1.4f, 1f));
        }
    }
}