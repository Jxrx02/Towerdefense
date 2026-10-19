using System;
using System.Collections.Generic;
using ScriptableObjects;
using UnityEngine;
using UnityEngine.UI;

namespace TowerDefense
{
    public class InteractionPopup : MonoBehaviour
    {
        public static InteractionPopup instance;

        private enum PopupMode
        {
            None,
            Wall,
            TowerPurchase,
            TowerUpgrade,
            Inspect          // Klick auf Turm, Held oder Gegner: aktuelle Stats
        }

        [Header("Coins")]
        [SerializeField] private Transform coinContainer;
        [SerializeField] private CoinUI coinPrefab;

        [Header("Upgrade Info")]
        [SerializeField] private GameObject upgradeInfoPanel;
        [SerializeField] private Text upgradeTitle;
        [SerializeField] private Text upgradeDescription;
        [SerializeField] private Text upgradeCost;
        [Header("Upgrade Stats")]
        [SerializeField] private Transform upgradeStatsContainer;
        [SerializeField] private UpgradeStatEntry upgradeStatPrefab;

        [SerializeField] private Text interactionText;

        [Header("Tower Selection")]
        [SerializeField] private GameObject towerSelectionPanel;
        [SerializeField] private Transform towerSelectionContainer;
        [SerializeField] private TowerSelectionEntry towerSelectionEntryPrefab;
        [Tooltip("Türme, die an einer Baustelle gekauft werden können.")]
        [SerializeField] private List<GameObject> towerPrefabs = new List<GameObject>();
        [SerializeField] private KeyCode previousKey = KeyCode.Q;
        [SerializeField] private KeyCode nextKey = KeyCode.R;
        [Header("Input")]
        [SerializeField] private bool allowMouseSelection = true;
        [SerializeField] private bool allowWasdSelection = true;
        [SerializeField] private bool allowArrowSelection = true;
        [Header("Tower Info (nur bei gekauftem Turm)")]
        [SerializeField] private Text txtLevel;
        [SerializeField] private Slider experienceBar;
        [SerializeField] private Text experienceText;
        [SerializeField] private GameObject targetTypeRoot;   // Panel:TargetType
        [SerializeField] private Text txtTargetPreference;
        [SerializeField] private Button btnPrevTarget;
        [SerializeField] private Button btnNextTarget;
        [Header("Sell (Turm + Wall)")]
        [SerializeField] private GameObject sellRoot;         // Pnl:Sell
        [SerializeField] private Button btnSell;
        [SerializeField] private Text txtSellPrice;
        private Action wallSellAction;

        // Cache, damit die Texte nur bei Änderung neu geschrieben werden
        private int shownLevel = -1;
        private int shownExp = -1;
        private int shownReqExp = -1;
        private int shownSellPrice = -1;
        private TargetType shownTarget;

        [Header("Position")]
        [SerializeField] private Vector3 worldOffset = new Vector3(0f, 0.8f, 0f);

        [Header("Stat Icons")]
        [SerializeField] public Sprite damageIcon;
        [SerializeField] public Sprite rangeIcon;
        [SerializeField] public Sprite attackSpeedIcon;
        [SerializeField] public Sprite goldIcon;
        [SerializeField] public Sprite regenIcon;
        [SerializeField] public Sprite healthIcon;
        [SerializeField] public Sprite damageMultiplierIcon;
        [SerializeField] public Sprite rangeMultiplierIcon;
        [SerializeField] public Sprite attackSpeedMultiplierIcon;
        [SerializeField] public Sprite slowMultiplierIcon;

        [Header("Stat Icons (LoL-Stats)")]
        [Tooltip("Fehlt ein Icon, steht stattdessen der Stat-Name hinter dem Wert.")]
        [SerializeField] public Sprite armorIcon;
        [SerializeField] public Sprite magicResistIcon;
        [SerializeField] public Sprite critChanceIcon;
        [SerializeField] public Sprite critDamageIcon;
        [SerializeField] public Sprite lethalityIcon;
        [SerializeField] public Sprite armorPenIcon;
        [SerializeField] public Sprite magicPenIcon;
        [SerializeField] public Sprite omnivampIcon;

        [Header("Stat Icons (Inspektion)")]
        [SerializeField] public Sprite shieldIcon;
        [SerializeField] public Sprite blockIcon;
        [SerializeField] public Sprite antiHealIcon;
        [SerializeField] public Sprite moveSpeedIcon;
        [SerializeField] public Sprite evasionIcon;
        [SerializeField] public Sprite levelIcon;

        [Header("Inspektion (Klick auf Turm, Held, Gegner)")]
        [Tooltip("Sekunden zwischen zwei Aktualisierungen der angezeigten Werte")]
        [SerializeField] private float inspectRefreshInterval = 0.25f;

        private readonly List<CoinUI> coins = new List<CoinUI>();

        private RectTransform rectTransform;
        private Canvas canvas;

        private int requiredCoins;
        private int filledCoins;

        // Tower-Modus
        private PopupMode mode = PopupMode.None;
        private object owner;
        private Vector3 anchorPosition;
        private Tower upgradeTarget;
        private readonly List<Tower> purchaseTowers = new List<Tower>();
        private readonly List<TowerSelectionEntry> entries = new List<TowerSelectionEntry>();
        private int selectedIndex = -1;

        // Inspektion
        private object inspectTarget;
        private string inspectKey;
        private float inspectTimer;
        private readonly List<InspectStat> inspectBuffer = new List<InspectStat>();
        private readonly List<UpgradeStatEntry> inspectEntries = new List<UpgradeStatEntry>();

