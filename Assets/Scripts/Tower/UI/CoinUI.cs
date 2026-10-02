using UnityEngine;
using UnityEngine.UI;

namespace TowerDefense
{
    public class CoinUI : MonoBehaviour
    {
        [SerializeField] private Image fill;

        public void SetFilled(bool filled)
        {
            if (fill != null)
                fill.gameObject.SetActive(filled);
        }

        public void ResetCoin()
        {
            SetFilled(false);
        }
    }
}
