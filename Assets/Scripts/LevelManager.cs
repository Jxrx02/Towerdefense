using UnityEngine.Rendering.Universal;
using LevelSelector;
using LevelSelector.Definitions;

namespace TowerDefense
{
    using System;
    using System.Collections;
    using UnityEngine;
    using UnityEngine.UI;

    public class LevelManager : MonoBehaviour
    {
        public static LevelManager instance;

        [Header("Current Game-Variables")]
        private bool doubleTime;
        public int cur_coins;
        public int start_coins = 35;
        public int cur_health;
        public int start_health = 20;

        [Header("Frühstart-Bonus")]
        [Tooltip("Maximaler Bonus")]
        public int maxEarlyBonus = 120;

        [Header("GameObject-Linking")]
        public GameObject pauseCanvas;
        public GameObject gameCanvas;
        public GameObject deathScreen;
        public GameObject questionPanel;
        public GameObject endScreen;

        public Text txt_money;
        public Text txt_health;

        [Header("Day / Night")]
        public GameObject dayScene;
        public GameObject nightScene;
        public Light2D sceneLight;

        [SerializeField] private float dayNightTransitionDuration = 1.5f;

        private readonly Color nightColor = new Color32(19, 40, 91, 255); // #13285B
        private readonly Color dayColor = Color.white;
        
        [Tooltip("Text der den Frühstart-Bonus anzeigt, z.B. '+ 85 Gold'")]
        public Text txt_earlyBonus;
        [Tooltip("Button zum manuellen Starten der nächsten Welle")]
        public Button btn_startWave;

        public GameObject towerUIPrefab;
        public GameObject waveManager;
        public GameObject clickToStartGameObject;
        [SerializeField] private DialogCanvas dialogCanvasPrefab;

        [Header("Texture-Linking")]
        public Sprite img_doubleTime;
        public Sprite img_normspeed;
        public GameObject btn_image_fastForward;
        public Sprite img_pause;
        public Sprite img_play;
        public GameObject btn_image_pause;

        public Boolean heroFielded;

        // ── Frühstart-State ───────────────────────────────────────
        private float _earlyStartTimer = 0f;
        private bool _earlyStartActive = false;
        private Coroutine _earlyStartCoroutine;
        private StatDiffDisplay statDiffDisplay;

        // ── Level Runtime Stats ─────────────────────────────────────

        private float _levelStartTime;
        private int _enemiesKilled;
        private bool _levelFinished;
        
        public bool isDay = true;

        private void Awake()
        {
            statDiffDisplay = GetComponent<StatDiffDisplay>();
            isDay = true;
        }

        void Start()
        {
            Actions.onEnemyReachedEnd  += LoseHealth;
            Actions.onEnemyDeath       += GainCoins;
            Actions.onLvlComplete      += LvlCompleted;
            Actions.onWaveSpawnComplete += OnWaveSpawnComplete;

            // Neues Event: wird vom WaveManager gefeuert wenn eine Welle vollständig
            // besiegt wurde (alle Gegner tot/entkommen) → Frühstart-Fenster öffnen
            Actions.onWaveCleared      += OnWaveCleared;

            cur_health = start_health;
            cur_coins  = start_coins;
            UpdateStats();

            if (TowerUI.Instance == null)       Instantiate(towerUIPrefab);
            if (DialogCanvas.instance == null)  Instantiate(dialogCanvasPrefab);
            if (instance == null)               instance = this;
            else                                Destroy(gameObject);

            SetStartWaveButtonVisible(true);


        }

        // ── Wave-Events ───────────────────────────────────────────

        private Boolean _allWavesSpawned = false;
        private int _waveIndex = 0;
        private bool _waitingForPlayerStart;
        
        private void OnWaveSpawnComplete()
        {
            _allWavesSpawned = true;
            
            Actions.onLvlComplete.Invoke();
        }

        /// <summary>
        /// Wird aufgerufen wenn alle Gegner einer Welle besiegt wurden.
        /// Startet das Frühstart-Fenster.
        /// </summary>

        private void OnWaveCleared()
        {
            if (_allWavesSpawned)
                return;

            if (_earlyStartCoroutine != null)
                StopCoroutine(_earlyStartCoroutine);

            _waveIndex++;

            isDay = (_waveIndex % 3 == 0);
            
            if (isDay)
            {
                // 3 Waves geschafft → neuer Tag
                SetDay();
                _earlyStartCoroutine = StartCoroutine(WaitForPlayerStart());
            }
            else
            {
                _earlyStartCoroutine = StartCoroutine(EarlyStartCountdown());
            }
        }
        private IEnumerator WaitForPlayerStart()
        {
            _waitingForPlayerStart = true;
            _earlyStartActive = false;

            SetStartWaveButtonVisible(true);

            while (_waitingForPlayerStart)
                yield return null;
        }
        private IEnumerator EarlyStartCountdown()
        {
            var waveManagerComponent = waveManager.GetComponent<WaveManager>();

            if (waveManagerComponent == null || waveManagerComponent.waveConfig == null)
            {
                Debug.LogError("WaveManager oder WaveConfig nicht gefunden.");
                yield break;
            }

            _earlyStartTimer = waveManagerComponent.waveConfig.timeBetweenWaves;
            _earlyStartActive = true;
            SetStartWaveButtonVisible(true);

            while (_earlyStartTimer > 0f)
            {
                _earlyStartTimer -= Time.deltaTime;
                UpdateEarlyBonusDisplay();
                yield return null;
            }

            // Zeit abgelaufen → nächste Welle startet automatisch ohne Bonus
            _earlyStartActive = false;
            SetStartWaveButtonVisible(false);
            TriggerNextWave(earlyBonus: 0);
        }