        /// <summary>Index des aktuell gewählten Turms (Kauf) bzw. Upgrade-Pfads (Upgrade).</summary>
        public int SelectedIndex => selectedIndex;

        private void Awake()
        {
            if (instance == null)
                instance = this;
            else
            {
                Destroy(gameObject);
                return;
            }

            rectTransform = GetComponent<RectTransform>();
            canvas = GetComponentInParent<Canvas>();

            Hide();

            if (btnPrevTarget != null) btnPrevTarget.onClick.AddListener(OnPrevTargetClicked);
            if (btnNextTarget != null) btnNextTarget.onClick.AddListener(OnNextTargetClicked);
            if (btnSell != null)       btnSell.onClick.AddListener(OnSellClicked);
        }

        private void Update()
        {
            if (mode == PopupMode.Inspect)
            {
                UpdateInspect();
                return;
            }

            if (mode == PopupMode.TowerUpgrade)
                RefreshTowerInfo();

            if (mode != PopupMode.TowerPurchase && mode != PopupMode.TowerUpgrade)
                return;

            // Während E gehalten wird, bleibt die Auswahl fix
            if (Input.GetKey(KeyCode.E))
                return;

            bool next =
                (allowArrowSelection && (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.DownArrow))) ||
                (allowWasdSelection  && (Input.GetKeyDown(KeyCode.D)          || Input.GetKeyDown(KeyCode.S)));

            bool previous =
                (allowArrowSelection && (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.UpArrow))) ||
                (allowWasdSelection  && (Input.GetKeyDown(KeyCode.A)         || Input.GetKeyDown(KeyCode.W)));

