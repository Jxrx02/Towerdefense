using LevelSelector.Definitions;
using UnityEngine;

namespace LevelSelector
{
    public class LevelSelectorManager : MonoBehaviour
    {
        public const int MaxPerks = 5;
        public const int MaxMutators = 3;

        public static LevelSelectorManager Instance { get; private set; }

        public LevelDefinition CurrentLevel { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        public void OpenLevel(LevelDefinition level)
        {
            if (level == null)
            {
                Debug.LogError("OpenLevel: LevelDefinition ist null.");
                return;
            }

            CurrentLevel = level;

            // Neue Auswahl für dieses Level erstellen
            if (LevelLoadoutManager.Instance == null)
            {
                Debug.LogError("LevelLoadoutManager wurde nicht gefunden.");
                return;
            }

            LevelLoadoutManager.Instance.CreateLoadout(level);

            // Loadout-UI anzeigen
            if (LevelLoadoutUI.Instance == null)
            {
                Debug.LogError("LevelLoadoutUI wurde nicht gefunden.");
                return;
            }

            LevelLoadoutUI.Instance.Show(level);
        }

        // ── Regeln für ein gültiges Loadout ───────────────────────
        //
        // Perks (max. MaxPerks) und Mutatoren (max. MaxMutators) sind optional: 0 bis Max, mit 0 lässt sich starten.
        // Die Obergrenzen erzwingt LevelLoadoutUI beim Auswählen. Pflicht ist nur die Waffe.

        /// <summary>Null, wenn das Loadout startklar ist. Sonst ein kurzer Hinweis, was noch fehlt.</summary>
        public static string GetStartBlocker(LevelLoadout loadout)
        {
            if (loadout == null)
                return "No level selected.";

            if (loadout.weapon == null)
                return "Select a weapon.";

            return null;
        }

        // ── Start ─────────────────────────────────────────────────

        /// <summary>Für Inspector-Events (UnityEvents zeigen nur void-Methoden).</summary>
        public void StartLevel()
        {
            TryStartLevel();
        }

        /// <summary>Prüft das Loadout (Waffe gewählt) und wendet es an. false = Loadout unvollständig.</summary>
        public bool TryStartLevel()
        {
            if (LevelLoadoutManager.Instance == null)
                return false;

            var loadout = LevelLoadoutManager.Instance.CurrentLoadout;
            string blocker = GetStartBlocker(loadout);

            if (blocker != null)
            {
                Debug.LogWarning(blocker);
                return false;
            }

            LevelLoadoutManager.Instance.ApplyLoadout();

            // Der Szenenwechsel hängt am Start-Button des neuen Loadout-Screens
            // (LevelLoadoutUI > On Start Confirmed).
            return true;
        }
    }
}