using System.Collections.Generic;
using UnityEngine;

namespace TowerDefense
{
    /// <summary>
    /// HP-Leiste für Turm, Held und Gegner. Baut sich zur Laufzeit selbst aus Sprites (kein Prefab nötig)
    /// und hängt an den Vitals-Events.
    ///
    ///   Nachzieh-Leiste (gelb)   zeigt, wie viel der letzte Treffer gekostet hat
    ///   Block-Schild             HP-Leiste wird lila, kleine Punkte zeigen mehrere Ladungen
    ///   HP-Schild                weißer Abschnitt hinter den HP, die Leiste verlängert sich entsprechend
    ///   Heilung                  grünes Aufblitzen
    ///   Anti-Heal                dünner dunkelroter Streifen unter der Leiste
    ///   Kritisch                 unter 25 % pulsiert die Leiste rot
    ///   Teilstriche              bei großen Lebenspools alle 100 (bzw. 1000) HP, jede 10. höher
    /// Die Leiste hängt nicht am Objekt, deshalb wird sie von Spiegelung und Rotation nicht beeinflusst.
    /// </summary>
    [DisallowMultipleComponent]
    public class HealthBar : MonoBehaviour
    {
        [Header("Größe & Position")]
        public float width = 1f;
        public float height = 0.12f;
        [Tooltip("Abstand zwischen Oberkante des Sprites und der Leiste")]
        public float padding = 0.12f;
        [Tooltip("Höhe automatisch aus dem Sprite bestimmen")]
        public bool autoOffset = true;
        [Tooltip("Zusätzlicher Versatz (bei autoOffset ein Feintuning)")]
        public Vector2 manualOffset;

        [Header("Sichtbarkeit")]
        [Tooltip("Immer anzeigen (z. B. beim Helden). Sonst nur bei Schaden, Schild oder Block.")]
        public bool alwaysVisible;
        [Tooltip("Sekunden, die die Leiste nach der letzten Änderung sichtbar bleibt, wenn sie voll ist")]
        public float hideDelay = 2.5f;
        public float fadeTime = 0.25f;

        [Header("Farben")]
        public Color backgroundColor = new Color(0f, 0f, 0f, 0.65f);
        public Color healthColor = new Color(0.25f, 0.85f, 0.35f);
        public Color blockColor = new Color(0.65f, 0.35f, 1f);
        public Color shieldColor = Color.white;
        public Color lagColor = new Color(1f, 0.85f, 0.2f, 0.95f);
        public Color criticalColor = new Color(0.95f, 0.15f, 0.1f);
        public Color healFlashColor = new Color(0.7f, 1f, 0.75f);
        public Color antiHealColor = new Color(0.55f, 0.05f, 0.15f);
        public Color tickColor = new Color(0f, 0f, 0f, 0.55f);

        [Header("Nachzieh-Leiste")]
        public float lagDelay = 0.35f;
        [Tooltip("Anteil der Leiste pro Sekunde, um den die Nachzieh-Leiste schrumpft")]
        public float lagSpeed = 0.9f;

        [Header("Kritisch")]
        [Range(0f, 1f)] public float criticalThreshold = 0.25f;
        public float criticalPulseSpeed = 8f;

        [Header("Teilstriche")]
        [Tooltip("HP pro Teilstrich. 0 = automatisch (100 oder 1000, erst ab minHealthForTicks)")]
        public float tickEveryHP = 0f;
        public float minHealthForTicks = 250f;
        public int maxTicks = 24;
        [Tooltip("Jeder n-te Strich ist höher (bei 100 HP pro Strich also alle 1000 HP)")]
        public int majorTickEvery = 10;
        public float tickWidth = 0.015f;

        [Header("Block-Anzeige")]
        public bool showBlockPips = true;
        public int maxPips = 5;

        [Header("Sorting")]
        public string sortingLayerName = "Player";
        public int sortingOrder = 200;

        // ---------- intern ----------
        private Vitals vitals;
        private bool bound;
        private Transform root;
        private SpriteRenderer bg, lag, hp, shield, flash, antiHeal;
        private readonly List<SpriteRenderer> ticks = new List<SpriteRenderer>();
        private readonly List<SpriteRenderer> pips = new List<SpriteRenderer>();
        private SpriteRenderer targetSprite;

        private float offsetY = 1f;
        private float offsetTimer;
        private float visibility;
        private float lastChangeTime = -999f;
        private float lagValue;
        private float lagTimer;
        private float flashAlpha;
        private Color flashColor;
        private bool flashFullBar;

