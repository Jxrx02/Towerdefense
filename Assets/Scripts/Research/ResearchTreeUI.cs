using System;
using System.Collections.Generic;
using System.Text;
using ScriptableObjects;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TowerDefense.Research
{
    /// <summary>
    /// Skilltree-Ansicht für die Forschung.
    ///
    ///   Links    Liste aller Türme (gesperrt / x von y Upgrades)
    ///   Mitte    Baum des gewählten Turms: Wurzel = Turm selbst, pro Upgrade-Pfad eine Reihe von Knoten
    ///   Rechts   Details des gewählten Knotens + Button "Unlock" / "Research"
    ///
    /// Alle Regeln (Reihenfolge im Pfad, Kosten, Speichern) liegen im ResearchManager.
    /// Diese Klasse zeigt nur an und ruft TryUnlockTower / TryResearchUpgrade auf. Alle Texte kommen aus Texts.
    /// Der Baum wird bei jeder Änderung (ResearchManager.Changed bzw. Texts.Changed) komplett neu aufgebaut.
    ///
    /// Tastatur (nur solange das Canvas aktiv ist):
    ///   Pfeil links/rechts   im Pfad zurück/vor (links von Stufe 1 = Turm-Wurzel, rechts von der Wurzel = erster Pfad)
    ///   Pfeil hoch/runter    zwischen den Pfaden; an der Wurzel: vorheriger/nächster Turm
    ///   Q/E oder BildAuf/Ab  vorheriger/nächster Turm
    ///   Enter / Leertaste    Freischalten bzw. Erforschen
    ///   Esc                  Zurück (schließt zuerst den Reset-Dialog)
    /// </summary>
    public class ResearchTreeUI : MonoBehaviour
    {
        private const int RootPath = -1;

        [Header("Turm-Liste")]
        [SerializeField] private Transform towerListContainer;
        [SerializeField] private ResearchTowerButton towerButtonPrefab;

        [Header("Baum")]
        [SerializeField] private RectTransform treeContent;
        [SerializeField] private ResearchNodeView nodePrefab;
        [SerializeField] private Text pathLabelPrefab;
        [SerializeField] private float nodeSpacingX = 130f;
        [SerializeField] private float rowHeight = 120f;
        [SerializeField] private float padding = 70f;
        [SerializeField] private float lineThickness = 4f;
        [Tooltip("Abstand zum Rand, den ein per Tastatur gewählter Knoten im sichtbaren Bereich hat (Platz für die Kosten unter dem Knoten)")]
        [SerializeField] private float scrollMargin = 48f;
        [SerializeField] private Color lineInactiveColor = new Color(0.30f, 0.24f, 0.17f, 1f);
        [SerializeField] private Color lineActiveColor = new Color(1f, 0.78f, 0.29f, 1f);

        [Header("Details")]
        [SerializeField] private Text txtTitle;
        [SerializeField] private Text txtSubtitle;
        [SerializeField] private Text txtDescription;
        [SerializeField] private Text txtStats;
        [SerializeField] private Text txtHint;
        [SerializeField] private Button btnAction;
        [SerializeField] private Text txtAction;

        [Header("Kopfzeile und statische Beschriftungen")]
        [SerializeField] private Text txtScreenTitle;
        [SerializeField] private Text txtTowersHeader;
        [SerializeField] private Text txtUpgradesHeader;
        [SerializeField] private Text txtKeyHints;
        [SerializeField] private Text txtPoints;
        [SerializeField] private Button btnBack;
        [SerializeField] private Button btnReset;
        [SerializeField] private ConfirmDialog confirmDialog;
        [Tooltip("Wird bei Zurück ausgeblendet (das ResearchCanvas). MainMenuUI erkennt das und zeigt das Hauptmenü wieder.")]
        [SerializeField] private GameObject closeTarget;

        [Header("Farben")]
        [SerializeField] private Color goodColor = new Color(0.62f, 0.80f, 0.38f);
        [SerializeField] private Color badColor = new Color(0.90f, 0.35f, 0.30f);

        public event Action BackRequested;

        private readonly List<ResearchTowerButton> _towerButtons = new List<ResearchTowerButton>();
        private readonly Dictionary<(int path, int level), ResearchNodeView> _nodes =
            new Dictionary<(int, int), ResearchNodeView>();

        // Pfadindizes der Baum-Reihen, von oben nach unten (für die Tastaturnavigation)
        private readonly List<int> _rows = new List<int>();

        private ScrollRect _treeScroll;
        private ScrollRect _listScroll;

        private string _selectedTower;
        private int _selectedPath = RootPath;
        private int _selectedLevel;

        // Knoten, der nach dem nächsten Aufbau aufpoppen soll
        private (int path, int level)? _popAfterRebuild;

        private ResearchManager Manager => ResearchManager.Instance;

        // ── Lebenszyklus ──────────────────────────────────────────

        private void Awake()
        {
            btnAction.onClick.AddListener(OnActionClicked);

            if (btnBack != null)
                btnBack.onClick.AddListener(OnBackClicked);

            if (btnReset != null)
                btnReset.onClick.AddListener(OnResetClicked);

            _treeScroll = treeContent != null ? treeContent.GetComponentInParent<ScrollRect>() : null;
            _listScroll = towerListContainer != null ? towerListContainer.GetComponentInParent<ScrollRect>() : null;

            BindStaticTexts();
        }

        /// <summary>Beschriftungen, die sich nie ändern außer bei Sprachwechsel.</summary>
        private void BindStaticTexts()
        {
            LocalizedText.Bind(txtScreenTitle, () => Texts.Research.Title);
            LocalizedText.Bind(txtTowersHeader, () => Texts.Research.TowersHeader.ToUpperInvariant());
            LocalizedText.Bind(txtUpgradesHeader, () => Texts.Research.UpgradesHeader.ToUpperInvariant());
            LocalizedText.Bind(txtKeyHints, () => Texts.Research.KeyHints);

            if (btnBack != null)
                LocalizedText.Bind(btnBack.GetComponentInChildren<Text>(), () => Texts.Common.Back);

            if (btnReset != null)
                LocalizedText.Bind(btnReset.GetComponentInChildren<Text>(), () => Texts.Research.ResetButton);
        }

        private void OnEnable()
        {
            if (Manager == null)
            {
                Debug.LogWarning("ResearchTreeUI: Kein ResearchManager vorhanden.", this);
                return;
            }

            Manager.Changed += OnResearchChanged;
            Texts.Changed += OnResearchChanged;

            BuildTowerList();

            if (string.IsNullOrEmpty(_selectedTower) || Manager.FindTower(_selectedTower) == null)
                SelectTower(Manager.availableTowers.Count > 0 ? Manager.availableTowers[0].towerName : null);
            else
                RefreshAll();
        }

        private void OnDisable()
        {
            if (Manager != null)
                Manager.Changed -= OnResearchChanged;

            Texts.Changed -= OnResearchChanged;
        }

        private void OnResearchChanged()
        {
            if (Manager != null)
                RefreshAll();
        }

        private void OnBackClicked()
        {
            BackRequested?.Invoke();

            if (closeTarget != null)
                closeTarget.SetActive(false);
        }

        // ── Tastatur ──────────────────────────────────────────────

        private void Update()
        {
            if (Manager == null)
                return;

            // Der Dialog gehört sich selbst: Esc schließt nur ihn, sonst wird nichts bedient
            if (confirmDialog != null && confirmDialog.IsOpen)
            {
                if (Input.GetKeyDown(KeyCode.Escape))
                    confirmDialog.Close();

                return;
            }

            // Nach einem Mausklick bleibt der Button im EventSystem "ausgewählt" und würde bei Enter/Leertaste
            // zusätzlich auslösen. Die Tastatur wird hier selbst bedient, deshalb die Auswahl leeren.
            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null)
                EventSystem.current.SetSelectedGameObject(null);

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                OnBackClicked();
                return;
            }

            if (Input.GetKeyDown(KeyCode.LeftArrow)) MoveSelection(-1, 0);
            else if (Input.GetKeyDown(KeyCode.RightArrow)) MoveSelection(1, 0);
            else if (Input.GetKeyDown(KeyCode.UpArrow)) MoveSelection(0, -1);
            else if (Input.GetKeyDown(KeyCode.DownArrow)) MoveSelection(0, 1);

            if (Input.GetKeyDown(KeyCode.Q) || Input.GetKeyDown(KeyCode.PageUp)) SwitchTower(-1);
            else if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.PageDown)) SwitchTower(1);

            bool confirm = Input.GetKeyDown(KeyCode.Return) ||
                           Input.GetKeyDown(KeyCode.KeypadEnter) ||
                           Input.GetKeyDown(KeyCode.Space);

            if (confirm && btnAction.gameObject.activeInHierarchy && btnAction.interactable)
                OnActionClicked();
        }

        /// <param name="dx">-1 = links, +1 = rechts</param>
        /// <param name="dy">-1 = hoch, +1 = runter</param>
        private void MoveSelection(int dx, int dy)
        {
            if (string.IsNullOrEmpty(_selectedTower))
                return;

            TowerResearchInfo tower = Manager.FindTower(_selectedTower);
            if (tower == null)
                return;

            int path = _selectedPath;
            int level = _selectedLevel;

            if (path == RootPath)
            {
                // An der Wurzel: rechts geht in den ersten Pfad, hoch/runter wechselt den Turm
                if (dx > 0 && _rows.Count > 0)
                {
                    path = _rows[0];
                    level = 0;
                }
                else if (dy != 0)
                {
                    SwitchTower(dy);
                    return;
                }
                else
                {
                    return;
                }
            }
            else if (dx != 0)
            {
                level += dx;

                if (level < 0)
                {
                    path = RootPath;
                    level = 0;
                }
                else
                {
                    level = Mathf.Min(level, LevelCount(tower, path) - 1);
                }
            }
            else if (dy != 0)
            {
                int row = Mathf.Clamp(_rows.IndexOf(path) + dy, 0, _rows.Count - 1);
                path = _rows[row];
                level = Mathf.Min(level, LevelCount(tower, path) - 1);
            }

            if (path == _selectedPath && level == _selectedLevel)
                return;

            SelectNode(path, level);

            if (_nodes.TryGetValue((path, level), out ResearchNodeView node))
                ScrollIntoView(_treeScroll, (RectTransform)node.transform, scrollMargin);
        }

        private void SwitchTower(int delta)
        {
            var towers = Manager.availableTowers;
            if (towers.Count == 0)
                return;

            int index = towers.FindIndex(t => t.towerName == _selectedTower);
            index = index < 0 ? 0 : (index + delta + towers.Count) % towers.Count;

            SelectTower(towers[index].towerName);

            if (index < _towerButtons.Count)
                ScrollIntoView(_listScroll, (RectTransform)_towerButtons[index].transform, 8f);
        }

        private static int LevelCount(TowerResearchInfo tower, int path)
        {
            UpgradePath p = tower.upgradePaths[path];
            return p != null && p.levels != null ? p.levels.Length : 0;
        }

        /// <summary>
        /// Verschiebt den Inhalt eines ScrollRect, bis 'item' (mit Rand) im sichtbaren Bereich liegt.
        /// Erwartet Inhalt, der oben (links) verankert ist. Das gilt für Baum und Turm-Liste.
        /// </summary>
        private static void ScrollIntoView(ScrollRect scroll, RectTransform item, float margin)
        {
            if (scroll == null || item == null || scroll.viewport == null || scroll.content == null)
                return;

            RectTransform viewport = scroll.viewport;
            RectTransform content = scroll.content;

            // Die Liste liegt in einer Layout-Gruppe: Positionen müssen aktuell sein
            Canvas.ForceUpdateCanvases();

            var corners = new Vector3[4];
            item.GetWorldCorners(corners);

            float minX = float.MaxValue, maxX = float.MinValue;
            float minY = float.MaxValue, maxY = float.MinValue;

            foreach (Vector3 corner in corners)
            {
                Vector3 local = viewport.InverseTransformPoint(corner);
                minX = Mathf.Min(minX, local.x);
                maxX = Mathf.Max(maxX, local.x);
                minY = Mathf.Min(minY, local.y);
                maxY = Mathf.Max(maxY, local.y);
            }

            Rect view = viewport.rect;
            Vector2 position = content.anchoredPosition;

            // Inhalt nach unten schieben = anchoredPosition.y sinkt, der Eintrag rutscht dabei nach unten
            if (scroll.vertical)
            {
                if (maxY > view.yMax - margin) position.y -= maxY - (view.yMax - margin);
                else if (minY < view.yMin + margin) position.y += (view.yMin + margin) - minY;

                position.y = Mathf.Clamp(position.y, 0f, Mathf.Max(0f, content.rect.height - view.height));
            }

            if (scroll.horizontal)
            {
                if (maxX > view.xMax - margin) position.x -= maxX - (view.xMax - margin);
                else if (minX < view.xMin + margin) position.x += (view.xMin + margin) - minX;

                position.x = Mathf.Clamp(position.x, Mathf.Min(0f, view.width - content.rect.width), 0f);
            }

            scroll.velocity = Vector2.zero;
            content.anchoredPosition = position;
        }

        // ── Turm-Liste ────────────────────────────────────────────

        private void BuildTowerList()
        {
            Clear(towerListContainer);
            _towerButtons.Clear();

            foreach (TowerResearchInfo tower in Manager.availableTowers)
            {
                ResearchTowerButton button = Instantiate(towerButtonPrefab, towerListContainer);
                string towerName = tower.towerName;

                button.Setup(IconOf(tower), towerName, () => SelectTower(towerName));
                _towerButtons.Add(button);
            }
        }

        private void RefreshTowerList()
        {
            for (int i = 0; i < _towerButtons.Count && i < Manager.availableTowers.Count; i++)
            {
                TowerResearchInfo tower = Manager.availableTowers[i];
                bool unlocked = Manager.IsTowerUnlocked(tower.towerName);
                int unlockCost = Manager.GetTowerUnlockCost(tower);

                _towerButtons[i].Refresh(
                    unlocked,
                    Manager.CountResearchedUpgrades(tower),
                    Manager.CountTotalUpgrades(tower),
                    unlockCost,
                    Manager.CanAffordResearch(unlockCost));

                _towerButtons[i].SetSelected(tower.towerName == _selectedTower);
            }
        }

        private void SelectTower(string towerName)
        {
            _selectedTower = towerName;
            _selectedPath = RootPath;
            _selectedLevel = 0;

            // Neuer Baum hat eine andere Größe: wieder oben links beginnen
            if (treeContent != null)
                treeContent.anchoredPosition = Vector2.zero;

            RefreshAll();
        }

        // ── Auswahl eines Knotens ─────────────────────────────────

        private void SelectNode(int path, int level)
        {
            _selectedPath = path;
            _selectedLevel = level;

            foreach (var pair in _nodes)
                pair.Value.SetSelected(pair.Key.path == path && pair.Key.level == level);

            RefreshDetails();
        }

        // ── Gesamt-Aufbau ─────────────────────────────────────────

        private void RefreshAll()
        {
            if (Manager == null) return;

            if (txtPoints != null)
                txtPoints.text = Texts.Research.Points(Manager.researchPoints);

            RefreshTowerList();
            BuildTree();
            RefreshDetails();

            if (_popAfterRebuild.HasValue && _nodes.TryGetValue(_popAfterRebuild.Value, out ResearchNodeView node))
                node.Pop();

            _popAfterRebuild = null;
        }

        // ── Baum ──────────────────────────────────────────────────

        private void BuildTree()
        {
            Clear(treeContent);
            _nodes.Clear();
            _rows.Clear();

            TowerResearchInfo tower = string.IsNullOrEmpty(_selectedTower) ? null : Manager.FindTower(_selectedTower);
            if (tower == null || tower.upgradePaths == null)
                return;

            // Reihen und längster Pfad bestimmen
            int maxLevels = 0;

            for (int p = 0; p < tower.upgradePaths.Length; p++)
            {
                UpgradePath path = tower.upgradePaths[p];
                if (path == null || path.levels == null || path.levels.Length == 0) continue;

                _rows.Add(p);
                maxLevels = Mathf.Max(maxLevels, path.levels.Length);
            }

            int rows = Mathf.Max(1, _rows.Count);
            float width = padding * 2f + nodeSpacingX * (maxLevels + 1);
            float height = padding * 2f + rowHeight * rows;

            treeContent.sizeDelta = new Vector2(width, height);

            // Positionen (oben links als Ursprung, y nach unten negativ)
            float rootX = padding + nodeSpacingX * 0.25f;
            float rootY = -height * 0.5f;
            float trunkX = rootX + nodeSpacingX * 0.5f;

            // Linien zuerst, damit sie hinter den Knoten liegen
            bool towerUnlocked = Manager.IsTowerUnlocked(tower.towerName);

            float firstY = -padding - rowHeight * 0.5f;
            float lastY = -padding - rowHeight * (_rows.Count - 0.5f);

            // Wurzel -> Stamm (einmal), Stamm senkrecht über alle Reihen (einmal)
            CreateLine(new Vector2(rootX, rootY), new Vector2(trunkX, rootY), towerUnlocked);

            if (_rows.Count > 0)
                CreateLine(new Vector2(trunkX, firstY), new Vector2(trunkX, lastY), towerUnlocked);

            for (int row = 0; row < _rows.Count; row++)
            {
                int p = _rows[row];
                UpgradePath path = tower.upgradePaths[p];
                float y = -padding - rowHeight * (row + 0.5f);

                // Stamm -> erste Stufe
                CreateLine(new Vector2(trunkX, y), new Vector2(PathNodeX(rootX, 0), y), towerUnlocked);

                // Stufe -> Stufe
                for (int l = 1; l < path.levels.Length; l++)
                {
                    bool previousResearched =
                        Manager.GetUpgradeStatus(tower, p, l - 1) == ResearchStatus.Researched;

                    CreateLine(new Vector2(PathNodeX(rootX, l - 1), y),
                               new Vector2(PathNodeX(rootX, l), y), previousResearched);
                }
            }

            // Wurzelknoten
            ResearchNodeView root = Instantiate(nodePrefab, treeContent);
            Place(root, new Vector2(rootX, rootY));
            root.Setup(IconOf(tower), "", () => SelectNode(RootPath, 0));

            int unlockCost = Manager.GetTowerUnlockCost(tower);
            root.SetState(
                towerUnlocked ? ResearchNodeView.State.Researched : ResearchNodeView.State.Available,
                Manager.CanAffordResearch(unlockCost),
                Texts.Research.Cost(unlockCost));
            _nodes[(RootPath, 0)] = root;

            // Upgrade-Knoten
            for (int row = 0; row < _rows.Count; row++)
            {
                int p = _rows[row];
                UpgradePath path = tower.upgradePaths[p];
                float y = -padding - rowHeight * (row + 0.5f);

                if (pathLabelPrefab != null)
                {
                    Text label = Instantiate(pathLabelPrefab, treeContent);
                    label.text = path.towerName;
                    RectTransform labelRect = label.rectTransform;
                    labelRect.anchorMin = labelRect.anchorMax = new Vector2(0f, 1f);
                    labelRect.pivot = new Vector2(0f, 0f);
                    labelRect.anchoredPosition = new Vector2(PathNodeX(rootX, 0) - 40f, y + 46f);
                }

                for (int l = 0; l < path.levels.Length; l++)
                {
                    int pathIndex = p;
                    int levelIndex = l;
                    TowerUpgradeLevel level = path.levels[l];

                    ResearchNodeView node = Instantiate(nodePrefab, treeContent);
                    Place(node, new Vector2(PathNodeX(rootX, l), y));

                    Sprite icon = level.pathShopIcon != null && level.pathShopIcon.Length > 0
                        ? level.pathShopIcon[0]
                        : IconOf(tower);

                    node.Setup(icon, (l + 1).ToString(), () => SelectNode(pathIndex, levelIndex));

                    ResearchStatus status = Manager.GetUpgradeStatus(tower, p, l);
                    node.SetState(
                        status == ResearchStatus.Researched ? ResearchNodeView.State.Researched :
                        status == ResearchStatus.Available ? ResearchNodeView.State.Available :
                        ResearchNodeView.State.Locked,
                        Manager.CanAffordResearch(level.researchCost),
                        Texts.Research.Cost(level.researchCost));

                    _nodes[(p, l)] = node;
                }
            }

            // Auswahl erhalten (Pfad/Stufe kann nach Turmwechsel nicht existieren -> Wurzel)
            if (!_nodes.ContainsKey((_selectedPath, _selectedLevel)))
            {
                _selectedPath = RootPath;
                _selectedLevel = 0;
            }

            _nodes[(_selectedPath, _selectedLevel)].SetSelected(true);
        }

        private float PathNodeX(float rootX, int level)
        {
            return rootX + nodeSpacingX * (level + 1);
        }

        private static void Place(ResearchNodeView node, Vector2 position)
        {
            RectTransform rect = (RectTransform)node.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
        }

        /// <summary>Waagrechte oder senkrechte Linie zwischen zwei Punkten.</summary>
        private void CreateLine(Vector2 from, Vector2 to, bool active)
        {
            GameObject go = new GameObject("Line", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(treeContent, false);

            Image image = go.GetComponent<Image>();
            image.color = active ? lineActiveColor : lineInactiveColor;
            image.raycastTarget = false;

            RectTransform rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = (from + to) * 0.5f;
            rect.sizeDelta = new Vector2(
                Mathf.Max(Mathf.Abs(to.x - from.x), lineThickness),
                Mathf.Max(Mathf.Abs(to.y - from.y), lineThickness));
        }

        // ── Details ───────────────────────────────────────────────

        private void RefreshDetails()
        {
            TowerResearchInfo tower = string.IsNullOrEmpty(_selectedTower) ? null : Manager.FindTower(_selectedTower);

            if (tower == null)
            {
                txtTitle.text = "";
                txtSubtitle.text = "";
                txtDescription.text = "";
                txtStats.text = "";
                txtHint.text = "";
                btnAction.gameObject.SetActive(false);
                return;
            }

            btnAction.gameObject.SetActive(true);

            if (_selectedPath == RootPath)
                ShowTowerDetails(tower);
            else
                ShowUpgradeDetails(tower, _selectedPath, _selectedLevel);
        }

        private void ShowTowerDetails(TowerResearchInfo tower)
        {
            bool unlocked = Manager.IsTowerUnlocked(tower.towerName);
            int cost = Manager.GetTowerUnlockCost(tower);

            txtTitle.text = tower.towerName;
            txtSubtitle.text = unlocked ? Texts.Common.Unlocked : Texts.Common.Locked;
            txtDescription.text = tower.description;
            txtStats.text = BuildBaseStats(tower);
            txtHint.color = badColor;

            if (unlocked)
            {
                txtHint.text = Manager.IsStarterTower(tower.towerName) ? Texts.Research.StarterHint : "";
                SetAction(Texts.Research.ResearchedButton, false);
                return;
            }

            bool affordable = Manager.CanAffordResearch(cost);
            txtHint.text = affordable ? "" : Texts.Research.MorePointsNeeded(cost - Manager.researchPoints);
            SetAction(Texts.Research.UnlockButton(cost), affordable);
        }

        private void ShowUpgradeDetails(TowerResearchInfo tower, int pathIndex, int levelIndex)
        {
            UpgradePath path = tower.upgradePaths[pathIndex];
            TowerUpgradeLevel level = path.levels[levelIndex];
            ResearchStatus status = Manager.GetUpgradeStatus(tower, pathIndex, levelIndex);

            txtTitle.text = level.upgradeName;
            txtSubtitle.text = Texts.Research.UpgradeSubtitle(tower.towerName, path.towerName, levelIndex + 1, path.levels.Length);
            txtDescription.text = level.description;
            txtStats.text = BuildUpgradeStats(level);

            txtHint.color = badColor;

            switch (status)
            {
                case ResearchStatus.Researched:
                    txtHint.text = "";
                    SetAction(Texts.Research.ResearchedButton, false);
                    break;

                case ResearchStatus.TowerLocked:
                    txtHint.text = Texts.Research.UnlockTowerFirst(tower.towerName);
                    SetAction(Texts.Research.Cost(level.researchCost), false);
                    break;

                case ResearchStatus.PreviousLocked:
                    txtHint.text = Texts.Research.ResearchPreviousFirst;
                    SetAction(Texts.Research.Cost(level.researchCost), false);
                    break;

                default:
                    bool affordable = Manager.CanAffordResearch(level.researchCost);
                    txtHint.text = affordable ? "" : Texts.Research.MorePointsNeeded(level.researchCost - Manager.researchPoints);
                    SetAction(Texts.Research.ResearchButton(level.researchCost), affordable);
                    break;
            }
        }

        private void SetAction(string label, bool interactable)
        {
            txtAction.text = label;
            btnAction.interactable = interactable;
        }

        // ── Aktionen ──────────────────────────────────────────────

        private void OnActionClicked()
        {
            if (string.IsNullOrEmpty(_selectedTower)) return;

            // Das Changed-Event baut synchron neu auf (RefreshAll) und lässt dabei den Knoten aufpoppen
            _popAfterRebuild = (_selectedPath, _selectedLevel);

            bool success = _selectedPath == RootPath
                ? Manager.TryUnlockTower(_selectedTower)
                : Manager.TryResearchUpgrade(_selectedTower, _selectedPath, _selectedLevel);

            if (!success)
                _popAfterRebuild = null;
        }

        private void OnResetClicked()
        {
            if (confirmDialog == null)
            {
                Manager.ResetResearchData();
                return;
            }

            confirmDialog.Open(Texts.Research.ResetConfirm, Manager.ResetResearchData);
        }

        // ── Texte ─────────────────────────────────────────────────

        private string BuildBaseStats(TowerResearchInfo t)
        {
            var sb = new StringBuilder();
            Line(sb, Texts.Stats.Damage, t.baseDamage, "", 0, false);
            Line(sb, Texts.Stats.Range, t.baseRange, "", 1, false);
            Line(sb, Texts.Stats.AttackSpeed, t.baseAttackSpeed, "/s", 1, false);
            Line(sb, Texts.Stats.Health, t.baseHealth, "", 0, false);
            Line(sb, Texts.Stats.GoldPerSecond, t.baseGoldPerSecond, "/s", 1, false);
            return sb.ToString().TrimEnd();
        }

        private string BuildUpgradeStats(TowerUpgradeLevel u)
        {
            // bonus kann fehlen; gleiche Stats aus Upgrade und Bonus werden zu einer Zeile summiert
            StatBonus b = u.bonus ?? new StatBonus();
            var sb = new StringBuilder();

            Line(sb, Texts.Stats.Damage, u.damageIncrease + b.damage, "", 0, true);
            Line(sb, Texts.Stats.Range, u.rangeIncrease + b.range, "", 1, true);
            Line(sb, Texts.Stats.AttackSpeed, u.attackSpeedIncrease, "", 1, true);
            Line(sb, Texts.Stats.Cooldown, -(u.timeInBetweenShotsDecrease + b.cooldownDecrease), "s", 2, true, lowerIsBetter: true);
            Line(sb, Texts.Stats.Health, u.healthpoints + b.health, "", 0, true);
            Line(sb, Texts.Stats.Regen, u.healthRegenPerSecond + b.healthRegen, "/s", 1, true);
            Line(sb, Texts.Stats.GoldPerSecond, u.coinsEarnedPerSecond, "/s", 1, true);

            Line(sb, Texts.Stats.AuraRange, u.rangeMultiplier * 100f, "%", 0, true);
            Line(sb, Texts.Stats.AuraDamage, u.dmgMultiplier * 100f, "%", 0, true);
            Line(sb, Texts.Stats.AuraAttackSpeed, u.attackSpeedMultiplier * 100f, "%", 0, true);
            Line(sb, Texts.Stats.AuraSlow, u.slowMultiplier * 100f, "%", 0, true);
            Line(sb, Texts.Stats.AuraRegen, u.regenMultiplier * 100f, "%", 0, true);
            Line(sb, Texts.Stats.AuraOmnivamp, u.omnivampMultiplier * 100f, "%", 0, true);

            Line(sb, Texts.Stats.Armor, b.armor, "", 0, true);
            Line(sb, Texts.Stats.MagicResist, b.magicResist, "", 0, true);
            Line(sb, Texts.Stats.CritChance, b.critChance * 100f, "%", 0, true);
            Line(sb, Texts.Stats.CritDamage, b.critDamage * 100f, "%", 0, true);
            Line(sb, Texts.Stats.Lethality, b.lethality, "", 0, true);
            Line(sb, Texts.Stats.ArmorPen, b.armorPenPercent * 100f, "%", 0, true);
            Line(sb, Texts.Stats.MagicPen, b.magicPenFlat, "", 0, true);
            Line(sb, Texts.Stats.MagicPen, b.magicPenPercent * 100f, "%", 0, true);
            Line(sb, Texts.Stats.Omnivamp, b.omnivamp * 100f, "%", 0, true);

            return sb.Length == 0 ? Texts.Research.NoStatChanges : sb.ToString().TrimEnd();
        }

        /// <summary>
        /// Zeile "Label  Wert". Mit delta = true mit Vorzeichen und Farbe (grün = besser, rot = schlechter).
        /// lowerIsBetter: kleinere Werte sind besser (z. B. Cooldown). 0-Werte entfallen.
        /// </summary>
        private void Line(StringBuilder sb, string label, float value, string suffix, int decimals,
            bool delta, bool lowerIsBetter = false)
        {
            if (Mathf.Approximately(value, 0f)) return;

            string number = value.ToString(decimals <= 0 ? "0" : "0." + new string('0', decimals),
                System.Globalization.CultureInfo.InvariantCulture);

            if (!delta)
            {
                sb.Append(label).Append("   ").Append(number).Append(suffix).Append('\n');
                return;
            }

            bool better = lowerIsBetter ? value < 0f : value > 0f;
            string color = ColorUtility.ToHtmlStringRGB(better ? goodColor : badColor);
            string sign = value > 0f ? "+" : "";

            sb.Append(label).Append("   <color=#").Append(color).Append('>')
              .Append(sign).Append(number).Append(suffix).Append("</color>\n");
        }

        // ── Hilfen ────────────────────────────────────────────────

        private static Sprite IconOf(TowerResearchInfo tower)
        {
            return tower.sprites != null && tower.sprites.Length > 0 ? tower.sprites[0] : null;
        }

        private static void Clear(Transform container)
        {
            if (container == null) return;

            for (int i = container.childCount - 1; i >= 0; i--)
            {
                Transform child = container.GetChild(i);
                child.SetParent(null);
                Destroy(child.gameObject);
            }
        }
    }
}