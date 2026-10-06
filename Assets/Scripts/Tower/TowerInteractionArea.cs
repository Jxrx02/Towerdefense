using UnityEngine;

namespace TowerDefense
{
    


    public class TowerInteractionArea : MonoBehaviour
    {
     
        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other == null)
                return;
            var othertower = other.GetComponent<Tower>();

            if (othertower == null)
                return;
         
 
            othertower.EnterTowerRange(this.gameObject.GetComponent<Tower>());
        }
 
        private void OnTriggerExit2D(Collider2D other)
        {
            var othertower = other.GetComponent<Tower>();
 
            if (othertower == null)
                return;
 
            othertower.ExitTowerRange(this.gameObject.GetComponent<Tower>());
        }
    }

}
