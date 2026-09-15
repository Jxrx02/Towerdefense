using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System;
using ScriptableObjects;
using UnityEngine.UI;

namespace TowerDefense.Research
{
    [System.Serializable]
    public class TowerResearchInfo
    {
        public string towerName;
        public int researchCost;
        public UpgradePath[] upgradePaths = new UpgradePath[3];
        public Sprite[] sprites;
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

        public ResearchSaveData(List<ResearchedTower> towers, int researchPoints)
        {
            researchedTowers = towers;
            this.researchPoints = researchPoints;

        }
    }

        public class ResearchManager : MonoBehaviour
    {
        public static ResearchManager Instance { get; private set; }

        [Header("Tower Prefabs (mit TowerResearchInfo-Komponente)")]
        public List<GameObject> towerPrefabs = new List<GameObject>();

        public List<TowerResearchInfo> availableTowers = new List<TowerResearchInfo>();
        public List<ResearchedTower> unlockedResearch = new List<ResearchedTower>();
       
        public ConfirmPopupManager confirmPopupManager; // Dein neues Popup-Manager-Script
        public Button resetButton;

        public int researchPoints = 100;
        public Text txtResearchPunkte;

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
                
                resetButton.onClick.AddListener(OnResetButtonClicked); // Reset-Button logik

            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void LoadTowersFromPrefabs()
        {
            availableTowers.Clear();
            foreach (var prefab in towerPrefabs)
            {
                var towerComponent = prefab.GetComponent<Tower>();
                if (towerComponent != null)
                {
                    TowerResearchInfo towerResearchInfo = new TowerResearchInfo
                    {
                        towerName = towerComponent.towerName,
                        researchCost = towerComponent.towerInitPrice,
                        upgradePaths = towerComponent.upgradePaths,
                        sprites =  towerComponent.GetComponent<SpriteAnim>().idle_sprites
                    };

                    availableTowers.Add(towerResearchInfo);
                }
                else
                {
                    Debug.LogWarning($"Kein Tower-Script auf Prefab {prefab.name} gefunden!");
                }
            }
            SetPointsText();

        }

        public void SaveResearchData()
        {
            try
            {
                string json = JsonUtility.ToJson(new ResearchSaveData(unlockedResearch, researchPoints), true);
                File.WriteAllText(savePath, json);
                Debug.Log("Forschung gespeichert.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Fehler beim Speichern: {ex.Message}");
            }
        }

        public void LoadResearchData()
        {
            if (File.Exists(savePath))
            {
                try
                {
                    string json = File.ReadAllText(savePath);
                    ResearchSaveData data = JsonUtility.FromJson<ResearchSaveData>(json);
                    unlockedResearch = data.researchedTowers ?? new List<ResearchedTower>();
                    researchPoints = data.researchPoints;
                    SetPointsText();

                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"Fehler beim Laden: {ex.Message}. Neue Daten erstellt.");
                    unlockedResearch = new List<ResearchedTower>();
                    researchPoints = 0;
                    SaveResearchData();
                }
            }
            else
            {
                unlockedResearch = new List<ResearchedTower>();
                researchPoints = 0;
                SetPointsText();
                SaveResearchData();
            }
        }

        public bool IsUpgradeUnlocked(string towerName, string pathName, string upgradeName)
        {
            var tower = unlockedResearch.FirstOrDefault(t => t.towerName == towerName);
            if (tower == null) return false;

            var path = tower.researchedPaths.FirstOrDefault(p => p.pathName == pathName);
            if (path == null) return false;

            return path.unlockedUpgrades.Contains(upgradeName);
        }

        public bool CanAffordResearch(int cost)
        {
            return researchPoints >= cost;
        }

        public void SpendResearchPoints(int amount)
        {
            researchPoints -= amount;
            SetPointsText();
            SaveResearchData();
        }
        
        public void UnlockUpgrade(string towerName, string pathName, string upgradeName)
        {
            var tower = unlockedResearch.FirstOrDefault(t => t.towerName == towerName);
            if (tower == null)
            {
                tower = new ResearchedTower { towerName = towerName };
                unlockedResearch.Add(tower);
            }

            var path = tower.researchedPaths.FirstOrDefault(p => p.pathName == pathName);
            if (path == null)
            {
                path = new ResearchedPath { pathName = pathName };
                tower.researchedPaths.Add(path);
            }

            if (!path.unlockedUpgrades.Contains(upgradeName))
            {
                path.unlockedUpgrades.Add(upgradeName);
                SetPointsText();
                SaveResearchData();
            }
        }

        public void AddResearchPoints(int amount)
        {
            researchPoints += amount;
            SetPointsText();
            SaveResearchData();

        }

        private void OnResetButtonClicked()
        {
            confirmPopupManager.ShowConfirmPopup("Willst du wirklich deine Forschungspunkte zurücksetzen?", ConfirmResetResearch);
        }

        private void ConfirmResetResearch()
        {
            ResetResearchData();
            SaveResearchData();

        }
        
        public void ResetResearchData()
        {
            int refundedResearchPoints = 0;

            foreach (var researchedTower in unlockedResearch)
            {
                foreach (var path in researchedTower.researchedPaths)
                {
                    foreach (var upgradeName in path.unlockedUpgrades)
                    {
                        var towerInfo = availableTowers.FirstOrDefault(t => t.towerName == researchedTower.towerName);
                        if (towerInfo != null)
                        {
                            var upgrade = towerInfo.upgradePaths
                                .SelectMany(p => p.levels)
                                .FirstOrDefault(u => u.upgradeName == upgradeName);

                            if (upgrade != null)
                            {
                                refundedResearchPoints += upgrade.researchCost;
                            }
                        }
                    }
                }
            }

            unlockedResearch.Clear();
            AddResearchPoints(refundedResearchPoints);
            Debug.Log($"Alle Forschungen zurückgesetzt. Zurückerstattete Forschungspunkte: {refundedResearchPoints}");
        }
        
        public void SetPointsText()
        {
            txtResearchPunkte.text = $"Forschungspunkte: {ResearchManager.Instance.researchPoints}";
        }
    }
    
}
