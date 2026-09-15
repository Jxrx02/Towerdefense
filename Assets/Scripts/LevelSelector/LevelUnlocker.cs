using LevelSelector;
using TowerDefense.Research;

namespace TowerDefense
{
    using UnityEngine;
    using UnityEngine.SceneManagement;

    public class LevelUnlocker : MonoBehaviour
    {
        // Aufruf nach dem Abschluss eines Levels
        public void CompleteLevel()
        {
            int currentLevel = SceneManager.GetActiveScene().buildIndex;
            int nextLevel = currentLevel + 1;

            // Aktuellen Fortschritt prüfen
            int unlockedLevel = PlayerPrefs.GetInt("unlockedLevel", 2);

            // Nur erhöhen, wenn nextLevel > bisheriger Fortschritt
            if (nextLevel > unlockedLevel)
            {
                PlayerPrefs.SetInt("unlockedLevel", nextLevel);
                PlayerPrefs.Save();
                Debug.Log("Level " + nextLevel + " freigeschaltet!");
            }

            ResearchManager.Instance.researchPoints++;
            ResearchManager.Instance.SaveResearchData();
        }
        
    }

}