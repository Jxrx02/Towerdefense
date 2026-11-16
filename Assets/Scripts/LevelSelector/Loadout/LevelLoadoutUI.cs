using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using LevelSelector.Definitions;
using ScriptableObjects;
using TowerDefense;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace LevelSelector
{
    /// <summary>
    /// Loadout-Screen (Stil des Pausemenüs). Klassenname und API (Instance, Show, Close) wie bisher,
    /// damit LevelSelectorManager.OpenLevel unverändert funktioniert.
    ///
    ///   Links:  Level, Highscore, Gegner-Vorschau, Quests
    ///   Mitte:  Waffe, Perks, Mutatoren (freigeschaltete zuerst, gesperrte ausgegraut dahinter, mit "LV n")
    ///   Rechts: Details (mit Stat-Chips), Multiplikatoren, Start / Clear / Back
    ///
    /// Die Heldenprogression (Level, XP, nächste Freischaltung) liegt im Hauptmenü (ProgressionBarUI).
    /// </summary>
    public class LevelLoadoutUI : MonoBehaviour
    {
        public static LevelLoadoutUI Instance { get; private set; }

        public static bool IsOpen => Instance != null && Instance.panel != null && Instance.panel.activeSelf;

        /// <summary>Frame, in dem der Screen zuletzt geschlossen wurde (verhindert, dass dieselbe ESC-Taste noch ein Menü öffnet).</summary>
        public static int LastClosedFrame { get; private set; } = -1;

        [Header("Panel")]
        [Tooltip("Wurzel des Screens. Wird beim Start ausgeblendet (bei der alten Version: das alte Panel).")]
        [SerializeField] private GameObject panel;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private float fadeTime = 0.15f;
        [SerializeField] private bool closeOnEscape = true;

        [Header("Level")]
        [SerializeField] private Text txtLevelName;
        [SerializeField] private Text txtLevelDescription;
        [SerializeField] private Text txtHighscore;

        [Header("Gegner-Vorschau")]
        [SerializeField] private GameObject enemyCard;
        [SerializeField] private Transform enemyContainer;
        [SerializeField] private GameObject enemyIconPrefab;   // Button, Kind "Icon" (Image)
        [SerializeField] private EnemyInfoPanel enemyInfoPanel;

        [Header("Auswahl")]
        [SerializeField] private Transform weaponContainer;
        [SerializeField] private Transform perkContainer;
        [SerializeField] private Transform mutatorContainer;
        [SerializeField] private Transform questContainer;
        [SerializeField] private Text txtWeaponCounter;
        [SerializeField] private Text txtPerkCounter;
        [SerializeField] private Text txtMutatorCounter;

        [Header("Details")]
        [SerializeField] private GameObject detailEmpty;
        [SerializeField] private GameObject detailRoot;
        [SerializeField] private Image detailIcon;
        [SerializeField] private Text txtDetailType;
        [SerializeField] private Text txtDetailName;
        [SerializeField] private Text txtDetailMultiplier;
        [SerializeField] private Text txtDetailBody;
        [Tooltip("Kauf-Button für freigeschaltete, noch nicht gekaufte Waffen (optional)")]
        [SerializeField] private Button btnDetailBuy;
        [SerializeField] private Text txtDetailBuy;

        [Header("Multiplikatoren / Buttons")]
        [Tooltip("ScorePanelUI auf der Multiplikator-Karte (gleiche Zeilen wie im Pausemenü)")]
        [SerializeField] private ScorePanelUI multiplierPanel;
        [Tooltip("Optional (alte Zusammenfassung). Mit multiplierPanel nicht mehr nötig.")]
        [SerializeField] private Text txtSummaryWeapon;
        [SerializeField] private Text txtSummaryMultiplier;
        [SerializeField] private Text txtStartHint;
        [SerializeField] private Button btnStart;
        [SerializeField] private Button btnClear;
        [SerializeField] private Button btnBack;

        [Header("Prefabs")]
        [SerializeField] private GameObject questPrefab;   // QuestUI-Prefab
        [SerializeField] private GameObject btnPrefab;     // LoadoutSelectionButton-Prefab

        [Header("Farben")]
        [SerializeField] private Color colorWeapon = new Color(0.20f, 0.40f, 0.75f, 1f);
        [SerializeField] private Color colorPerk = new Color(0.15f, 0.70f, 0.30f, 1f);
        [SerializeField] private Color colorMutator = new Color(0.70f, 0.20f, 0.20f, 1f);
        [SerializeField] private Color goldColor = new Color(1f, 0.78f, 0.29f);
        [SerializeField] private Color dimColor = new Color(0.74f, 0.67f, 0.55f);
        [SerializeField] private Color positiveColor = new Color(0.62f, 0.80f, 0.38f);
        [SerializeField] private Color negativeColor = new Color(0.90f, 0.35f, 0.30f);

        [Header("Ereignisse")]
        [Tooltip("Wird nach erfolgreichem Start ausgelöst, wenn das Loadout gültig ist. Hier den Szenenwechsel eintragen.")]
        [SerializeField] private UnityEvent onStartConfirmed;

        private const int MaxWeapons = 1;

        private LevelDefinition currentLevel;
        private WeaponDefinition buyTarget;
        private ScriptableObject focusedDefinition;   // zuletzt gewählter Perk/Mutator (Highlight im Multiplikator-Panel)
        private StatChipBar statChips;
        private readonly List<StatId> chipBuffer = new();

        private readonly List<LoadoutSelectionButton> weaponButtons = new();
        private readonly List<LoadoutSelectionButton> perkButtons = new();
        private readonly List<LoadoutSelectionButton> mutatorButtons = new();

        private Font DetailFont => txtDetailName != null ? txtDetailName.font : null;

        // ── Lebenszyklus ──────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);   // nur die Komponente, nicht das ganze Objekt
                return;
            }

            Instance = this;

            if (btnStart != null) btnStart.onClick.AddListener(OnStartPressed);
            if (btnClear != null) btnClear.onClick.AddListener(ClearSelection);
            if (btnBack != null) btnBack.onClick.AddListener(Close);
            if (btnDetailBuy != null) btnDetailBuy.onClick.AddListener(OnBuyPressed);

            // Info-Chips direkt unter dem Beschreibungstext (Wurzel braucht dafür keinen Builder-Umbau)
            if (txtDetailBody != null)
                statChips = StatChipBar.Create(txtDetailBody.transform.parent, txtDetailBody.font,
                    txtDetailBody.transform.GetSiblingIndex() + 1);

            if (panel != null)
                panel.SetActive(false);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (!closeOnEscape || !IsOpen || !Input.GetKeyDown(KeyCode.Escape))
                return;

            // Das Stat-Info-Fenster schließt sich mit Esc selbst
            if (StatInfoPanel.ConsumesEscape)
                return;

            if (enemyInfoPanel != null && enemyInfoPanel.IsOpen)
                enemyInfoPanel.Close();
            else
                Close();
        }

        // ── Öffnen / Schließen ────────────────────────────────────

        public void Show(LevelDefinition level)
        {
            if (level == null)
            {
                Debug.LogError("LevelLoadoutUI.Show(): LevelDefinition ist null.");
                return;
            }

            if (LevelLoadoutManager.Instance == null)
            {
                Debug.LogError("LevelLoadoutUI.Show(): LevelLoadoutManager wurde nicht gefunden.");
                return;
            }

            currentLevel = level;
            focusedDefinition = null;
            OverworldSession.LastLevelIndex = level.levelIndex;
            LevelLoadoutManager.Instance.CreateLoadout(level);

            panel.SetActive(true);

            if (canvasGroup != null)
            {
                StopAllCoroutines();
                StartCoroutine(FadeIn());
            }

            txtLevelName.text = level.levelName;
            txtLevelDescription.text = StatMarkup.Format(level.description);

            int highscore = LevelProgressManager.Instance != null
                ? LevelProgressManager.Instance.GetHighscore(level.levelIndex)
                : 0;
            txtHighscore.text = highscore > 0 ? highscore.ToString() : "-";

            ClearAllContainers();

            BuildWeaponUI();
            BuildPerkUI();
            BuildMutatorUI();
            BuildQuestUI();
            BuildEnemyPreview();

            RestoreMemory();
            ClearDetails();
            RefreshAll();
        }

        public void Close()
        {
            if (enemyInfoPanel != null)
                enemyInfoPanel.Close();

            if (panel != null)
                panel.SetActive(false);

            LastClosedFrame = Time.frameCount;
        }

        private IEnumerator FadeIn()
        {
            canvasGroup.alpha = 0f;

            for (float t = 0f; t < fadeTime; t += Time.unscaledDeltaTime)
            {
                canvasGroup.alpha = t / fadeTime;
                yield return null;
            }

            canvasGroup.alpha = 1f;
        }

        // ── Start ─────────────────────────────────────────────────

        public void OnStartPressed()
        {
            if (LevelSelectorManager.Instance == null)
            {
                Debug.LogError("LevelLoadoutUI: LevelSelectorManager wurde nicht gefunden.");
                return;
            }

            if (!LevelSelectorManager.Instance.TryStartLevel())
            {
                RefreshAll();
                return;
            }

            SaveMemory();
            onStartConfirmed?.Invoke();
        }

        // ── Aufbau ────────────────────────────────────────────────

        private LoadoutSelectionButton SpawnButton(Transform container)
        {
            GameObject obj = Instantiate(btnPrefab, container);
            LoadoutSelectionButton button = obj.GetComponent<LoadoutSelectionButton>();

            if (button == null)
            {
                Debug.LogError("btnPrefab besitzt kein LoadoutSelectionButton-Script.");
                Destroy(obj);
                return null;
            }

            return button;
        }

        /// <summary>
        /// Freigeschaltete Inhalte zuerst (Reihenfolge der LevelDefinition bleibt), gesperrte dahinter nach
        /// Freischalt-Level. OrderBy ist stabil, gleiche Ränge behalten also ihre Reihenfolge.
        /// </summary>
        private static List<T> Ordered<T>(List<T> source, Func<T, int> rank, Func<T, int> unlockLevel, int lockedRank)
            where T : UnityEngine.Object
        {
            var items = new List<T>();
            if (source == null) return items;

            foreach (T item in source)
                if (item != null) items.Add(item);

            return items
                .OrderBy(rank)
                .ThenBy(item => rank(item) == lockedRank ? unlockLevel(item) : 0)
                .ToList();
        }

        private void BuildWeaponUI()
        {
            List<WeaponDefinition> ordered = Ordered(
                currentLevel.availableWeapons,
                w =>
                {
                    switch (PlayerProgress.GetWeaponState(w))
                    {
                        case UnlockState.Locked: return 2;
                        case UnlockState.Purchasable: return 1;
                        default: return 0;
                    }
                },
                w => PlayerProgress.GetUnlockLevel(w),
                2);

            foreach (WeaponDefinition weapon in ordered)
            {
                LoadoutSelectionButton button = SpawnButton(weaponContainer);
                if (button == null) continue;

                button.SetupWeapon(weapon, weaponButtons.Count, colorWeapon, OnWeaponSelected);
                ApplyWeaponOverlay(button);
                weaponButtons.Add(button);
            }
        }

        private void BuildPerkUI()
        {
            List<PerkDefinition> ordered = Ordered(
                currentLevel.availablePerks,
                p => PlayerProgress.IsPerkUnlocked(p) ? 0 : 1,
                p => PlayerProgress.GetUnlockLevel(p),
                1);

            foreach (PerkDefinition perk in ordered)
            {
                LoadoutSelectionButton button = SpawnButton(perkContainer);
                if (button == null) continue;

                button.SetupPerk(perk, perkButtons.Count, colorPerk, OnPerkSelected);

                bool locked = !PlayerProgress.IsPerkUnlocked(perk);
                button.SetAvailability(locked ? UnlockState.Locked : UnlockState.Unlocked,
                    Texts.Progression.LevelBadge(PlayerProgress.GetUnlockLevel(perk)), DetailFont);

                perkButtons.Add(button);
            }
        }

        private void BuildMutatorUI()
        {
            List<MutatorDefinition> ordered = Ordered(
                currentLevel.availableMutators,
                m => PlayerProgress.IsMutatorUnlocked(m) ? 0 : 1,
                m => PlayerProgress.GetUnlockLevel(m),
                1);

            foreach (MutatorDefinition mutator in ordered)
            {
                LoadoutSelectionButton button = SpawnButton(mutatorContainer);
                if (button == null) continue;

                button.SetupMutator(mutator, mutatorButtons.Count, colorMutator, OnMutatorSelected);

                bool locked = !PlayerProgress.IsMutatorUnlocked(mutator);
                button.SetAvailability(locked ? UnlockState.Locked : UnlockState.Unlocked,
                    Texts.Progression.LevelBadge(PlayerProgress.GetUnlockLevel(mutator)), DetailFont);

                mutatorButtons.Add(button);
            }
        }

        /// <summary>Waffe: gesperrt = ausgegraut mit LV n, kaufbar = Preis, im Besitz = ohne Badge.</summary>
        private void ApplyWeaponOverlay(LoadoutSelectionButton button)
        {
            WeaponDefinition weapon = button.GetWeapon();
            UnlockState state = PlayerProgress.GetWeaponState(weapon);

            string badge = state == UnlockState.Locked
                ? Texts.Progression.LevelBadge(PlayerProgress.GetUnlockLevel(weapon))
                : state == UnlockState.Purchasable ? Texts.Research.Cost(weapon.purchaseCost) : "";

            button.SetAvailability(state, badge, DetailFont);
        }

        private void BuildQuestUI()
        {
            if (currentLevel.quests == null || questPrefab == null) return;

            foreach (LevelQuestDefinition quest in currentLevel.quests)
            {
                if (quest == null) continue;

                GameObject obj = Instantiate(questPrefab, questContainer);
                QuestUI questUI = obj.GetComponent<QuestUI>();

                if (questUI == null)
                {
                    Debug.LogError("questPrefab besitzt kein QuestUI-Script.");
                    Destroy(obj);
                    continue;
                }

                bool completed = LevelProgressManager.Instance != null &&
                                 LevelProgressManager.Instance.IsQuestCompleted(currentLevel.levelIndex, quest);

                questUI.Setup(quest, completed);
            }
        }

        private void BuildEnemyPreview()
        {
            bool any = currentLevel.enemyPreview != null && currentLevel.enemyPreview.Count > 0;

            if (enemyCard != null)
                enemyCard.SetActive(any);

            if (!any || enemyContainer == null || enemyIconPrefab == null) return;

            foreach (EnemyConfig enemy in currentLevel.enemyPreview)
            {
                if (enemy == null) continue;

                GameObject obj = Instantiate(enemyIconPrefab, enemyContainer);

                Transform iconTransform = obj.transform.Find("Icon");
                Image icon = iconTransform != null ? iconTransform.GetComponent<Image>() : null;
                Sprite sprite = enemy.walkAnim != null && enemy.walkAnim.Length > 0 ? enemy.walkAnim[0] : null;

                if (icon != null)
                {
                    icon.sprite = sprite;
                    icon.enabled = sprite != null;
                }

                Button button = obj.GetComponent<Button>();
                EnemyConfig captured = enemy;

                if (button != null)
                    button.onClick.AddListener(() =>
                    {
                        if (enemyInfoPanel != null) enemyInfoPanel.Open(captured);
                    });
            }
        }

        // ── Auswahl ───────────────────────────────────────────────

        private void OnWeaponSelected(int index)
        {
            if (index < 0 || index >= weaponButtons.Count) return;

            SelectWeapon(weaponButtons[index].GetWeapon());
        }

        /// <summary>Gesperrt: Hinweis. Freigeschaltet, aber nicht gekauft: Details mit Kauf-Button. Im Besitz: ausrüsten.</summary>
        private void SelectWeapon(WeaponDefinition weapon)
        {
            if (weapon == null) return;

            focusedDefinition = null;

            string hex = ColorUtility.ToHtmlStringRGB(goldColor);
            string body =
                $"<color=#{hex}>{Texts.Progression.PassiveHeader}</color>\n{StatMarkup.Format(weapon.passiveDescription, false)}\n\n" +
                $"<color=#{hex}>{Texts.Progression.ActiveHeader}</color>\n{StatMarkup.Format(weapon.activeDescription, false)}";

            string type = Texts.Progression.Kind(UnlockKind.Weapon);

            switch (PlayerProgress.GetWeaponState(weapon))
            {
                case UnlockState.Locked:
                {
                    string negative = ColorUtility.ToHtmlStringRGB(negativeColor);
                    ShowDetails(weapon.icon, $"{type} ({Texts.Common.Locked.ToLowerInvariant()})", weapon.weaponName,
                        $"{body}\n\n<color=#{negative}>{Texts.Progression.UnlocksAtLevel(PlayerProgress.GetUnlockLevel(weapon))}</color>",
                        "", dimColor, true);
                    return;
                }

                case UnlockState.Purchasable:
                {
                    bool affordable = PlayerProgress.CanAffordWeapon(weapon);
                    string info = affordable
                        ? Texts.Research.Points(PlayerProgress.PointsBalance)
                        : Texts.Progression.NotEnoughPoints(weapon.purchaseCost - PlayerProgress.PointsBalance);

                    ShowDetails(weapon.icon, type, weapon.weaponName,
                        $"{body}\n\n{Texts.Progression.WeaponPurchaseHint}", info,
                        affordable ? goldColor : negativeColor, true);
                    ShowBuy(weapon, affordable);
                    return;
                }
            }

            LevelLoadoutManager.Instance.SetWeapon(weapon);
            RefreshSelectionVisuals();
            ShowDetails(weapon.icon, type, weapon.weaponName, body, "", dimColor, true);
            RefreshAll();
        }

        private void ShowBuy(WeaponDefinition weapon, bool affordable)
        {
            if (btnDetailBuy == null) return;

            buyTarget = weapon;
            btnDetailBuy.gameObject.SetActive(true);
            btnDetailBuy.interactable = affordable;

            if (txtDetailBuy != null)
                txtDetailBuy.text = Texts.Progression.BuyButton(weapon.purchaseCost);
        }

        private void HideBuy()
        {
            buyTarget = null;

            if (btnDetailBuy != null)
                btnDetailBuy.gameObject.SetActive(false);
        }

        private void OnBuyPressed()
        {
            WeaponDefinition weapon = buyTarget;
            if (weapon == null) return;

            if (PlayerProgress.TryPurchaseWeapon(weapon) != PurchaseResult.Success)
            {
                SelectWeapon(weapon);   // Anzeige aktualisieren (z. B. zu wenige Punkte)
                return;
            }

            foreach (LoadoutSelectionButton button in weaponButtons)
                ApplyWeaponOverlay(button);

            SelectWeapon(weapon);       // jetzt im Besitz: wird direkt ausgerüstet
        }

        private void OnPerkSelected(int index)
        {
            if (index < 0 || index >= perkButtons.Count) return;

            PerkDefinition perk = perkButtons[index].GetPerk();
            if (perk == null) return;

            // Gesperrt: nur Details mit Hinweis zeigen
            if (!PlayerProgress.IsPerkUnlocked(perk))
            {
                string hex = ColorUtility.ToHtmlStringRGB(negativeColor);
                ShowDetails(perk.icon, $"{Texts.Progression.Kind(UnlockKind.Perk)} ({Texts.Common.Locked.ToLowerInvariant()})", perk.perkName,
                    $"{perk.description}\n\n<color=#{hex}>{Texts.Progression.UnlocksAtLevel(PlayerProgress.GetUnlockLevel(perk))}</color>",
                    "", dimColor);
                return;
            }

            LevelLoadout loadout = LevelLoadoutManager.Instance.CurrentLoadout;

            if (loadout.HasPerk(perk))
            {
                LevelLoadoutManager.Instance.RemovePerk(perk);
            }
            else
            {
                if (loadout.perks.Count >= LevelSelectorManager.MaxPerks)
                    return;

                LevelLoadoutManager.Instance.AddPerk(perk);
            }

            focusedDefinition = loadout.HasPerk(perk) ? perk : null;

            RefreshSelectionVisuals();
            ShowMultiplierDetails(perk.icon, Texts.Progression.Kind(UnlockKind.Perk), perk.perkName, perk.description, perk.ScoreMultiplier);
            RefreshAll(true);
        }

        private void OnMutatorSelected(int index)
        {
            if (index < 0 || index >= mutatorButtons.Count) return;

            MutatorDefinition mutator = mutatorButtons[index].GetMutator();
            if (mutator == null) return;

            // Gesperrt: nur Details mit Hinweis zeigen
            if (!PlayerProgress.IsMutatorUnlocked(mutator))
            {
                string hex = ColorUtility.ToHtmlStringRGB(negativeColor);
                ShowDetails(mutator.icon, $"{Texts.Progression.Kind(UnlockKind.Mutator)} ({Texts.Common.Locked.ToLowerInvariant()})", mutator.mutatorName,
                    $"{mutator.description}\n\n<color=#{hex}>{Texts.Progression.UnlocksAtLevel(PlayerProgress.GetUnlockLevel(mutator))}</color>",
                    "", dimColor);
                return;
            }

            LevelLoadout loadout = LevelLoadoutManager.Instance.CurrentLoadout;

            if (loadout.HasMutator(mutator))
            {
                LevelLoadoutManager.Instance.RemoveMutator(mutator);
            }
            else
            {
                if (loadout.mutators.Count >= LevelSelectorManager.MaxMutators)
                    return;

                LevelLoadoutManager.Instance.AddMutator(mutator);
            }

            focusedDefinition = loadout.HasMutator(mutator) ? mutator : null;

            RefreshSelectionVisuals();
            ShowMultiplierDetails(mutator.icon, Texts.Progression.Kind(UnlockKind.Mutator), mutator.mutatorName, mutator.description, mutator.ScoreMultiplier);
            RefreshAll(true);
        }

        public void ClearSelection()
        {
            LevelLoadoutManager manager = LevelLoadoutManager.Instance;
            LevelLoadout loadout = manager != null ? manager.CurrentLoadout : null;
            if (loadout == null) return;

            manager.SetWeapon(null);

            foreach (PerkDefinition perk in new List<PerkDefinition>(loadout.perks))
                manager.RemovePerk(perk);

            foreach (MutatorDefinition mutator in new List<MutatorDefinition>(loadout.mutators))
                manager.RemoveMutator(mutator);

            focusedDefinition = null;

            RefreshSelectionVisuals();
            ClearDetails();
            RefreshAll(true);
        }

        private void RefreshSelectionVisuals()
        {
            LevelLoadout loadout = LevelLoadoutManager.Instance.CurrentLoadout;

            foreach (LoadoutSelectionButton button in weaponButtons)
                button.SetSelected(loadout.weapon != null && button.GetWeapon() == loadout.weapon);

            foreach (LoadoutSelectionButton button in perkButtons)
                button.SetSelected(loadout.HasPerk(button.GetPerk()));

            foreach (LoadoutSelectionButton button in mutatorButtons)
                button.SetSelected(loadout.HasMutator(button.GetMutator()));
        }

        // ── Anzeige ───────────────────────────────────────────────

        private void ClearDetails()
        {
            HideBuy();
            if (detailEmpty != null) detailEmpty.SetActive(true);
            if (detailRoot != null) detailRoot.SetActive(false);
            if (statChips != null) statChips.Hide();
        }

        private void ShowMultiplierDetails(Sprite icon, string type, string title, string description, float multiplier)
        {
            Color color = multiplier > 1.0001f ? positiveColor : (multiplier < 0.9999f ? negativeColor : dimColor);
            ShowDetails(icon, type, title, description, $"Score x{multiplier:0.00}", color);
        }

        /// <param name="formatted">true: der Text ist bereits aufbereitet (Waffen mit PASSIVE/ACTIVE-Überschriften).</param>
        private void ShowDetails(Sprite icon, string type, string title, string body, string multiplierText,
            Color multiplierColor, bool formatted = false)
        {
            HideBuy();
            detailEmpty.SetActive(false);
            detailRoot.SetActive(true);

            detailIcon.sprite = icon;
            detailIcon.enabled = icon != null;

            txtDetailType.text = type;
            txtDetailName.text = title;
            txtDetailBody.text = formatted ? body : StatMarkup.Format(body);

            txtDetailMultiplier.gameObject.SetActive(!string.IsNullOrEmpty(multiplierText));
            txtDetailMultiplier.text = multiplierText;
            txtDetailMultiplier.color = multiplierColor;

            // Ein Chip je erwähntem Stat: Klick öffnet dieselbe Erklärung wie im InteractionPopup
            if (statChips != null)
            {
                chipBuffer.Clear();
                StatMarkup.FindStats(txtDetailBody.text, chipBuffer);
                statChips.Show(chipBuffer, StatSubject.Hero);
            }
        }

        /// <param name="pulseTotal">Auswahl hat sich geändert: Gesamtzeile leuchtet kurz auf.</param>
        private void RefreshAll(bool pulseTotal = false)
        {
            LevelLoadout loadout = LevelLoadoutManager.Instance != null ? LevelLoadoutManager.Instance.CurrentLoadout : null;
            if (loadout == null || currentLevel == null) return;

            // Perks und Mutatoren sind optional (0 bis Max). Gold = Obergrenze erreicht.
            SetCounter(txtWeaponCounter, loadout.weapon != null ? 1 : 0, MaxWeapons);
            SetCounter(txtPerkCounter, loadout.perks.Count, LevelSelectorManager.MaxPerks);
            SetCounter(txtMutatorCounter, loadout.mutators.Count, LevelSelectorManager.MaxMutators);

            // Multiplikatoren: gleiche Zeilen und gleiches Highlight wie im Pausemenü
            if (multiplierPanel != null)
            {
                multiplierPanel.RebuildLoadoutMultipliers(loadout);
                multiplierPanel.Highlight(focusedDefinition);

                if (pulseTotal)
                    multiplierPanel.PulseTotal();
            }

            if (txtSummaryWeapon != null)
                txtSummaryWeapon.text = loadout.weapon != null ? loadout.weapon.weaponName : "-";

            if (txtSummaryMultiplier != null)
            {
                float multiplier = 1f;
                foreach (PerkDefinition perk in loadout.perks)
                    if (perk != null) multiplier *= perk.ScoreMultiplier;
                foreach (MutatorDefinition mutator in loadout.mutators)
                    if (mutator != null) multiplier *= mutator.ScoreMultiplier;

                txtSummaryMultiplier.text = $"x{multiplier:0.00}";
                txtSummaryMultiplier.color = multiplier > 1.0001f ? positiveColor : (multiplier < 0.9999f ? negativeColor : dimColor);
            }

            string blocker = LevelSelectorManager.GetStartBlocker(loadout);
            btnStart.interactable = blocker == null;
            txtStartHint.text = blocker ?? Texts.Loadout.ReadyToStart;
            txtStartHint.color = blocker == null ? positiveColor : dimColor;
        }

        private void SetCounter(Text text, int count, int max)
        {
            text.text = $"{count}/{max}";
            text.color = count >= max ? goldColor : dimColor;
        }

        // ── Letztes Loadout merken ────────────────────────────────

        private string MemoryKey => "last_loadout_" + currentLevel.levelIndex;

        private void SaveMemory()
        {
            LevelLoadout loadout = LevelLoadoutManager.Instance.CurrentLoadout;
            if (loadout == null) return;

            string weapon = loadout.weapon != null ? loadout.weapon.name : "";
            string perks = string.Join(",", loadout.perks.ConvertAll(p => p.name));
            string mutators = string.Join(",", loadout.mutators.ConvertAll(m => m.name));

            PlayerPrefs.SetString(MemoryKey, $"{weapon};{perks};{mutators}");
            PlayerPrefs.Save();
        }

        /// <summary>Stellt die Auswahl des letzten Starts wieder her (nur, was noch gültig und freigeschaltet ist).</summary>
        private void RestoreMemory()
        {
            string saved = PlayerPrefs.GetString(MemoryKey, "");
            if (string.IsNullOrEmpty(saved)) return;

            string[] parts = saved.Split(';');
            if (parts.Length != 3) return;

            LevelLoadoutManager manager = LevelLoadoutManager.Instance;

            foreach (LoadoutSelectionButton button in weaponButtons)
            {
                if (button.GetWeapon() != null && button.GetWeapon().name == parts[0] &&
                    PlayerProgress.IsWeaponOwned(button.GetWeapon()))
                {
                    manager.SetWeapon(button.GetWeapon());
                    break;
                }
            }

            foreach (string perkName in parts[1].Split(','))
            {
                if (string.IsNullOrEmpty(perkName)) continue;

                foreach (LoadoutSelectionButton button in perkButtons)
                {
                    PerkDefinition perk = button.GetPerk();
                    if (perk != null && perk.name == perkName && PlayerProgress.IsPerkUnlocked(perk))
                        manager.AddPerk(perk);
                }
            }

            foreach (string mutatorName in parts[2].Split(','))
            {
                if (string.IsNullOrEmpty(mutatorName)) continue;

                foreach (LoadoutSelectionButton button in mutatorButtons)
                {
                    MutatorDefinition mutator = button.GetMutator();
                    if (mutator != null && mutator.name == mutatorName && PlayerProgress.IsMutatorUnlocked(mutator))
                        manager.AddMutator(mutator);
                }
            }

            RefreshSelectionVisuals();
        }

        // ── Aufräumen ─────────────────────────────────────────────

        private void ClearAllContainers()
        {
            ClearContainer(weaponContainer);
            ClearContainer(perkContainer);
            ClearContainer(mutatorContainer);
            ClearContainer(questContainer);
            ClearContainer(enemyContainer);

            weaponButtons.Clear();
            perkButtons.Clear();
            mutatorButtons.Clear();
        }

        private static void ClearContainer(Transform container)
        {
            if (container == null) return;

            for (int i = container.childCount - 1; i >= 0; i--)
            {
                Transform child = container.GetChild(i);
                child.SetParent(null);   // zählt sofort nicht mehr im Layout
                Destroy(child.gameObject);
            }
        }
    }
}