        private float lastHp = -1f, lastShield = -1f, lastMax = -1f;
        private int lastCharges = -1;
        private float tickScaleCache = -1f, tickMaxCache = -1f;

        private static Sprite whiteSprite;
        private static Material spriteMaterial;
        private static Transform container;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            whiteSprite = null;
            spriteMaterial = null;
            container = null;
        }

        private static Sprite WhiteSprite
        {
            get
            {
                if (whiteSprite == null)
                {
                    Texture2D tex = Texture2D.whiteTexture;
                    // Pivot links/mittig, 1 Einheit breit: scale.x = Länge in Welteinheiten
                    whiteSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
                        new Vector2(0f, 0.5f), tex.width, 0, SpriteMeshType.FullRect);
                }
                return whiteSprite;
            }
        }

        private static Material SpriteMaterial
        {
            get
            {
                if (spriteMaterial == null)
                {
                    // Unlit, damit die Leiste auch nachts (2D-Lichter) lesbar bleibt
                    spriteMaterial = new Material(Shader.Find("Sprites/Default")) { hideFlags = HideFlags.HideAndDontSave };
                }
                return spriteMaterial;
            }
        }

        private static Transform Container
        {
            get
            {
                if (container == null)
                    container = new GameObject("HealthBars").transform;
                return container;
            }
        }

        // ------------------------------------------------------------------
        private void Start()
        {
            ResolveVitals();

            if (vitals == null)
            {
                Debug.LogWarning($"HealthBar auf {name}: keine Vitals-Komponente gefunden.", this);
                enabled = false;
                return;
            }

            BuildBar();
            Bind();

            lagValue = vitals.Health + vitals.Shield;
            visibility = alwaysVisible ? 1f : 0f;
        }

        private void OnEnable()
        {
            Bind();
            if (root != null) root.gameObject.SetActive(true);
        }

        private void OnDisable()
        {
            Unbind();
            if (root != null) root.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            Unbind();
            if (root != null) Destroy(root.gameObject);
        }

        private void ResolveVitals()
        {
            if (vitals == null) vitals = GetComponent<Vitals>();
            if (vitals == null) vitals = GetComponentInParent<Vitals>();
        }

        private void Bind()
        {
            if (bound) return;

            ResolveVitals();
            if (vitals == null) return;

            vitals.Damaged += OnDamaged;
            vitals.Healed += OnHealed;
            vitals.ShieldChanged += OnShieldChanged;
            vitals.BlockShieldChanged += OnBlockChanged;
            bound = true;
        }

        private void Unbind()
        {
            if (!bound || vitals == null)
            {
                bound = false;
                return;
            }

            vitals.Damaged -= OnDamaged;
            vitals.Healed -= OnHealed;
            vitals.ShieldChanged -= OnShieldChanged;
            vitals.BlockShieldChanged -= OnBlockChanged;
            bound = false;
        }

        // ------------------------------------------------------------------
        // Events
        // ------------------------------------------------------------------
        private void OnDamaged(DamageResult result)
        {
            lastChangeTime = Time.time;

            if (result.blocked)
            {
                Flash(blockColor, true);
                return;
            }

            if (result.healthDamage > 0f || result.shieldDamage > 0f)
                lagTimer = lagDelay;   // Nachzieh-Leiste bleibt kurz stehen
        }

        private void OnHealed(float amount)
        {
            lastChangeTime = Time.time;
            Flash(healFlashColor, false);
        }

        private void OnShieldChanged(float value)
        {
            lastChangeTime = Time.time;
        }

        private void OnBlockChanged(int charges)
        {
            lastChangeTime = Time.time;
        }

        private void Flash(Color color, bool fullBar)
        {
            flashColor = color;
            flashFullBar = fullBar;
            flashAlpha = 1f;
        }

        // ------------------------------------------------------------------
        // Aufbau
        // ------------------------------------------------------------------
        private void BuildBar()
        {
            root = new GameObject($"HealthBar ({name})").transform;
            root.SetParent(Container, false);

            bg = CreateSprite("Background", 0);
            lag = CreateSprite("Lag", 1);
            hp = CreateSprite("Health", 2);
            shield = CreateSprite("Shield", 3);
            flash = CreateSprite("Flash", 4);
            antiHeal = CreateSprite("AntiHeal", 5);

            for (int i = 0; i < Mathf.Max(0, maxPips); i++)
                pips.Add(CreateSprite("Pip", 7));

            for (int i = 0; i < Mathf.Max(0, maxTicks); i++)
                ticks.Add(CreateSprite("Tick", 6));

            foreach (SpriteRenderer sr in ticks) sr.enabled = false;
            foreach (SpriteRenderer sr in pips) sr.enabled = false;
        }

        private SpriteRenderer CreateSprite(string spriteName, int orderOffset)
        {
            GameObject go = new GameObject(spriteName);
            go.transform.SetParent(root, false);

            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = WhiteSprite;
            sr.sharedMaterial = SpriteMaterial;
            sr.sortingOrder = sortingOrder + orderOffset;

            if (HasSortingLayer(sortingLayerName))
                sr.sortingLayerName = sortingLayerName;

            return sr;
        }

        private static bool HasSortingLayer(string layerName)
        {
            foreach (SortingLayer layer in SortingLayer.layers)
                if (layer.name == layerName) return true;
            return false;
        }

        // ------------------------------------------------------------------
        // Update
        // ------------------------------------------------------------------
        private void LateUpdate()
        {
            if (root == null || vitals == null)
                return;

            float dt = Time.deltaTime;

            float max = Mathf.Max(1f, vitals.MaxHealth);
            float hpValue = Mathf.Clamp(vitals.Health, 0f, max);
            float shieldValue = vitals.Shield;
            bool block = vitals.HasBlock;
            int charges = vitals.BlockCharges;
            bool dead = vitals.IsDead;

            // Änderungen erkennen (Regeneration, Max-HP-Änderung usw. melden keine Events)
            if (!Mathf.Approximately(hpValue, lastHp) || !Mathf.Approximately(shieldValue, lastShield) ||
                !Mathf.Approximately(max, lastMax) || charges != lastCharges)
            {
                lastChangeTime = Time.time;
                lastHp = hpValue;
                lastShield = shieldValue;
                lastMax = max;
                lastCharges = charges;
            }

            float total = hpValue + shieldValue;
            float scale = Mathf.Max(max, total);   // Schild verlängert die Leiste

            // Nachzieh-Leiste
            if (total >= lagValue)
            {
                lagValue = total;
            }
            else if (lagTimer > 0f)
            {
                lagTimer -= dt;
            }
            else
            {
                lagValue = Mathf.MoveTowards(lagValue, total, lagSpeed * scale * dt);
            }

            // Sichtbarkeit
            bool needed = !dead && (alwaysVisible || hpValue < max - 0.01f || shieldValue > 0.01f || block ||
                                    vitals.AntiHealPercent > 0f || Time.time - lastChangeTime < hideDelay);

            visibility = Mathf.MoveTowards(visibility, needed ? 1f : 0f,
                Time.unscaledDeltaTime / Mathf.Max(0.01f, fadeTime));

            bool visible = visibility > 0.002f;
            if (root.gameObject.activeSelf != visible)
                root.gameObject.SetActive(visible);

            if (!visible)
                return;

            Follow();

            float hpFrac = hpValue / scale;
            float totalFrac = total / scale;
            float lagFrac = Mathf.Clamp01(lagValue / scale);

            // Hintergrund (mit kleinem Rand)
            Vector3 bgPos = new Vector3(-width * 0.5f - 0.02f, 0f, 0f);
            bg.transform.localPosition = bgPos;
            bg.transform.localScale = new Vector3(width + 0.04f, height + 0.04f, 1f);
            Tint(bg, backgroundColor);

            // Nachzieh-, HP- und Schildleiste
            Place(lag, 0f, lagFrac);
            Tint(lag, lagColor);

            Place(hp, 0f, hpFrac);
            Tint(hp, ChooseHealthColor(hpValue / max, block));

            Place(shield, hpFrac, totalFrac);
            Tint(shield, shieldColor);

            // Aufblitzen (Heilung grün, Block lila über die ganze Leiste)
            if (flashAlpha > 0f)
            {
                flashAlpha -= Time.unscaledDeltaTime / 0.3f;
                Place(flash, 0f, flashFullBar ? 1f : Mathf.Max(hpFrac, 0.02f));
                Color f = flashColor;
                f.a = Mathf.Clamp01(flashAlpha) * 0.7f;
                Tint(flash, f);
            }
            else if (flash.enabled)
            {
                flash.enabled = false;
            }

            // Anti-Heal-Streifen unter der Leiste
            float anti = vitals.AntiHealPercent;
            if (anti > 0f)
            {
                float h = height * 0.25f;
                Place(antiHeal, 0f, 1f, -(height * 0.5f + h * 0.5f + 0.01f), h);
                Color a = antiHealColor;
                a.a = Mathf.Lerp(0.5f, 1f, Mathf.Clamp01(anti));
                Tint(antiHeal, a);
            }
            else if (antiHeal.enabled)
            {
                antiHeal.enabled = false;
            }

            UpdateTicks(max, scale);
            UpdatePips(block, charges);
        }

        private Color ChooseHealthColor(float healthPercent, bool block)
        {
            if (block)
                return blockColor;

            if (healthPercent <= criticalThreshold)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * criticalPulseSpeed);
                return Color.Lerp(criticalColor, Color.Lerp(criticalColor, Color.white, 0.35f), pulse);
            }

            return healthColor;
        }

        private void Follow()
        {
            offsetTimer -= Time.unscaledDeltaTime;

            if (autoOffset && offsetTimer <= 0f)
            {
                offsetTimer = 0.5f;

                if (targetSprite == null)
                    targetSprite = GetComponent<SpriteRenderer>();

                if (targetSprite != null && targetSprite.sprite != null)
                    offsetY = targetSprite.bounds.max.y - transform.position.y + padding;
            }

            Vector3 p = transform.position;
            float y = (autoOffset ? offsetY : 0f) + manualOffset.y;

            root.position = new Vector3(p.x + manualOffset.x, p.y + y, p.z);
            root.rotation = Quaternion.identity;
        }

        // ------------------------------------------------------------------
        // Teilstriche und Block-Punkte
        // ------------------------------------------------------------------
        private float ResolveTickStep(float max)
        {
            if (tickEveryHP > 0f)
                return tickEveryHP;

            if (max < minHealthForTicks)
                return 0f;

            float step = max / 100f <= maxTicks ? 100f : 1000f;
            if (max / step > maxTicks) step = 10000f;
            return step;
        }

        private void UpdateTicks(float max, float scale)
        {
            if (Mathf.Approximately(scale, tickScaleCache) && Mathf.Approximately(max, tickMaxCache))
            {
                // Farbe/Alpha bei Fade mitführen
                foreach (SpriteRenderer sr in ticks)
                    if (sr.enabled) Tint(sr, tickColor);
                return;
            }

            tickScaleCache = scale;
            tickMaxCache = max;

            float step = ResolveTickStep(max);
            int used = 0;

            if (step > 0f)
            {
                for (int k = 1; k * step < scale && used < ticks.Count; k++)
                {
                    float frac = k * step / scale;
                    bool major = majorTickEvery > 0 && k % majorTickEvery == 0;

                    float h = major ? height : height * 0.5f;
                    float y = major ? 0f : -height * 0.25f;

                    SpriteRenderer sr = ticks[used++];
                    sr.enabled = true;
                    sr.transform.localPosition = new Vector3(-width * 0.5f + frac * width - tickWidth * 0.5f, y, 0f);
                    sr.transform.localScale = new Vector3(tickWidth, h, 1f);
                    Tint(sr, tickColor);
                }
            }

            for (int i = used; i < ticks.Count; i++)
                ticks[i].enabled = false;
        }

        private void UpdatePips(bool block, int charges)
        {
            // Bei 1 Ladung reicht die lila Leiste. Zeitbasierte Blocks (int.MaxValue) bekommen keine Punkte.
            int count = showBlockPips && block && charges > 1 && charges <= pips.Count ? charges : 0;

            float size = height * 0.7f;
            float spacing = size * 1.5f;
            float y = height * 0.5f + size * 0.75f;

            for (int i = 0; i < pips.Count; i++)
            {
                SpriteRenderer sr = pips[i];

                if (i >= count)
                {
                    if (sr.enabled) sr.enabled = false;
                    continue;
                }

                sr.enabled = true;
                float x = (i - (count - 1) * 0.5f) * spacing - size * 0.5f;
                sr.transform.localPosition = new Vector3(x, y, 0f);
                sr.transform.localScale = new Vector3(size, size, 1f);
                Tint(sr, blockColor);
            }
        }

        // ------------------------------------------------------------------
        // Zeichnen
        // ------------------------------------------------------------------
        /// <summary>Platziert einen Balken von 'from' bis 'to' (Anteile 0..1 der Leistenbreite).</summary>
        private void Place(SpriteRenderer sr, float from, float to, float y = 0f, float h = -1f)
        {
            float len = Mathf.Max(0f, to - from);
            bool on = len > 0.0005f;

            if (sr.enabled != on)
                sr.enabled = on;

            if (!on)
                return;

            Transform t = sr.transform;
            t.localPosition = new Vector3(-width * 0.5f + from * width, y, 0f);
            t.localScale = new Vector3(len * width, h < 0f ? height : h, 1f);
        }

        private void Tint(SpriteRenderer sr, Color color)
        {
            color.a *= visibility;
            sr.color = color;
        }
    }
}