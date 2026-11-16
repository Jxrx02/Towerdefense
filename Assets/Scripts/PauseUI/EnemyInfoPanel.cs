using System;
using System.Globalization;
using ScriptableObjects;
using UnityEngine;
using UnityEngine.UI;

namespace TowerDefense
{
    /// <summary>
    /// Eigenes Panel mit allen Werten eines Gegners. Felder mit Wert 0 (bzw. leer/false) werden nicht angezeigt,
    /// Abschnitte ohne Zeilen auch nicht. Keine Reflection: jedes Feld des EnemyConfig steht explizit unten.
    /// </summary>
    public class EnemyInfoPanel : MonoBehaviour
    {
        [Header("Kopf")]
        [SerializeField] private Image icon;
        [SerializeField] private Text txtName;
        [SerializeField] private Text txtDescription;

        [Header("Werte")]
        [SerializeField] private Transform statContainer;
        [SerializeField] private GameObject statRowPrefab;      // Kinder "Label" und "Value" (Text)
        [SerializeField] private GameObject statHeaderPrefab;   // Kind "Label" (Text)

        [SerializeField] private Button btnClose;
        [SerializeField] private float animationFps = 8f;

        private Sprite[] _frames;
        private string _pendingHeader;

        public bool IsOpen => gameObject.activeSelf;

        private void Awake()
        {
            btnClose.onClick.AddListener(Close);
        }

        private void Update()
        {
            // Walk-Animation, unabhängig von Time.timeScale (Spiel ist pausiert)
            if (_frames == null || _frames.Length < 2 || icon == null) return;

            int frame = (int)(Time.unscaledTime * animationFps) % _frames.Length;
            icon.sprite = _frames[frame];
        }

        public void Open(EnemyConfig cfg)
        {
            if (cfg == null) return;

            txtName.text = cfg.name;

            bool hasDescription = !string.IsNullOrWhiteSpace(cfg.description);
            txtDescription.gameObject.SetActive(hasDescription);
            txtDescription.text = hasDescription ? StatMarkup.Format(cfg.description) : "";
            _frames = cfg.walkAnim;
            if (icon != null)
            {
                icon.sprite = _frames != null && _frames.Length > 0 ? _frames[0] : null;
                icon.enabled = icon.sprite != null;
            }

            BuildStats(cfg);
            gameObject.SetActive(true);
        }

        public void Close()
        {
            _frames = null;
            gameObject.SetActive(false);
        }

        // ── Werte ─────────────────────────────────────────────────

