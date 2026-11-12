using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System;
using ScriptableObjects;
using UnityEngine.UI;

namespace TowerDefense.Research
{
    // ── Datenmodell (Speicherformat bleibt kompatibel, nur additive Felder) ──

    [System.Serializable]
    public class TowerResearchInfo
    {
        public string towerName;
        public int researchCost;
        public UpgradePath[] upgradePaths = new UpgradePath[3];
        public Sprite[] sprites;

        // Neu: Infos für die Turm-Detailansicht im Skilltree
        public string description;
        public float baseDamage;
        public float baseRange;
        public float baseAttackSpeed;
        public float baseHealth;
        public float baseGoldPerSecond;
    }

    [System.Serializable]
    public class ResearchedTower
    {
        public string towerName;
        public List<ResearchedPath> researchedPaths = new List<ResearchedPath>();
    }

    [System.Serializable]
    public class ResearchedPath
    {
        public string pathName;
        public List<string> unlockedUpgrades = new List<string>();
    }

    [System.Serializable]
    public class ResearchSaveData
    {
        public List<ResearchedTower> researchedTowers;
        public int researchPoints;

        // Neu: freigeschaltete Türme. Alte Savegames laden mit null (wird in LoadResearchData behandelt).
        public List<string> unlockedTowers;

        public ResearchSaveData(List<ResearchedTower> towers, int researchPoints)
        {
            researchedTowers = towers;
            this.researchPoints = researchPoints;
        }
    }

    public enum ResearchStatus
    {
        Researched,
        Available,
        TowerLocked,     // der Turm selbst ist noch nicht freigeschaltet
        PreviousLocked   // das vorherige Upgrade im Pfad fehlt noch
    }

    public struct ResearchReward
    {
        public int basePoints;
        public int questPoints;
        public int scorePoints;

        public int Total => basePoints + questPoints + scorePoints;
    }

    public class ResearchManager : MonoBehaviour
    {
        public static ResearchManager Instance { get; private set; }

        /// <summary>Wird bei jeder Änderung (Punkte, Türme, Upgrades) ausgelöst. Der Skilltree aktualisiert sich darüber.</summary>
        public event Action Changed;

        [Header("Türme (Quelle für den Skilltree)")]
        [Tooltip("Tower-Prefabs mit Tower- und SpriteAnim-Komponente. Daraus entsteht availableTowers.")]
        public List<GameObject> towerPrefabs = new List<GameObject>();
        public List<TowerResearchInfo> availableTowers = new List<TowerResearchInfo>();
        public List<ResearchedTower> unlockedResearch = new List<ResearchedTower>();

        [Tooltip("Die ersten N Türme der Liste sind von Anfang an freigeschaltet (kostenlos).")]
        [SerializeField] private int startingUnlockedTowers = 1;
        [Tooltip("Forschungskosten eines Turms = towerInitPrice x Faktor")]
        [SerializeField] private float towerUnlockCostFactor = 1f;

        [Header("Punkte")]
        public int researchPoints = 100;
        [Tooltip("Startguthaben bei einem neuen Spielstand")]
        [SerializeField] private int startingResearchPoints = 0;

        [Header("Belohnung nach einem Level")]
        [SerializeField] private int pointsFirstClear = 6;
        [SerializeField] private int pointsRepeatClear = 2;
        [SerializeField] private int pointsPerNewQuest = 3;
        [Tooltip("1 Bonuspunkt je X Score (0 = aus)")]
        [SerializeField] private int scorePerBonusPoint = 3000;
        [SerializeField] private int maxScoreBonusPoints = 5;

        [Header("Alte UI (optional, wird vom neuen Skilltree nicht mehr gebraucht)")]
        public ConfirmPopupManager confirmPopupManager;
        public Button resetButton;
        public Text txtResearchPunkte;

        private readonly List<string> unlockedTowerNames = new List<string>();
        private string savePath;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);

                savePath = Path.Combine(Application.persistentDataPath, "researchData.json");

