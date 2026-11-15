using UnityEngine;
using UnityEngine.UI;

namespace LevelSelector
{
    /// <summary>
    /// Eine Heldenstufe auf der Levelachse: Knoten mit Stufennummer, Verbindungslinie zur vorherigen und nächsten
    /// Stufe (mit Fortschrittsfüllung) und darunter die Inhalte, die mit dieser Stufe freigeschaltet werden.
    ///
    /// Die Linie zwischen zwei Stufen besteht aus der rechten Hälfte dieser und der linken Hälfte der nächsten Spalte.
    /// Der Fortschritt bis zur nächsten Stufe füllt sie von links nach rechts.
    /// </summary>
    public class ProgressionColumnView : MonoBehaviour
    {
        [SerializeField] private RectTransform leftLine;
        [SerializeField] private RectTransform leftFill;
        [SerializeField] private RectTransform rightLine;
        [SerializeField] private RectTransform rightFill;

        [SerializeField] private RectTransform node;
        [SerializeField] private Image nodeBorder;
        [SerializeField] private Image nodeBackground;
        [SerializeField] private Text txtLevel;
        [SerializeField] private Text txtXp;
        [SerializeField] private Transform itemContainer;

        [Header("Farben")]
        [SerializeField] private Color lockedBorder = new Color(0.30f, 0.24f, 0.17f, 1f);
        [SerializeField] private Color lockedFill = new Color(0.16f, 0.12f, 0.09f, 1f);
        [SerializeField] private Color lockedText = new Color(0.55f, 0.50f, 0.42f, 1f);
        [SerializeField] private Color reachedBorder = new Color(0.62f, 0.46f, 0.27f, 1f);
        [SerializeField] private Color reachedFill = new Color(0.35f, 0.25f, 0.12f, 1f);
        [SerializeField] private Color currentBorder = new Color(1f, 0.78f, 0.29f, 1f);
        [SerializeField] private Color currentFill = new Color(0.82f, 0.55f, 0.20f, 1f);
        [SerializeField] private Color textColor = new Color(0.96f, 0.92f, 0.82f, 1f);
        [SerializeField] private Color pulseColor = new Color(1f, 0.78f, 0.29f, 1f);

        private bool _current;

        public Transform ItemContainer => itemContainer;
        public int Level { get; private set; }

        public void Setup(int level, bool reached, bool current, bool hasLeft, bool hasRight,
            float leftAmount, float rightAmount, string xpText)
        {
            Level = level;
            _current = current;

            txtLevel.text = level.ToString();

            leftLine.gameObject.SetActive(hasLeft);
            rightLine.gameObject.SetActive(hasRight);
            SetFill(leftFill, leftAmount);
            SetFill(rightFill, rightAmount);

            nodeBorder.color = current ? currentBorder : (reached ? reachedBorder : lockedBorder);
            nodeBackground.color = current ? currentFill : (reached ? reachedFill : lockedFill);
            txtLevel.color = reached || current ? textColor : lockedText;

            txtXp.gameObject.SetActive(current && !string.IsNullOrEmpty(xpText));
            txtXp.text = xpText;

            node.localScale = Vector3.one;
        }

        private void Update()
        {
            if (!_current)
                return;

            // Aktuelle Stufe: leichtes Pulsieren von Größe und Rahmen
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 3f);
            node.localScale = Vector3.one * (1f + 0.08f * pulse);
            nodeBorder.color = Color.Lerp(currentBorder, pulseColor, pulse * 0.5f);
        }

        private static void SetFill(RectTransform fill, float amount)
        {
            fill.anchorMin = new Vector2(0f, 0f);
            fill.anchorMax = new Vector2(Mathf.Clamp01(amount), 1f);
            fill.offsetMin = Vector2.zero;
            fill.offsetMax = Vector2.zero;
        }
    }
}