using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TowerDefense
{
    public class TowerSelectionEntry :
        MonoBehaviour,
        IPointerEnterHandler,
        IPointerClickHandler
    {
        [Header("UI")]
        [SerializeField] private Image towerIcon;
        [SerializeField] private Text towerName;
        [SerializeField] private Text description;
        [SerializeField] private Text price;

        [Header("Stats")]
        [SerializeField] private Transform statsContainer;
        [SerializeField] private TowerStatEntry statPrefab;

        [Header("Selection")]
        [SerializeField] private GameObject selectedFrame;

        private int index;


        // =========================================================
        // INITIALIZE
        // =========================================================

        public void Initialize(
            int index,
            Tower tower)
        {
            this.index = index;

            if (tower == null)
                return;

            // -----------------------------------------------------
            // NAME
            // -----------------------------------------------------

            if (towerName != null)
                towerName.text = tower.towerName;


            // -----------------------------------------------------
            // DESCRIPTION
            // -----------------------------------------------------

            if (description != null)
                description.text = tower.towerDesc;


            // -----------------------------------------------------
            // PRICE
            // -----------------------------------------------------

            if (price != null)
            {
                price.text =
                    tower.towerInitPrice.ToString();
            }


            // -----------------------------------------------------
            // ICON
            // -----------------------------------------------------

            if (towerIcon != null)
            {
                SpriteRenderer sr =
                    tower.GetComponent<SpriteRenderer>();

                if (sr != null)
                    towerIcon.sprite = sr.sprite;
            }


            // -----------------------------------------------------
            // STATS
            // -----------------------------------------------------

            CreateStats(tower);


            // -----------------------------------------------------
            // SELECTION
            // -----------------------------------------------------

            SetSelected(false);
        }


        // =========================================================
        // CREATE STATS
        // =========================================================

        private void CreateStats(Tower tower)
        {
            ClearStats();

            if (statsContainer == null)
                return;

            if (statPrefab == null)
                return;

            TowerDisplayedStats stats =
                tower.DisplayedStats;


            // -----------------------------------------------------
            // DAMAGE
            // -----------------------------------------------------

            if (HasStat(
                    stats,
                    TowerDisplayedStats.Damage))
            {
                AddStat(
                    InteractionPopup.instance.damageIcon,
                    tower.damage
                );
            }


            // -----------------------------------------------------
            // RANGE
            // -----------------------------------------------------

            if (HasStat(
                    stats,
                    TowerDisplayedStats.Range))
            {
                AddStat(
                    InteractionPopup.instance.rangeIcon,
                    tower.range,
                    1
                );
            }


            // -----------------------------------------------------
            // ATTACK SPEED
            // -----------------------------------------------------

            if (HasStat(
                    stats,
                    TowerDisplayedStats.AttackSpeed))
            {
                AddStat(
                    InteractionPopup.instance.attackSpeedIcon,
                    tower.timeInBetweenShots,
                    1,
                    "s"
                );
            }


            // -----------------------------------------------------
            // HEALTH
            // -----------------------------------------------------

            if (HasStat(
                    stats,
                    TowerDisplayedStats.Health))
            {
                AddStat(
                    InteractionPopup.instance.healthIcon,
                    tower.statHealthPoints
                );
            }


            // -----------------------------------------------------
            // GOLD
            // -----------------------------------------------------

            if (HasStat(
                    stats,
                    TowerDisplayedStats.Gold))
            {
                AddStat(
                    InteractionPopup.instance.goldIcon,
                    tower.statCoinsEarnedPerSecond,
                    1,
                    "/s"
                );
            }


            // -----------------------------------------------------
            // REGEN
            // -----------------------------------------------------

            if (HasStat(
                    stats,
                    TowerDisplayedStats.Regen))
            {
                AddStat(
                    InteractionPopup.instance.regenIcon,
                    tower.statHealthRegenPerSecond,
                    1,
                    "/s"
                );
            }


            // -----------------------------------------------------
            // DAMAGE MULTIPLIER
            // -----------------------------------------------------

            if (HasStat(
                    stats,
                    TowerDisplayedStats.DamageMultiplier))
            {
                AddStat(
                    InteractionPopup.instance.damageMultiplierIcon,
                    tower.statDmgMultiplier,
                    2,
                    "x"
                );
            }


            // -----------------------------------------------------
            // RANGE MULTIPLIER
            // -----------------------------------------------------

            if (HasStat(
                    stats,
                    TowerDisplayedStats.RangeMultiplier))
            {
                AddStat(
                    InteractionPopup.instance.rangeMultiplierIcon,
                    tower.statRangeMultiplier,
                    2,
                    "x"
                );
            }


            // -----------------------------------------------------
            // ATTACK SPEED MULTIPLIER
            // -----------------------------------------------------

            if (HasStat(
                    stats,
                    TowerDisplayedStats.AttackSpeedMultiplier))
            {
                AddStat(
                    InteractionPopup.instance.attackSpeedMultiplierIcon,
                    tower.statAttackSpeedMultiplier,
                    2,
                    "x"
                );
            }


            // -----------------------------------------------------
            // SLOW MULTIPLIER
            // -----------------------------------------------------

            if (HasStat(
                    stats,
                    TowerDisplayedStats.SlowMultiplier))
            {
                AddStat(
                    InteractionPopup.instance.slowMultiplierIcon,
                    tower.statSlowMultiplier,
                    2,
                    "x"
                );
            }
        }


        // =========================================================
        // CHECK STAT
        // =========================================================

        private bool HasStat(TowerDisplayedStats stats, TowerDisplayedStats stat)
        {
            return (stats & stat) != 0;
        }


        // =========================================================
        // ADD STAT
        // =========================================================

        private void AddStat(
            Sprite icon,
            float value,
            int decimals = 0,
            string suffix = "")
        {
            if (statPrefab == null ||
                statsContainer == null)
                return;

            TowerStatEntry entry =
                Instantiate(
                    statPrefab,
                    statsContainer
                );

            entry.Setup(
                icon,
                value,
                decimals,
                suffix
            );
        }


        // =========================================================
        // CLEAR STATS
        // =========================================================

        private void ClearStats()
        {
            if (statsContainer == null)
                return;

            for (int i = statsContainer.childCount - 1;
                 i >= 0;
                 i--)
            {
                Destroy(statsContainer.GetChild(i).gameObject);
            }
        }


        // =========================================================
        // SELECTION
        // =========================================================

        public void SetSelected(bool selected)
        {
            if (selectedFrame != null)
                selectedFrame.SetActive(selected);
        }


        // =========================================================
        // MOUSE
        // =========================================================

        public void OnPointerEnter(
            PointerEventData eventData)
        {
            if (InteractionUISelectTower.instance == null)
                return;

            InteractionUISelectTower.instance.SelectFromMouse(index);
        }


        public void OnPointerClick(
            PointerEventData eventData)
        {
            if (InteractionUISelectTower.instance == null)
                return;

            InteractionUISelectTower.instance.SelectFromMouse(index);
        }
    }
}