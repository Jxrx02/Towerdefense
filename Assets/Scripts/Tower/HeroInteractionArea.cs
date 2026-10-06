using TowerDefense;
using UnityEngine;

namespace TowerDefense{

public class HeroInteractionArea : MonoBehaviour
 {
     [SerializeField] private Hero hero;
     
     private void OnTriggerEnter2D(Collider2D other)
     {
         Tower tower = other.GetComponent<Tower>();
 
         if (tower == null || tower == hero)
             return;
 
         hero.EnterTowerRange(tower);
         
     }
 
     private void OnTriggerExit2D(Collider2D other)
     {
         Tower tower = other.GetComponent<Tower>();
 
         if (tower == null || tower == hero)
             return;
 
         hero.ExitTowerRange(tower);
     }
 }
}