        /// <summary>
        /// Spieler klickt manuell auf „Welle starten" → Bonus wird ausbezahlt.
        /// Diesen Aufruf an den Button im Inspector hängen.
        /// </summary>
        public void StartWaveButton()
        {
            //wird nur 1x aufgerufen
            StartGame();
            
            if (_waitingForPlayerStart)
            {
                _waitingForPlayerStart = false;
                
                TriggerNextWave(0);
                SetStartWaveButtonVisible(false);
                return;
            }

            if (_earlyStartActive)
            {
                OnEarlyStartClick();
            }
            SetStartWaveButtonVisible(false);

        }
        public void OnEarlyStartClick()
        {

            
            if (!_earlyStartActive) return;

            if (_earlyStartCoroutine != null)
                StopCoroutine(_earlyStartCoroutine);

            int bonus = CalculateEarlyBonus();
            _earlyStartActive = false;
            SetStartWaveButtonVisible(false);

            if (bonus > 0)
            {
                cur_coins += bonus;
                UpdateStats();
                ShowBonusPopup(bonus); 
            }

            TriggerNextWave(earlyBonus: bonus);
        }

        // ── Hilfsmethoden ─────────────────────────────────────────
        private void StartGame()
        {
            if (_waveIndex > 0 || _allWavesSpawned)
                return;

            clickToStartGameObject?.SetActive(false);

            // Nacht beginnt mit der ersten Welle
            SetNight();

            waveManager.gameObject.SetActive(true);

            waveManager.GetComponent<WaveManager>()?.OnGameStarted();

            SetStartWaveButtonVisible(false);
            
            _levelStartTime = Time.time;
            _enemiesKilled = 0;
            _levelFinished = false;
            
            TriggerNextWave(0);

            Debug.Log("Game gestartet – erste Welle gestartet.");
        }
        private int CalculateEarlyBonus()
        {
            var waveManagerComponent = waveManager.GetComponent<WaveManager>();
            
            int bonus = Mathf.RoundToInt(_earlyStartTimer * waveManagerComponent.waveConfig.goldPerSecondEarlyStart);
            return Mathf.Clamp(bonus, 0, maxEarlyBonus);
        }

        private void UpdateEarlyBonusDisplay()
        {
            if (txt_earlyBonus == null) return;
            int bonus = CalculateEarlyBonus();
            txt_earlyBonus.text = bonus > 0 ? $"+ {bonus} Gold" : "";
        }

        private void SetStartWaveButtonVisible(bool visible)
        {
            if (btn_startWave != null)
                btn_startWave.gameObject.SetActive(visible);
            if (txt_earlyBonus != null)
                txt_earlyBonus.gameObject.SetActive(visible);
            if (_waitingForPlayerStart)
                txt_earlyBonus.text = "Let the sun set";
            
        }

        /// <summary>
        /// Signalisiert dem WaveManager die nächste Welle zu starten.
        /// Passe dies an deine WaveManager-API an.
        /// </summary>
        private void TriggerNextWave(int earlyBonus)
        {
            Debug.Log($"Nächste Welle gestartet. Frühstart-Bonus: {earlyBonus} Gold.");
            var waveManagerComponent = waveManager.GetComponent<WaveManager>();
            waveManagerComponent.AllowNextWave();        
        }
        private void SetNight()
        {
            if (dayScene != null)
                dayScene.SetActive(false);

            if (nightScene != null)
                nightScene.SetActive(true);

            StartCoroutine(TransitionLight(nightColor, 0.7f));
        }

        private void SetDay()
        {
            if (dayScene != null)
                dayScene.SetActive(true);

            if (nightScene != null)
                nightScene.SetActive(false);

            StartCoroutine(TransitionLight(dayColor, 1f));
        }
        private IEnumerator TransitionLight(Color targetColor, float targetIntensity)
        {
            if (sceneLight == null)
                yield break;

            Color startColor = sceneLight.color;
            float startIntensity = sceneLight.intensity;

            float elapsed = 0f;

            while (elapsed < dayNightTransitionDuration)
            {
                elapsed += Time.deltaTime;

                float t = Mathf.Clamp01(elapsed / dayNightTransitionDuration);

                // Smooth transition
                t = Mathf.SmoothStep(0f, 1f, t);

                sceneLight.color = Color.Lerp(startColor, targetColor, t);
                sceneLight.intensity = Mathf.Lerp(startIntensity, targetIntensity, t);

                yield return null;
            }

            sceneLight.color = targetColor;
            sceneLight.intensity = targetIntensity;
        }
        private void ShowBonusPopup(int bonus)
        {

        }
        
