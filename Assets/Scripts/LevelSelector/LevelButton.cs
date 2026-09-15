using LevelSelector.Definitions;
using UnityEngine;
using UnityEngine.UI;

namespace LevelSelector
{
    public class LevelButton : MonoBehaviour
    {
        [SerializeField]
        private LevelDefinition levelDefinition;

        [SerializeField]
        private Sprite unlockedSprite;

        [SerializeField]
        private Sprite lockedSprite;

        private Button button;
        private Image buttonImage;

        private void Awake()
        {
            button = GetComponent<Button>();
            buttonImage = GetComponent<Image>();
        }

        private void Start()
        {
            Refresh();
        }

        public void Refresh()
        {
            bool unlocked =
                levelDefinition.levelIndex <=
                LevelProgressManager.GetUnlockedLevel();

            buttonImage.sprite =
                unlocked ? unlockedSprite : lockedSprite;

            button.interactable = unlocked;
        }

        public void SelectLevel()
        {
            if (levelDefinition == null)
                return;

            LevelSelectorManager.Instance.OpenLevel(levelDefinition);
        }
    }
}