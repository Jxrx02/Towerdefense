using TowerDefense;
using UnityEngine;
using UnityEngine.UI;

namespace LevelSelector
{
    /// <summary>
    /// Hauptmenü über der Übersichtskarte. Beim Spielstart: Play / Research / Settings / Quit.
    /// Mit ESC öffnet es sich später als Pausemenü der Karte ("Continue").
    /// Die Forschung bleibt dein bestehendes ResearchCanvas: es wird nur ein- und ausgeblendet.
    /// </summary>
    public class MainMenuUI : MonoBehaviour
    {
        public static MainMenuUI Instance { get; private set; }

        /// <summary>Menü, Einstellungen oder Forschung sind offen. Der Held steht dann still.</summary>
        public static bool IsOpen => Instance != null && Instance._open;

        [Header("Panels")]
        [SerializeField] private GameObject menuPanel;
        [SerializeField] private SettingsPanel settingsPanel;
        [SerializeField] private ConfirmDialog confirmDialog;
        [Tooltip("Dein bestehendes Forschungs-Canvas (optional)")]
        [SerializeField] private GameObject researchCanvas;

        [Header("Buttons")]
        [SerializeField] private Button btnPlay;
        [SerializeField] private Text txtPlay;
        [SerializeField] private Button btnResearch;
        [SerializeField] private Button btnSettings;
        [SerializeField] private Button btnQuit;

        [Header("Texte")]
        [SerializeField] private Text txtProfile;

        private bool _open;
        private bool _firstOpen;
        private bool _inResearch;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;

            btnPlay.onClick.AddListener(Close);
            btnSettings.onClick.AddListener(OpenSettings);
            btnQuit.onClick.AddListener(AskQuit);
            btnResearch.onClick.AddListener(OpenResearch);
            btnResearch.gameObject.SetActive(researchCanvas != null);

            if (settingsPanel != null)
                settingsPanel.BackRequested += ShowMenu;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            _firstOpen = !OverworldSession.MenuShown;
            OverworldSession.MenuShown = true;

            if (_firstOpen)
                Open();
            else
                Close();
        }

        private void Update()
        {
            // Zurück aus der Forschung: das Canvas wurde von seinem eigenen Zurück-Button geschlossen
            if (_inResearch && researchCanvas != null && !researchCanvas.activeInHierarchy)
            {
                _inResearch = false;
                Open();
                return;
            }

            if (!Input.GetKeyDown(KeyCode.Escape))
                return;

            // Der Loadout-Screen schließt sich selbst mit ESC. Dieselbe Taste darf nicht auch das Menü öffnen.
            if (LevelLoadoutUI.IsOpen || LevelLoadoutUI.LastClosedFrame == Time.frameCount)
                return;

            if (confirmDialog != null && confirmDialog.IsOpen)
            {
                confirmDialog.Close();
                return;
            }

            if (_inResearch)
                return;

            if (settingsPanel != null && settingsPanel.gameObject.activeSelf)
            {
                if (!settingsPanel.IsRebinding)
                    ShowMenu();
                return;
            }

            if (_open)
            {
                if (!_firstOpen)   // beim ersten Start geht es nur über "Play"
                    Close();
            }
            else
            {
                Open();
            }
        }

        // ── Öffnen / Schließen ────────────────────────────────────

        public void Open()
        {
            _open = true;
            menuPanel.SetActive(true);

            if (settingsPanel != null)
                settingsPanel.gameObject.SetActive(false);

            txtPlay.text = _firstOpen ? "Play" : "Continue";

            if (txtProfile != null)
                txtProfile.text = $"Player level {PlayerProgress.CurrentLevel}";
        }

        public void Close()
        {
            _open = false;
            _firstOpen = false;
            _inResearch = false;
            menuPanel.SetActive(false);

            if (settingsPanel != null)
                settingsPanel.gameObject.SetActive(false);
        }

        private void ShowMenu()
        {
            if (settingsPanel != null)
                settingsPanel.gameObject.SetActive(false);

            menuPanel.SetActive(true);
        }

        private void OpenSettings()
        {
            menuPanel.SetActive(false);
            settingsPanel.gameObject.SetActive(true);
        }

        private void OpenResearch()
        {
            if (researchCanvas == null) return;

            menuPanel.SetActive(false);
            _inResearch = true;
            researchCanvas.SetActive(true);
        }

        private void AskQuit()
        {
            if (confirmDialog == null)
            {
                DoQuit();
                return;
            }

            confirmDialog.Open("Quit the game?", DoQuit);
        }

        private static void DoQuit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}