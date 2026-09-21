using LevelSelector.Definitions;
using LevelSelector.Quest;
using UnityEngine;
using UnityEngine.UI;

namespace LevelSelector
{
    public class QuestUI : MonoBehaviour
    {
        [Header("Text")]
        [SerializeField] private Text questName;
        [SerializeField] private Text questDescription;

        [Header("Conditions")]
        [SerializeField] private Transform conditionContainer;
        [SerializeField] private GameObject conditionIconPrefab;

        [Header("Completed")]
        [SerializeField] private GameObject completedOverlay;

        public void Setup(
            LevelQuestDefinition quest,
            bool completed)
        {
            questName.text = quest.questName;
            questDescription.text = quest.description;

            BuildConditions(quest);

            if (completedOverlay != null)
                completedOverlay.SetActive(completed);

            SetInteractableVisual(!completed);
        }

        private void BuildConditions(LevelQuestDefinition quest)
        {
            foreach (Transform child in conditionContainer)
            {
                Destroy(child.gameObject);
            }

            if (quest.conditions == null)
                return;

            foreach (QuestCondition condition in quest.conditions)
            {
                if (condition == null)
                    continue;

                GameObject obj = Instantiate(
                    conditionIconPrefab,
                    conditionContainer);

                Image image = obj.GetComponent<Image>();

                if (image == null)
                    continue;

                if (condition is WeaponQuestCondition weaponCondition)
                {
                    if (weaponCondition.requiredWeapon != null)
                        image.sprite = weaponCondition.requiredWeapon.icon;
                }
                else if (condition is PerkQuestCondition perkCondition)
                {
                    if (perkCondition.requiredPerk != null)
                        image.sprite = perkCondition.requiredPerk.icon;
                }
                else if (condition is MutatorQuestCondition mutatorCondition)
                {
                    if (mutatorCondition.requiredMutator != null)
                        image.sprite = mutatorCondition.requiredMutator.icon;
                }
            }
        }

        private void SetInteractableVisual(bool active)
        {
            CanvasGroup canvasGroup =
                GetComponent<CanvasGroup>();

            if (canvasGroup == null)
                canvasGroup = gameObject.AddComponent<CanvasGroup>();

            canvasGroup.alpha = active ? 1f : 0.45f;
        }
    }
}