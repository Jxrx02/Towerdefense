using LevelSelector.Definitions;
using UnityEngine;
using UnityEngine.UI;

namespace LevelSelector
{
    /// <summary>
    /// Popup über dem Level-Knoten in Reichweite (Stil des InteractionPopup):
    /// Name, Highscore, Quests mit Abschluss-Markierung und "Press [E] to select".
    /// </summary>
    public class LevelNodePopup : MonoBehaviour
    {
        [SerializeField] private RectTransform root;
        [SerializeField] private Text txtName;
        [SerializeField] private Text txtHighscore;
        [SerializeField] private Transform questContainer;
        [SerializeField] private GameObject questRowPrefab;   // Root: Text
        [SerializeField] private GameObject promptRoot;
        [SerializeField] private Text txtKey;
        [SerializeField] private GameObject lockedRoot;
        [SerializeField] private Text txtLocked;

        [Header("Farben")]
        [SerializeField] private Color textColor = new Color(0.96f, 0.92f, 0.82f);
        [SerializeField] private Color doneColor = new Color(0.62f, 0.80f, 0.38f);

        private LevelNode _current;
        private Camera _camera;

        private void Awake()
        {
            if (root != null)
                root.gameObject.SetActive(false);
        }

        private void LateUpdate()
        {
            TowerDefense.Hero hero = TowerDefense.Hero.Instance;
            LevelNode node = hero != null && hero.IsOverworld && !TowerDefense.Hero.IsMapBlocked ? hero.NearestNode : null;

            if (node != _current)
            {
                _current = node;

                if (node == null)
                    root.gameObject.SetActive(false);
                else
                    Build(node, hero);
            }

            if (_current != null)
                Position(_current.PopupAnchor);
        }

        private void Build(LevelNode node, TowerDefense.Hero hero)
        {
            LevelDefinition level = node.Level;

            txtName.text = level != null ? level.levelName : "?";

            int highscore = node.Highscore;
            txtHighscore.text = highscore > 0 ? $"Highscore: {highscore}" : "Not completed yet";

            // Quests
            for (int i = questContainer.childCount - 1; i >= 0; i--)
            {
                Transform child = questContainer.GetChild(i);
                child.SetParent(null);
                Destroy(child.gameObject);
            }

            if (level != null && level.quests != null)
            {
                foreach (LevelQuestDefinition quest in level.quests)
                {
                    if (quest == null) continue;

                    bool done = LevelProgressManager.Instance != null &&
                                LevelProgressManager.Instance.IsQuestCompleted(level.levelIndex, quest);

                    Text row = Instantiate(questRowPrefab, questContainer).GetComponent<Text>();
                    row.text = (done ? "[X] " : "[  ] ") + quest.questName;
                    row.color = done ? doneColor : textColor;
                }
            }

            bool unlocked = node.IsUnlocked;
            promptRoot.SetActive(unlocked);
            lockedRoot.SetActive(!unlocked);

            if (unlocked)
                txtKey.text = hero.InteractKey.ToString();
            else if (level != null)
                txtLocked.text = $"Locked. Complete level {level.levelIndex - 1} first.";

            root.gameObject.SetActive(true);
            LayoutRebuilder.ForceRebuildLayoutImmediate(root);
        }

        private void Position(Vector3 worldPosition)
        {
            if (_camera == null)
                _camera = Camera.main;
            if (_camera == null)
                return;

            RectTransform parent = (RectTransform)root.parent;
            Vector2 screenPoint = _camera.WorldToScreenPoint(worldPosition);

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screenPoint, null, out Vector2 local))
                return;

            Rect area = parent.rect;
            Vector2 position = local - area.center;   // anchoredPosition bezieht sich auf die Mitte

            // Im Bild halten (Pivot = unten Mitte)
            const float margin = 8f;
            Vector2 size = root.rect.size;

            float minX = -area.width * 0.5f + size.x * 0.5f + margin;
            float maxX = area.width * 0.5f - size.x * 0.5f - margin;
            float minY = -area.height * 0.5f + margin;
            float maxY = area.height * 0.5f - size.y - margin;

            if (maxX > minX) position.x = Mathf.Clamp(position.x, minX, maxX);
            if (maxY > minY) position.y = Mathf.Clamp(position.y, minY, maxY);

            root.anchoredPosition = position;
        }
    }
}