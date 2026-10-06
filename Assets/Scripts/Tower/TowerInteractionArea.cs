using UnityEngine;

namespace TowerDefense
{
    
    

    public class TowerInteractionArea : MonoBehaviour
    {
        private TowerConstructionSite towerConstructionSite;


     
        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other == null)
                return;
            var othertower = other.GetComponent<Tower>();

           
            if (othertower == null)
                return;
            
            towerConstructionSite = GetComponentInParent<TowerConstructionSite>();

            if (othertower.GetComponent<Hero>() && towerConstructionSite !=null)
            {
                
                Debug.Log(othertower.towerName + " ist in Range");
                towerConstructionSite.EnterInteractionRange(othertower.GetComponent<Hero>());
            }        
 
            othertower.EnterTowerRange(this.gameObject.GetComponent<Tower>());



        }
 
        private void OnTriggerExit2D(Collider2D other)
        {
            var othertower = other.GetComponent<Tower>();
 
            if (othertower == null)
                return;
 
            if (othertower.GetComponent<Hero>()&& towerConstructionSite !=null)
            {
                towerConstructionSite.ExitInteractionRange(other.GetComponent<Hero>());
            }
            

            othertower.ExitTowerRange(this.gameObject.GetComponent<Tower>());
        }
    }

}