        private void BuildStats(EnemyConfig c)
        {
            for (int i = statContainer.childCount - 1; i >= 0; i--)
            {
                Transform child = statContainer.GetChild(i);
                child.SetParent(null);
                Destroy(child.gameObject);
            }

            Section("Core");
            if (c.health > 0)         Row("Health", c.health.ToString(), StatId.Health);
            if (c.movementSpeed > 0f) Row("Movement Speed", N(c.movementSpeed), StatId.MoveSpeed);
            if (c.tenacity > 0)       Row("Tenacity", c.tenacity + "%");
            if (c.xpDrop > 0)         Row("XP Drop", c.xpDrop.ToString(), StatId.XpDrop);
            if (c.canFly)             Row("Flying", "Yes");

            Section("Defense");
            if (c.armor > 0f)                Row("Armor", N(c.armor), StatId.Armor);
            if (c.magicResist > 0f)          Row("Magic Resist", N(c.magicResist), StatId.MagicResist);
            if (c.healthRegenPerSecond > 0f) Row("Health Regen", N(c.healthRegenPerSecond) + "/s", StatId.Regen);
            if (c.evasionChance > 0)         Row("Evasion", c.evasionChance + "%", StatId.Evasion);

            Section("Shields");
            if (c.startShield > 0f)
                Row("Shield", N(c.startShield) + (c.shieldDuration > 0f ? $"  ({N(c.shieldDuration)}s)" : ""), StatId.Shield);
            if (c.blockCharges > 0)
            {
                Row("Block Charges", c.blockCharges + (c.blockDuration > 0f ? $"  ({N(c.blockDuration)}s)" : ""), StatId.Block);
                if (c.blockRechargeTime > 0f)
                    Row("Block Recharge", N(c.blockRechargeTime) + "s", StatId.Block);
            }

            Section("Attack");
            if (c.attackDamage > 0)
            {
                Row("Damage", $"{c.attackDamage} {c.damageType}", StatId.Damage);
                Row("Attack Type", c.isMeele ? "Melee" : "Ranged");
            }
            if (c.attackCooldown > 0f) Row("Attack Cooldown", N(c.attackCooldown) + "s", StatId.AttackSpeed);
            if (c.attackRange > 0f)    Row("Attack Range", N(c.attackRange), StatId.Range);

            OffenseStats o = c.offense;
            if (o != null)
            {
                if (o.critChance > 0f)
                {
                    Row("Crit Chance", N(o.critChance * 100f) + "%", StatId.Crit);
                    if (o.critDamage > 0f) Row("Crit Damage", N(o.critDamage * 100f) + "%", StatId.CritDamage);
                }
                if (o.lethality > 0f)       Row("Lethality", N(o.lethality), StatId.Lethality);
                if (o.armorPenPercent > 0f) Row("Armor Pen", N(o.armorPenPercent * 100f) + "%", StatId.ArmorPen);
                if (o.magicPenFlat > 0f)    Row("Magic Pen", N(o.magicPenFlat), StatId.MagicPen);
                if (o.magicPenPercent > 0f) Row("Magic Pen", N(o.magicPenPercent * 100f) + "%", StatId.MagicPenPercent);
                if (o.omnivamp > 0f)        Row("Omnivamp", N(o.omnivamp * 100f) + "%", StatId.Omnivamp);
            }

            Section("Behaviour");
            if (c.aggroRange > 0f) Row("Aggro Range", N(c.aggroRange));
            if (c.leashRange > 0f) Row("Leash Range", N(c.leashRange));
            if (c.targetPriority != null && c.targetPriority.Length > 0)
                Row("Targets", string.Join(" > ", c.targetPriority));

            Section("Economy");
            if (c.goldReward > 0)           Row("Gold Reward", c.goldReward.ToString(), StatId.Gold);
            if (c.penaltyOnReachingEnd > 0) Row("Penalty at End", c.penaltyOnReachingEnd.ToString());
        }

        private void Row(string label, string value, StatId? stat = null)
        {
            if (_pendingHeader != null)
            {
                GameObject header = Instantiate(statHeaderPrefab, statContainer);
                header.transform.Find("Label").GetComponent<Text>().text = _pendingHeader.ToUpperInvariant();
                _pendingHeader = null;
            }

            GameObject row = Instantiate(statRowPrefab, statContainer);
            Text labelText = row.transform.Find("Label").GetComponent<Text>();
            labelText.text = label;
            row.transform.Find("Value").GetComponent<Text>().text = value;

            Font font = txtName != null ? txtName.font : null;

            if (stat.HasValue)
            {
                StatId id = stat.Value;
                labelText.color = StatPalette.ColorOf(id);   // gleiche Farbsprache wie überall

                if (StatInfoButton.Create(row.transform, font,
                        () => StatInfoPanel.Show(id, null, StatSubject.Enemy, font)) != null)
                    return;
            }

            StatInfoButton.CreateSpacer(row.transform);
        }

        /// <summary>Überschrift wird erst angelegt, wenn der Abschnitt mindestens eine Zeile bekommt.</summary>
        private void Section(string title) => _pendingHeader = title;

        private void Row(string label, string value)
        {
            if (_pendingHeader != null)
            {
                GameObject header = Instantiate(statHeaderPrefab, statContainer);
                header.transform.Find("Label").GetComponent<Text>().text = _pendingHeader.ToUpperInvariant();
                _pendingHeader = null;
            }

            GameObject row = Instantiate(statRowPrefab, statContainer);
            row.transform.Find("Label").GetComponent<Text>().text = label;
            row.transform.Find("Value").GetComponent<Text>().text = value;
        }

        private static string N(float value) => value.ToString("0.##", CultureInfo.InvariantCulture);
    }
}