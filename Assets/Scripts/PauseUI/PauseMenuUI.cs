using System.Collections.Generic;
using LevelSelector;
using LevelSelector.Definitions;
using LoadingScreen;
using ScriptableObjects;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TowerDefense
{
    /// <summary>
    /// Auf das pauseCanvas bzw. dessen Root-Panel legen.
    /// Wird bei jedem Öffnen (OnEnable) komplett neu aufgebaut.
    /// </summary>
    public class PauseMenuUI : MonoBehaviour
    {
        [Header("Main Buttons")]
        [SerializeField] private Button btnContinue;
        [SerializeField] private Button btnSettings;
        [SerializeField] private Button btnRestart;
        [SerializeField] private Button btnQuit;
        [SerializeField] private string mapSceneName = "LevelSelector";

        [Header("Dialog / Fade")]
        [SerializeField] private ConfirmDialog confirmDialog;
        [SerializeField] private CanvasGroup canvasGroup;   // auf dem Pause-Canvas-Root
        [SerializeField] private float fadeTime = 0.15f;

        [Header("Panels")]
        [SerializeField] private GameObject mainPanel;
        [SerializeField] private SettingsPanel settingsPanel;

        [Header("Wave Browser")]
        [SerializeField] private Button btnPrevWave;
        [SerializeField] private Button btnNextWave;
        [SerializeField] private Text txtWaveTitle;     // "NIGHT: 1/8"
        [SerializeField] private Transform waveEntryContainer;
        [SerializeField] private WaveEnemyEntry waveEntryPrefab;
        [SerializeField] private Text txtWaveSummary;
        [SerializeField] private EnemyInfoPanel enemyInfoPanel;

        [Header("Scene Change")]
        [Tooltip("SceneFadeIn aus der Szene. Szenenwechsel laufen darüber (Fade, dann LoaderInfo).")]
        [SerializeField] private SceneFadeIn sceneFader;

        [Header("Loadout (nutzt LoadoutSelectionButton)")]
        [SerializeField] private Transform weaponContainer;
        [SerializeField] private Transform perkContainer;
        [SerializeField] private Transform mutatorContainer;
        [SerializeField] private GameObject loadoutButtonPrefab;   // Prefab mit LoadoutSelectionButton
        [SerializeField] private Text txtLoadoutDetailTitle;
        [SerializeField] private Text txtLoadoutDetail;

        [Tooltip("Gleiche Farben wie in LevelLoadoutUI (colorWeapon / colorPerk / colorMutator).")]
        [SerializeField] private Color colorWeapon  = new Color(0.55f, 0.25f, 0.20f, 1f);
        [SerializeField] private Color colorPerk    = new Color(0.25f, 0.38f, 0.55f, 1f);
        [SerializeField] private Color colorMutator = new Color(0.40f, 0.25f, 0.50f, 1f);

        [Header("Quests")]
        [SerializeField] private Transform questContainer;
        [SerializeField] private GameObject questPrefab;        // dein QuestUI-Prefab

        [Header("Score")]
        [SerializeField] private ScorePanelUI scorePanel;

        private int _shownWave;
        private int _openedFrame;
        private bool _leaving;
        private WaveManager _waveManager;

        private void Awake()
        {
            btnContinue.onClick.AddListener(OnContinue);
            btnSettings.onClick.AddListener(OnSettings);
            btnRestart.onClick.AddListener(OnRestart);
            btnQuit.onClick.AddListener(OnQuit);
            btnPrevWave.onClick.AddListener(() => ChangeWave(-1));
            btnNextWave.onClick.AddListener(() => ChangeWave(+1));
        }

        private void OnEnable()
        {
            _openedFrame = Time.frameCount; // verhindert, dass dieselbe ESC-Taste sofort wieder schließt
            _waveManager = FindFirstObjectByType<WaveManager>();

            ShowMain();
            if (confirmDialog != null) confirmDialog.Close();
            if (enemyInfoPanel != null) enemyInfoPanel.Close();
            if (sceneFader == null) sceneFader = FindFirstObjectByType<SceneFadeIn>(FindObjectsInactive.Include);
            StopAllCoroutines();
            if (canvasGroup != null) StartCoroutine(FadeIn());

            _shownWave = _waveManager != null ? _waveManager.currentWaveIndex : 0;
            RefreshWave();
            if (scorePanel != null) scorePanel.Rebuild();
            RefreshLoadout();
            RefreshQuests();
        }

        private void Update()
        {
            // Schließen-Reihenfolge: Dialog -> Gegner-Panel -> Settings -> Menü
            if (Time.frameCount != _openedFrame && KeyBindings.Down(GameAction.Pause) && !(settingsPanel != null && settingsPanel.IsRebinding))
            {
                if (confirmDialog != null && confirmDialog.IsOpen)
                    confirmDialog.Close();
                else if (enemyInfoPanel != null && enemyInfoPanel.IsOpen)
                    enemyInfoPanel.Close();
                else if (settingsPanel != null && settingsPanel.gameObject.activeSelf)
                    ShowMain();
                else
                    OnContinue();
            }
        }

        // ── Hauptbuttons ───────────────────────────────────────────

        private void OnContinue() => LevelManager.instance.UnPause();

        private void OnSettings()
        {
            mainPanel.SetActive(false);
            settingsPanel.gameObject.SetActive(true);
        }

        public void ShowMain()
        {
            if (settingsPanel != null) settingsPanel.gameObject.SetActive(false);
            mainPanel.SetActive(true);
        }

        private void OnRestart()
        {
            confirmDialog.Open("Restart level? Progress will be lost.", () =>
            {
                // LevelLoadoutManager ist DontDestroyOnLoad -> Loadout bleibt erhalten
                LoadScene(SceneManager.GetActiveScene().buildIndex);   // int
            });
        }

        private void OnQuit()
        {
            confirmDialog.Open("Back to map? Progress will be lost.", () =>
            {
                LoadScene(1);                            
            });
        }

        // Zentrale Stelle für Szenenwechsel: immer über SceneFadeIn (Fade-Out, danach LoaderInfo).
        // Time.timeScale setzt SceneFadeIn selbst zurück, direkt bevor der Loader startet.
        private void LoadScene(int buildIndex)
        {
            if (_leaving) return;
            _leaving = true;

            if (sceneFader != null)
                sceneFader.FadeToScene(buildIndex);
            else
            {
                Debug.LogWarning("PauseMenuUI: Kein SceneFadeIn gefunden, lade ohne Fade.");
                Time.timeScale = 1f;
                LoaderInfo.LoadScene(buildIndex);
            }
        }

        private void LoadScene(string sceneName)
        {
            if (_leaving) return;
            _leaving = true;

            if (sceneFader != null)
                sceneFader.FadeToScene(sceneName);
            else
            {
                Debug.LogWarning("PauseMenuUI: Kein SceneFadeIn gefunden, lade ohne Fade.");
                Time.timeScale = 1f;
                LoaderInfo.LoadScene(sceneName);
            }
        }

        // Unscaled, weil Time.timeScale beim Pausieren 0 ist
        private System.Collections.IEnumerator FadeIn()
        {
            canvasGroup.alpha = 0f;
            for (float t = 0f; t < fadeTime; t += Time.unscaledDeltaTime)
            {
                canvasGroup.alpha = t / fadeTime;
                yield return null;
            }
            canvasGroup.alpha = 1f;
        }

        // ── Wave-Browser ───────────────────────────────────────────

        private void ChangeWave(int dir)
        {
            if (_waveManager == null) return;
            int max = _waveManager.waveConfig.waves.Count - 1;
            _shownWave = Mathf.Clamp(_shownWave + dir, 0, max);
            RefreshWave();
        }

        private void RefreshWave()
        {
            if (_waveManager == null || _waveManager.waveConfig == null) return;

            var waves = _waveManager.waveConfig.waves;
            int total = waves.Count;

            btnPrevWave.interactable = _shownWave > 0;
            btnNextWave.interactable = _shownWave < total - 1;
            txtWaveTitle.text = $"NIGHT: {_shownWave + 1}/{total}";

            // Pro Gegnertyp die Anzahl über alle Bursts der Welle summieren
            var counts = new Dictionary<EnemyConfig, int>();
            var order = new List<EnemyConfig>();

            foreach (var burst in waves[_shownWave].bursts)
            {
                foreach (var cfg in burst.burstConfigs)
                {
                    if (cfg == null || cfg.enemyConfig == null) continue;

                    EnemyConfig enemy = cfg.enemyConfig;
                    if (!counts.ContainsKey(enemy))
                    {
                        counts[enemy] = 0;
                        order.Add(enemy);
                    }
                    counts[enemy] += cfg.spawnCount;
                }
            }

            Clear(waveEntryContainer);

            int totalEnemies = 0;
            int totalGold = 0;

            foreach (EnemyConfig enemy in order)
            {
                int count = counts[enemy];
                totalEnemies += count;
                totalGold += count * enemy.goldReward;

                WaveEnemyEntry entry = Instantiate(waveEntryPrefab, waveEntryContainer);
                entry.Setup(enemy, count, OpenEnemyInfo);
            }

            txtWaveSummary.text = $"{totalEnemies} enemies   |   {totalGold} Gold";
        }

        private void OpenEnemyInfo(EnemyConfig config)
        {
            if (enemyInfoPanel != null)
                enemyInfoPanel.Open(config);
        }

        // ── Loadout ────────────────────────────────────────────────

        private readonly List<LoadoutSelectionButton> _loadoutButtons = new();
        private readonly List<(string title, string text, ScriptableObject def)> _loadoutDetails = new();

        private void RefreshLoadout()
        {
            Clear(weaponContainer);
            Clear(perkContainer);
            Clear(mutatorContainer);
            _loadoutButtons.Clear();
            _loadoutDetails.Clear();
            ShowDetail("", "");

            var loadout = LevelLoadoutManager.Instance != null
                ? LevelLoadoutManager.Instance.CurrentLoadout
                : null;

            // Kann null sein, wenn das Level direkt im Editor gestartet wurde (ohne Level-Auswahl)
            if (loadout == null) return;

            if (loadout.weapon != null)
            {
                WeaponDefinition w = loadout.weapon;
                LoadoutSelectionButton btn = SpawnLoadoutButton(weaponContainer);
                if (btn != null)
                {
                    btn.SetupWeapon(w, _loadoutButtons.Count, colorWeapon, OnLoadoutClicked);
                    RegisterLoadoutButton(btn, w, w.weaponName,
                        $"PASSIVE\n{w.passiveDescription}\n\nACTIVE\n{w.activeDescription}");
                }
            }

            foreach (PerkDefinition perk in loadout.perks)
            {
                if (perk == null) continue;

                LoadoutSelectionButton btn = SpawnLoadoutButton(perkContainer);
                if (btn == null) continue;

                btn.SetupPerk(perk, _loadoutButtons.Count, colorPerk, OnLoadoutClicked);
                RegisterLoadoutButton(btn, perk, perk.perkName,
                    $"{perk.description}\n\nScore: x{perk.ScoreMultiplier:0.00}");
            }

            foreach (MutatorDefinition mutator in loadout.mutators)
            {
                if (mutator == null) continue;

                LoadoutSelectionButton btn = SpawnLoadoutButton(mutatorContainer);
                if (btn == null) continue;

                btn.SetupMutator(mutator, _loadoutButtons.Count, colorMutator, OnLoadoutClicked);
                RegisterLoadoutButton(btn, mutator, mutator.mutatorName,
                    $"{mutator.description}\n\nScore: x{mutator.ScoreMultiplier:0.00}");
            }
        }

        private LoadoutSelectionButton SpawnLoadoutButton(Transform container)
        {
            GameObject obj = Instantiate(loadoutButtonPrefab, container);
            LoadoutSelectionButton btn = obj.GetComponent<LoadoutSelectionButton>();

            if (btn == null)
            {
                Debug.LogError("PauseMenuUI: loadoutButtonPrefab besitzt kein LoadoutSelectionButton-Script.");
                Destroy(obj);
                return null;
            }

            return btn;
        }

        private void RegisterLoadoutButton(LoadoutSelectionButton btn, ScriptableObject def, string title, string text)
        {
            _loadoutButtons.Add(btn);
            _loadoutDetails.Add((title, text, def));
        }

        private void OnLoadoutClicked(int index)
        {
            if (index < 0 || index >= _loadoutButtons.Count) return;

            for (int i = 0; i < _loadoutButtons.Count; i++)
                _loadoutButtons[i].SetSelected(i == index);

            ShowDetail(_loadoutDetails[index].title, _loadoutDetails[index].text);

            // Zugehörigen Multiplikator im Score hervorheben (Waffe hat keinen -> Highlight wird gelöscht)
            if (scorePanel != null)
                scorePanel.Highlight(_loadoutDetails[index].def);
        }

        private void ShowDetail(string title, string text)
        {
            txtLoadoutDetailTitle.text = title;
            txtLoadoutDetail.text = text;
        }

        // ── Quests ─────────────────────────────────────────────────

        private void RefreshQuests()
        {
            Clear(questContainer);

            var loadout = LevelLoadoutManager.Instance?.CurrentLoadout;
            if (loadout == null || loadout.levelDefinition == null) return;

            foreach (var quest in loadout.levelDefinition.quests)
            {
                if (quest == null) continue;

                GameObject obj = Instantiate(questPrefab, questContainer);
                var ui = obj.GetComponent<QuestUI>();
                if (ui == null) { Destroy(obj); continue; }

                bool completed = LevelProgressManager.Instance != null &&
                                 LevelProgressManager.Instance.IsQuestCompleted(loadout.levelIndex, quest);

                ui.Setup(quest, completed);
            }
        }

        // ── Hilfen ─────────────────────────────────────────────────

        private static void Clear(Transform container)
        {
            if (container == null) return;

            for (int i = container.childCount - 1; i >= 0; i--)
            {
                Transform child = container.GetChild(i);
                child.SetParent(null);
                Destroy(child.gameObject);
            }
        }
    }
}