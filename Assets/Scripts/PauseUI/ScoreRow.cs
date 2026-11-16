using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace TowerDefense
{
    /// <summary>Eine Zeile der Score-Aufschlüsselung: Label | Mitte (z. B. "x0.90") | Wert.</summary>
    public class ScoreRow : MonoBehaviour
    {
        [SerializeField] private Image background;
        [SerializeField] private Text txtLabel;
        [SerializeField] private Text txtMiddle;
        [SerializeField] private Text txtValue;
        [SerializeField] private Color highlightColor = new Color(0.82f, 0.55f, 0.20f, 0.35f);

        private Color _restColor = new Color(0f, 0f, 0f, 0f);
        private Coroutine _pulse;

        public void Set(string label, string middle, string value,
            Color labelColor, Color middleColor, Color valueColor, bool bold = false)
        {
            txtLabel.text = label;
            txtMiddle.text = middle;
            txtValue.text = value;

            txtLabel.color = labelColor;
            txtMiddle.color = middleColor;
            txtValue.color = valueColor;

            FontStyle style = bold ? FontStyle.Bold : FontStyle.Normal;
            txtLabel.fontStyle = style;
            txtMiddle.fontStyle = FontStyle.Bold;
            txtValue.fontStyle = style;

            SetHighlight(false);
        }

        /// <summary>Dauerhafte Hintergrundfarbe der Zeile (z. B. die Gesamtzeile im Loadout).</summary>
        public void SetRestColor(Color color)
        {
            _restColor = color;
            SetHighlight(false);
        }

        public void SetHighlight(bool on)
        {
            if (background != null)
                background.color = on ? highlightColor : _restColor;
        }

        /// <summary>Kurzes Aufleuchten und Aufpoppen des Werts (Multiplikator hat sich geändert).</summary>
        public void Pulse()
        {
            if (!isActiveAndEnabled) return;

            if (_pulse != null) StopCoroutine(_pulse);
            _pulse = StartCoroutine(PulseRoutine());
        }

        // Unskalierte Zeit: Pausemenü und Menüs laufen mit Time.timeScale = 0
        private IEnumerator PulseRoutine()
        {
            const float duration = 0.45f;
            Transform value = txtValue.transform;

            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                float k = t / duration;
                value.localScale = Vector3.one * (1f + 0.25f * Mathf.Sin(k * Mathf.PI));

                if (background != null)
                    background.color = Color.Lerp(highlightColor, _restColor, k);

                yield return null;
            }

            value.localScale = Vector3.one;
            SetHighlight(false);
            _pulse = null;
        }
    }
}