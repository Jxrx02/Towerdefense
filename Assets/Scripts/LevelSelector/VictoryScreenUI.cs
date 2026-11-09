using System.Collections;
using LevelSelector;
using LevelSelector.Definitions;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TowerDefense
{
    /// <summary>
    /// Victory-Screen: Score-Aufschlüsselung, Highscore, Spieler-Level mit XP-Balken und
    /// "NEW PERK"-Popup bei jedem Level-Up. Wird vom LevelManager über Show() geöffnet.
    /// Alle Animationen laufen mit unskalierter Zeit (das Spiel steht bei Time.timeScale = 0).
    /// </summary>
    public class VictoryScreenUI : MonoBehaviour
    {
        [Header("Titel")]
        [Tooltip("Optional: Krone. Ohne Sprite wird das Bild ausgeblendet.")]
        [SerializeField] private Image crownIcon;

        [Header("Score")]
        [SerializeField] private ScorePanelUI scorePanel;

        [Header("Highscore")]
        [SerializeField] private Text txtHighscore;
        [SerializeField] private GameObject newRecordObject;

        [Header("Level")]
        [SerializeField] private GameObject levelCard;
        [SerializeField] private Text txtLevel;
        [SerializeField] private Text txtXpGained;
        [SerializeField] private Text txtXp;
        [SerializeField] private RectTransform barFill;
        [SerializeField] private GameObject nextPerkRoot;
        [SerializeField] private Image nextPerkIcon;
        [SerializeField] private float fillDuration = 1.8f;

        [Header("Neuer Perk (Popup)")]
        [SerializeField] private GameObject perkPopup;
        [SerializeField] private Text txtPerkName;
        [SerializeField] private Text txtPerkDescription;
        [SerializeField] private Image imgPerkIcon;
        [SerializeField] private Button btnPerkContinue;

        [Header("Buttons")]
        [SerializeField] private Button btnMap;
        [SerializeField] private Button btnRestart;
        [SerializeField] private string mapSceneName = "LevelSelector";

        [Header("Einblenden")]
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private float fadeTime = 0.25f;

        private bool _shown;
        private bool _leaving;
        private bool _popupDismissed;

        private void Awake()
        {
            btnPerkContinue.onClick.AddListener(() => _popupDismissed = true);
            btnMap.onClick.AddListener(() => Leave(() => SceneTransition.Load(mapSceneName)));
            btnRestart.onClick.AddListener(() => Leave(() => SceneTransition.Load(SceneManager.GetActiveScene().buildIndex)));
        }

        private void Leave(System.Action load)
        {
            if (_leaving) return;
            _leaving = true;
            load();
        }

        /// <summary>
        /// Wird einmal vom LevelManager aufgerufen. previousHighscore ist der Wert VOR LevelProgressManager.CompleteLevel
        /// (danach steht dort schon der neue Score). Speichert die XP und startet die Animation.
        /// </summary>
        public void Show(LevelManager.ScoreBreakdown breakdown, int previousHighscore)
        {
            if (_shown) return;
            _shown = true;

            Time.timeScale = 0f;
            gameObject.SetActive(true);

            if (crownIcon != null)
                crownIcon.gameObject.SetActive(crownIcon.sprite != null);

            // Score: eingefrorene Werte (Zeitbonus ändert sich nicht mehr)
            scorePanel.Rebuild(breakdown);

            // Highscore: angezeigt wird der bisherige Wert, "NEW RECORD" wenn er geschlagen wurde.
            // Gespeichert hat ihn schon LevelProgressManager.CompleteLevel.
            bool newRecord = breakdown.total > previousHighscore;
            txtHighscore.text = $"Highscore: {previousHighscore}";
            newRecordObject.SetActive(newRecord);

            // Level-System
            PlayerProgressionDefinition definition = PlayerProgress.Definition;
            bool hasProgression = definition != null && definition.levels != null && definition.levels.Count > 0;
            levelCard.SetActive(hasProgression);
            perkPopup.SetActive(false);

            int startXp = PlayerProgress.TotalXp;
            int gained = hasProgression ? Mathf.RoundToInt(breakdown.total * definition.xpPerScorePoint) : 0;

            if (hasProgression)
            {
                PlayerProgress.AddXp(gained);
                txtXpGained.text = $"+{gained} XP";
            }

            StartCoroutine(Run(startXp, startXp + gained, hasProgression));
        }

        private IEnumerator Run(int startXp, int endXp, bool hasProgression)
        {
            // Einblenden
            if (canvasGroup != null)
            {
                for (float t = 0f; t < fadeTime; t += Time.unscaledDeltaTime)
                {
                    canvasGroup.alpha = t / fadeTime;
                    yield return null;
                }
                canvasGroup.alpha = 1f;
            }

            if (!hasProgression)
                yield break;

            PlayerProgress.LevelState start = PlayerProgress.Evaluate(startXp);
            ApplyState(start);
            int shownLevel = start.level;

            if (endXp > startXp)
            {
                yield return new WaitForSecondsRealtime(0.4f);

                for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / Mathf.Max(0.1f, fillDuration))
                {
                    float xp = Mathf.Lerp(startXp, endXp, Mathf.SmoothStep(0f, 1f, t));
                    PlayerProgress.LevelState state = PlayerProgress.Evaluate(Mathf.RoundToInt(xp));

                    while (shownLevel < state.level)
                    {
                        shownLevel++;
                        yield return LevelUp(shownLevel);
                    }

                    ApplyState(state);
                    yield return null;
                }
            }

            // Restliche Level-Ups am Ende der Animation
            PlayerProgress.LevelState end = PlayerProgress.Evaluate(endXp);
            while (shownLevel < end.level)
            {
                shownLevel++;
                yield return LevelUp(shownLevel);
            }

            ApplyState(end);
        }

        /// <summary>Balken füllt sich, dann kommt (falls vorhanden) das Popup mit dem neuen Perk.</summary>
        private IEnumerator LevelUp(int newLevel)
        {
            SetFill(1f);
            yield return new WaitForSecondsRealtime(0.25f);

            PerkDefinition perk = PlayerProgress.GetPerkUnlockedAtLevel(newLevel);
            if (perk != null)
                yield return ShowPerkPopup(perk);

            txtLevel.text = $"LEVEL {newLevel}";
            SetFill(0f);
        }

        private IEnumerator ShowPerkPopup(PerkDefinition perk)
        {
            _popupDismissed = false;

            txtPerkName.text = perk.perkName;
            txtPerkDescription.text = perk.description;
            imgPerkIcon.sprite = perk.icon;
            imgPerkIcon.enabled = perk.icon != null;

            perkPopup.SetActive(true);
            yield return new WaitUntil(() => _popupDismissed);
            perkPopup.SetActive(false);
        }

        // ── Anzeige ───────────────────────────────────────────────

        private void ApplyState(PlayerProgress.LevelState state)
        {
            txtLevel.text = $"LEVEL {state.level}";
            txtXp.text = state.isMax ? "MAX" : $"{state.xpIntoLevel} / {state.xpForNext}";
            SetFill(state.Fraction);

            // Vorschau: nächster Perk, der noch freigeschaltet wird
            bool hasNext = PlayerProgress.GetNextUnlock(state.level, out PerkDefinition perk, out _);
            nextPerkRoot.SetActive(hasNext);

            if (hasNext)
            {
                nextPerkIcon.sprite = perk.icon;
                nextPerkIcon.enabled = perk.icon != null;
            }
        }

        private void SetFill(float fraction)
        {
            barFill.anchorMin = new Vector2(0f, 0f);
            barFill.anchorMax = new Vector2(Mathf.Clamp01(fraction), 1f);
        }
    }
}