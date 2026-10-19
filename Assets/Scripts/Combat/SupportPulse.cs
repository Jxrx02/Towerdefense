using UnityEngine;

namespace TowerDefense
{
    /// <summary>
    /// Support-Gegner (Heiler, Schildträger): pulst in Intervallen auf andere Gegner im Radius.
    /// Heilung unterliegt dem Anti-Heal der Ziele, Schilde nicht.
    /// Das Objekt selbst muss in TowerHeroManager.instance.enemies stehen, um sich selbst zu treffen.
    /// </summary>
    public class SupportPulse : MonoBehaviour
    {
        [SerializeField] float radius = 3f;
        [SerializeField] float interval = 2f;
        [SerializeField] bool includeSelf = true;

        [Header("Heilung")]
        [SerializeField] float healFlat;
        [Range(0f, 1f)] [SerializeField] float healPercentOfMaxHp = 0.05f;

        [Header("HP-Schild (zusätzliche HP)")]
        [SerializeField] float shieldAmount;
        [SerializeField] float shieldDuration = 5f;

        [Header("Block-Schild (blockt komplette Treffer)")]
        [SerializeField] int blockCharges;
        [Tooltip("0 = bis die Ladungen verbraucht sind")]
        [SerializeField] float blockDuration;

        float timer;

        void Update()
        {
            timer += Time.deltaTime;
            if (timer < interval) return;
            timer -= interval;
            Pulse();
        }

        void Pulse()
        {
            foreach (var go in TowerHeroManager.instance.enemies.ToArray())
            {
                if (go == null) continue;
                if (go == gameObject && !includeSelf) continue;
                if (Vector3.Distance(transform.position, go.transform.position) > radius) continue;

                var v = go.GetComponent<Vitals>();
                if (v == null || v.IsDead) continue;

                float heal = healFlat + v.MaxHealth * healPercentOfMaxHp;
                if (heal > 0f) v.Heal(heal, gameObject);

                if (shieldAmount > 0f) v.AddShield(shieldAmount, shieldDuration, false);
                if (blockCharges > 0) v.AddBlockShield(blockCharges, blockDuration);
            }
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.9f);
            Gizmos.DrawWireSphere(transform.position, radius);
        }
#endif
    }
}