                LoadTowersFromPrefabs();
                LoadResearchData();

                if (resetButton != null)
                    resetButton.onClick.AddListener(OnResetButtonClicked);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ── Türme aus den Prefabs lesen ───────────────────────────

        private void LoadTowersFromPrefabs()
        {
            availableTowers.Clear();

            foreach (var prefab in towerPrefabs)
            {
                if (prefab == null) continue;

                var towerComponent = prefab.GetComponent<Tower>();

                if (towerComponent == null)
                {
                    Debug.LogWarning($"Kein Tower-Script auf Prefab {prefab.name} gefunden!");
                    continue;
                }

                var anim = prefab.GetComponent<SpriteAnim>();

                availableTowers.Add(new TowerResearchInfo
                {
                    towerName = towerComponent.towerName,
                    researchCost = towerComponent.towerInitPrice,
                    upgradePaths = towerComponent.upgradePaths,
                    sprites = anim != null ? anim.idle_sprites : null,
                    description = towerComponent.towerDesc,
                    baseDamage = towerComponent.damage,
                    baseRange = towerComponent.range,
                    baseAttackSpeed = 1f / Mathf.Max(0.1f, towerComponent.timeInBetweenShots),
                    baseHealth = towerComponent.statHealthPoints,
                    baseGoldPerSecond = towerComponent.statCoinsEarnedPerSecond
                });
            }

            SetPointsText();
        }

        public TowerResearchInfo FindTower(string towerName)
        {
            return availableTowers.FirstOrDefault(t => t.towerName == towerName);
        }

        // ── Speichern / Laden ─────────────────────────────────────

        public void SaveResearchData()
        {
            try
            {
                var data = new ResearchSaveData(unlockedResearch, researchPoints)
                {
                    unlockedTowers = unlockedTowerNames
                };

                File.WriteAllText(savePath, JsonUtility.ToJson(data, true));
            }
            catch (Exception ex)
            {
                Debug.LogError($"Fehler beim Speichern: {ex.Message}");
            }
        }

