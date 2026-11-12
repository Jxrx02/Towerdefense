using TowerDefense;
using UnityEngine;

namespace LevelSelector
{
    /// <summary>
    /// Einfacher Held für die Übersichtskarte (WASD/Pfeiltasten, Tasten aus KeyBindings).
    /// Der Kampf-Held (Hero) bleibt dem Level vorbehalten.
    /// </summary>
    public class OverworldHero : MonoBehaviour
    {
        public static OverworldHero Instance { get; private set; }

        [Header("Bewegung")]
        [SerializeField] private float moveSpeed = 5f;
        [SerializeField] private Vector2 boundsMin = new Vector2(-60f, -30f);
        [SerializeField] private Vector2 boundsMax = new Vector2(60f, 30f);

        [Header("Darstellung")]
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private Sprite idleSprite;
        [SerializeField] private Sprite[] walkSprites;
        [SerializeField] private float walkFps = 8f;
        [SerializeField] private bool spriteFacesRight = true;

        [Header("Interaktion")]
        [SerializeField] private KeyCode interactKey = KeyCode.E;

        private bool _returning;
        private float _animTime;

        public KeyCode InteractKey => interactKey;

        /// <summary>Level-Knoten in Reichweite (nächster), sonst null.</summary>
        public LevelNode NearestNode { get; private set; }

        /// <summary>Menü oder Loadout-Screen ist offen: der Held steht still.</summary>
        public static bool IsBlocked => MainMenuUI.IsOpen || LevelLoadoutUI.IsOpen;

        private void Awake()
        {
            Instance = this;
            _returning = OverworldSession.MenuShown;   // vor MainMenuUI.Start lesen

            if (spriteRenderer == null)
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            // Zurück aus einem Level: am zuletzt gespielten Knoten starten
            if (_returning)
                PlaceAtLastNode();
        }

        private void Update()
        {
            if (IsBlocked)
            {
                Animate(Vector2.zero);
                return;
            }

            Vector2 input = new Vector2(
                Axis(GameAction.CameraRight, KeyCode.RightArrow, GameAction.CameraLeft, KeyCode.LeftArrow),
                Axis(GameAction.CameraUp, KeyCode.UpArrow, GameAction.CameraDown, KeyCode.DownArrow));

            if (input.sqrMagnitude > 1f)
                input.Normalize();

            Vector3 position = transform.position + (Vector3)(input * moveSpeed * Time.deltaTime);
            position.x = Mathf.Clamp(position.x, boundsMin.x, boundsMax.x);
            position.y = Mathf.Clamp(position.y, boundsMin.y, boundsMax.y);
            transform.position = position;

            Animate(input);
            FindNearestNode();

            if (NearestNode != null && Input.GetKeyDown(interactKey))
                Interact(NearestNode);
        }

        public void TeleportTo(Vector3 position)
        {
            position.z = transform.position.z;
            transform.position = position;
        }

        private static float Axis(GameAction positive, KeyCode positiveAlt, GameAction negative, KeyCode negativeAlt)
        {
            float value = 0f;
            if (KeyBindings.Held(positive) || Input.GetKey(positiveAlt)) value += 1f;
            if (KeyBindings.Held(negative) || Input.GetKey(negativeAlt)) value -= 1f;
            return value;
        }

        private void FindNearestNode()
        {
            LevelNode found = null;
            float best = float.MaxValue;

            foreach (LevelNode node in LevelNode.All)
            {
                float distance = Vector2.Distance(transform.position, node.transform.position);

                if (distance <= node.InteractRadius && distance < best)
                {
                    best = distance;
                    found = node;
                }
            }

            NearestNode = found;
        }

        private void Interact(LevelNode node)
        {
            if (!node.IsUnlocked || node.Level == null)
                return;

            if (LevelSelectorManager.Instance == null)
            {
                Debug.LogError("OverworldHero: LevelSelectorManager wurde nicht gefunden.");
                return;
            }

            LevelSelectorManager.Instance.OpenLevel(node.Level);
        }

        private void PlaceAtLastNode()
        {
            foreach (LevelNode node in LevelNode.All)
            {
                if (node.Level != null && node.Level.levelIndex == OverworldSession.LastLevelIndex)
                {
                    TeleportTo(node.HeroSpawnPosition);
                    return;
                }
            }
        }

        private void Animate(Vector2 input)
        {
            if (spriteRenderer == null) return;

            bool moving = input.sqrMagnitude > 0.01f;

            if (Mathf.Abs(input.x) > 0.01f)
                spriteRenderer.flipX = spriteFacesRight ? input.x < 0f : input.x > 0f;

            if (moving && walkSprites != null && walkSprites.Length > 0)
            {
                _animTime += Time.deltaTime * walkFps;
                spriteRenderer.sprite = walkSprites[(int)_animTime % walkSprites.Length];
            }
            else
            {
                _animTime = 0f;
                if (idleSprite != null)
                    spriteRenderer.sprite = idleSprite;
            }

            spriteRenderer.sortingOrder = Mathf.RoundToInt(-transform.position.y * 10f);
        }
    }
}