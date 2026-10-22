using UnityEngine;

namespace TowerDefense
{
    /// <summary>
    /// Zeigt den Aufladefortschritt des Helden als Leiste unter seinen Füßen.
    /// Sichtbar nur während des Aufladens. Die Farbe wechselt von Grau über Gelb zu Orange,
    /// bei voller Aufladung blitzt die Leiste auf und pulsiert, bis die Taste losgelassen wird.
    /// Die Leiste hängt nicht am Helden und wird von Rotation und Spiegelung nicht beeinflusst.
    /// </summary>
    [DisallowMultipleComponent]
    public class HeroChargeIndicator : MonoBehaviour
    {
        [Header("Größe & Position")]
        public float width = 0.9f;
        public float height = 0.09f;
        [Tooltip("Versatz zum Helden. Negativ = unter den Füßen.")]
        public Vector2 offset = new Vector2(0f, -0.2f);

        [Header("Farben")]
        public Color backgroundColor = new Color(0f, 0f, 0f, 0.7f);
        public Color lowColor = new Color(0.85f, 0.85f, 0.85f);
        public Color midColor = new Color(1f, 0.85f, 0.2f);
        public Color fullColor = new Color(1f, 0.45f, 0.1f);
        public Color flashColor = Color.white;

        [Header("Verhalten")]
        public float fadeTime = 0.12f;
        public float fullPulseSpeed = 14f;
        public float flashTime = 0.25f;

        [Header("Sorting")]
        public string sortingLayerName = "Player";
        public int sortingOrder = 210;

        private IChargeSource source;
        private Transform root;
        private SpriteRenderer background, fill, flash;

        private float visibility;
        private float shownProgress;
        private float flashAlpha;

        public void Bind(IChargeSource chargeSource)
        {
            Unbind();

            source = chargeSource;
            if (source != null)
                source.FullyCharged += OnFullyCharged;

            if (root == null)
                Build();
        }

        private void Unbind()
        {
            if (source != null)
                source.FullyCharged -= OnFullyCharged;

            source = null;
        }

        private void OnDestroy()
        {
            Unbind();

            if (root != null)
                Destroy(root.gameObject);
        }

        private void Build()
        {
            root = new GameObject("ChargeIndicator").transform;
            root.SetParent(RuntimeBarSprites.Container, false);

            background = RuntimeBarSprites.Create("Background", root, sortingOrder, sortingLayerName);
            fill = RuntimeBarSprites.Create("Fill", root, sortingOrder + 1, sortingLayerName);
            flash = RuntimeBarSprites.Create("Flash", root, sortingOrder + 2, sortingLayerName);

            root.gameObject.SetActive(false);
        }

        private void OnFullyCharged()
        {
            flashAlpha = 1f;
        }

        private void LateUpdate()
        {
            if (source == null || root == null)
                return;

            bool charging = source.IsCharging;

            // Beim Ausblenden den letzten Wert stehen lassen, statt auf 0 zu springen
            if (charging)
                shownProgress = source.ChargeProgress;

            visibility = Mathf.MoveTowards(visibility, charging ? 1f : 0f,
                Time.unscaledDeltaTime / Mathf.Max(0.01f, fadeTime));

            bool visible = visibility > 0.002f;
            if (root.gameObject.activeSelf != visible)
                root.gameObject.SetActive(visible);

            if (!visible)
                return;

            root.position = transform.position + new Vector3(offset.x, offset.y, 0f);
            root.rotation = Quaternion.identity;

            float left = -width * 0.5f;
            bool full = shownProgress >= 1f;

            // Hintergrund mit kleinem Rand
            background.transform.localPosition = new Vector3(left - 0.02f, 0f, 0f);
            background.transform.localScale = new Vector3(width + 0.04f, height + 0.04f, 1f);
            Tint(background, backgroundColor);

            // Füllung
            fill.enabled = shownProgress > 0.001f;
            fill.transform.localPosition = new Vector3(left, 0f, 0f);
            fill.transform.localScale = new Vector3(width * shownProgress, height, 1f);
            Tint(fill, ProgressColor(shownProgress, full));

            // Aufblitzen bei voller Aufladung
            flash.enabled = flashAlpha > 0f;
            if (flash.enabled)
            {
                flashAlpha -= Time.unscaledDeltaTime / Mathf.Max(0.01f, flashTime);
                flash.transform.localPosition = new Vector3(left, 0f, 0f);
                flash.transform.localScale = new Vector3(width, height, 1f);

                Color color = flashColor;
                color.a = Mathf.Clamp01(flashAlpha) * 0.8f;
                Tint(flash, color);
            }
        }

        private Color ProgressColor(float progress, bool full)
        {
            Color color = progress < 0.5f
                ? Color.Lerp(lowColor, midColor, progress / 0.5f)
                : Color.Lerp(midColor, fullColor, (progress - 0.5f) / 0.5f);

            if (full)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * fullPulseSpeed);
                color = Color.Lerp(color, Color.white, pulse * 0.4f);
            }

            return color;
        }

        private void Tint(SpriteRenderer renderer, Color color)
        {
            color.a *= visibility;
            renderer.color = color;
        }
    }
}