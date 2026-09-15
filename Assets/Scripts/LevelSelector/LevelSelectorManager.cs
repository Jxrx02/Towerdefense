using LevelSelector.Definitions;
using UnityEngine;

namespace LevelSelector
{
    public class LevelSelectorManager : MonoBehaviour
    {
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


        public void StartLevel()
        {
            var loadout = LevelLoadoutManager.Instance.CurrentLoadout;

            if (loadout == null)
                return;

            if (loadout.weapon == null)
            {
                Debug.LogWarning("Keine Waffe ausgewählt.");
                return;
            }

            if (loadout.perks.Count != 5)
            {
                Debug.LogWarning("Es müssen 5 Perks ausgewählt werden.");
                return;
            }

            if (loadout.mutators.Count != 3)
            {
                Debug.LogWarning("Es müssen 3 Mutatoren ausgewählt werden.");
                return;
            }

            LevelLoadoutManager.Instance.ApplyLoadout();

            // Dein bestehender Szenenwechsel kommt hier hin.
        }
    }
}