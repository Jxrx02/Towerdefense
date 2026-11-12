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

        /// <summary>So viele Perks müssen gewählt werden: max. 5, aber nie mehr, als freigeschaltet sind.</summary>
        public static int GetRequiredPerks(LevelDefinition level)
        {
            if (level == null || level.availablePerks == null) return 0;

            int unlocked = 0;
            foreach (var perk in level.availablePerks)
                if (perk != null && PlayerProgress.IsPerkUnlocked(perk))
                    unlocked++;

            return Mathf.Min(unlocked,MaxPerks);
        }

        /// <summary>So viele Mutatoren müssen gewählt werden: max. 3, aber nie mehr, als das Level anbietet.</summary>
        public static int GetRequiredMutators(LevelDefinition level)
        {
            if (level == null || level.availableMutators == null) return 0;

            int available = 0;
            foreach (var mutator in level.availableMutators)
                if (mutator != null)
                    available++;

            return Mathf.Min(available,MaxMutators);
        }

        /// <summary>Null, wenn das Loadout startklar ist. Sonst ein kurzer Hinweis, was noch fehlt.</summary>
        public static string GetStartBlocker(LevelLoadout loadout)
        {
            if (loadout == null)
                return "No level selected.";

            if (loadout.weapon == null)
                return "Select a weapon.";

            int perksMissing = GetRequiredPerks(loadout.levelDefinition) - loadout.perks.Count;
            if (perksMissing > 0)
                return perksMissing == 1 ? "Select 1 more perk." : $"Select {perksMissing} more perks.";

            int mutatorsMissing = GetRequiredMutators(loadout.levelDefinition) - loadout.mutators.Count;
            if (mutatorsMissing > 0)
                return mutatorsMissing == 1 ? "Select 1 more mutator." : $"Select {mutatorsMissing} more mutators.";

            return null;
        }

        // ── Start ─────────────────────────────────────────────────

        /// <summary>Für Inspector-Events (UnityEvents zeigen nur void-Methoden).</summary>
        public void StartLevel()
        {
            TryStartLevel();
        }

        /// <summary>Prüft das Loadout und wendet es an. false = Loadout unvollständig.</summary>
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