        public void LoadResearchData()
        {
            unlockedTowerNames.Clear();

            if (File.Exists(savePath))
            {
                try
                {
                    string json = File.ReadAllText(savePath);
                    ResearchSaveData data = JsonUtility.FromJson<ResearchSaveData>(json);

                    unlockedResearch = data.researchedTowers ?? new List<ResearchedTower>();
                    researchPoints = data.researchPoints;

                    if (data.unlockedTowers != null)
                        unlockedTowerNames.AddRange(data.unlockedTowers);

                    // Alte Savegames: Türme mit erforschten Upgrades gelten als freigeschaltet
                    foreach (var researched in unlockedResearch)
                        if (!unlockedTowerNames.Contains(researched.towerName))
                            unlockedTowerNames.Add(researched.towerName);

                    NotifyChanged();
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"Fehler beim Laden: {ex.Message}. Neue Daten erstellt.");
                    CreateNewSave();
                }
            }
            else
            {
                CreateNewSave();
            }
        }

        private void CreateNewSave()
        {
            unlockedResearch = new List<ResearchedTower>();
            unlockedTowerNames.Clear();
            researchPoints = startingResearchPoints;
            SaveResearchData();
            NotifyChanged();
        }

        // ── Türme ─────────────────────────────────────────────────

        public bool IsStarterTower(string towerName)
        {
            int index = availableTowers.FindIndex(t => t.towerName == towerName);
            return index >= 0 && index < startingUnlockedTowers;
        }

        public bool IsTowerUnlocked(string towerName)
        {
            return IsStarterTower(towerName) || unlockedTowerNames.Contains(towerName);
        }

        public int GetTowerUnlockCost(TowerResearchInfo tower)
        {
            if (tower == null || IsStarterTower(tower.towerName)) return 0;
            return Mathf.Max(0, Mathf.RoundToInt(tower.researchCost * towerUnlockCostFactor));
        }

        public bool TryUnlockTower(string towerName)
        {
            TowerResearchInfo tower = FindTower(towerName);

            if (tower == null || IsTowerUnlocked(towerName))
                return false;

            int cost = GetTowerUnlockCost(tower);

            if (!CanAffordResearch(cost))
                return false;

            researchPoints -= cost;
            unlockedTowerNames.Add(towerName);

            SaveResearchData();
            NotifyChanged();
            return true;
        }

        // ── Upgrades ──────────────────────────────────────────────

        public bool IsUpgradeUnlocked(string towerName, string pathName, string upgradeName)
        {
            var tower = unlockedResearch.FirstOrDefault(t => t.towerName == towerName);
            if (tower == null) return false;

            var path = tower.researchedPaths.FirstOrDefault(p => p.pathName == pathName);
            if (path == null) return false;

            return path.unlockedUpgrades.Contains(upgradeName);
        }

        /// <summary>Status eines Upgrades im Skilltree. Upgrades eines Pfads werden der Reihe nach erforscht.</summary>
        public ResearchStatus GetUpgradeStatus(TowerResearchInfo tower, int pathIndex, int levelIndex)
        {
            UpgradePath path = tower.upgradePaths[pathIndex];
            TowerUpgradeLevel level = path.levels[levelIndex];

            if (IsUpgradeUnlocked(tower.towerName, path.towerName, level.upgradeName))
                return ResearchStatus.Researched;

            if (!IsTowerUnlocked(tower.towerName))
                return ResearchStatus.TowerLocked;

            if (levelIndex > 0 &&
                !IsUpgradeUnlocked(tower.towerName, path.towerName, path.levels[levelIndex - 1].upgradeName))
                return ResearchStatus.PreviousLocked;

            return ResearchStatus.Available;
        }

        public bool TryResearchUpgrade(string towerName, int pathIndex, int levelIndex)
        {
            TowerResearchInfo tower = FindTower(towerName);

            if (tower == null || tower.upgradePaths == null ||
                pathIndex < 0 || pathIndex >= tower.upgradePaths.Length ||
                tower.upgradePaths[pathIndex] == null ||
                levelIndex < 0 || levelIndex >= tower.upgradePaths[pathIndex].levels.Length)
                return false;

            if (GetUpgradeStatus(tower, pathIndex, levelIndex) != ResearchStatus.Available)
                return false;

            UpgradePath path = tower.upgradePaths[pathIndex];
            TowerUpgradeLevel level = path.levels[levelIndex];

            if (!CanAffordResearch(level.researchCost))
                return false;

            researchPoints -= level.researchCost;
            AddUpgrade(towerName, path.towerName, level.upgradeName);

            SaveResearchData();
            NotifyChanged();
            return true;
        }

        public int CountResearchedUpgrades(TowerResearchInfo tower)
        {
            int count = 0;
            if (tower == null || tower.upgradePaths == null) return 0;

            foreach (UpgradePath path in tower.upgradePaths)
            {
                if (path == null || path.levels == null) continue;

                foreach (TowerUpgradeLevel level in path.levels)
                    if (IsUpgradeUnlocked(tower.towerName, path.towerName, level.upgradeName))
                        count++;
            }

            return count;
        }

        public int CountTotalUpgrades(TowerResearchInfo tower)
        {
            int count = 0;
            if (tower == null || tower.upgradePaths == null) return 0;

            foreach (UpgradePath path in tower.upgradePaths)
                if (path != null && path.levels != null)
                    count += path.levels.Length;

            return count;
        }

        /// <summary>Alte API: schaltet ein Upgrade ohne Kosten frei (Punkte abziehen macht der Aufrufer).</summary>
        public void UnlockUpgrade(string towerName, string pathName, string upgradeName)
        {
            if (AddUpgrade(towerName, pathName, upgradeName))
            {
                SaveResearchData();
                NotifyChanged();
            }
        }

        private bool AddUpgrade(string towerName, string pathName, string upgradeName)
        {
            var tower = unlockedResearch.FirstOrDefault(t => t.towerName == towerName);
            if (tower == null)
            {
                tower = new ResearchedTower { towerName = towerName };
                unlockedResearch.Add(tower);
            }

            if (!unlockedTowerNames.Contains(towerName))
                unlockedTowerNames.Add(towerName);

            var path = tower.researchedPaths.FirstOrDefault(p => p.pathName == pathName);
            if (path == null)
            {
                path = new ResearchedPath { pathName = pathName };
                tower.researchedPaths.Add(path);
            }

            if (path.unlockedUpgrades.Contains(upgradeName))
                return false;

            path.unlockedUpgrades.Add(upgradeName);
            return true;
        }

        // ── Punkte ────────────────────────────────────────────────

        public bool CanAffordResearch(int cost)
        {
            return researchPoints >= cost;
        }

        public void SpendResearchPoints(int amount)
        {
            researchPoints -= amount;
            SaveResearchData();
            NotifyChanged();
        }

        public void AddResearchPoints(int amount)
        {
            researchPoints += amount;
            SaveResearchData();
            NotifyChanged();
        }

        /// <summary>
        /// Belohnung nach einem gewonnenen Level: Grundwert (Erstabschluss höher als Wiederholung),
        /// neue Quests und ein Bonus nach Score. Schreibt die Punkte gut.
        /// </summary>
        public ResearchReward AwardLevelReward(int score, bool firstClear, int newQuests)
        {
            var reward = new ResearchReward
            {
                basePoints = firstClear ? pointsFirstClear : pointsRepeatClear,
                questPoints = Mathf.Max(0, newQuests) * pointsPerNewQuest,
                scorePoints = scorePerBonusPoint > 0 ? Mathf.Min(maxScoreBonusPoints, Mathf.Max(0, score) / scorePerBonusPoint) : 0
            };

            AddResearchPoints(reward.Total);
            return reward;
        }

        // ── Zurücksetzen ──────────────────────────────────────────

        private void OnResetButtonClicked()
        {
            if (confirmPopupManager != null)
                confirmPopupManager.ShowConfirmPopup("Willst du wirklich deine Forschungspunkte zurücksetzen?", ConfirmResetResearch);
        }

        private void ConfirmResetResearch()
        {
            ResetResearchData();
        }

        /// <summary>Setzt alle erforschten Upgrades zurück und erstattet ihre Kosten. Freigeschaltete Türme bleiben.</summary>
        public void ResetResearchData()
        {
            int refundedResearchPoints = 0;

            foreach (var researchedTower in unlockedResearch)
            {
                var towerInfo = availableTowers.FirstOrDefault(t => t.towerName == researchedTower.towerName);
                if (towerInfo == null) continue;

                foreach (var path in researchedTower.researchedPaths)
                {
                    foreach (var upgradeName in path.unlockedUpgrades)
                    {
                        var upgrade = towerInfo.upgradePaths
                            .Where(p => p != null && p.levels != null)
                            .SelectMany(p => p.levels)
                            .FirstOrDefault(u => u.upgradeName == upgradeName);

                        if (upgrade != null)
                            refundedResearchPoints += upgrade.researchCost;
                    }
                }
            }

            unlockedResearch.Clear();
            AddResearchPoints(refundedResearchPoints);
            Debug.Log($"Alle Forschungen zurückgesetzt. Zurückerstattete Forschungspunkte: {refundedResearchPoints}");
        }

        // ── Hilfen ────────────────────────────────────────────────

        public void SetPointsText()
        {
            if (txtResearchPunkte != null)
                txtResearchPunkte.text = $"Forschungspunkte: {researchPoints}";
        }

        private void NotifyChanged()
        {
            SetPointsText();
            Changed?.Invoke();
        }

        [ContextMenu("Test: +50 Forschungspunkte")]
        private void DebugAddPoints()
        {
            AddResearchPoints(50);
        }
    }
}