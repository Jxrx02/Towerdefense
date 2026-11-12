using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TowerDefense
{
    /// <summary>
    /// Settings mit Tabs: Video / Audio / Gameplay / Controls.
    /// Alle Änderungen werden sofort angewendet und in PlayerPrefs gespeichert.
    /// </summary>
    public class SettingsPanel : MonoBehaviour
    {
        [Header("Tabs")]
        [SerializeField] private Button[] tabButtons;        // Video, Audio, Gameplay, Controls
        [SerializeField] private GameObject[] tabPanels;     // gleiche Reihenfolge
        [SerializeField] private Color tabActiveColor = new Color(1f, 0.92f, 0.75f);
        [SerializeField] private Color tabInactiveColor = new Color(1f, 1f, 1f, 0.45f);

        [Header("Video")]
        [SerializeField] private Button btnResPrev, btnResNext;
        [SerializeField] private Text txtResolution;
        [SerializeField] private Toggle toggleFullscreen, toggleVSync, togglePost;
        [SerializeField] private Button btnAaPrev, btnAaNext;
        [SerializeField] private Text txtAa;
        [SerializeField] private Slider sliderRenderScale;
        [SerializeField] private Text txtRenderScale;
        [SerializeField] private Button btnResetVideo;

        [Header("Audio")]
        [SerializeField] private Slider sliderMaster, sliderGameplay, sliderMusic, sliderEnvironment;
        [SerializeField] private Text txtMaster, txtGameplay, txtMusic, txtEnvironment;
        [SerializeField] private Button btnResetAudio;

        [Header("Gameplay")]
        [SerializeField] private Toggle toggleShake, toggleDamageNumbers, toggleHealthBars, toggleAutoPause, toggleFps;
        [SerializeField] private Button btnResetGameplay;

        [Header("Controls")]
        [SerializeField] private Transform bindingContainer;
        [SerializeField] private GameObject bindingRowPrefab;   // Root: Text "Label", Child "Key" (Button + Text)
        [SerializeField] private Button btnResetControls;

        [Header("Navigation")]
        [SerializeField] private Button btnBack;
        [SerializeField] private PauseMenuUI pauseMenu;

        private static readonly int[] AaOptions = { 0, 2, 4, 8 };

        private readonly Dictionary<GameAction, Text> _bindingTexts = new();
        private GameAction? _rebinding;
        private int _tab;
        private bool _initializing;

        public bool IsRebinding => _rebinding.HasValue;

        /// <summary>Wird beim Klick auf "Back" ausgelöst (Hauptmenü nutzt das statt PauseMenuUI).</summary>
        public event System.Action BackRequested;

        // ── Setup ─────────────────────────────────────────────────

        private void Awake()
        {
            for (int i = 0; i < tabButtons.Length; i++)
            {
                int idx = i;
                tabButtons[i].onClick.AddListener(() => ShowTab(idx));
            }

            btnResPrev.onClick.AddListener(() => ChangeResolution(-1));
            btnResNext.onClick.AddListener(() => ChangeResolution(+1));
            btnAaPrev.onClick.AddListener(() => ChangeAa(-1));
            btnAaNext.onClick.AddListener(() => ChangeAa(+1));

            toggleFullscreen.onValueChanged.AddListener(v => { if (!_initializing) { GameSettings.Fullscreen = v; GameSettings.ApplyVideo(); } });
            toggleVSync.onValueChanged.AddListener(v => { if (!_initializing) { GameSettings.VSync = v; GameSettings.ApplyVideo(); } });
            togglePost.onValueChanged.AddListener(v => { if (!_initializing) { GameSettings.PostProcessing = v; GameSettings.ApplyPostProcessing(); } });

            sliderRenderScale.minValue = 0.5f;
            sliderRenderScale.maxValue = 1.5f;
            sliderRenderScale.onValueChanged.AddListener(v =>
            {
                v = Mathf.Round(v * 20f) / 20f; // 0.05er Schritte
                txtRenderScale.text = v.ToString("0.00");
                if (_initializing) return;
                GameSettings.RenderScale = v;
                GameSettings.ApplyVideo();
            });

            HookVolume(sliderMaster, txtMaster, v => GameSettings.Master = v);
            HookVolume(sliderGameplay, txtGameplay, v => GameSettings.Gameplay = v);
            HookVolume(sliderMusic, txtMusic, v => GameSettings.Music = v);
            HookVolume(sliderEnvironment, txtEnvironment, v => GameSettings.Environment = v);

            toggleShake.onValueChanged.AddListener(v => { if (!_initializing) GameSettings.ScreenShake = v; });
            toggleDamageNumbers.onValueChanged.AddListener(v => { if (!_initializing) GameSettings.ShowDamageNumbers = v; });
            toggleHealthBars.onValueChanged.AddListener(v => { if (!_initializing) GameSettings.ShowHealthBars = v; });
            toggleAutoPause.onValueChanged.AddListener(v => { if (!_initializing) GameSettings.AutoPauseOnFocusLoss = v; });
            toggleFps.onValueChanged.AddListener(v => { if (!_initializing) GameSettings.ShowFps = v; });

            btnResetVideo.onClick.AddListener(() => { GameSettings.ResetVideo(); RefreshAll(); });
            btnResetAudio.onClick.AddListener(() => { GameSettings.ResetAudio(); RefreshAll(); });
            btnResetGameplay.onClick.AddListener(() => { GameSettings.ResetGameplay(); RefreshAll(); });
            btnResetControls.onClick.AddListener(() => { KeyBindings.ResetAll(); RefreshBindingTexts(); });

            btnBack.onClick.AddListener(() =>
            {
                if (pauseMenu != null) pauseMenu.ShowMain();
                BackRequested?.Invoke();
            });

            BuildBindingRows();
        }

        private void OnEnable()
        {
            _rebinding = null;
            RefreshAll();
            ShowTab(_tab);
        }

        private void OnDisable()
        {
            _rebinding = null;
            PlayerPrefs.Save();
        }

        private void HookVolume(Slider slider, Text label, System.Action<float> setter)
        {
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.onValueChanged.AddListener(v =>
            {
                label.text = v.ToString("0.00");
                if (_initializing) return;
                setter(v);
                GameSettings.ApplyAudio();
            });
        }

        // ── Tabs ──────────────────────────────────────────────────

        public void ShowTab(int index)
        {
            _tab = Mathf.Clamp(index, 0, tabPanels.Length - 1);
            _rebinding = null;
            RefreshBindingTexts();

            for (int i = 0; i < tabPanels.Length; i++)
            {
                tabPanels[i].SetActive(i == _tab);

                var txt = tabButtons[i].GetComponentInChildren<Text>();
                if (txt != null) txt.color = i == _tab ? tabActiveColor : tabInactiveColor;
            }
        }

        /// <summary>Q/E bzw. LB/RB zum Tab-Wechsel – gängig in Spielen.</summary>
        private void Update()
        {
            if (IsRebinding)
            {
                ListenForKey();
                return;
            }

            if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.JoystickButton5))
                ShowTab((_tab + 1) % tabPanels.Length);
            if (Input.GetKeyDown(KeyCode.Q) || Input.GetKeyDown(KeyCode.JoystickButton4))
                ShowTab((_tab - 1 + tabPanels.Length) % tabPanels.Length);
        }

        // ── Refresh ───────────────────────────────────────────────

        private void RefreshAll()
        {
            _initializing = true;

            // Video
            UpdateResolutionText();
            toggleFullscreen.isOn = GameSettings.Fullscreen;
            toggleVSync.isOn = GameSettings.VSync;
            togglePost.isOn = GameSettings.PostProcessing;
            txtAa.text = GameSettings.AntiAliasing == 0 ? "Off" : GameSettings.AntiAliasing + "x";
            sliderRenderScale.value = GameSettings.RenderScale;
            txtRenderScale.text = GameSettings.RenderScale.ToString("0.00");

            // Audio
            sliderMaster.value = GameSettings.Master;           txtMaster.text = GameSettings.Master.ToString("0.00");
            sliderGameplay.value = GameSettings.Gameplay;       txtGameplay.text = GameSettings.Gameplay.ToString("0.00");
            sliderMusic.value = GameSettings.Music;             txtMusic.text = GameSettings.Music.ToString("0.00");
            sliderEnvironment.value = GameSettings.Environment; txtEnvironment.text = GameSettings.Environment.ToString("0.00");

            // Gameplay
            toggleShake.isOn = GameSettings.ScreenShake;
            toggleDamageNumbers.isOn = GameSettings.ShowDamageNumbers;
            toggleHealthBars.isOn = GameSettings.ShowHealthBars;
            toggleAutoPause.isOn = GameSettings.AutoPauseOnFocusLoss;
            toggleFps.isOn = GameSettings.ShowFps;

            RefreshBindingTexts();

            _initializing = false;
        }

        // ── Video-Helfer ──────────────────────────────────────────

        private void ChangeResolution(int dir)
        {
            var list = GameSettings.Resolutions;
            if (list.Count == 0) return;

            GameSettings.ResolutionIndex = Mathf.Clamp(GameSettings.ResolutionIndex + dir, 0, list.Count - 1);
            GameSettings.ApplyVideo();
            UpdateResolutionText();
        }

        private void UpdateResolutionText()
        {
            var list = GameSettings.Resolutions;
            if (list.Count == 0) { txtResolution.text = "-"; return; }

            var r = list[Mathf.Clamp(GameSettings.ResolutionIndex, 0, list.Count - 1)];
            txtResolution.text = $"{r.width} x {r.height} @ {Mathf.RoundToInt((float)r.refreshRateRatio.value)}Hz";

            btnResPrev.interactable = GameSettings.ResolutionIndex > 0;
            btnResNext.interactable = GameSettings.ResolutionIndex < list.Count - 1;
        }

        private void ChangeAa(int dir)
        {
            int idx = System.Array.IndexOf(AaOptions, GameSettings.AntiAliasing);
            idx = Mathf.Clamp(idx + dir, 0, AaOptions.Length - 1);
            GameSettings.AntiAliasing = AaOptions[idx];
            GameSettings.ApplyVideo();
            txtAa.text = AaOptions[idx] == 0 ? "Off" : AaOptions[idx] + "x";
        }

        // ── Controls / Rebinding ──────────────────────────────────

        private void BuildBindingRows()
        {
            foreach (Transform c in bindingContainer) Destroy(c.gameObject);
            _bindingTexts.Clear();

            foreach (GameAction action in KeyBindings.AllActions)
            {
                var row = Instantiate(bindingRowPrefab, bindingContainer);

                var label = row.GetComponentInChildren<Text>();
                if (label != null) label.text = Nicify(action.ToString());

                var keyButton = row.transform.Find("Key")?.GetComponent<Button>();
                var keyText = keyButton != null ? keyButton.GetComponentInChildren<Text>() : null;
                if (keyButton == null || keyText == null) continue;

                _bindingTexts[action] = keyText;

                var a = action;
                keyButton.onClick.AddListener(() =>
                {
                    _rebinding = a;
                    RefreshBindingTexts();
                });
            }

            RefreshBindingTexts();
        }

        private void RefreshBindingTexts()
        {
            foreach (var kv in _bindingTexts)
            {
                kv.Value.text = _rebinding == kv.Key
                    ? "press a key..."
                    : KeyBindings.Get(kv.Key).ToString();
            }
        }

        private void ListenForKey()
        {
            if (!Input.anyKeyDown) return;

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                _rebinding = null; // Abbrechen
                RefreshBindingTexts();
                return;
            }

            foreach (KeyCode key in System.Enum.GetValues(typeof(KeyCode)))
            {
                if (key == KeyCode.None || key == KeyCode.Mouse0) continue; // Mouse0 = der Klick auf den Button
                if (!Input.GetKeyDown(key)) continue;

                KeyBindings.Set(_rebinding.Value, key);
                _rebinding = null;
                RefreshBindingTexts();
                return;
            }
        }

        private static string Nicify(string s)
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < s.Length; i++)
            {
                if (i > 0 && char.IsUpper(s[i])) sb.Append(' ');
                sb.Append(s[i]);
            }
            return sb.ToString();
        }
    }
}