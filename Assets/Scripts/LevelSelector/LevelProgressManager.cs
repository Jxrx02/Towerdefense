using System;
using System.Collections.Generic;
using System.IO;
using LevelSelector.Definitions;
using UnityEngine;

namespace LevelSelector
{
    [Serializable]
    public class LevelProgress
    {
        public int levelIndex;
        public int highscore;
        public bool completed;
        public List<string> completedQuests = new();
    }

    [Serializable]
    public class LevelProgressSaveData
    {
        public int unlockedLevel = 1;

        // Spieler-XP für das Level-System (Perk-Freischaltungen). Alte Savegames laden mit 0.
        public int playerXp;

        public List<LevelProgress> levels = new();
    }

    public class LevelProgressManager : MonoBehaviour
    {
        public static LevelProgressManager Instance { get; private set; }

        private LevelProgressSaveData data;

        private string savePath;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            savePath = Path.Combine(
                Application.persistentDataPath,
                "levelProgress.json");

            Load();
        }

        public static int GetUnlockedLevel()
        {
            if (Instance == null)
                return 1;

            return Instance.data.unlockedLevel;
        }

        // ── Spieler-XP ────────────────────────────────────────────

        public static int GetPlayerXp()
        {
            return Instance != null ? Instance.data.playerXp : 0;
        }

        public static void AddPlayerXp(int amount)
        {
            if (Instance == null)
            {
                Debug.LogWarning("LevelProgressManager.AddPlayerXp: Kein LevelProgressManager vorhanden, XP wurden nicht gespeichert.");
                return;
            }

            Instance.data.playerXp = Mathf.Max(0, Instance.data.playerXp + amount);
            Instance.Save();
        }

        /// <summary>Zum Testen: Spieler-XP zurücksetzen.</summary>
        public static void ResetPlayerXp()
        {
            if (Instance == null) return;

            Instance.data.playerXp = 0;
            Instance.Save();
        }

        // ── Level ─────────────────────────────────────────────────

        public int GetHighscore(int levelIndex)
        {
            var level = GetLevel(levelIndex);

            return level != null
                ? level.highscore
                : 0;
        }

        public void CompleteLevel(
            LevelResult result,
            LevelDefinition definition)
        {
            if (!result.victory)
                return;

            var progress = GetOrCreateLevel(result.levelIndex);

            progress.completed = true;

            if (result.score > progress.highscore)
            {
                progress.highscore = result.score;
            }

            if (result.levelIndex + 1 > data.unlockedLevel)
            {
                data.unlockedLevel = result.levelIndex + 1;
            }

            foreach (var quest in definition.quests)
            {
                if (quest.IsCompleted(
                        LevelLoadoutManager.Instance.CurrentLoadout,
                        result))
                {
                    if (!progress.completedQuests
                        .Contains(quest.name))
                    {
                        progress.completedQuests.Add(quest.name);
                    }
                }
            }

            Save();
        }

        public bool IsQuestCompleted(
            int levelIndex,
            LevelQuestDefinition quest)
        {
            var level = GetLevel(levelIndex);

            if (level == null)
                return false;

            return level.completedQuests.Contains(quest.name);
        }

        private LevelProgress GetOrCreateLevel(int levelIndex)
        {
            var level = GetLevel(levelIndex);

            if (level == null)
            {
                level = new LevelProgress
                {
                    levelIndex = levelIndex
                };

                data.levels.Add(level);
            }

            return level;
        }

        private LevelProgress GetLevel(int levelIndex)
        {
            return data.levels.Find(
                x => x.levelIndex == levelIndex);
        }

        private void Save()
        {
            string json =
                JsonUtility.ToJson(data, true);

            File.WriteAllText(savePath, json);
        }

        private void Load()
        {
            if (!File.Exists(savePath))
            {
                data = new LevelProgressSaveData();
                Save();
                return;
            }

            try
            {
                string json = File.ReadAllText(savePath);

                data =
                    JsonUtility.FromJson<LevelProgressSaveData>(json);

                if (data == null)
                    data = new LevelProgressSaveData();
            }
            catch
            {
                data = new LevelProgressSaveData();
            }
        }
    }
}