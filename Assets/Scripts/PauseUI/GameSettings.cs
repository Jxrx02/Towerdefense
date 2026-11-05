using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace TowerDefense
{
    // ───────────────────────── Key Bindings ─────────────────────────

    public enum GameAction
    {
        Pause,
        StartWave,
        FastForward,
        CameraUp,
        CameraDown,
        CameraLeft,
        CameraRight,
        HeroActive
    }

    public static class KeyBindings
    {
        private static readonly Dictionary<GameAction, KeyCode> Defaults = new()
        {
            { GameAction.Pause,       KeyCode.Escape },
            { GameAction.StartWave,   KeyCode.Space },
            { GameAction.FastForward, KeyCode.F },
            { GameAction.CameraUp,    KeyCode.W },
            { GameAction.CameraDown,  KeyCode.S },
            { GameAction.CameraLeft,  KeyCode.A },
            { GameAction.CameraRight, KeyCode.D },
            { GameAction.HeroActive,  KeyCode.Q },
        };

        private static readonly Dictionary<GameAction, KeyCode> Current = new();
        private static bool _loaded;

        public static IEnumerable<GameAction> AllActions => Defaults.Keys;

        private static void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true;

            foreach (var kv in Defaults)
                Current[kv.Key] = (KeyCode)PlayerPrefs.GetInt("key_" + kv.Key, (int)kv.Value);
        }

        public static KeyCode Get(GameAction a) { EnsureLoaded(); return Current[a]; }

        public static bool Down(GameAction a) => Input.GetKeyDown(Get(a));
        public static bool Held(GameAction a) => Input.GetKey(Get(a));

        /// <summary>Setzt eine Taste. Ist sie schon belegt, werden beide Aktionen getauscht.</summary>
        public static void Set(GameAction a, KeyCode key)
        {
            EnsureLoaded();

            foreach (var other in new List<GameAction>(Current.Keys))
            {
                if (other != a && Current[other] == key)
                {
                    Current[other] = Current[a];
                    PlayerPrefs.SetInt("key_" + other, (int)Current[other]);
                }
            }

            Current[a] = key;
            PlayerPrefs.SetInt("key_" + a, (int)key);
            PlayerPrefs.Save();
        }

        public static void ResetAll()
        {
            EnsureLoaded();
            foreach (var kv in Defaults)
            {
                Current[kv.Key] = kv.Value;
                PlayerPrefs.DeleteKey("key_" + kv.Key);
            }
            PlayerPrefs.Save();
        }
    }

    // ───────────────────────── Game Settings ────────────────────────

    public static class GameSettings
    {
        // Optional: AudioMixer mit exposed Parametern
        // "MasterVol", "GameplayVol", "MusicVol", "EnvironmentVol"
        public static AudioMixer Mixer;

        // ── Defaults ──
        public const float DefaultVolume = 1f;
        public const bool  DefaultFullscreen = true;
        public const bool  DefaultVSync = true;
        public const bool  DefaultPostProcessing = true;
        public const int   DefaultAA = 4;
        public const float DefaultRenderScale = 1f;

        // ── Video ──
        public static int ResolutionIndex { get => PlayerPrefs.GetInt("res", -1); set => PlayerPrefs.SetInt("res", value); }
        public static bool Fullscreen     { get => Bool("fullscreen", DefaultFullscreen); set => SetBool("fullscreen", value); }
        public static bool VSync          { get => Bool("vsync", DefaultVSync);           set => SetBool("vsync", value); }
        public static bool PostProcessing { get => Bool("post", DefaultPostProcessing);   set => SetBool("post", value); }
        public static int  AntiAliasing   { get => PlayerPrefs.GetInt("aa", DefaultAA);   set => PlayerPrefs.SetInt("aa", value); }
        public static float RenderScale   { get => PlayerPrefs.GetFloat("rscale", DefaultRenderScale); set => PlayerPrefs.SetFloat("rscale", value); }

        // ── Audio ──
        public static float Master      { get => PlayerPrefs.GetFloat("vol_master", DefaultVolume);      set => PlayerPrefs.SetFloat("vol_master", value); }
        public static float Gameplay    { get => PlayerPrefs.GetFloat("vol_gameplay", DefaultVolume);    set => PlayerPrefs.SetFloat("vol_gameplay", value); }
        public static float Music       { get => PlayerPrefs.GetFloat("vol_music", DefaultVolume);       set => PlayerPrefs.SetFloat("vol_music", value); }
        public static float Environment { get => PlayerPrefs.GetFloat("vol_environment", DefaultVolume); set => PlayerPrefs.SetFloat("vol_environment", value); }

        // ── Gameplay ──
        public static bool ScreenShake       { get => Bool("shake", true);      set => SetBool("shake", value); }
        public static bool ShowDamageNumbers { get => Bool("dmgnum", true);     set => SetBool("dmgnum", value); }
        public static bool ShowHealthBars    { get => Bool("hpbars", true);     set => SetBool("hpbars", value); }
        public static bool AutoPauseOnFocusLoss { get => Bool("autopause", true); set => SetBool("autopause", value); }
        public static bool ShowFps           { get => Bool("fps", false);       set => SetBool("fps", value); }

        // Auflösungen (einmalig, ohne Duplikate bei verschiedenen Hz)
        public static List<Resolution> Resolutions { get; private set; } = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Boot()
        {
            BuildResolutionList();
            ApplyAll();
        }

        private static void BuildResolutionList()
        {
            Resolutions.Clear();
            foreach (var r in Screen.resolutions)
            {
                // Letzte (höchste Hz) pro Größe gewinnt
                int idx = Resolutions.FindIndex(x => x.width == r.width && x.height == r.height);
                if (idx >= 0) Resolutions[idx] = r;
                else Resolutions.Add(r);
            }

            if (ResolutionIndex < 0 || ResolutionIndex >= Resolutions.Count)
                ResolutionIndex = Resolutions.FindLastIndex(r =>
                    r.width == Screen.currentResolution.width && r.height == Screen.currentResolution.height);
            if (ResolutionIndex < 0) ResolutionIndex = Resolutions.Count - 1;
        }

        public static void ApplyAll()
        {
            ApplyVideo();
            ApplyAudio();
            PlayerPrefs.Save();
        }

        public static void ApplyVideo()
        {
            if (Resolutions.Count > 0)
            {
                var r = Resolutions[Mathf.Clamp(ResolutionIndex, 0, Resolutions.Count - 1)];
                Screen.SetResolution(r.width, r.height,
                    Fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
            }

            QualitySettings.vSyncCount = VSync ? 1 : 0;

            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
            {
                urp.renderScale = Mathf.Clamp(RenderScale, 0.5f, 2f);
                urp.msaaSampleCount = AntiAliasing;
            }

            ApplyPostProcessing();
        }

        public static void ApplyPostProcessing()
        {
            var cam = Camera.main;
            if (cam == null) return;

            var data = cam.GetUniversalAdditionalCameraData();
            if (data != null) data.renderPostProcessing = PostProcessing;
        }

        public static void ApplyAudio()
        {
            if (Mixer != null)
            {
                SetMixer("MasterVol", Master);
                SetMixer("GameplayVol", Gameplay);
                SetMixer("MusicVol", Music);
                SetMixer("EnvironmentVol", Environment);
            }
            else
            {
                AudioListener.volume = Master; // Fallback ohne Mixer
            }
        }

        private static void SetMixer(string param, float linear)
        {
            float db = linear <= 0.0001f ? -80f : Mathf.Log10(linear) * 20f;
            Mixer.SetFloat(param, db);
        }

        public static void ResetVideo()
        {
            foreach (var k in new[] { "res", "fullscreen", "vsync", "post", "aa", "rscale" })
                PlayerPrefs.DeleteKey(k);
            BuildResolutionList();
            ApplyVideo();
            PlayerPrefs.Save();
        }

        public static void ResetAudio()
        {
            foreach (var k in new[] { "vol_master", "vol_gameplay", "vol_music", "vol_environment" })
                PlayerPrefs.DeleteKey(k);
            ApplyAudio();
            PlayerPrefs.Save();
        }

        public static void ResetGameplay()
        {
            foreach (var k in new[] { "shake", "dmgnum", "hpbars", "autopause", "fps" })
                PlayerPrefs.DeleteKey(k);
            PlayerPrefs.Save();
        }

        private static bool Bool(string key, bool def) => PlayerPrefs.GetInt(key, def ? 1 : 0) == 1;
        private static void SetBool(string key, bool v) => PlayerPrefs.SetInt(key, v ? 1 : 0);
    }
}