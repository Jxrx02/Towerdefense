using System.Collections.Generic;
using System.Reflection;
using System.Text;
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
        

        [Header("Dialog / Fade")]
        [SerializeField] private SceneFadeIn sceneFadeIn;
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
        [SerializeField] private Text txtWaveInfo;      // Mobs, Gold, Stats (Rich Text)

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
        [SerializeField] private Text txtScoreLabels;   // linke Spalte
        [SerializeField] private Text txtScoreValues;   // rechte Spalte (rechtsbündig)

        private int _shownWave;
        private int _openedFrame;
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
            StopAllCoroutines();
            if (canvasGroup != null) StartCoroutine(FadeIn());

            _shownWave = _waveManager != null ? _waveManager.currentWaveIndex : 0;
            RefreshWave();
            RefreshLoadout();
            RefreshQuests();
        }

        private void Update()
        {
            // Score live aktualisieren (Time.time steht bei timeScale 0, daher stabil)
            RefreshScore();

            // Schließen-Reihenfolge: Dialog -> Settings -> Menü
            if (Time.frameCount != _openedFrame && KeyBindings.Down(GameAction.Pause) && !(settingsPanel != null && settingsPanel.IsRebinding))
            {
                if (confirmDialog != null && confirmDialog.IsOpen)
                    confirmDialog.Close();
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

        // Zentrale Stelle für Szenenwechsel über deinen Ladebildschirm (Szene 0 -> SceneLoader).
        // Time.timeScale MUSS wieder 1 sein: SceneLoader nutzt Invoke und Time.deltaTime.
        // Mit Fade: den Aufruf in den beiden Methoden gegen deine Fade-Klasse tauschen
        // (Wrapper muss auf dem Fade-Objekt laufen, nicht auf diesem Menü).
        private void LoadScene(int buildIndex)
        {
            Time.timeScale = 1f;
            sceneFadeIn.FadeToScene(buildIndex);
        }

        private void LoadScene(string sceneName)
        {
            Time.timeScale = 1f;
            sceneFadeIn.FadeToScene(sceneName);
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

            // Aggregieren: pro EnemyConfig Anzahl summieren
            var counts = new Dictionary<ScriptableObject, int>();
            var order = new List<ScriptableObject>();

            foreach (var burst in waves[_shownWave].bursts)
            {
                foreach (var cfg in burst.burstConfigs)
                {
                    if (cfg == null || cfg.enemyConfig == null) continue;

                    var key = cfg.enemyConfig;
                    if (!counts.ContainsKey(key))
                    {
                        counts[key] = 0;
                        order.Add(key);
                    }
                    counts[key] += cfg.spawnCount;
                }
            }

            var sb = new StringBuilder();
            int totalEnemies = 0;
            int totalGold = 0;

            foreach (var key in order)
            {
                var enemyCfg = (EnemyConfig)key;
                int count = counts[key];
                int gold = count * enemyCfg.goldReward;

                totalEnemies += count;
                totalGold += gold;

                sb.AppendLine($"<b>{count}x {enemyCfg.name}</b>   <color=#E8B04A>{gold} Gold</color>");
                sb.AppendLine(BuildStatLine(enemyCfg));
                sb.AppendLine();
            }

            sb.AppendLine($"<b>Gesamt:</b> {totalEnemies} Gegner  |  <color=#E8B04A>{totalGold} Gold</color>");
            txtWaveInfo.text = sb.ToString();
        }

        /// <summary>
        /// Liest alle öffentlichen int/float-Felder des EnemyConfig per Reflection aus
        /// (Health, Speed, ... – egal wie sie bei dir heißen). goldReward wird übersprungen,
        /// weil es separat angezeigt wird.
        /// </summary>
        private static string BuildStatLine(EnemyConfig cfg)
        {
            var parts = new List<string>();

            foreach (FieldInfo f in cfg.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (f.Name == "goldReward") continue;

                if (f.FieldType == typeof(int) || f.FieldType == typeof(float))
                {
                    object v = f.GetValue(cfg);
                    string val = v is float fl ? fl.ToString("0.##") : v.ToString();
                    parts.Add($"{Nicify(f.Name)}: {val}");
                }
            }

            return parts.Count == 0 ? "  –" : "  " + string.Join("   ", parts);
        }

        private static string Nicify(string fieldName)
        {
#if UNITY_EDITOR
            return UnityEditor.ObjectNames.NicifyVariableName(fieldName);
#else
            return fieldName;
#endif
        }

        // ── Loadout ────────────────────────────────────────────────

        private readonly List<LoadoutSelectionButton> _loadoutButtons = new();
        private readonly List<(string title, string text)> _loadoutDetails = new();

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
                    RegisterLoadoutButton(btn, w.weaponName,
                        $"PASSIVE\n{w.passiveDescription}\n\nACTIVE\n{w.activeDescription}");
                }
            }

            foreach (PerkDefinition perk in loadout.perks)
            {
                if (perk == null) continue;

                LoadoutSelectionButton btn = SpawnLoadoutButton(perkContainer);
                if (btn == null) continue;

                btn.SetupPerk(perk, _loadoutButtons.Count, colorPerk, OnLoadoutClicked);
                RegisterLoadoutButton(btn, perk.perkName,
                    $"{perk.description}\n\nScore: x{perk.ScoreMultiplier:0.00}");
            }

            foreach (MutatorDefinition mutator in loadout.mutators)
            {
                if (mutator == null) continue;

                LoadoutSelectionButton btn = SpawnLoadoutButton(mutatorContainer);
                if (btn == null) continue;

                btn.SetupMutator(mutator, _loadoutButtons.Count, colorMutator, OnLoadoutClicked);
                RegisterLoadoutButton(btn, mutator.mutatorName,
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

        private void RegisterLoadoutButton(LoadoutSelectionButton btn, string title, string text)
        {
            _loadoutButtons.Add(btn);
            _loadoutDetails.Add((title, text));
        }

        private void OnLoadoutClicked(int index)
        {
            if (index < 0 || index >= _loadoutButtons.Count) return;

            for (int i = 0; i < _loadoutButtons.Count; i++)
                _loadoutButtons[i].SetSelected(i == index);

            ShowDetail(_loadoutDetails[index].title, _loadoutDetails[index].text);
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

        // ── Score ──────────────────────────────────────────────────

        private void RefreshScore()
        {
            if (txtScoreLabels == null || txtScoreValues == null || LevelManager.instance == null) return;

            var s = LevelManager.instance.GetScoreBreakdown();

            // Beide Texte haben exakt gleich viele Zeilen, dadurch stehen Label und Wert auf einer Höhe
            txtScoreLabels.text =
                $"Kills ({s.kills} x 10)\n" +
                $"Waves ({s.waves} x 100)\n" +
                $"Gold ({s.gold} x 2)\n" +
                $"Time bonus ({s.timeSeconds}s)\n" +
                "\n" +
                "Base\n" +
                "Multiplier\n" +
                "<b>Total</b>";

            txtScoreValues.text =
                $"{s.killScore}\n" +
                $"{s.waveScore}\n" +
                $"{s.goldScore}\n" +
                $"{s.timeBonus}\n" +
                "\n" +
                $"{s.baseScore}\n" +
                $"x{s.multiplier:0.00}\n" +
                $"<b>{s.total}</b>";
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