            if (next)
                SelectNext();
            else if (previous)
                SelectPrevious();
        }

        public void SelectNext()     => ChangeSelection(1);
        public void SelectPrevious() => ChangeSelection(-1);

        // --------------------------------------------------
        // SHOW (Wall – unverändert, nur Modus-Reset)
        // --------------------------------------------------

        public void Show(
            Vector3 worldPosition,
            string interactiontext,
            string title,
            int goldCost,
            string description,
            bool isUpgrade
        )
        {
            mode = PopupMode.Wall;
            owner = null;
            upgradeTarget = null;
            ClearEntries();
            SetTowerInfoActive(false);
            wallSellAction = null;
            if (towerSelectionPanel != null)
                towerSelectionPanel.SetActive(false);

            ShowInternal(worldPosition, interactiontext, title, goldCost, description, isUpgrade);
        }

        private void ShowInternal(
            Vector3 worldPosition,
            string interactiontext,
            string title,
            int goldCost,
            string description,
            bool isUpgrade
        )
        {
            requiredCoins = Mathf.CeilToInt(goldCost / 10f);

            CreateCoins(requiredCoins);

            filledCoins = 0;

            SetCoinsProgress(0);

            if (isUpgrade)
            {
                if (upgradeInfoPanel != null)
                    upgradeInfoPanel.SetActive(true);

                if (upgradeTitle != null)
                    upgradeTitle.text = title;

                if (upgradeDescription != null)
                    upgradeDescription.text = description;

                if (upgradeCost != null)
                    upgradeCost.text = $"{goldCost} Gold";
            }

            Position(worldPosition);
            interactionText.text = interactiontext;
            gameObject.SetActive(true);

            RebuildLayout();
        }

        public void ShowUpgradeStats(List<UpgradeStatData> stats)
        {
            if (upgradeStatsContainer == null || upgradeStatPrefab == null)
                return;

            DestroyChildren(upgradeStatsContainer);

            if (stats != null)
            {
                foreach (UpgradeStatData stat in stats)
                {
                    if (stat == null)
                        continue;

                    UpgradeStatEntry entry = Instantiate(upgradeStatPrefab, upgradeStatsContainer);

                    entry.Setup(
                        stat.icon, stat.oldValue, stat.newValue, stat.color,
                        stat.suffixNewValueOnly, stat.suffix, stat.decimals);
                }
            }

            RebuildLayout();
        }

        private void DestroyChildren(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                Transform child = parent.GetChild(i);
                child.SetParent(null);          // zählt sofort nicht mehr im Layout
                Destroy(child.gameObject);
            }
        }

        private void RebuildLayout()
        {
            if (rectTransform == null || !gameObject.activeInHierarchy)
                return;

            Canvas.ForceUpdateCanvases();

            // Von innen nach außen, damit jeder ContentSizeFitter die fertige Kindgröße kennt
            if (upgradeStatsContainer != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(upgradeStatsContainer as RectTransform);

            if (upgradeInfoPanel != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(upgradeInfoPanel.transform as RectTransform);

            LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);
        }

        // --------------------------------------------------
        // TOWER PURCHASE
        // --------------------------------------------------

        /// <summary>Zeigt die Turm-Auswahl an einer Baustelle.</summary>
        public bool ShowTowerPurchase(object newOwner, Vector3 worldPosition)
        {
            purchaseTowers.Clear();

            foreach (GameObject prefab in towerPrefabs)
            {
                if (prefab == null)
                    continue;

                Tower t = prefab.GetComponent<Tower>();

                if (t != null)
                    purchaseTowers.Add(t);
            }

            if (purchaseTowers.Count == 0)
            {
                Debug.LogWarning("InteractionPopup: Keine Tower-Prefabs zugewiesen.", this);
                return false;
            }

            mode = PopupMode.TowerPurchase;
            owner = newOwner;
            anchorPosition = worldPosition;
            upgradeTarget = null;
            SetTowerInfoActive(false);

            BuildEntries(purchaseTowers.Count, (i, entry) =>
                entry.Initialize(i, purchaseTowers[i]));

            selectedIndex = 0;

            RefreshTowerView();
            return true;
        }

        /// <summary>Prefab des aktuell gewählten Turms (nur im Kauf-Modus).</summary>
        public GameObject GetSelectedTowerPrefab()
        {
            if (mode != PopupMode.TowerPurchase)
                return null;

            if (selectedIndex < 0 || selectedIndex >= purchaseTowers.Count)
                return null;

            return purchaseTowers[selectedIndex].gameObject;
        }

        // --------------------------------------------------
        // TOWER UPGRADE
        // --------------------------------------------------

        /// <summary>
        /// Zeigt die Upgrade-Pfade eines gekauften Turms.
        /// Gibt false zurück (und schließt den Popup), wenn nichts mehr upgradebar ist.
        /// </summary>
        public bool ShowTowerUpgrade(Tower tower, Vector3 worldPosition)
        {
            if (tower == null || tower.upgradePaths == null)
                return false;

            int pathCount = tower.upgradePaths.Length;

            // Auswahl behalten, wenn derselbe Turm erneut aktualisiert wird
            int previous = (mode == PopupMode.TowerUpgrade && owner == (object)tower)
                ? selectedIndex
                : -1;

            int active = tower.GetActivePathIndex();
            int firstAvailable = -1;

            for (int i = 0; i < pathCount; i++)
            {
                if (GetNextLevel(tower, i) != null && !tower.IsPathLocked(i))
                {
                    firstAvailable = i;
                    break;
                }
            }

            // Alles maximal: auf dem aktiven Pfad bleiben (Level/XP/Sell bleiben sichtbar)
            if (firstAvailable < 0)
                firstAvailable = active >= 0 ? active : 0;

            mode = PopupMode.TowerUpgrade;
            owner = tower;
            anchorPosition = worldPosition;
            upgradeTarget = tower;
            purchaseTowers.Clear();

            BuildEntries(pathCount, (i, entry) =>
            {
                TowerUpgradeLevel next = GetNextLevel(tower, i);

                Sprite icon = null;
                if (next != null && next.pathShopIcon != null && next.pathShopIcon.Length > 0)
                    icon = next.pathShopIcon[0];

                entry.InitializeCustom(i, next != null ? next.upgradeName : "Maximal", icon);
            });
            selectedIndex =
                (previous >= 0 && previous < pathCount
                               && GetNextLevel(tower, previous) != null
                               && !tower.IsPathLocked(previous))
                    ? previous
                    : firstAvailable;

            RefreshTowerView();
            return true;
        }

        // --------------------------------------------------
        // SELECTION
        // --------------------------------------------------

        public void SelectFromMouse(int index)
        {
            if (!allowMouseSelection)
                return;

            if (mode != PopupMode.TowerPurchase && mode != PopupMode.TowerUpgrade)
                return;

            if (Input.GetKey(KeyCode.E))
                return;

            SetSelection(index);
        }

        private void ChangeSelection(int delta)
        {
            if (entries.Count == 0)
                return;

            int idx = selectedIndex;

            for (int n = 0; n < entries.Count; n++)
            {
                idx = (idx + delta + entries.Count) % entries.Count;

                if (IsSelectable(idx))
                {
                    SetSelection(idx);
                    return;
                }
            }
        }

        private bool IsSelectable(int index)
        {
            if (mode != PopupMode.TowerUpgrade || upgradeTarget == null)
                return true;

            return !upgradeTarget.IsPathLocked(index);
        }

        private void SetSelection(int index)
        {
            if (index < 0 || index >= entries.Count || index == selectedIndex)
                return;

            if (!IsSelectable(index))
                return;

            selectedIndex = index;
            RefreshTowerView();
        }

        private void RefreshTowerView()
        {
            bool pathChosen =
                mode == PopupMode.TowerUpgrade &&
                upgradeTarget != null &&
                upgradeTarget.GetActivePathIndex() >= 0;

            if (towerSelectionPanel != null)
                towerSelectionPanel.SetActive(!pathChosen);

            for (int i = 0; i < entries.Count; i++)
                entries[i].SetSelected(i == selectedIndex);

            if (mode == PopupMode.TowerPurchase)
            {
                if (selectedIndex < 0 || selectedIndex >= purchaseTowers.Count)
                    return;

                Tower t = purchaseTowers[selectedIndex];

                ShowInternal(
                    anchorPosition,
                    "to buy",
                    "Buy: " + t.towerName,
                    t.towerInitPrice,
                    t.towerDesc,
                    true
                );

                ShowUpgradeStats(BuildPurchaseStats(t));
            }
            else if (mode == PopupMode.TowerUpgrade)
            {
                SetTowerInfoActive(true);
                RefreshTowerInfo(true);

                TowerUpgradeLevel next = GetNextLevel(upgradeTarget, selectedIndex);

                int maxLevel = upgradeTarget.upgradePaths[selectedIndex].levels.Length;
                int currentPathLevel = upgradeTarget.pathLevels[selectedIndex];

                if (next == null)
                {
                    ShowInternal(anchorPosition, "", $"Maximal (Lv {maxLevel}/{maxLevel})",
                        0, "This path is fully upgraded.", true);
                    ShowUpgradeStats(null);
                    return;
                }

                if (!upgradeTarget.IsUpgradeUnlocked(selectedIndex))
                {
                    ShowInternal(anchorPosition, "locked",
                        $"Locked: {next.upgradeName} (Lv {currentPathLevel + 1}/{maxLevel})",
                        0, "This upgrade has not been researched yet.", true);
                    ShowUpgradeStats(null);
                    return;
                }

                ShowInternal(
                    anchorPosition,
                    "to upgrade",
                    $"Upgrade: {next.upgradeName} (Lv {currentPathLevel + 1}/{maxLevel})",
                    next.upgradeCost,
                    next.description,
                    true
                );

                ShowUpgradeStats(BuildUpgradeStats(upgradeTarget, next));
            }
        }

        private void BuildEntries(int count, Action<int, TowerSelectionEntry> init)
        {
            ClearEntries();

            if (towerSelectionContainer == null || towerSelectionEntryPrefab == null)
                return;

            for (int i = 0; i < count; i++)
            {
                TowerSelectionEntry entry = Instantiate(
                    towerSelectionEntryPrefab,
                    towerSelectionContainer
                );

                init(i, entry);
                entries.Add(entry);
            }
        }

        private void ClearEntries()
        {
            entries.Clear();

            if (towerSelectionContainer == null)
                return;

            DestroyChildren(towerSelectionContainer);
        }

        private static TowerUpgradeLevel GetNextLevel(Tower tower, int pathIndex)
        {
            if (tower == null || tower.upgradePaths == null)
                return null;

            if (pathIndex < 0 || pathIndex >= tower.upgradePaths.Length)
                return null;

            var path = tower.upgradePaths[pathIndex];

            if (path == null || path.levels == null)
                return null;

            int level = tower.pathLevels[pathIndex];

            return level < path.levels.Length ? path.levels[level] : null;
        }

        // --------------------------------------------------
        // STATS
        // --------------------------------------------------

        // Farben der LoL-Stats
        private static readonly Color armorColor = new Color(1f, 0.8f, 0.3f);
        private static readonly Color magicResistColor = new Color(0.5f, 0.6f, 1f);
        private static readonly Color critColor = new Color(1f, 0.5f, 0.1f);
        private static readonly Color penColor = new Color(0.9f, 0.25f, 0.25f);
        private static readonly Color magicPenColor = new Color(0.7f, 0.3f, 1f);
        private static readonly Color omnivampColor = new Color(0.9f, 0.1f, 0.5f);

        private List<UpgradeStatData> BuildPurchaseStats(Tower t)
        {
            var list = new List<UpgradeStatData>();

            AddStat(list, t, TowerDisplayedStats.Damage, "Damage",
                0, t.damage, damageIcon, Color.red);

            AddStat(list, t, TowerDisplayedStats.Range, "Range",
                0, t.range, rangeIcon, new Color(0.2f, 0.8f, 1f), "", 1);

            AddStat(list, t, TowerDisplayedStats.AttackSpeed, "Attack Speed",
                0, 1f / Mathf.Max(0.1f, t.timeInBetweenShots), attackSpeedIcon,
                new Color(1f, 0.35f, 0.6f), "/s", 1);

            AddStat(list, t, TowerDisplayedStats.Health, "Health",
                0, t.statHealthPoints, healthIcon, new Color(0.13f, 0.55f, 0.13f));

            AddStat(list, t, TowerDisplayedStats.Gold, "Gold/sec",
                0, t.statCoinsEarnedPerSecond, goldIcon, new Color(1f, 0.84f, 0f), "/s", 1);

            AddStat(list, t, TowerDisplayedStats.Regen, "Regen/sec",
                0, t.statHealthRegenPerSecond, regenIcon, new Color(0.3f, 1f, 0.3f), "/s", 1);

            AddStat(list, t, TowerDisplayedStats.DamageMultiplier, "Dmg Mult.",
                0, t.statDmgMultiplier * 100f, damageMultiplierIcon, new Color(1f, 0.4f, 0.4f), "%");

            AddStat(list, t, TowerDisplayedStats.RangeMultiplier, "Range Mult.",
                0, t.statRangeMultiplier * 100f, rangeMultiplierIcon, new Color(0.5f, 0.9f, 1f), "%");

            AddStat(list, t, TowerDisplayedStats.AttackSpeedMultiplier, "AtkSpeed Mult.",
                0, t.statAttackSpeedMultiplier * 100f, attackSpeedMultiplierIcon, new Color(1f, 0.7f, 0.2f), "%");

            AddStat(list, t, TowerDisplayedStats.SlowMultiplier, "Slow Mult.",
                0, t.statSlowMultiplier * 100f, slowMultiplierIcon, new Color(0.6f, 0.4f, 1f), "%");

            // LoL-Stats: beim Kauf nur anzeigen, wenn sie einen Wert haben (sonst viele Nullzeilen)
            OffenseStats o = t.offense;

            AddNonZeroStat(list, t, TowerDisplayedStats.Armor, "Armor",
                t.Armor, armorIcon, armorColor);

            AddNonZeroStat(list, t, TowerDisplayedStats.MagicResist, "Magic Resist",
                t.MagicResist, magicResistIcon, magicResistColor);

            AddNonZeroStat(list, t, TowerDisplayedStats.CritChance, "Crit",
                o.critChance * 100f, critChanceIcon, critColor, "%");

            if (o.critChance > 0f)
                AddNonZeroStat(list, t, TowerDisplayedStats.CritDamage, "Crit Damage",
                    o.critDamage * 100f, critDamageIcon, critColor, "%");

            AddNonZeroStat(list, t, TowerDisplayedStats.Penetration, "Lethality",
                o.lethality, lethalityIcon, penColor);

            AddNonZeroStat(list, t, TowerDisplayedStats.Penetration, "Armor Pen",
                o.armorPenPercent * 100f, armorPenIcon, penColor, "%");

            AddNonZeroStat(list, t, TowerDisplayedStats.Penetration, "Magic Pen",
                o.magicPenFlat, magicPenIcon, magicPenColor);

            AddNonZeroStat(list, t, TowerDisplayedStats.Penetration, "Magic Pen",
                o.magicPenPercent * 100f, magicPenIcon, magicPenColor, "%");

            AddNonZeroStat(list, t, TowerDisplayedStats.Omnivamp, "Omnivamp",
                o.omnivamp * 100f, omnivampIcon, omnivampColor, "%");

            return list;
        }

        private List<UpgradeStatData> BuildUpgradeStats(Tower t, TowerUpgradeLevel lvl)
        {
            var list = new List<UpgradeStatData>();

            // Neue Stats (Rüstung, MR, Crit, Penetration, Omnivamp ...) stehen in lvl.bonus
            StatBonus b = lvl.bonus ?? new StatBonus();

            float newShotTime = Mathf.Max(0.1f, t.timeInBetweenShots - lvl.timeInBetweenShotsDecrease - b.cooldownDecrease);

            AddStat(list, t, TowerDisplayedStats.Damage, "Damage",
                t.damage, t.damage + lvl.damageIncrease + b.damage, damageIcon, Color.red,
                "", 0, true);

            AddStat(list, t, TowerDisplayedStats.Range, "Range",
                t.range, t.range + lvl.rangeIncrease + b.range, rangeIcon, new Color(0.2f, 0.8f, 1f),
                "", 1, true);

            AddStat(list, t, TowerDisplayedStats.AttackSpeed, "Attack Speed",
                1f / Mathf.Max(0.1f, t.timeInBetweenShots), 1f / newShotTime,
                attackSpeedIcon, new Color(1f, 0.35f, 0.6f), "/s", 1, true);

            AddStat(list, t, TowerDisplayedStats.Health, "Health",
                t.statHealthPoints, t.statHealthPoints + lvl.healthpoints + b.health,
                healthIcon, new Color(0.13f, 0.55f, 0.13f), "", 0, true);

            AddStat(list, t, TowerDisplayedStats.Gold, "Gold/sec",
                t.statCoinsEarnedPerSecond, t.statCoinsEarnedPerSecond + lvl.coinsEarnedPerSecond,
                goldIcon, new Color(1f, 0.84f, 0f), "/s", 1, true);

            AddStat(list, t, TowerDisplayedStats.Regen, "Regen/sec",
                t.statHealthRegenPerSecond, t.statHealthRegenPerSecond + lvl.healthRegenPerSecond + b.healthRegen,
                regenIcon, new Color(0.3f, 1f, 0.3f), "/s", 1, true);

            AddStat(list, t, TowerDisplayedStats.DamageMultiplier, "Dmg Mult.",
                t.statDmgMultiplier * 100f, (t.statDmgMultiplier + lvl.dmgMultiplier) * 100f,
                damageMultiplierIcon, new Color(1f, 0.4f, 0.4f), "%", 0, true);

            AddStat(list, t, TowerDisplayedStats.RangeMultiplier, "Range Mult.",
                t.statRangeMultiplier * 100f, (t.statRangeMultiplier + lvl.rangeMultiplier) * 100f,
                rangeMultiplierIcon, new Color(0.5f, 0.9f, 1f), "%", 0, true);

            AddStat(list, t, TowerDisplayedStats.AttackSpeedMultiplier, "AtkSpeed Mult.",
                t.statAttackSpeedMultiplier * 100f, (t.statAttackSpeedMultiplier + lvl.attackSpeedMultiplier) * 100f,
                attackSpeedMultiplierIcon, new Color(1f, 0.7f, 0.2f), "%", 0, true);

            AddStat(list, t, TowerDisplayedStats.SlowMultiplier, "Slow Mult.",
                t.statSlowMultiplier * 100f, (t.statSlowMultiplier + lvl.slowMultiplier) * 100f,
                slowMultiplierIcon, new Color(0.6f, 0.4f, 1f), "%", 0, true);

            // LoL-Stats (Prozentwerte werden als 0..100 angezeigt)
            OffenseStats o = t.offense;

            AddStat(list, t, TowerDisplayedStats.Armor, "Armor",
                t.Armor, t.Armor + b.armor, armorIcon, armorColor, "", 0, true);

            AddStat(list, t, TowerDisplayedStats.MagicResist, "Magic Resist",
                t.MagicResist, t.MagicResist + b.magicResist, magicResistIcon, magicResistColor, "", 0, true);

            AddStat(list, t, TowerDisplayedStats.CritChance, "Crit",
                o.critChance * 100f, Mathf.Min(1f, o.critChance + b.critChance) * 100f,
                critChanceIcon, critColor, "%", 0, true);

            AddStat(list, t, TowerDisplayedStats.CritDamage, "Crit Damage",
                o.critDamage * 100f, (o.critDamage + b.critDamage) * 100f,
                critDamageIcon, critColor, "%", 0, true);

            AddStat(list, t, TowerDisplayedStats.Penetration, "Lethality",
                o.lethality, o.lethality + b.lethality, lethalityIcon, penColor, "", 0, true);

            AddStat(list, t, TowerDisplayedStats.Penetration, "Armor Pen",
                o.armorPenPercent * 100f, Mathf.Min(1f, o.armorPenPercent + b.armorPenPercent) * 100f,
                armorPenIcon, penColor, "%", 0, true);

            AddStat(list, t, TowerDisplayedStats.Penetration, "Magic Pen",
                o.magicPenFlat, o.magicPenFlat + b.magicPenFlat, magicPenIcon, magicPenColor, "", 0, true);

            AddStat(list, t, TowerDisplayedStats.Penetration, "Magic Pen",
                o.magicPenPercent * 100f, Mathf.Min(1f, o.magicPenPercent + b.magicPenPercent) * 100f,
                magicPenIcon, magicPenColor, "%", 0, true);

            AddStat(list, t, TowerDisplayedStats.Omnivamp, "Omnivamp",
                o.omnivamp * 100f, Mathf.Min(1f, o.omnivamp + b.omnivamp) * 100f,
                omnivampIcon, omnivampColor, "%", 0, true);

            return list;
        }

        /// <summary>Wie AddStat im Kauf-Modus, aber ohne Zeile, wenn der Wert 0 ist.</summary>
        private void AddNonZeroStat(
            List<UpgradeStatData> list,
            Tower t,
            TowerDisplayedStats flag,
            string statName,
            float value,
            Sprite icon,
            Color color,
            string suffix = "",
            int decimals = 0)
        {
            if (Mathf.Approximately(value, 0f))
                return;

            AddStat(list, t, flag, statName, 0, value, icon, color, suffix, decimals);
        }

        /// <summary>
        /// Upgrade-Ansicht (onlyIfChanged): zeigt nur Stats, die sich durch das Upgrade ändern.
        /// Kauf-Ansicht: zeigt die am Turm als "displayedStats" markierten Stats
        /// (ist nichts markiert, alle mit Wert ungleich 0).
        /// Fehlt das Icon, steht der Stat-Name hinter dem neuen Wert, damit die Zeile erkennbar bleibt.
        /// </summary>
        private void AddStat(
            List<UpgradeStatData> list,
            Tower t,
            TowerDisplayedStats flag,
            string statName,
            float oldValue,
            float newValue,
            Sprite icon,
            Color color,
            string suffix = "",
            int decimals = 0,
            bool onlyIfChanged = false)
        {
            if (onlyIfChanged)
            {
                // Upgrade-Ansicht: genau die Stats, die dieses Upgrade verändert, unabhängig von displayedStats.
                // Änderungen, die in der gewählten Nachkommastelle nicht sichtbar wären, werden übersprungen.
                float epsilon = 0.5f * Mathf.Pow(10f, -Mathf.Max(0, decimals));
                if (Mathf.Abs(newValue - oldValue) < epsilon)
                    return;
            }
            else
            {
                // Kauf-Ansicht: markierte Stats (displayedStats). Ist nichts markiert, alle Stats ungleich 0.
                bool noneFlagged = t.DisplayedStats == TowerDisplayedStats.None;

                if (!noneFlagged && (t.DisplayedStats & flag) == 0)
                    return;

                if (noneFlagged && Mathf.Approximately(newValue, 0f))
                    return;
            }

            string nameFallback = icon == null ? " " + statName : "";

            list.Add(new UpgradeStatData(
                statName, oldValue, newValue, icon, color, nameFallback, suffix, decimals));
        }

        // --------------------------------------------------
        // TOWER INFO (Level, XP, Target, Sell)
        // --------------------------------------------------

        private void SetTowerInfoActive(bool active)
        {
            if (txtLevel != null)       txtLevel.gameObject.SetActive(active);
            if (experienceBar != null)  experienceBar.gameObject.SetActive(active);
            if (targetTypeRoot != null) targetTypeRoot.SetActive(active);

            SetSellActive(active);
            RebuildLayout();
        }

        private void SetSellActive(bool active)
        {
            if (sellRoot != null)
                sellRoot.SetActive(active);
        }

        /// <summary>Sell-Button für Walls (voller Preis, Aktion kommt von der WallGroup).</summary>
        public void SetWallSell(int refund, Action onSell)
        {
            if (mode != PopupMode.Wall)
                return;

            wallSellAction = onSell;

            if (txtSellPrice != null)
                txtSellPrice.text = $"{refund} Coins";

            SetSellActive(true);
            RebuildLayout();
        }

        private void RefreshTowerInfo(bool force = false)
        {
            Tower t = upgradeTarget;

            if (t == null)
                return;

            int sellPrice = t.CalculateSellPrice();

            if (!force &&
                t.level == shownLevel &&
                t.experience == shownExp &&
                t.requiredExperience == shownReqExp &&
                sellPrice == shownSellPrice &&
                t.targetPreference == shownTarget)
                return;

            shownLevel = t.level;
            shownExp = t.experience;
            shownReqExp = t.requiredExperience;
            shownSellPrice = sellPrice;
            shownTarget = t.targetPreference;

            if (txtLevel != null)
                txtLevel.text = $"Level: {t.level}";

            if (experienceBar != null)
            {
                experienceBar.minValue = 0;
                experienceBar.maxValue = Mathf.Max(1, t.requiredExperience);
                experienceBar.value = t.experience;
            }

            if (experienceText != null)
                experienceText.text = $"{t.experience} / {t.requiredExperience} Exp";

            if (txtTargetPreference != null)
                txtTargetPreference.text = t.targetPreference.ToString();

            if (txtSellPrice != null)
                txtSellPrice.text = $"{sellPrice} Coins";
        }

        private void OnPrevTargetClicked()
        {
            if (mode != PopupMode.TowerUpgrade || upgradeTarget == null)
                return;

            upgradeTarget.PreviousTargetType();
            RefreshTowerInfo();
        }

        private void OnNextTargetClicked()
        {
            if (mode != PopupMode.TowerUpgrade || upgradeTarget == null)
                return;

            upgradeTarget.NextTargetType();
            RefreshTowerInfo();
        }

        private void OnSellClicked()
        {
            if (mode == PopupMode.Wall)
            {
                Action action = wallSellAction;
                wallSellAction = null;
                action?.Invoke();   // WallGroup kümmert sich selbst um den Popup
                return;
            }

            if (mode != PopupMode.TowerUpgrade || upgradeTarget == null)
                return;

            upgradeTarget.SellTower();   // schließt den Popup über StopUpgradeInteraction
            Hide();
        }

        // --------------------------------------------------
        // INSPEKTION (Klick auf Turm, Held oder Gegner)
        // --------------------------------------------------

        public bool IsInspecting => mode == PopupMode.Inspect;

        /// <summary>Kauf-, Upgrade- oder Mauer-Popup ist offen. Inspektion darf es nicht überschreiben.</summary>
        public bool IsBusyWithInteraction => mode != PopupMode.None && mode != PopupMode.Inspect;

        public void ShowInspect(Tower tower)
        {
            if (tower == null || IsBusyWithInteraction)
                return;

            string title = tower.towerName;
            string description = tower.towerDesc;

            // Mauern: Name und Beschreibung der aktuellen Ausbaustufe der Gruppe
            if (tower is WallSegment wall && wall.WallGroup != null)
            {
                TowerUpgradeLevel level = wall.WallGroup.GetCurrentUpgrade();
                if (level != null)
                {
                    title = level.upgradeName;
                    description = level.description;
                }
            }

            BeginInspect(tower, title, description);
        }

        public void ShowInspect(Enemy enemy)
        {
            if (enemy == null || IsBusyWithInteraction)
                return;

            EnemyConfig cfg = enemy.enemyConfig;
            BeginInspect(enemy, cfg != null ? cfg.name : enemy.name, cfg != null ? cfg.description : "");
        }

        private void BeginInspect(object target, string title, string description)
        {
            mode = PopupMode.Inspect;
            owner = target;
            inspectTarget = target;
            upgradeTarget = null;
            selectedIndex = -1;
            inspectKey = null;
            inspectTimer = 0f;
            inspectEntries.Clear();

            ClearEntries();
            SetTowerInfoActive(false);
            wallSellAction = null;

            if (towerSelectionPanel != null)
                towerSelectionPanel.SetActive(false);

            Vector3 position = GetInspectPosition();
            anchorPosition = position;

            ShowInternal(position, "", title, 0, description, true);

            if (upgradeCost != null)
                upgradeCost.text = "";

            RefreshInspect();
        }

        private void UpdateInspect()
        {
            if (!IsInspectTargetAlive())
            {
                Hide();
                return;
            }

            // Bewegliche Ziele (Gegner, Held): Popup folgt
            Position(GetInspectPosition());

            inspectTimer -= Time.unscaledDeltaTime;
            if (inspectTimer <= 0f)
            {
                inspectTimer = inspectRefreshInterval;
                RefreshInspect();
            }
        }

        private bool IsInspectTargetAlive()
        {
            switch (inspectTarget)
            {
                case Tower t:
                    return t != null && t.gameObject.activeInHierarchy && t.IsTargetable;
                case Enemy e:
                    return e != null && (e.Vitals == null || !e.Vitals.IsDead);
                default:
                    return false;
            }
        }

        private Vector3 GetInspectPosition()
        {
            if (inspectTarget is Tower t && t != null)
                return t.transform.position;

            if (inspectTarget is Enemy e && e != null)
                return e.transform.position;

            return anchorPosition;
        }

        private void RefreshInspect()
        {
            inspectBuffer.Clear();

            if (inspectTarget is Tower tower)
                InspectStats.Collect(tower, inspectBuffer);
            else if (inspectTarget is Enemy enemy)
                InspectStats.Collect(enemy, inspectBuffer);

            // Zeilen nur neu bauen, wenn sich die Zeilenliste ändert (z. B. Shred beginnt: Rüstung zeigt "alt → neu").
            // Sonst werden nur die Werte aktualisiert, das spart Layout-Arbeit.
            var key = new System.Text.StringBuilder();
            foreach (InspectStat stat in inspectBuffer)
                key.Append((int)stat.id).Append(stat.Changed ? '~' : '.').Append(',');

            string newKey = key.ToString();

            if (newKey != inspectKey || inspectEntries.Count != inspectBuffer.Count)
            {
                RebuildInspectEntries();
                inspectKey = newKey;
            }

            for (int i = 0; i < inspectBuffer.Count && i < inspectEntries.Count; i++)
                ApplyInspectStat(inspectEntries[i], inspectBuffer[i]);
        }

        private void RebuildInspectEntries()
        {
            inspectEntries.Clear();

            if (upgradeStatsContainer == null || upgradeStatPrefab == null)
                return;

            DestroyChildren(upgradeStatsContainer);

            for (int i = 0; i < inspectBuffer.Count; i++)
                inspectEntries.Add(Instantiate(upgradeStatPrefab, upgradeStatsContainer));

            RebuildLayout();
        }

        private void ApplyInspectStat(UpgradeStatEntry entry, InspectStat stat)
        {
            if (entry == null)
                return;

            Sprite icon = IconFor(stat.id);

            // Ohne Icon steht der Stat-Name hinter dem Wert, damit die Zeile erkennbar bleibt
            string label = (icon == null && !string.IsNullOrEmpty(stat.name) ? " " + stat.name : "") + stat.extraLabel;

            Color color = ColorFor(stat);

            if (stat.Changed)
                entry.Setup(icon, stat.baseValue, stat.value, color, label, stat.valueSuffix, stat.decimals);
            else
                entry.SetupSingle(icon, stat.value, color, label, stat.valueSuffix, stat.decimals);
        }

        private Sprite IconFor(StatId id)
        {
            switch (id)
            {
                case StatId.Level: return levelIcon;
                case StatId.Health: return healthIcon;
                case StatId.Shield: return shieldIcon;
                case StatId.Block: return blockIcon;
                case StatId.AntiHeal: return antiHealIcon;
                case StatId.Armor: return armorIcon;
                case StatId.MagicResist: return magicResistIcon;
                case StatId.Damage: return damageIcon;
                case StatId.AttackSpeed: return attackSpeedIcon;
                case StatId.Range: return rangeIcon;
                case StatId.Crit: return critChanceIcon;
                case StatId.CritDamage: return critDamageIcon;
                case StatId.Lethality: return lethalityIcon;
                case StatId.ArmorPen: return armorPenIcon;
                case StatId.MagicPen:
                case StatId.MagicPenPercent: return magicPenIcon;
                case StatId.Omnivamp: return omnivampIcon;
                case StatId.Regen: return regenIcon;
                case StatId.MoveSpeed: return moveSpeedIcon;
                case StatId.Evasion: return evasionIcon;
                case StatId.Gold: return goldIcon;
                case StatId.GiveRange: return rangeMultiplierIcon;
                case StatId.GiveDamage: return damageMultiplierIcon;
                case StatId.GiveAttackSpeed: return attackSpeedMultiplierIcon;
                case StatId.GiveSlow: return slowMultiplierIcon;
                default: return null;
            }
        }

        private Color ColorFor(InspectStat stat)
        {
            // Geändert gegenüber dem Grundwert: grün = verbessert (Aura, Buff), rot = verschlechtert (Shred, Slow)
            if (stat.Changed)
                return stat.value > stat.baseValue ? new Color(0.35f, 1f, 0.4f) : new Color(1f, 0.35f, 0.3f);

            switch (stat.id)
            {
                case StatId.Health: return new Color(0.3f, 0.85f, 0.35f);
                case StatId.Shield: return Color.white;
                case StatId.Block: return new Color(0.75f, 0.45f, 1f);
                case StatId.AntiHeal: return new Color(0.75f, 0.2f, 0.3f);
                case StatId.Armor: return armorColor;
                case StatId.MagicResist: return magicResistColor;
                case StatId.Damage: return Color.red;
                case StatId.AttackSpeed: return new Color(1f, 0.35f, 0.6f);
                case StatId.Range: return new Color(0.2f, 0.8f, 1f);
                case StatId.Crit:
                case StatId.CritDamage: return critColor;
                case StatId.Lethality:
                case StatId.ArmorPen: return penColor;
                case StatId.MagicPen:
                case StatId.MagicPenPercent: return magicPenColor;
                case StatId.Omnivamp: return omnivampColor;
                case StatId.Regen: return new Color(0.3f, 1f, 0.3f);
                case StatId.Gold: return new Color(1f, 0.84f, 0f);
                case StatId.MoveSpeed: return new Color(0.9f, 0.9f, 0.5f);
                default: return Color.white;
            }
        }

        // --------------------------------------------------
        // HIDE
        // --------------------------------------------------

        public void Hide()
        {
            mode = PopupMode.None;
            owner = null;
            upgradeTarget = null;
            selectedIndex = -1;

            inspectTarget = null;
            inspectKey = null;
            inspectEntries.Clear();

            ClearEntries();
            SetTowerInfoActive(false);
            wallSellAction = null;

            if (towerSelectionPanel != null)
                towerSelectionPanel.SetActive(false);

            gameObject.SetActive(false);

            if (upgradeInfoPanel != null)
                upgradeInfoPanel.SetActive(false);

            ClearUpgradeStats();

            ResetProgress();
        }

        /// <summary>Schließt den Popup nur, wenn er gerade von 'who' geöffnet wurde.</summary>
        public void HideFor(object who)
        {
            if (owner == who)
                Hide();
        }

        private void ClearUpgradeStats()
        {
            if (upgradeStatsContainer == null)
                return;

            DestroyChildren(upgradeStatsContainer);
        }

        // --------------------------------------------------
        // COINS
        // --------------------------------------------------

        private void CreateCoins(int amount)
        {
            for (int i = coinContainer.childCount - 1; i >= 0; i--)
            {
                Destroy(coinContainer.GetChild(i).gameObject);
            }

            coins.Clear();

            for (int i = 0; i < amount; i++)
            {
                CoinUI coin = Instantiate(
                    coinPrefab,
                    coinContainer
                );

                coin.ResetCoin();

                coins.Add(coin);
            }
        }

        /// <summary>progress = 0..1</summary>
        public void SetProgress(float progress)
        {
            if (requiredCoins <= 0)
                return;

            progress = Mathf.Clamp01(progress);

            int coinsToFill = Mathf.FloorToInt(
                progress * requiredCoins
            );

            SetCoinsProgress(coinsToFill);
        }

        private void SetCoinsProgress(int amount)
        {
            amount = Mathf.Clamp(
                amount,
                0,
                coins.Count
            );

            for (int i = 0; i < coins.Count; i++)
            {
                coins[i].SetFilled(i < amount);
            }

            filledCoins = amount;
        }

        public bool IsComplete()
        {
            return filledCoins >= requiredCoins;
        }

        public void ResetProgress()
        {
            filledCoins = 0;

            foreach (CoinUI coin in coins)
            {
                coin.ResetCoin();
            }
        }

        // --------------------------------------------------
        // POSITION
        // --------------------------------------------------

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