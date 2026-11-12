using System.Collections;
using System.Collections.Generic;
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
    ///   Links:  Level, Highscore, Spieler-Level, Gegner-Vorschau, Quests
    ///   Mitte:  Waffe, Perks (gesperrte mit "LV n"), Mutatoren
    ///   Rechts: Details zum gewählten Eintrag, Score-Multiplikator, Start / Clear / Back
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

        [Header("Spieler-Level")]
        [SerializeField] private GameObject playerLevelRoot;
        [SerializeField] private Text txtPlayerLevel;
        [SerializeField] private Text txtPlayerXp;
        [SerializeField] private RectTransform playerBarFill;
        [SerializeField] private GameObject nextUnlockRoot;
        [SerializeField] private Image nextUnlockIcon;
        [SerializeField] private Text txtNextUnlock;

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

        [Header("Zusammenfassung / Buttons")]
        [SerializeField] private Text txtSummaryWeapon;
        [SerializeField] private Text txtSummaryMultiplier;
        [SerializeField] private Text txtStartHint;
        [SerializeField] private Button btnStart;
        [SerializeField] private Button btnClear;
        [SerializeField] private Button btnBack;

        [Header("Prefabs")]
        [SerializeField] private GameObject questPrefab;   // QuestUI-Prefab
        [SerializeField] private GameObject btnPrefab;     // LoadoutSelectionButton-Prefab (optional Kind "Lock")

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

        private readonly List<LoadoutSelectionButton> weaponButtons = new();
        private readonly List<LoadoutSelectionButton> perkButtons = new();
        private readonly List<LoadoutSelectionButton> mutatorButtons = new();

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
            OverworldSession.LastLevelIndex = level.levelIndex;
            LevelLoadoutManager.Instance.CreateLoadout(level);

            panel.SetActive(true);

            if (canvasGroup != null)
            {
                StopAllCoroutines();
                StartCoroutine(FadeIn());
            }

            txtLevelName.text = level.levelName;
            txtLevelDescription.text = level.description;

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

            RefreshPlayerLevel();
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

        private void BuildWeaponUI()
        {
            if (currentLevel.availableWeapons == null) return;

            foreach (WeaponDefinition weapon in currentLevel.availableWeapons)
            {
                if (weapon == null) continue;

                LoadoutSelectionButton button = SpawnButton(weaponContainer);
                if (button == null) continue;

                button.SetupWeapon(weapon, weaponButtons.Count, colorWeapon, OnWeaponSelected);
                SetLocked(button, false, 0);
                weaponButtons.Add(button);
            }
        }

        private void BuildPerkUI()
        {
            if (currentLevel.availablePerks == null) return;

            foreach (PerkDefinition perk in currentLevel.availablePerks)
            {
                if (perk == null) continue;

                LoadoutSelectionButton button = SpawnButton(perkContainer);
                if (button == null) continue;

                button.SetupPerk(perk, perkButtons.Count, colorPerk, OnPerkSelected);

                bool locked = !PlayerProgress.IsPerkUnlocked(perk);
                SetLocked(button, locked, PlayerProgress.GetUnlockLevel(perk));

                perkButtons.Add(button);
            }
        }

        private void BuildMutatorUI()
        {
            if (currentLevel.availableMutators == null) return;

            foreach (MutatorDefinition mutator in currentLevel.availableMutators)
            {
                if (mutator == null) continue;

                LoadoutSelectionButton button = SpawnButton(mutatorContainer);
                if (button == null) continue;

                button.SetupMutator(mutator, mutatorButtons.Count, colorMutator, OnMutatorSelected);
                SetLocked(button, false, 0);
                mutatorButtons.Add(button);
            }
        }

        private static void SetLocked(LoadoutSelectionButton button, bool locked, int unlockLevel)
        {
            Transform overlay = button.transform.Find("Lock");
            if (overlay == null) return;

            overlay.gameObject.SetActive(locked);

            if (!locked) return;

            Text label = overlay.GetComponentInChildren<Text>(true);
            if (label != null)
                label.text = $"LV {unlockLevel}";
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

        private void RefreshPlayerLevel()
        {
            PlayerProgressionDefinition definition = PlayerProgress.Definition;
            bool has = definition != null && definition.levels != null && definition.levels.Count > 0;

            if (playerLevelRoot != null)
                playerLevelRoot.SetActive(has);

            if (!has) return;

            PlayerProgress.LevelState state = PlayerProgress.Evaluate(PlayerProgress.TotalXp);

            txtPlayerLevel.text = $"LEVEL {state.level}";
            txtPlayerXp.text = state.isMax ? "MAX" : $"{state.xpIntoLevel} / {state.xpForNext}";

            playerBarFill.anchorMin = new Vector2(0f, 0f);
            playerBarFill.anchorMax = new Vector2(Mathf.Clamp01(state.Fraction), 1f);

            bool hasNext = PlayerProgress.GetNextUnlock(state.level, out PerkDefinition perk, out int atLevel);
            nextUnlockRoot.SetActive(hasNext);

            if (hasNext)
            {
                nextUnlockIcon.sprite = perk.icon;
                nextUnlockIcon.enabled = perk.icon != null;
                txtNextUnlock.text = $"Next perk: {perk.perkName} (level {atLevel})";
            }
        }

        // ── Auswahl ───────────────────────────────────────────────

        private void OnWeaponSelected(int index)
        {
            if (index < 0 || index >= weaponButtons.Count) return;

            WeaponDefinition weapon = weaponButtons[index].GetWeapon();
            if (weapon == null) return;

            LevelLoadoutManager.Instance.SetWeapon(weapon);
            RefreshSelectionVisuals();

            string hex = ColorUtility.ToHtmlStringRGB(goldColor);
            ShowDetails(weapon.icon, "Weapon", weapon.weaponName,
                $"<color=#{hex}>PASSIVE</color>\n{weapon.passiveDescription}\n\n" +
                $"<color=#{hex}>ACTIVE</color>\n{weapon.activeDescription}",
                "", dimColor);

            RefreshAll();
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
                ShowDetails(perk.icon, "Perk (locked)", perk.perkName,
                    $"{perk.description}\n\n<color=#{hex}>Unlocks at player level {PlayerProgress.GetUnlockLevel(perk)}.</color>",
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

            RefreshSelectionVisuals();
            ShowMultiplierDetails(perk.icon, "Perk", perk.perkName, perk.description, perk.ScoreMultiplier);
            RefreshAll();
        }

        private void OnMutatorSelected(int index)
        {
            if (index < 0 || index >= mutatorButtons.Count) return;

            MutatorDefinition mutator = mutatorButtons[index].GetMutator();
            if (mutator == null) return;

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

            RefreshSelectionVisuals();
            ShowMultiplierDetails(mutator.icon, "Mutator", mutator.mutatorName, mutator.description, mutator.ScoreMultiplier);
            RefreshAll();
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

            RefreshSelectionVisuals();
            ClearDetails();
            RefreshAll();
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
            if (detailEmpty != null) detailEmpty.SetActive(true);
            if (detailRoot != null) detailRoot.SetActive(false);
        }

        private void ShowMultiplierDetails(Sprite icon, string type, string title, string description, float multiplier)
        {
            Color color = multiplier > 1.0001f ? positiveColor : (multiplier < 0.9999f ? negativeColor : dimColor);
            ShowDetails(icon, type, title, description, $"Score x{multiplier:0.00}", color);
        }

        private void ShowDetails(Sprite icon, string type, string title, string body, string multiplierText, Color multiplierColor)
        {
            detailEmpty.SetActive(false);
            detailRoot.SetActive(true);

            detailIcon.sprite = icon;
            detailIcon.enabled = icon != null;

            txtDetailType.text = type;
            txtDetailName.text = title;
            txtDetailBody.text = body;

            txtDetailMultiplier.gameObject.SetActive(!string.IsNullOrEmpty(multiplierText));
            txtDetailMultiplier.text = multiplierText;
            txtDetailMultiplier.color = multiplierColor;
        }

        private void RefreshAll()
        {
            LevelLoadout loadout = LevelLoadoutManager.Instance != null ? LevelLoadoutManager.Instance.CurrentLoadout : null;
            if (loadout == null || currentLevel == null) return;

            // Perks und Mutatoren sind optional (0 bis Max). Gold = Obergrenze erreicht.
            SetCounter(txtWeaponCounter, loadout.weapon != null ? 1 : 0, MaxWeapons);
            SetCounter(txtPerkCounter, loadout.perks.Count, LevelSelectorManager.MaxPerks);
            SetCounter(txtMutatorCounter, loadout.mutators.Count, LevelSelectorManager.MaxMutators);

            // Score-Multiplikator der aktuellen Auswahl
            float multiplier = 1f;
            foreach (PerkDefinition perk in loadout.perks)
                if (perk != null) multiplier *= perk.ScoreMultiplier;
            foreach (MutatorDefinition mutator in loadout.mutators)
                if (mutator != null) multiplier *= mutator.ScoreMultiplier;

            txtSummaryWeapon.text = loadout.weapon != null ? loadout.weapon.weaponName : "-";
            txtSummaryMultiplier.text = $"x{multiplier:0.00}";
            txtSummaryMultiplier.color = multiplier > 1.0001f ? positiveColor : (multiplier < 0.9999f ? negativeColor : dimColor);

            string blocker = LevelSelectorManager.GetStartBlocker(loadout);
            btnStart.interactable = blocker == null;
            txtStartHint.text = blocker ?? "Ready to start.";
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
                if (button.GetWeapon() != null && button.GetWeapon().name == parts[0])
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
                    if (mutator != null && mutator.name == mutatorName)
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