using UnityEngine;

namespace LevelSelector
{
    /// <summary>
    /// Zustand, der Szenenwechsel überlebt (static). Dadurch zeigt die Karte das Hauptmenü nur beim Spielstart
    /// und platziert den Helden nach einem Level wieder am zuletzt gespielten Level-Knoten.
    /// </summary>
    public static class OverworldSession
    {
        private const string LastLevelKey = "overworld_last_level";

        /// <summary>true, sobald das Hauptmenü in dieser Spielsitzung einmal gezeigt wurde.</summary>
        public static bool MenuShown;

        public static int LastLevelIndex
        {
            get => PlayerPrefs.GetInt(LastLevelKey, 0);
            set => PlayerPrefs.SetInt(LastLevelKey, value);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            MenuShown = false;
        }
    }
}