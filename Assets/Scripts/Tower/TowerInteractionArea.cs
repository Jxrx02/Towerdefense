using UnityEngine;

namespace TowerDefense
{
    public class TowerInteractionArea : MonoBehaviour
    {
        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other == null)
                return;

            Tower otherTower = other.GetComponent<Tower>();

            if (otherTower == null)
                return;

            Tower ownTower = GetComponentInParent<Tower>();

            if (otherTower is Hero hero)
            {
                TowerConstructionSite site = GetComponentInParent<TowerConstructionSite>();

                if (site != null)
                    site.EnterInteractionRange(hero);
                else if (ownTower != null && ownTower != otherTower)
                    ownTower.EnterInteractionRange(hero);
            }

            otherTower.EnterTowerRange(ownTower);
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (other == null)
                return;

            Tower otherTower = other.GetComponent<Tower>();

            if (otherTower == null)
                return;

            Tower ownTower = GetComponentInParent<Tower>();

            if (otherTower is Hero hero)
            {
                TowerConstructionSite site = GetComponentInParent<TowerConstructionSite>();

                if (site != null)
                    site.ExitInteractionRange(hero);
                else if (ownTower != null && ownTower != otherTower)
                    ownTower.ExitInteractionRange(hero);
            }

            otherTower.ExitTowerRange(ownTower);
        }
    }
}