using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TowerDefense
{
    /// <summary>
    /// Zeigt Schadenszahlen über einem Turm, Helden oder Gegner (wie StatDiffDisplay, nur für Treffer).
    /// Hört auf die Vitals-Events: Damaged (HP-Schaden, Schildschaden, Crit, Block) und Healed.
    /// </summary>
    public class DamageNumberSpawner : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("World-Space-Canvas als Kind des Objekts. Text-Positionen sind lokale Canvas-Einheiten.")]
        [SerializeField] private GameObject damageCanvas;
        [Tooltip("Prefab mit einer Unity-UI-Text-Komponente")]
        [SerializeField] private GameObject damageTextPrefab;
        [SerializeField] private int poolSize = 8;

        [Header("Animation")]
        [SerializeField] private float startY = 1f;
        [SerializeField] private float endY = 1.5f;
        [SerializeField] private float duration = 0.8f;
        [SerializeField] private float horizontalSpread = 0.25f;
        [Tooltip("Zusätzliche Größe bei kritischen Treffern (1 = keine)")]
        [SerializeField] private float critPunchScale = 1.5f;
        [Tooltip("Schildschaden-Zahl erscheint so viel tiefer als die HP-Zahl, damit sich beide nicht überlagern")]
        [SerializeField] private float shieldRowOffset = 0.18f;
        [Tooltip("Wie weit eine Zahl beim Aufsteigen zur Seite driftet (Richtung zufällig links oder rechts)")]
        [SerializeField] private float horizontalDrift = 0.35f;
        [Header("Colors")]
        [SerializeField] private Color normalDamageColor = Color.white;
        [SerializeField] private Color criticalDamageColor = new Color(1f, 0.25f, 0.15f);
        [SerializeField] private Color shieldDamageColor = new Color(0.4f, 0.8f, 1f);
        [SerializeField] private Color blockedColor = new Color(0.75f, 0.45f, 1f);
        [SerializeField] private Color healColor = new Color(0.35f, 1f, 0.45f);

        [Header("Text")]
        [SerializeField] private string criticalPrefix = "CRIT ";
        [SerializeField] private string blockedText = "BLOCK";
        [SerializeField] private string healPrefix = "+";

        [Header("Heilung")]
        [SerializeField] private bool showHealNumbers = true;
        [Tooltip("Kleinere Heilungen (z. B. Omnivamp-Tropfen) werden nicht angezeigt")]
        [SerializeField] private float minHealToShow = 1f;

        [Header("Verhalten")]
        [Tooltip("Hält die Zahlen aufrecht, auch wenn der Gegner per Rotation gespiegelt wird")]
        [SerializeField] private bool keepUpright = true;
        [Tooltip("Gegner: Canvas beim Tod lösen, damit die letzte Zahl noch zu Ende läuft")]
        [SerializeField] private bool detachOnDeath = true;

        [Header("Hit Stop")]
        [Tooltip("Frames Hit Stop bei einem kritischen Treffer auf dieses Objekt (0 = aus)")]
        [SerializeField] private int hitStopFramesOnCrit = 3;
        [Tooltip("Frames Hit Stop bei jedem Treffer auf dieses Objekt (0 = aus). Sinnvoll nur für den Helden.")]
        [SerializeField] private int hitStopFramesOnHit = 0;

        public Vitals vitals;
        private bool bound;
        private bool poolBuilt;
        private readonly List<FloatingNumber> pool = new List<FloatingNumber>();

        // --------------------For Wallgroup-----------------------------

        public GameObject DamageTextPrefab => damageTextPrefab;
        public GameObject DamageCanvas => damageCanvas;
        
        public void Initialize(GameObject canvas, GameObject textPrefab)
        {
            damageCanvas = canvas;
            damageTextPrefab = textPrefab;

            if (damageCanvas == null)
                Debug.LogError($"[{nameof(DamageNumberSpawner)}] Canvas fehlt auf {name}.", this);

            if (damageTextPrefab == null)
                Debug.LogError($"[{nameof(DamageNumberSpawner)}] Text-Prefab fehlt auf {name}.", this);
        }
        
        public void ForwardDamageEvent(DamageResult result)
        {
            HandleDamaged(result);
        }
        public void ForwardHealEvent(float heal)
        {
            HandleHealed(heal);
        }
        // ------------------------------------------------------------------
        private void Awake()
        {
            ResolveVitals();
        }

        private void Start()
        {
            // Vitals kann erst jetzt existieren (Tower fügt es in Awake zur Laufzeit hinzu)
            BuildPool();
            Bind();
        }

        private void OnEnable()
        {
            Bind();
        }

        private void OnDisable()
        {
            Unbind();
        }

        private void OnDestroy()
        {
            Unbind();
        }

        private void LateUpdate()
        {
            if (keepUpright && damageCanvas != null)
                damageCanvas.transform.rotation = Quaternion.identity;
        }

        // ------------------------------------------------------------------
        private void ResolveVitals()
        {
            if (vitals == null) vitals = GetComponent<Vitals>();
            if (vitals == null) vitals = GetComponentInParent<Vitals>();
        }

        /// <summary>Idempotent: darf aus Start und OnEnable aufgerufen werden.</summary>
        private void Bind()
        {
            if (bound) return;

            ResolveVitals();
            if (vitals == null) return;

            vitals.Damaged += HandleDamaged;
            vitals.Healed += HandleHealed;
            vitals.Died += HandleDied;
            bound = true;
        }

        private void Unbind()
        {
            if (!bound || vitals == null)
            {
                bound = false;
                return;
            }

            vitals.Damaged -= HandleDamaged;
            vitals.Healed -= HandleHealed;
            vitals.Died -= HandleDied;
            bound = false;
        }

        private void BuildPool()
        {
            if (poolBuilt) return;

            if (damageCanvas == null)
            {
                Debug.LogWarning($"{nameof(DamageNumberSpawner)} auf {name}: kein Damage-Canvas zugewiesen.", this);
                return;
            }

            if (damageTextPrefab == null)
            {
                Debug.LogWarning($"{nameof(DamageNumberSpawner)} auf {name}: kein Text-Prefab zugewiesen.", this);
                return;
            }

            if (damageTextPrefab.GetComponent<Text>() == null)
            {
                Debug.LogError($"{nameof(DamageNumberSpawner)} auf {name}: Das Text-Prefab braucht eine Unity-UI-Text-Komponente.", this);
                return;
            }

            for (int i = 0; i < poolSize; i++)
            {
                GameObject go = Instantiate(damageTextPrefab, damageCanvas.transform);
                go.SetActive(false);
                go.transform.localPosition = new Vector3(0f, startY, 0f);

                FloatingNumber number = go.GetComponent<FloatingNumber>();
                if (number == null)
                    number = go.AddComponent<FloatingNumber>();

                pool.Add(number);
            }

            poolBuilt = true;
        }

        // ------------------------------------------------------------------
        // Events
        // ------------------------------------------------------------------
        private void HandleDamaged(DamageResult result)
        {
            if (result.blocked)
            {
                Spawn(blockedText, blockedColor, true, 1f, 0f);
                return;
            }

            bool shown = false;

            if (result.healthDamage > 0f)
            {
                int amount = Mathf.Max(1, Mathf.RoundToInt(result.healthDamage));
                string value = (result.isCrit ? criticalPrefix : "") + amount;

                Spawn(value,
                    result.isCrit ? criticalDamageColor : normalDamageColor,
                    result.isCrit,
                    result.isCrit ? critPunchScale : 1f,
                    0f);

                shown = true;
            }

            if (result.shieldDamage > 0f)
            {
                int amount = Mathf.Max(1, Mathf.RoundToInt(result.shieldDamage));
                Spawn("-" + amount, shieldDamageColor, false, 1f, -shieldRowOffset);
                shown = true;
            }

            if (!shown)
                return;

            int frames = result.isCrit ? Mathf.Max(hitStopFramesOnCrit, hitStopFramesOnHit) : hitStopFramesOnHit;
            if (frames > 0)
                HitStop.Request(frames);
        }

        private void HandleHealed(float amount)
        {
            if (!showHealNumbers || amount < minHealToShow)
                return;

            Spawn(healPrefix + Mathf.RoundToInt(amount), healColor, false, 1f, 0f);
        }

        private void HandleDied(Vitals v)
        {
            // Gegner werden zerstört. Canvas lösen, damit die letzte Zahl noch zu Ende läuft.
            if (!detachOnDeath || damageCanvas == null || !TryGetComponent<Enemy>(out _))
                return;

            damageCanvas.transform.SetParent(null, true);
            Destroy(damageCanvas, duration + 0.25f);
            damageCanvas = null;
            keepUpright = false;
        }

        // ------------------------------------------------------------------
        // Zahlen
        // ------------------------------------------------------------------
        private void Spawn(string value, Color color, bool bold, float punch, float yOffset)
        {
            FloatingNumber number = GetNumber();
            if (number == null)
                return;

            float startX = Random.Range(-horizontalSpread, horizontalSpread);

            // Jede Zahl driftet in eine zufällige Richtung, mit etwas Variation in Weite und Höhe
            float direction = Random.value < 0.5f ? -1f : 1f;
            float endX = startX + direction * Random.Range(horizontalDrift * 0.4f, horizontalDrift);
            float endHeight = endY + Random.Range(-0.08f, 0.08f);

            Vector3 from = new Vector3(startX, startY + yOffset, 0f);
            Vector3 to = new Vector3(endX, endHeight + yOffset, 0f);

            number.Play(value, color, bold, from, to, duration, punch);
        }

        /// <summary>Freie Zahl, sonst die älteste laufende wiederverwenden (nie Zahlen verlieren, nie neu instanziieren).</summary>
        private FloatingNumber GetNumber()
        {
            FloatingNumber oldest = null;

            foreach (FloatingNumber number in pool)
            {
                if (number == null) continue;

                if (!number.IsPlaying)
                    return number;

                if (oldest == null || number.Age > oldest.Age)
                    oldest = number;
            }

            return oldest;
        }
    }
    
}