using System;
using System.Collections.Generic;
using TowerDefense;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.EventSystems;

namespace TowerDefense
{
        

    public class TowerHeroManager : MonoBehaviour
    {
        public static TowerHeroManager instance;
        
        public List<GameObject> towers = new List<GameObject>();
        public List<GameObject> enemies = new List<GameObject>();
        [HideInInspector] public List<GameObject> walls = new List<GameObject>();

        public Tower selectedTower;

        void Start()
        {
            Actions.onEnemySpawn += RegisterEnemy;
            Actions.onEnemyReachedEnd += UnregisterEnemy;
            Actions.onEnemyDeath += UnregisterEnemy;
            
        }

        void Awake()
        {
            if (instance == null)
            {
                instance = this;
            }
            else
            {
                Destroy(gameObject);
            }
        }
        private void Update()
        {
            if (towers.Count == 0) return;
            
            foreach (var tower in towers)
            {
                // Finde Gegner basierend auf dem Zieltyp
                Tower _tower = tower.GetComponent<Tower>();
                if (_tower.isAttacking) break;
                
                (GameObject, int) target = _tower.FindTargetInRange(enemies);
                if (target.Item1 != null)
                {
                    _tower.Attack(target);
                    Debug.Log(_tower.towerName + " attacks: " + target);

                }
            }
            OneClickInWorldListener.ListenOnce((Vector3 pos) =>
            {
                instance.DeselectTower();
            });

        }

        public void RegisterTower(GameObject tower)
        {
            if (tower.GetComponent<Tower>() is WallSegment)
            {
                if (!walls.Contains(tower.gameObject))
                {
                    walls.Add(tower);
                }
            }
            else
            {
                if (!towers.Contains(tower.gameObject))
                {
                    towers.Add(tower);
                }
            }

        }
        public void UnRegisterTower(GameObject tower)
        {
            if (tower.GetComponent<Tower>() is WallSegment)
            {
                if (walls.Contains(tower.gameObject))
                {
                    walls.Remove(tower);
                }
            }
            else
            {
                if (towers.Contains(tower.gameObject))
                {
                    towers.Remove(tower);
                }
            }

            
        }
        

        public void RegisterEnemy(GameObject enemy)
        {
            if (!enemies.Contains(enemy))
            {
                enemies.Add(enemy);
            }
        }

        public void UnregisterEnemy(GameObject enemy)
        {
            if (enemies.Contains(enemy))
            {
                enemies.Remove(enemy);
            }

        }
        
        
        
        
        public void SelectTower(TowerDefense.Tower tower)
        {
            // Falls ein Turm bereits ausgewählt ist, deaktivieren
            if (instance.selectedTower != null && instance.selectedTower != tower)
            {
                instance.selectedTower.SetIsSelected(false);
                instance.selectedTower.SetInteraction(false);
                instance.selectedTower.SetHighlighted(false);

            }

            instance.selectedTower = tower;
            instance.selectedTower.SetIsSelected(true);
            instance.selectedTower.SetInteraction(true);
            instance.selectedTower.SetHighlighted(true);

            // UI anzeigen und aktualisieren
            TowerUI.Instance.FocusTowerUI(tower);
            TowerUI.Instance.gameObject.SetActive(true);
        }

        public void DeselectTower()
        {
            if (instance.selectedTower != null)
            {
                instance.selectedTower.SetIsSelected(false);
                instance.selectedTower.SetInteraction(false);
                instance.selectedTower.SetHighlighted(false);
                instance.selectedTower = null;
            }

            // UI ausblenden
            TowerUI.Instance.gameObject.SetActive(false);
        }


    }

}