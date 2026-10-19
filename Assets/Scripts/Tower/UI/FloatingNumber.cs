using UnityEngine;
using UnityEngine.UI;

namespace TowerDefense
{
    /// <summary>
    /// Eine einzelne schwebende Zahl. Animiert sich selbst (kein Coroutine-Zustand im Spawner),
    /// dadurch läuft sie auch weiter, wenn der Spawner samt Gegner schon zerstört wird.
    /// Nutzt die Spielzeit: bei Pause oder Hit Stop friert sie mit dem Spiel ein.
    /// </summary>
    [RequireComponent(typeof(Text))]
    public class FloatingNumber : MonoBehaviour
    {
        private Text text;
        private RectTransform rect;
        private Vector3 baseScale = Vector3.one;

        private Vector3 startPosition;
        private Vector3 endPosition;
        private Color startColor;
        private float duration;
        private float punchScale;
        private float elapsed;

        public bool IsPlaying { get; private set; }
        public float Age => elapsed;

        private void Awake()
        {
            Cache();
        }

        private void Cache()
        {
            if (text != null) return;

            text = GetComponent<Text>();
            rect = text.rectTransform;
            baseScale = rect.localScale;
        }

        public void Play(string value, Color color, bool bold, Vector3 from, Vector3 to, float seconds, float punch)
        {
            Cache();

            text.text = value;
            text.color = color;
            text.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;

            startColor = color;
            startPosition = from;
            endPosition = to;
            duration = Mathf.Max(0.05f, seconds);
            punchScale = Mathf.Max(1f, punch);
            elapsed = 0f;
            IsPlaying = true;

            rect.localPosition = from;
            rect.localScale = baseScale * punchScale;

            gameObject.SetActive(true);
        }

        private void Update()
        {
            if (!IsPlaying) return;

            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float smooth = Mathf.SmoothStep(0f, 1f, t);

            rect.localPosition = Vector3.Lerp(startPosition, endPosition, smooth);

            // Erst kurz "aufploppen" (Crit), dann normale Größe
            float punchT = Mathf.Clamp01(t * 5f);
            rect.localScale = Vector3.Lerp(baseScale * punchScale, baseScale, punchT);

            // Die erste Hälfte voll sichtbar, danach ausblenden
            Color c = startColor;
            c.a = startColor.a * (1f - Mathf.Clamp01((t - 0.5f) * 2f));
            text.color = c;

            if (t >= 1f)
                Stop();
        }

        public void Stop()
        {
            IsPlaying = false;

            if (text != null)
                text.color = startColor;

            if (rect != null)
                rect.localScale = baseScale;

            gameObject.SetActive(false);
        }
    }
}