        private void LvlCompleted()
        {
            if (!_allWavesSpawned) return;
            FinishLevel();
            FindAnyObjectByType<LevelUnlocker>().CompleteLevel();
            endScreen.gameObject.SetActive(true);
        }
        public void FinishLevel()
        {
            if (_levelFinished)
                return;

            _levelFinished = true;

            LevelLoadoutManager loadoutManager = LevelLoadoutManager.Instance;

            if (loadoutManager == null)
            {
                Debug.LogError(
                    "LevelLoadoutManager wurde nicht gefunden. " +
                    "Das Level kann nicht abgeschlossen werden.");

                return;
            }

            LevelLoadout loadout = loadoutManager.CurrentLoadout;

            if (loadout == null)
            {
                Debug.LogError("Kein aktuelles LevelLoadout vorhanden.");
                return;
            }

            int completionTime = Mathf.Max(0, Mathf.RoundToInt(Time.time - _levelStartTime));
            int score = CalculateLevelScore(completionTime);

            LevelResult result = new LevelResult
            {
                levelIndex = loadout.levelIndex,
                victory = true,
                score = score,
                completionTime = completionTime,
                remainingHealth = cur_health,
                enemiesKilled = _enemiesKilled,
                wavesCompleted = _waveIndex,
                coinsEarned = cur_coins - start_coins <=0?0:cur_coins-start_coins
            };

            LevelDefinition levelDefinition = loadout.levelDefinition;

            if (levelDefinition == null)
            {
                Debug.LogError("Keine LevelDefinition im aktuellen Loadout gefunden.");
                return;
            }

            LevelProgressManager.Instance.CompleteLevel(
                result,
                levelDefinition);

            Debug.Log(
                $"Level {result.levelIndex} abgeschlossen! " +
                $"Score: {result.score} | " +
                $"Zeit: {result.completionTime}s | " +
                $"Kills: {result.enemiesKilled}");

            if (endScreen != null)
                endScreen.SetActive(true);
        }

        private int CalculateLevelScore(int completionTime)
        {
            int score = 0;

            // Gegner
            score += _enemiesKilled * 10;

            // Überlebte Wellen
            score += _waveIndex * 100;

            // Verbleibendes Leben
            score += cur_health * 50;

            // Verbleibendes Gold
            score += cur_coins * 2;

            // Zeitbonus
            int timeBonus =
                Mathf.Max(0, 1000 - completionTime * 2);

            score += timeBonus;

            return Mathf.Max(0, score);
        }
        
        public Boolean CanPurchase(int price)  => (cur_coins - price) >= 0;

        public Boolean DoPurchase(int price)
        {
            if (!CanPurchase(price)) return false;
            cur_coins -= price;
            UpdateStats();
            return true;
        }

        private void UpdateStats()
        {
            if (txt_money  != null) txt_money.text  = cur_coins.ToString();
            else Debug.LogWarning("txt_money ist zerstört oder nicht zugewiesen!");

            if (txt_health != null) txt_health.text = cur_health.ToString();
            else Debug.LogWarning("txt_health ist zerstört oder nicht zugewiesen!");
        }

        public void Button_DoubleTime()
        {
            doubleTime = !doubleTime;
            btn_image_fastForward.GetComponent<Image>().sprite =
                doubleTime ? img_doubleTime : img_normspeed;
            Time.timeScale = doubleTime ? 3 : 1;
        }

        public void Pause()
        {
            btn_image_pause.GetComponent<Image>().sprite = img_pause;
            pauseCanvas.gameObject.SetActive(true);
            Time.timeScale = 0;
        }

        public void UnPause()
        {
            pauseCanvas.gameObject.SetActive(false);
            Time.timeScale = doubleTime ? 3 : 1;
            btn_image_pause.GetComponent<Image>().sprite = img_play;
        }

        public void LoseHealth(GameObject enemy)
        {
            if (cur_health > 0)
                cur_health -= enemy.GetComponent<Enemy>().currentHealth;
            else
                ShowDeathScreen();
            UpdateStats();
        }

        public void GainCoins(GameObject enemy)
        {
            _enemiesKilled++;
            cur_coins += enemy.GetComponent<Enemy>().enemyConfig.goldReward;
            UpdateStats();
        }

        public void ShowDeathScreen()
        {
            deathScreen.gameObject.SetActive(true);
            Time.timeScale = 0;
        }

        public void ReviveFailed()
        {
            Time.timeScale = 1;
            questionPanel.gameObject.SetActive(false);
            deathScreen.GetComponentInChildren<Button>().interactable = false;
            deathScreen.gameObject.SetActive(true);
        }

        public void DoRevive()
        {
            for (int i = 0; i < 6; i++)
            {
                try
                {
                    GameObject enemy = TowerHeroManager.instance.enemies[0];
                    TowerHeroManager.instance.UnregisterEnemy(enemy);
                    Destroy(enemy);
                }
                catch { }
            }

            cur_health += 5;
            UpdateStats();
            deathScreen.gameObject.SetActive(false);
            questionPanel.gameObject.SetActive(false);
            Time.timeScale = 1;
        }
    }
}