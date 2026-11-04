using System;
using System.Collections.Generic;
using ScriptableObjects;
using UnityEngine;
using UnityEngine.UI;
using System.Linq;

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
        [SerializeField] private GameObject promptRoot;   // "Press [E] to ..." mit Münzen
        
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
        [SerializeField] private GameObject towerInfoRoot;   // ganzer Info-Block (Divider, Level, XP, Ziel)

        [Header("Target Type (aufklappbar)")]
        [SerializeField] private Button btnTargetToggle;
        [SerializeField] private Text txtTargetArrow;
        [SerializeField] private GameObject targetPanel;
        [SerializeField] private Transform targetGrid;
        [SerializeField] private Color targetCellColor = new Color(0.27f, 0.20f, 0.14f, 1f);
        [SerializeField] private Color targetCellSelectedColor = new Color(0.82f, 0.55f, 0.20f, 1f);

        
        [Header("Sell (Turm + Wall)")]
        [SerializeField] private GameObject sellRoot;         // Pnl:Sell
        [SerializeField] private Button btnSell;
        [SerializeField] private Text txtSellPrice;
        private Action wallSellAction;
        private bool sellArmed;
        private float sellArmedUntil;
        private Text sellLabel;
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
        [SerializeField] public Sprite regenMultiplierIcon;
        [SerializeField] public Sprite omnivampMultiplierIcon;
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
        [SerializeField] public Sprite xpIcon;
        
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
        
        
        /// <summary>Reihenfolge der Felder im aufgeklappten Panel (Builder und Popup nutzen dieselbe Liste).</summary>
        public static readonly TargetType[] TargetOrder =
        {
            TargetType.First,     TargetType.Last,
            TargetType.Nearest,   TargetType.Farthest,
            TargetType.Strongest, TargetType.Weakest,
            TargetType.Armored,   TargetType.MagicResistant,
            TargetType.Shielded,  TargetType.Wounded,
            TargetType.Random
        };
        public static string TargetLabel(TargetType t)
        {
            switch (t)
            {
                case TargetType.MagicResistant: return "Resistant";
                default: return t.ToString();
            }
        }
        private readonly List<Image> targetCellImages = new List<Image>();
        
        /// <summary>Index des aktuell gewählten Turms (Kauf) bzw. Upgrade-Pfads (Upgrade).</summary>
        public int SelectedIndex => selectedIndex;
        public Tower CurrentUpgradeTarget =>
            mode == PopupMode.TowerUpgrade ? upgradeTarget : null;
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

            if (btnTargetToggle != null) btnTargetToggle.onClick.AddListener(ToggleTargetPanel);
            if (btnSell != null) btnSell.onClick.AddListener(OnSellClicked);  
            BindTargetCells();
            if (btnSell != null)
            {
                Transform labelTransform = btnSell.transform.Find("Label");
                if (labelTransform != null) sellLabel = labelTransform.GetComponent<Text>();
            }
        }

        private void Update()
        {
            if (sellArmed && Time.unscaledTime > sellArmedUntil)
                DisarmSell();
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
            requiredCoins = Mathf.CeilToInt(goldCost);

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
                {
                    upgradeCost.text = goldCost > 0 ? $"{goldCost} Gold" : "";
                    upgradeCost.gameObject.SetActive(goldCost > 0);
                }
            }
