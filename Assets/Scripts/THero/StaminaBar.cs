using UnityEngine;

namespace TowerDefense
{
    /// <summary>
    /// Ausdauer-Leiste direkt unter der HealthBar. Baut sich zur Laufzeit aus Sprites zusammen.
    ///
    ///   Nachzieh-Leiste (weiß)  zeigt, wie viel der letzte Angriff gekostet hat
    ///   Erschöpft               Leiste pulsiert rot, bis sich die Ausdauer wieder erholt hat
    ///   Zu wenig Ausdauer       roter Blitz, wenn eine Aktion abgelehnt wird
    /// Sichtbar, solange die Ausdauer nicht voll ist, danach blendet sie sich aus (alwaysVisible = immer).
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(110)]   // nach der HealthBar, an der sie hängt (sonst hinkt sie einen Frame hinterher)
    public class StaminaBar : MonoBehaviour
    {
        [Header("Größe & Position")]
        [Tooltip("Breite, wenn keine HealthBar vorhanden ist. Sonst ist die Leiste so breit wie die HealthBar.")]
        public float width = 1f;
        public float height = 0.07f;
        [Tooltip("Abstand zur HealthBar (0 = Ränder berühren sich)")]
        public float gap = 0.01f;
        [Tooltip("Nur ohne HealthBar: Abstand über dem Sprite")]
        public float padding = 0.12f;

        [Header("Sichtbarkeit")]
        public bool alwaysVisible;
        public float hideDelay = 1.5f;
        public float fadeTime = 0.2f;

        [Header("Farben")]
        public Color backgroundColor = new Color(0f, 0f, 0f, 0.65f);
        public Color normalColor = new Color(1f, 0.82f, 0.25f);
        public Color exhaustedColor = new Color(0.85f, 0.2f, 0.2f);
        public Color lagColor = new Color(1f, 1f, 1f, 0.8f);
        public Color deniedColor = new Color(1f, 0.15f, 0.15f);

        [Header("Nachzieh-Leiste")]
        public float lagDelay = 0.2f;
        [Tooltip("Anteil der Leiste pro Sekunde")]
        public float lagSpeed = 1.2f;

        [Header("Sorting")]
        public string sortingLayerName = "Player";
        public int sortingOrder = 200;

        private Stamina stamina;
        private HealthBar healthBar;
        private SpriteRenderer targetSprite;
        private Transform root;
        private SpriteRenderer background, lag, fill, flash;

        private float visibility;
        private float lastChangeTime = -999f;
        private float lastValue = -1f;
        private float lagValue;
        private float lagTimer;
        private float deniedAlpha;
        private float offsetY;
        private float offsetTimer;

        public void Bind(Stamina source)
        {
            Unbind();

            stamina = source;
            if (stamina == null)
                return;

            stamina.Spent += OnSpent;
            stamina.Denied += OnDenied;
            stamina.ExhaustedChanged += OnExhaustedChanged;

            if (root == null)
                Build();

            lagValue = stamina.Current;
        }

        private void Unbind()
        {
            if (stamina == null)
                return;

            stamina.Spent -= OnSpent;
            stamina.Denied -= OnDenied;
            stamina.ExhaustedChanged -= OnExhaustedChanged;
            stamina = null;
        }

        private void Start()
        {
            // Die HealthBar wird vom Tower beim Start hinzugefügt, deshalb erst hier suchen
            healthBar = GetComponent<HealthBar>();
        }

        private void OnDestroy()
        {
            Unbind();

            if (root != null)
                Destroy(root.gameObject);
        }

        private void Build()
        {
            root = new GameObject($"StaminaBar ({name})").transform;
            root.SetParent(RuntimeBarSprites.Container, false);

            background = RuntimeBarSprites.Create("Background", root, sortingOrder, sortingLayerName);
            lag = RuntimeBarSprites.Create("Lag", root, sortingOrder + 1, sortingLayerName);
            fill = RuntimeBarSprites.Create("Fill", root, sortingOrder + 2, sortingLayerName);
            flash = RuntimeBarSprites.Create("Flash", root, sortingOrder + 3, sortingLayerName);

            root.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------
        // Events
        // ------------------------------------------------------------------
        private void OnSpent(float amount)
        {
            lastChangeTime = Time.time;
            lagTimer = lagDelay;
        }

        private void OnDenied()
        {
            lastChangeTime = Time.time;
            deniedAlpha = 1f;
        }

        private void OnExhaustedChanged(bool exhausted)
        {
            lastChangeTime = Time.time;
        }

        // ------------------------------------------------------------------
        private void LateUpdate()
        {
            if (stamina == null || root == null)
                return;

            float dt = Time.deltaTime;
            float max = Mathf.Max(1f, stamina.Max);
            float current = Mathf.Clamp(stamina.Current, 0f, max);

            if (!Mathf.Approximately(current, lastValue))
            {
                lastValue = current;
                lastChangeTime = Time.time;
            }

            // Nachzieh-Leiste
            if (current >= lagValue)
                lagValue = current;
            else if (lagTimer > 0f)
                lagTimer -= dt;
            else
                lagValue = Mathf.MoveTowards(lagValue, current, lagSpeed * max * dt);

            // Sichtbarkeit
            bool needed = alwaysVisible || current < max - 0.01f || stamina.IsExhausted ||
                          Time.time - lastChangeTime < hideDelay;

            visibility = Mathf.MoveTowards(visibility, needed ? 1f : 0f,
                Time.unscaledDeltaTime / Mathf.Max(0.01f, fadeTime));

            bool visible = visibility > 0.002f;
            if (root.gameObject.activeSelf != visible)
                root.gameObject.SetActive(visible);

            if (!visible)
                return;

            float barWidth = healthBar != null ? healthBar.width : width;
            Follow();

            float left = -barWidth * 0.5f;

            background.transform.localPosition = new Vector3(left - 0.02f, 0f, 0f);
            background.transform.localScale = new Vector3(barWidth + 0.04f, height + 0.04f, 1f);
            Tint(background, backgroundColor);

            Place(lag, left, barWidth, lagValue / max);
            Tint(lag, lagColor);

            Place(fill, left, barWidth, current / max);
            Tint(fill, ChooseColor());

            // Roter Blitz bei abgelehnter Aktion
            if (deniedAlpha > 0f)
            {
                deniedAlpha -= Time.unscaledDeltaTime / 0.25f;
                Place(flash, left, barWidth, 1f);

                Color color = deniedColor;
                color.a = Mathf.Clamp01(deniedAlpha) * 0.7f;
                Tint(flash, color);
            }
            else if (flash.enabled)
            {
                flash.enabled = false;
            }
        }

        private Color ChooseColor()
        {
            if (!stamina.IsExhausted)
                return normalColor;

            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 10f);
            return Color.Lerp(exhaustedColor, Color.white, pulse * 0.3f);
        }

        /// <summary>Direkt unter der HealthBar (inklusive Platz für deren Anti-Heal-Streifen).</summary>
        private void Follow()
        {
            Vector3 position;

            if (healthBar != null)
            {
                Vector3 healthPosition = healthBar.WorldPosition;
                // Halbe Höhe der HealthBar, beide Hintergrundränder (je 0,02), Abstand, halbe Höhe dieser Leiste
                float below = healthBar.Height * 0.5f + 0.02f + gap + 0.02f + height * 0.5f;
                position = new Vector3(healthPosition.x, healthPosition.y - below, healthPosition.z);
            }
            else
            {
                offsetTimer -= Time.unscaledDeltaTime;

                if (offsetTimer <= 0f)
                {
                    offsetTimer = 0.5f;

                    if (targetSprite == null)
                        targetSprite = GetComponent<SpriteRenderer>();

                    if (targetSprite != null && targetSprite.sprite != null)
                        offsetY = targetSprite.bounds.max.y - transform.position.y + padding;
                }

                position = transform.position + new Vector3(0f, offsetY, 0f);
            }

            root.position = position;
            root.rotation = Quaternion.identity;
        }

        private void Place(SpriteRenderer sr, float left, float barWidth, float fraction)
        {
            fraction = Mathf.Clamp01(fraction);
            bool on = fraction > 0.0005f;

            if (sr.enabled != on)
                sr.enabled = on;

            if (!on)
                return;

            sr.transform.localPosition = new Vector3(left, 0f, 0f);
            sr.transform.localScale = new Vector3(barWidth * fraction, height, 1f);
        }

        private void Tint(SpriteRenderer sr, Color color)
        {
            color.a *= visibility;
            sr.color = color;
        }
    }
}