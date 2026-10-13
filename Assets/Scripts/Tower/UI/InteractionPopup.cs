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
            TowerUpgrade
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
                0, t.statDmgMultiplier, damageMultiplierIcon, new Color(1f, 0.4f, 0.4f), "", 1);

            AddStat(list, t, TowerDisplayedStats.RangeMultiplier, "Range Mult.",
                0, t.statRangeMultiplier, rangeMultiplierIcon, new Color(0.5f, 0.9f, 1f), "", 1);

            AddStat(list, t, TowerDisplayedStats.AttackSpeedMultiplier, "AtkSpeed Mult.",
                0, t.statAttackSpeedMultiplier, attackSpeedMultiplierIcon, new Color(1f, 0.7f, 0.2f), "", 1);

            AddStat(list, t, TowerDisplayedStats.SlowMultiplier, "Slow Mult.",
                0, t.statSlowMultiplier, slowMultiplierIcon, new Color(0.6f, 0.4f, 1f), "", 1);

            return list;
        }

        private List<UpgradeStatData> BuildUpgradeStats(Tower t, TowerUpgradeLevel lvl)
        {
            var list = new List<UpgradeStatData>();

            float newShotTime = Mathf.Max(0.1f, t.timeInBetweenShots - lvl.timeInBetweenShotsDecrease);

            AddStat(list, t, TowerDisplayedStats.Damage, "Damage",
                t.damage, t.damage + lvl.damageIncrease, damageIcon, Color.red,
                "", 0, true);

            AddStat(list, t, TowerDisplayedStats.Range, "Range",
                t.range, t.range + lvl.rangeIncrease, rangeIcon, new Color(0.2f, 0.8f, 1f),
                "", 1, true);

            AddStat(list, t, TowerDisplayedStats.AttackSpeed, "Attack Speed",
                1f / Mathf.Max(0.1f, t.timeInBetweenShots), 1f / newShotTime,
                attackSpeedIcon, new Color(1f, 0.35f, 0.6f), "/s", 1, true);

            AddStat(list, t, TowerDisplayedStats.Health, "Health",
                t.statHealthPoints, t.statHealthPoints + lvl.healthpoints,
                healthIcon, new Color(0.13f, 0.55f, 0.13f), "", 0, true);

            AddStat(list, t, TowerDisplayedStats.Gold, "Gold/sec",
                t.statCoinsEarnedPerSecond, t.statCoinsEarnedPerSecond + lvl.coinsEarnedPerSecond,
                goldIcon, new Color(1f, 0.84f, 0f), "/s", 1, true);

            AddStat(list, t, TowerDisplayedStats.Regen, "Regen/sec",
                t.statHealthRegenPerSecond, t.statHealthRegenPerSecond + lvl.healthRegenPerSecond,
                regenIcon, new Color(0.3f, 1f, 0.3f), "/s", 1, true);

            AddStat(list, t, TowerDisplayedStats.DamageMultiplier, "Dmg Mult.",
                t.statDmgMultiplier, t.statDmgMultiplier + lvl.dmgMultiplier,
                damageMultiplierIcon, new Color(1f, 0.4f, 0.4f), "", 1, true);

            AddStat(list, t, TowerDisplayedStats.RangeMultiplier, "Range Mult.",
                t.statRangeMultiplier, t.statRangeMultiplier + lvl.rangeMultiplier,
                rangeMultiplierIcon, new Color(0.5f, 0.9f, 1f), "", 1, true);

            AddStat(list, t, TowerDisplayedStats.AttackSpeedMultiplier, "AtkSpeed Mult.",
                t.statAttackSpeedMultiplier, t.statAttackSpeedMultiplier + lvl.attackSpeedMultiplier,
                attackSpeedMultiplierIcon, new Color(1f, 0.7f, 0.2f), "", 1, true);

            AddStat(list, t, TowerDisplayedStats.SlowMultiplier, "Slow Mult.",
                t.statSlowMultiplier, t.statSlowMultiplier + lvl.slowMultiplier,
                slowMultiplierIcon, new Color(0.6f, 0.4f, 1f), "", 1, true);

            return list;
        }

        /// <summary>
        /// Zeigt nur Stats, die am Turm als "displayedStats" markiert sind.
        /// Ist nichts markiert, werden alle relevanten (Kauf: != 0, Upgrade: geänderten) Stats gezeigt.
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
            bool noneFlagged = t.DisplayedStats == TowerDisplayedStats.None;

            if (!noneFlagged && (t.DisplayedStats & flag) == 0)
                return;

            if (onlyIfChanged)
            {
                if (Mathf.Approximately(oldValue, newValue))
                    return;
            }
            else if (noneFlagged && Mathf.Approximately(newValue, 0f))
            {
                return;
            }

            list.Add(new UpgradeStatData(
                statName, oldValue, newValue, icon, color, "", suffix, decimals));
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
        // HIDE
        // --------------------------------------------------

        public void Hide()
        {
            mode = PopupMode.None;
            owner = null;
            upgradeTarget = null;
            selectedIndex = -1;

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