// Ohne Aktion (maximal, gesperrt, Inspektion) kein "Press E"
            if (promptRoot != null)
                promptRoot.SetActive(!string.IsNullOrEmpty(interactiontext));
            
            anchorPosition = worldPosition;
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

    if (stat.single)
        entry.SetupSingle(stat.icon, stat.newValue, stat.color,
            stat.suffixNewValueOnly, stat.suffix, stat.decimals);
    else
        entry.Setup(stat.icon, stat.oldValue, stat.newValue, stat.color,
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
            Position(anchorPosition);
        }
        private void BindTargetCells()
        {
            targetCellImages.Clear();
            if (targetGrid == null || targetGrid.childCount == 0) return;

            Transform template = targetGrid.GetChild(0);

            for (int i = targetGrid.childCount; i < TargetOrder.Length; i++)
                Instantiate(template.gameObject, targetGrid, false);

            for (int i = targetGrid.childCount - 1; i >= TargetOrder.Length; i--)
            {
                Transform extra = targetGrid.GetChild(i);
                extra.SetParent(null);
                Destroy(extra.gameObject);
            }

            for (int i = 0; i < TargetOrder.Length; i++)
            {
                Transform cell = targetGrid.GetChild(i);
                TargetType type = TargetOrder[i];

                cell.name = type.ToString();

                Text label = cell.GetComponentInChildren<Text>(true);
                if (label != null) label.text = TargetLabel(type);

                targetCellImages.Add(cell.GetComponent<Image>());

                if (cell.TryGetComponent(out Button button))
                    button.onClick.AddListener(() => OnTargetCellClicked(type));
            }
        }

        private void ToggleTargetPanel()
        {
            if (ControlTarget == null || targetPanel == null) return;

            bool open = !targetPanel.activeSelf;
            targetPanel.SetActive(open);
            DisarmSell();
            UpdateSellInteractable();
            if (txtTargetArrow != null)
                txtTargetArrow.text = open ? "<" : ">";

            if (open)
                LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)targetPanel.transform);

            RebuildLayout();
        }
        private void OnTargetCellClicked(TargetType type)
        {
            Tower t = ControlTarget;
            if (t == null) return;

            t.SetTargetType(type);
            RefreshTowerInfo(true);
            ToggleTargetPanel();          // nach der Wahl wieder zuklappen
        }

        private void UpdateTargetCells(TargetType selected)
        {
            for (int i = 0; i < targetCellImages.Count; i++)
            {
                if (targetCellImages[i] != null)
                    targetCellImages[i].color = TargetOrder[i] == selected ? targetCellSelectedColor : targetCellColor;
            }
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
                    ShowInternal(anchorPosition, "", TowerTitle(upgradeTarget), 0, upgradeTarget.towerDesc, true);
                    ShowUpgradeStats(BuildUpgradeStats(upgradeTarget, null));   // alle aktuellen Werte
                    return;
                }

                if (!upgradeTarget.IsUpgradeUnlocked(selectedIndex))
                {
                    ShowInternal(anchorPosition, "locked",
                        $"Locked: {next.upgradeName} (Lv {currentPathLevel }/{maxLevel})",
                        0, "This upgrade has not been researched yet.", true);
                    ShowUpgradeStats(null);
                    return;
                }

                ShowInternal(
                    anchorPosition,
                    "to upgrade",
                    $"Upgrade: {next.upgradeName} (Lv {currentPathLevel}/{maxLevel})",
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
        private static readonly Color armorColor       = new Color(0.95f, 0.78f, 0.35f);
        private static readonly Color magicResistColor = new Color(0.55f, 0.68f, 0.95f);
        private static readonly Color critColor        = new Color(1f, 0.58f, 0.22f);
        private static readonly Color penColor         = new Color(0.90f, 0.35f, 0.30f);
        private static readonly Color magicPenColor    = new Color(0.72f, 0.45f, 0.95f);
        private static readonly Color omnivampColor    = new Color(0.90f, 0.30f, 0.55f);

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
AddStat(list, t, TowerDisplayedStats.RegenMultiplier, "Regen Mult.",
    0, t.statRegenMultiplier * 100f, regenMultiplierIcon, new Color(0.3f, 1f, 0.3f), "%");

AddStat(list, t, TowerDisplayedStats.OmnivampMultiplier, "Omnivamp Mult.",
    0, t.statOmnivampMultiplier * 100f, omnivampMultiplierIcon, omnivampColor, "%");
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

    // Ohne Upgrade (lvl == null) ändert sich nichts: es erscheinen nur die aktuellen Werte
    StatBonus b = lvl != null && lvl.bonus != null ? lvl.bonus : new StatBonus();
    float L(System.Func<TowerUpgradeLevel, float> f) => lvl != null ? f(lvl) : 0f;

    float dDamage    = L(x => x.damageIncrease);
    float dRange     = L(x => x.rangeIncrease);
    float dShotTime  = L(x => x.timeInBetweenShotsDecrease);
    float dHealth    = L(x => x.healthpoints);
    float dGold      = L(x => x.coinsEarnedPerSecond);
    float dRegen     = L(x => x.healthRegenPerSecond);
    float dDmgMult   = L(x => x.dmgMultiplier);
    float dRangeMult = L(x => x.rangeMultiplier);
    float dAtkMult   = L(x => x.attackSpeedMultiplier);
    float dSlowMult  = L(x => x.slowMultiplier);
    float dRegenMult = L(x => x.regenMultiplier);
    float dOmniMult  = L(x => x.omnivampMultiplier);

    float newShotTime = Mathf.Max(0.1f, t.timeInBetweenShots - dShotTime - b.cooldownDecrease);

    AddStat(list, t, TowerDisplayedStats.Damage, "Damage",
        t.damage, t.damage + dDamage + b.damage, damageIcon, Color.red, "", 0, true);

    AddStat(list, t, TowerDisplayedStats.Range, "Range",
        t.range, t.range + dRange + b.range, rangeIcon, new Color(0.2f, 0.8f, 1f), "", 1, true);

    AddStat(list, t, TowerDisplayedStats.AttackSpeed, "Attack Speed",
        1f / Mathf.Max(0.1f, t.timeInBetweenShots), 1f / newShotTime,
        attackSpeedIcon, new Color(1f, 0.35f, 0.6f), "/s", 1, true);

    AddStat(list, t, TowerDisplayedStats.Health, "Health",
        t.statHealthPoints, t.statHealthPoints + dHealth + b.health,
        healthIcon, new Color(0.13f, 0.55f, 0.13f), "", 0, true);

    AddStat(list, t, TowerDisplayedStats.Gold, "Gold/sec",
        t.statCoinsEarnedPerSecond, t.statCoinsEarnedPerSecond + dGold,
        goldIcon, new Color(1f, 0.84f, 0f), "/s", 1, true);

    AddStat(list, t, TowerDisplayedStats.Regen, "Regen/sec",
        t.statHealthRegenPerSecond, t.statHealthRegenPerSecond + dRegen + b.healthRegen,
        regenIcon, new Color(0.3f, 1f, 0.3f), "/s", 1, true);

    AddStat(list, t, TowerDisplayedStats.DamageMultiplier, "Dmg Mult.",
        t.statDmgMultiplier * 100f, (t.statDmgMultiplier + dDmgMult) * 100f,
        damageMultiplierIcon, new Color(1f, 0.4f, 0.4f), "%", 0, true);

    AddStat(list, t, TowerDisplayedStats.RangeMultiplier, "Range Mult.",
        t.statRangeMultiplier * 100f, (t.statRangeMultiplier + dRangeMult) * 100f,
        rangeMultiplierIcon, new Color(0.5f, 0.9f, 1f), "%", 0, true);

    AddStat(list, t, TowerDisplayedStats.AttackSpeedMultiplier, "AtkSpeed Mult.",
        t.statAttackSpeedMultiplier * 100f, (t.statAttackSpeedMultiplier + dAtkMult) * 100f,
        attackSpeedMultiplierIcon, new Color(1f, 0.7f, 0.2f), "%", 0, true);

    AddStat(list, t, TowerDisplayedStats.SlowMultiplier, "Slow Mult.",
        t.statSlowMultiplier * 100f, (t.statSlowMultiplier + dSlowMult) * 100f,
        slowMultiplierIcon, new Color(0.6f, 0.4f, 1f), "%", 0, true);

    AddStat(list, t, TowerDisplayedStats.RegenMultiplier, "Regen Mult.",
        t.statRegenMultiplier * 100f, (t.statRegenMultiplier + dRegenMult) * 100f,
        regenMultiplierIcon, new Color(0.3f, 1f, 0.3f), "%", 0, true);

    AddStat(list, t, TowerDisplayedStats.OmnivampMultiplier, "Omnivamp Mult.",
        t.statOmnivampMultiplier * 100f, (t.statOmnivampMultiplier + dOmniMult) * 100f,
        omnivampMultiplierIcon, omnivampColor, "%", 0, true);

    // LoL-Stats (Prozentwerte als 0..100)
    OffenseStats o = t.offense;

    AddStat(list, t, TowerDisplayedStats.Armor, "Armor",
        t.Armor, t.Armor + b.armor, armorIcon, armorColor, "", 0, true);

    AddStat(list, t, TowerDisplayedStats.MagicResist, "Magic Resist",
        t.MagicResist, t.MagicResist + b.magicResist, magicResistIcon, magicResistColor, "", 0, true);

    AddStat(list, t, TowerDisplayedStats.CritChance, "Crit",
        o.critChance * 100f, Mathf.Min(1f, o.critChance + b.critChance) * 100f,
        critChanceIcon, critColor, "%", 0, true);

    // Crit-Schaden hat einen Grundwert, ist aber ohne Crit-Chance bedeutungslos
    if (o.critChance > 0f || b.critChance > 0f || !Mathf.Approximately(b.critDamage, 0f))
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

    // Geänderte zuerst, unveränderte darunter (OrderBy ist stabil)
    return list.OrderBy(s => s.single).ToList();
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
/// <summary>Gedämpfte Variante für unveränderte Werte (zieht die Farbe zum Sand-Ton).</summary>
private static Color MutedColor(Color c)
    => Color.Lerp(c, new Color(0.74f, 0.67f, 0.55f), 0.5f);
/// <summary>"Flummi (Lv 2/2)": aktueller Name plus Stufe des gewählten Pfads. Ohne Pfad nur der Name.</summary>
private static string TowerTitle(Tower t)
{
    int a = t.GetActivePathIndex();

    if (a < 0 || t.upgradePaths[a] == null || t.upgradePaths[a].levels == null)
        return t.towerName;

    return $"{t.towerName} (Lv {t.pathLevels[a]}/{t.upgradePaths[a].levels.Length})";
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
                {
                    // Unverändert: nur zeigen, wenn der Turm den Stat überhaupt hat
                    if (Mathf.Abs(newValue) < epsilon)
                        return;

                    list.Add(new UpgradeStatData(
                            statName, oldValue, newValue, icon, MutedColor(color),
                            icon == null ? " " + statName : "", suffix, decimals)
                        { single = true });
                    return;
                }
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

        private Tower InfoTower =>
            mode == PopupMode.TowerUpgrade ? upgradeTarget :
            mode == PopupMode.Inspect ? inspectTarget as Tower : null;

        private void SetTowerInfoActive(bool active) => SetTowerInfoActive(active, active, active);

        private void SetTowerInfoActive(bool showLevel, bool showTarget, bool showSell)
        {
            if (towerInfoRoot != null)  towerInfoRoot.SetActive(showLevel);
            if (txtLevel != null)       txtLevel.gameObject.SetActive(showLevel);
            if (experienceBar != null)  experienceBar.gameObject.SetActive(showLevel);
            if (targetTypeRoot != null) targetTypeRoot.SetActive(showTarget);

            if (!showTarget)
            {
                if (targetPanel != null) targetPanel.SetActive(false);
                if (txtTargetArrow != null) txtTargetArrow.text = ">";
            }

            if (!showSell && btnSell != null) btnSell.interactable = true;

            SetSellActive(showSell);
            RebuildLayout();
        }

        private void SetSellActive(bool active)
        {
            if (sellRoot != null)
                sellRoot.SetActive(active);
            DisarmSell();
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
            Tower t = InfoTower;
            UpdateSellInteractable();
            if (t == null || t is WallSegment)
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
                txtTargetPreference.text = TargetLabel(t.targetPreference);

            UpdateTargetCells(t.targetPreference);

            if (txtSellPrice != null)
                txtSellPrice.text = $"{sellPrice} Coins";
        }

        private void OnSellClicked()
        {
            // Erster Klick scharf stellen, zweiter verkauft
            if (!sellArmed)
            {
                ArmSell();
                return;
            }

            DisarmSell();

            if (mode == PopupMode.Wall || (mode == PopupMode.Inspect && inspectTarget is WallSegment))
            {
                Action action = wallSellAction;
                wallSellAction = null;
                action?.Invoke();

                // Steht der Held in Reichweite, hat die WallGroup schon "to build" gezeigt (Modus Wall).
                // Sonst bleibt die Inspektion offen, also schließen.
                if (mode == PopupMode.Inspect)
                    Hide();
                return;
            }
            

            Tower t = ControlTarget;
            if (t == null || !HasSellControl(t)) return;

            t.SellTower();   // schließt das Popup über StopUpgradeInteraction
            Hide();  // schließt den Popup über StopUpgradeInteraction

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

            string title = TowerTitle(tower);
            string description = tower.towerDesc;

            // Mauern: Name und Beschreibung der aktuellen Ausbaustufe der Gruppe
            if (tower is WallSegment wall && wall.WallGroup != null)
            {
                TowerUpgradeLevel level = wall.WallGroup.GetCurrentUpgrade();
                if (level != null)
                {
                    title = $"{level.upgradeName} (Lv {wall.WallGroup.WallLevel + 1}/{wall.WallGroup.LevelCount})";
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
            Tower inspectedTower = target as Tower;
            bool showLevel = inspectedTower != null && !(inspectedTower is WallSegment);
            SetTowerInfoActive(showLevel, HasTargetControls(inspectedTower), HasSellControl(inspectedTower));            
            wallSellAction = null;
            // Gebaute Mauer: Sell-Button auch in der Inspektion
            if (inspectedTower is WallSegment ws && ws.WallGroup != null && ws.IsBuilt)
            {
                WallGroup group = ws.WallGroup;
                wallSellAction = group.SellWall;

                if (txtSellPrice != null)
                    txtSellPrice.text = $"{group.GetSellPrice()} Coins";

                SetSellActive(true);
            }
            
            if (towerSelectionPanel != null)
                towerSelectionPanel.SetActive(false);

            Vector3 position = GetInspectPosition();
            anchorPosition = position;

            ShowInternal(position, "", title, 0, description, true);

            if (upgradeCost != null)
                upgradeCost.text = "";

            RefreshInspect();
            RefreshTowerInfo(true);
        }

        private void UpdateInspect()
        {
            if (!IsInspectTargetAlive())
            {
                Hide();
                return;
            }

            anchorPosition = GetInspectPosition();
            Position(anchorPosition);

            inspectTimer -= Time.unscaledDeltaTime;
            if (inspectTimer <= 0f)
            {
                inspectTimer = inspectRefreshInterval;
                RefreshInspect();
                RefreshTowerInfo();
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
            
            inspectBuffer.RemoveAll(s => s.id == StatId.Level && inspectTarget is Tower);

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
                case StatId.GiveRegen:    return regenMultiplierIcon;
                case StatId.GiveOmnivamp: return omnivampMultiplierIcon;
                case StatId.XpDrop: return xpIcon;
                
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
                case StatId.GiveRegen:    return new Color(0.3f, 1f, 0.3f);
                case StatId.GiveOmnivamp: return omnivampColor;
                case StatId.XpDrop: return new Color(0.62f, 0.80f, 0.38f);   // gleiches Grün wie der XP-Balken
                
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

            DisarmSell();
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
        private void ArmSell()
        {
            sellArmed = true;
            sellArmedUntil = Time.unscaledTime + 2.5f;
            if (sellLabel != null) sellLabel.text = "Click again";
        }

        private void DisarmSell()
        {
            sellArmed = false;
            if (sellLabel != null) sellLabel.text = "Sell";
        }
        
        private static bool HasTargetControls(Tower t) => t != null && !(t is WallSegment);
        private static bool HasSellControl(Tower t) => t != null && !(t is Hero) && !(t is WallSegment);
        /// <summary>Turm, dessen Ziel und Verkauf das Popup gerade steuert (Upgrade oder Inspektion), sonst null.</summary>
        private Tower ControlTarget =>
            (mode == PopupMode.TowerUpgrade || mode == PopupMode.Inspect) && HasTargetControls(InfoTower)
                ? InfoTower : null;

        /// <summary>Verkaufen nur am Tag und nicht, solange das Ziel-Panel offen ist (Fehlklick-Schutz).</summary>
        private void UpdateSellInteractable()
        {
            if (btnSell == null || mode == PopupMode.Wall)
                return;

            bool panelOpen = targetPanel != null && targetPanel.activeSelf;
            bool day = LevelManager.instance != null && LevelManager.instance.isDay;

            btnSell.interactable = !panelOpen && day;
        }
        // --------------------------------------------------
        // COINS
        // --------------------------------------------------

        private void CreateCoins(int amount)
        {
            DestroyChildren(coinContainer);   // entfernt sofort aus dem Layout
            coins.Clear();

            for (int i = 0; i < amount; i++)
            {
                CoinUI coin = Instantiate(coinPrefab, coinContainer, false);

                RectTransform coinRect = (RectTransform)coin.transform;
                coinRect.localScale = Vector3.one;          // Prefab war evtl. für den World-Space-Canvas skaliert
                coinRect.localRotation = Quaternion.identity;

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
        private Camera worldCamera;

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

            if (worldCamera == null)
                worldCamera = Camera.main;
            if (worldCamera == null)
                return;

            // Welt -> Bildschirm immer mit der Spielkamera, nicht mit der UI-Kamera (bei Overlay null)
            Vector2 screenPoint = worldCamera.WorldToScreenPoint(worldPosition);
            Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            RectTransform parent = (RectTransform)rectTransform.parent;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screenPoint, uiCamera, out Vector2 local))
                return;

            Rect area = parent.rect;
            Vector2 pos = local - area.center;          // anchoredPosition bezieht sich auf die Mitte (Anker 0.5/0.5)

            // Im Bild halten (Pivot = unten Mitte)
            const float margin = 8f;
            float extraRight = ControlTarget != null ? 240f : 0f;
            Vector2 size = rectTransform.rect.size;

            float minX = -area.width * 0.5f + size.x * 0.5f + margin;
            float maxX =  area.width * 0.5f - size.x * 0.5f - extraRight - margin;
            float minY = -area.height * 0.5f + margin;
            float maxY =  area.height * 0.5f - size.y - margin;

            if (maxX > minX) pos.x = Mathf.Clamp(pos.x, minX, maxX);
            if (maxY > minY) pos.y = Mathf.Clamp(pos.y, minY, maxY);

            rectTransform.anchoredPosition = pos;
        }
    }
}