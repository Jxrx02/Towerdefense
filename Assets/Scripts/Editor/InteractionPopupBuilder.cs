#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace TowerDefense.EditorTools
{
    /// <summary>
    /// Baut das InteractionPopup neu auf: eine saubere Hierarchie mit Layout-Gruppen statt überlappender Elemente.
    ///
    ///   Info-Karte       Titel + Kosten, farbige Linie, Beschreibung, Stat-Zeilen, Level/XP/Ziel/Verkaufen
    ///   Auswahl-Karte    Türme bzw. Upgrade-Pfade als Raster (eigene Karte, überlappt nichts)
    ///   Prompt           "Press [E] to ..." mit den Münzen für das Gedrückthalten
    ///
    /// Das bestehende Popup bleibt erhalten und wird nur deaktiviert. Alle Einstellungen (Turm-Prefabs, Icons,
    /// Münz- und Auswahl-Prefab) werden in das neue Popup kopiert, danach lässt sich das alte löschen.
    /// Aufruf: Tools > UI > Interaction Popup neu aufbauen
    /// </summary>
    public static class InteractionPopupBuilder
    {
        // ---------- Stil (hier anpassen) ----------
        private const float CardWidth = 380f;
        private const int TitleSize = 16;
        private const int BodySize = 12;
        private const int SmallSize = 12;
        private const int StatSize = 12;
        private const int PromptSize = 16;
        

        private const float CoinSize = 20f;
        
private const float SelectionCellWidth = 114f;
private const float SelectionCellHeight = 84f;

        private const int SelectionColumns = 3;

        private const int TargetColumns = 2;
        private const float TargetCellHeight = 22f;
        private const float TargetCellWidth = 92f;
        private const float TargetCardGap = 18f;   // Abstand rechts neben der Info-Karte
        
        private const string PrefabFolder = "Assets/UI/Generated";

        private static readonly Color BorderColor = new Color(0.62f, 0.46f, 0.27f, 0.70f);   // Bronze  #9E7645
        private static readonly Color PanelColor  = new Color(0.12f, 0.09f, 0.07f, 0.95f);   // Dunkelbraun #1F1712
        private static readonly Color TextColor   = new Color(0.96f, 0.92f, 0.82f);          // Pergament #F5EBD1
        private static readonly Color DimColor    = new Color(0.74f, 0.67f, 0.55f);          // Sand #BDAB8C
        private static readonly Color GoldColor   = new Color(1f, 0.78f, 0.29f);             // Bernstein #FFC74A
        private static readonly Color BarBackColor = new Color(0.16f, 0.12f, 0.09f, 1f);     // #291F17
        private static readonly Color XpColor     = new Color(0.62f, 0.80f, 0.38f);          // Blattgrün #9ECC61
        private static readonly Color ButtonColor = new Color(0.27f, 0.20f, 0.14f, 1f);      // Holz #453324
        private static readonly Color SellColor = new Color(0.30f, 0.16f, 0.13f, 1f);   // dunkles Rostbraun statt Rostrot
        private static readonly Color KeyColor    = new Color(0.17f, 0.13f, 0.10f, 1f);      // #2B211A
        private static readonly Color DividerColor = new Color(0.62f, 0.46f, 0.27f, 0.35f);  // Bronze, blass
        private static readonly Color SelectedColor = new Color(0.82f, 0.55f, 0.20f, 1f);    // Kupfer #D18C33

        private class TowerInfo
        {
            public GameObject root, targetRoot, sellRoot, targetCard;
            public RectTransform targetGrid;
            public Text level, experience, target, targetArrow, sellPrice;
            public Slider experienceBar;
            public Button toggle, sell;
        }
        // ------------------------------------------------------------------
        [MenuItem("Tools/UI/Interaction Popup neu aufbauen")]
        private static void Build()
        {
            InteractionPopup[] all = Object.FindObjectsByType<InteractionPopup>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            if (all.Length == 0)
            {
                EditorUtility.DisplayDialog("Interaction Popup",
                    "In der Szene wurde kein InteractionPopup gefunden. Öffne die Szene mit dem bisherigen Popup.", "OK");
                return;
            }

            InteractionPopup old = System.Array.Find(all, p => !p.name.Contains("(neu)")) ?? all[0];
            Font font = FindFont(old);

            // ---------- Wurzel ----------
            GameObject root = new GameObject("InteractionPopup (neu)", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(root, "Interaction Popup neu aufbauen");
            root.transform.SetParent(old.transform.parent, false);
            root.transform.SetSiblingIndex(old.transform.GetSiblingIndex() + 1);

            RectTransform rootRect = (RectTransform)root.transform;
            rootRect.anchorMin = rootRect.anchorMax = new Vector2(0.5f, 0.5f);
            rootRect.pivot = new Vector2(0.5f, 0f);   // wächst von der Position des Objekts nach oben
            rootRect.sizeDelta = new Vector2(CardWidth, 0f);
            rootRect.anchoredPosition = Vector2.zero;

            VerticalLayoutGroup rootLayout = AddVertical(root, new RectOffset(0, 0, 0, 0), 6f);
            rootLayout.childAlignment = TextAnchor.LowerCenter;

            ContentSizeFitter fitter = root.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            // Einstellungen des alten Popups übernehmen (Prefabs, Icons, Tasten), UI-Referenzen werden unten ersetzt
            InteractionPopup popup = root.AddComponent<InteractionPopup>();
            EditorUtility.CopySerialized(old, popup);

            // ---------- Info-Karte ----------
            RectTransform info = CreateCard("InfoCard", root.transform, out GameObject infoCard);

            RectTransform header = NewRect("Header", info);
            AddVertical(header.gameObject, new RectOffset(0, 0, 0, 0), 3f);

            Text title = NewText("Title", header, font, TitleSize, TextColor, TextAnchor.UpperLeft, FontStyle.Bold);
            title.lineSpacing = 1.3f;

            Text cost = NewText("Cost", header, font, BodySize + 2, GoldColor, TextAnchor.UpperLeft, FontStyle.Bold);

            Image accent = NewImage("Accent", info, BorderColor);
            SetSize(accent.gameObject, height: 2f);

            Text description = NewText("Description", info, font, BodySize, DimColor, TextAnchor.UpperLeft);
            description.lineSpacing = 1.4f;

            RectTransform stats = NewRect("Stats", info);
            AddVertical(stats.gameObject, new RectOffset(0, 0, 0, 0), 2f);

            TowerInfo towerInfo = CreateTowerInfo(info, font);

            // ---------- Auswahl-Karte ----------
            RectTransform selection = CreateCard("SelectionCard", root.transform, out GameObject selectionCard);

            RectTransform grid = NewRect("Entries", selection);
            GridLayoutGroup gridLayout = grid.gameObject.AddComponent<GridLayoutGroup>();
            gridLayout.cellSize = new Vector2(SelectionCellWidth, SelectionCellHeight);
            gridLayout.spacing = new Vector2(4f, 4f);
            gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gridLayout.constraintCount = SelectionColumns;
            gridLayout.childAlignment = TextAnchor.UpperCenter;

            // ---------- Prompt (Press [E] ...) ----------
            RectTransform prompt = NewRect("Prompt", root.transform);
            VerticalLayoutGroup promptLayout = AddVertical(prompt.gameObject, new RectOffset(0, 0, 0, 0), 4f);
            promptLayout.childAlignment = TextAnchor.UpperCenter;

            RectTransform row = NewRect("Row", prompt);
            AddHorizontal(row.gameObject, new RectOffset(0, 0, 0, 0), 6f, TextAnchor.MiddleCenter);

            Text press = NewText("Press", row, font, PromptSize, TextColor, TextAnchor.MiddleCenter);
            press.text = "Press";
            press.horizontalOverflow = HorizontalWrapMode.Overflow;
            AddOutline(press);

            RectTransform keyBorder = NewImage("Key", row, new Color(0.96f, 0.92f, 0.82f, 0.9f)).rectTransform;
            AddVertical(keyBorder.gameObject, new RectOffset(1, 1, 1, 1), 0f);
            SetSize(keyBorder.gameObject, 22f, 22f, flexibleWidth: 0f);
            Image keyInner = NewImage("Inner", keyBorder, KeyColor);
            LayoutElement keyFill = keyInner.gameObject.AddComponent<LayoutElement>();
            keyFill.minWidth = 20f;
            keyFill.minHeight = 20f;
            keyFill.flexibleWidth = 0f;
            keyFill.flexibleHeight = 1f;
            Text keyText = NewText("E", keyInner.transform, font, PromptSize - 2, TextColor, TextAnchor.MiddleCenter, FontStyle.Bold);
            Stretch(keyText.rectTransform);
            keyText.text = "E";

            Text interaction = NewText("Interaction", row, font, PromptSize, TextColor, TextAnchor.MiddleCenter);
            interaction.horizontalOverflow = HorizontalWrapMode.Overflow;
            AddOutline(interaction);

            RectTransform coins = NewRect("Coins", prompt);
            GridLayoutGroup coinLayout = coins.gameObject.AddComponent<GridLayoutGroup>();
            coinLayout.cellSize = new Vector2(CoinSize, CoinSize);
            coinLayout.spacing = new Vector2(3f, 3f);
            coinLayout.childAlignment = TextAnchor.UpperCenter;

            // ---------- Prefab für die Stat-Zeilen ----------
            UpgradeStatEntry statPrefab = CreateStatRowPrefab(font);

            // ---------- Verdrahten ----------
            SerializedObject so = new SerializedObject(popup);

            Set(so, "coinContainer", coins);
            Set(so, "upgradeInfoPanel", infoCard);
            Set(so, "upgradeTitle", title);
            Set(so, "upgradeDescription", description);
            Set(so, "upgradeCost", cost);
            Set(so, "upgradeStatsContainer", stats);
            Set(so, "upgradeStatPrefab", statPrefab);
            Set(so, "interactionText", interaction);

            Set(so, "towerSelectionPanel", selectionCard);
            Set(so, "towerSelectionContainer", grid);

            Set(so, "towerInfoRoot", towerInfo.root);

            Set(so, "txtLevel", towerInfo.level);
            Set(so, "experienceBar", towerInfo.experienceBar);
            Set(so, "experienceText", towerInfo.experience);
            Set(so, "targetTypeRoot", towerInfo.targetRoot);
            Set(so, "txtTargetPreference", towerInfo.target);
            Set(so, "btnTargetToggle", towerInfo.toggle);
            Set(so, "txtTargetArrow", towerInfo.targetArrow);
            Set(so, "targetPanel", towerInfo.targetCard);
            Set(so, "targetGrid", towerInfo.targetGrid);
            Set(so, "sellRoot", towerInfo.sellRoot);
            Set(so, "btnSell", towerInfo.sell);
            Set(so, "txtSellPrice", towerInfo.sellPrice);
            SetColor(so, "targetCellColor", ButtonColor);
            SetColor(so, "targetCellSelectedColor", SelectedColor);
            Set(so, "promptRoot", prompt.gameObject);
            Set(so, "towerSelectionEntryPrefab", CreateSelectionEntryPrefab(font));
            so.ApplyModifiedProperties();

            // Das alte Popup bleibt als Vorlage erhalten, ist aber aus (sonst gäbe es zwei Instanzen)
            Undo.RecordObject(old.gameObject, "Altes Popup deaktivieren");
            old.gameObject.SetActive(false);

            EditorSceneManager.MarkSceneDirty(root.scene);
            Selection.activeGameObject = root;

            Debug.Log("InteractionPopup neu aufgebaut. Das alte Popup ist deaktiviert und kann gelöscht werden, " +
                      "sobald du das neue geprüft hast. Zellgröße der Auswahl: Objekt 'Entries' (GridLayoutGroup).");
        }
        private static void SetColor(SerializedObject so, string field, Color value)
        {
            SerializedProperty property = so.FindProperty(field);
            if (property != null) property.colorValue = value;
        }
        // ------------------------------------------------------------------
        // Bereiche
        // ------------------------------------------------------------------
      private static TowerInfo CreateTowerInfo(Transform parent, Font font)
{
    var info = new TowerInfo();

    // ---- Level + XP ----
    RectTransform root = NewRect("TowerInfo", parent);
    AddVertical(root.gameObject, new RectOffset(0, 0, 0, 0), 5f);
    info.root = root.gameObject;

    Image divider = NewImage("Divider", root, DividerColor);
    SetSize(divider.gameObject, height: 1f);

    RectTransform levelRow = NewRect("LevelRow", root);
    AddHorizontal(levelRow.gameObject, new RectOffset(0, 0, 0, 0), 8f, TextAnchor.MiddleLeft);

    info.level = NewText("Level", levelRow, font, BodySize + 1, TextColor, TextAnchor.MiddleLeft, FontStyle.Bold);
    SetSize(info.level.gameObject, flexibleWidth: 1f);

    info.experience = NewText("Experience", levelRow, font, SmallSize, DimColor, TextAnchor.MiddleRight);
    info.experience.horizontalOverflow = HorizontalWrapMode.Overflow;

    info.experienceBar = NewSlider("ExperienceBar", root, BarBackColor, XpColor, 6f);

    // ---- Ziel: Button + aufklappende Karte rechts daneben ----
    RectTransform targetRow = NewRect("TargetRow", parent);
    AddVertical(targetRow.gameObject, new RectOffset(0, 0, 0, 0), 0f);
    info.targetRoot = targetRow.gameObject;

    info.toggle = NewButton("TargetToggle", targetRow, ButtonColor);
    AddHorizontal(info.toggle.gameObject, new RectOffset(8, 8, 4, 4), 6f, TextAnchor.MiddleCenter);

    Text label = NewText("Label", info.toggle.transform, font, SmallSize, DimColor, TextAnchor.MiddleLeft);
    label.text = "Target";
    label.horizontalOverflow = HorizontalWrapMode.Overflow;

    info.target = NewText("Value", info.toggle.transform, font, BodySize, TextColor, TextAnchor.MiddleCenter, FontStyle.Bold);
    SetSize(info.target.gameObject, flexibleWidth: 1f);

    info.targetArrow = NewText("Arrow", info.toggle.transform, font, BodySize, DimColor, TextAnchor.MiddleRight);
    info.targetArrow.text = ">";
    info.targetArrow.horizontalOverflow = HorizontalWrapMode.Overflow;

    RectTransform cardContent = CreateCard("TargetCard", targetRow, out GameObject card);

    card.AddComponent<LayoutElement>().ignoreLayout = true;   // schiebt nichts im Layout
    RectTransform cardRect = (RectTransform)card.transform;
    cardRect.anchorMin = cardRect.anchorMax = new Vector2(1f, 0.5f);
    cardRect.pivot = new Vector2(0f, 0.5f);
    cardRect.anchoredPosition = new Vector2(TargetCardGap, 0f);

    ContentSizeFitter cardFitter = card.AddComponent<ContentSizeFitter>();
    cardFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
    cardFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

    RectTransform grid = NewRect("Entries", cardContent);
    GridLayoutGroup gridLayout = grid.gameObject.AddComponent<GridLayoutGroup>();
    gridLayout.cellSize = new Vector2(TargetCellWidth, TargetCellHeight);
    gridLayout.spacing = new Vector2(4f, 4f);
    gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
    gridLayout.constraintCount = TargetColumns;

    foreach (TargetType type in InteractionPopup.TargetOrder)
        CreateTargetCell(grid, font, type);

    card.SetActive(false);
    info.targetCard = card;
    info.targetGrid = grid;

    // ---- Verkaufen (auch bei Mauern) ----
    Image sellDivider = NewImage("SellDivider", parent, DividerColor);
    SetSize(sellDivider.gameObject, height: 1f);
    
    info.sell = NewButton("Sell", parent, SellColor);
    AddHorizontal(info.sell.gameObject, new RectOffset(8, 8, 5, 5), 6f, TextAnchor.MiddleCenter);
    info.sellRoot = info.sell.gameObject;

    Text sellLabel = NewText("Label", info.sell.transform, font, BodySize, TextColor, TextAnchor.MiddleLeft, FontStyle.Bold);
    sellLabel.text = "Sell";
    SetSize(sellLabel.gameObject, flexibleWidth: 1f);

    info.sellPrice = NewText("Price", info.sell.transform, font, BodySize, GoldColor, TextAnchor.MiddleRight, FontStyle.Bold);
    info.sellPrice.horizontalOverflow = HorizontalWrapMode.Overflow;

    return info;
}
private static void CreateTargetCell(Transform parent, Font font, TargetType type)
{
    Button button = NewButton(type.ToString(), parent, ButtonColor);

    Text label = NewText("Label", button.transform, font, BodySize, TextColor, TextAnchor.MiddleCenter);
    label.text = InteractionPopup.TargetLabel(type);
    Stretch(label.rectTransform);
}
        private static UpgradeStatEntry CreateStatRowPrefab(Font font)
        {
            EnsureFolder(PrefabFolder);

            GameObject row = new GameObject("StatRow", typeof(RectTransform));
            AddHorizontal(row, new RectOffset(0, 0, 1, 1), 5f, TextAnchor.MiddleLeft);
            SetSize(row, height: 22f);

            Image icon = NewImage("Icon", row.transform, Color.white);
            icon.preserveAspect = true;
            SetSize(icon.gameObject, 18f, 18f);

            Text oldValue = NewText("Old", row.transform, font, StatSize, DimColor, TextAnchor.MiddleLeft);
            oldValue.horizontalOverflow = HorizontalWrapMode.Overflow;

            Text arrow = NewText("Arrow", row.transform, font, StatSize, DimColor, TextAnchor.MiddleCenter);
            arrow.text = "›";
            arrow.horizontalOverflow = HorizontalWrapMode.Overflow;

            Text newValue = NewText("New", row.transform, font, StatSize, TextColor, TextAnchor.MiddleLeft, FontStyle.Bold);
            newValue.horizontalOverflow = HorizontalWrapMode.Overflow;

            Text label = NewText("Label", row.transform, font, SmallSize, DimColor, TextAnchor.MiddleLeft);
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            SetSize(label.gameObject, flexibleWidth: 1f);

            UpgradeStatEntry entry = row.AddComponent<UpgradeStatEntry>();
            SerializedObject so = new SerializedObject(entry);
            Set(so, "statIcon", icon);
            Set(so, "oldValueText", oldValue);
            Set(so, "newValueText", newValue);
            Set(so, "suffixNewValueText", label);
            Set(so, "arrow", arrow);
            so.ApplyModifiedPropertiesWithoutUndo();

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(row, $"{PrefabFolder}/StatRow.prefab");
            Object.DestroyImmediate(row);

            return saved.GetComponent<UpgradeStatEntry>();
        }
        private static TowerSelectionEntry CreateSelectionEntryPrefab(Font font)
        {
            EnsureFolder(PrefabFolder);

            GameObject root = new GameObject("SelectionEntry", typeof(RectTransform));
            Image background = root.AddComponent<Image>();
            background.color = ButtonColor;                      // raycastTarget bleibt an (Hover/Klick)
            AddVertical(root, new RectOffset(4, 4, 6, 4), 3f);

            // Auswahlrahmen: liegt hinter dem Inhalt und zählt nicht im Layout
            Image frame = NewImage("SelectedFrame", root.transform, SelectedColor);
            frame.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            Stretch(frame.rectTransform);
            frame.transform.SetAsFirstSibling();

            Image fill = NewImage("Fill", frame.transform, ButtonColor);
            Stretch(fill.rectTransform);
            fill.rectTransform.offsetMin = new Vector2(2f, 2f);
            fill.rectTransform.offsetMax = new Vector2(-2f, -2f);

            Image icon = NewImage("Icon", root.transform, Color.white);
            icon.preserveAspect = true;
            SetSize(icon.gameObject, height: 44f);

            Text label = NewText("Name", root.transform, font, 8, TextColor, TextAnchor.UpperCenter);
            label.lineSpacing = 1.3f;
            SetSize(label.gameObject, height: 24f);

            TowerSelectionEntry entry = root.AddComponent<TowerSelectionEntry>();
            SerializedObject so = new SerializedObject(entry);
            Set(so, "towerIcon", icon);
            Set(so, "towerName", label);
            Set(so, "selectedFrame", frame.gameObject);
            so.ApplyModifiedPropertiesWithoutUndo();

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabFolder}/SelectionEntry.prefab");
            Object.DestroyImmediate(root);

            return saved.GetComponent<TowerSelectionEntry>();
        }
        // ------------------------------------------------------------------
        // Bausteine
        // ------------------------------------------------------------------

        /// <summary>Karte mit 1 px Rahmen. Gibt den Inhaltsbereich zurück, 'outer' ist die ganze Karte (zum Ein-/Ausblenden).</summary>
        private static RectTransform CreateCard(string name, Transform parent, out GameObject outer)
        {
            Image border = NewImage(name, parent, BorderColor, true);
            AddVertical(border.gameObject, new RectOffset(1, 1, 1, 1), 0f);
            outer = border.gameObject;

            Image inner = NewImage("Content", border.transform, PanelColor, true);
            AddVertical(inner.gameObject, new RectOffset(10, 10, 8, 10), 6f);

            return inner.rectTransform;
        }

        private static Slider NewSlider(string name, Transform parent, Color back, Color fill, float height)
        {
            Image background = NewImage(name, parent, back);
            SetSize(background.gameObject, height: height);

            RectTransform fillArea = NewRect("Fill Area", background.transform);
            Stretch(fillArea);

            Image fillImage = NewImage("Fill", fillArea, fill);
            Stretch(fillImage.rectTransform);

            Slider slider = background.gameObject.AddComponent<Slider>();
            slider.fillRect = fillImage.rectTransform;
            slider.direction = Slider.Direction.LeftToRight;
            slider.interactable = false;
            slider.transition = Selectable.Transition.None;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 0f;

            return slider;
        }

        private static Button NewButton(string name, Transform parent, Color color)
        {
            Image image = NewImage(name, parent, color, true);

            Button button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;

            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.25f, 1.25f, 1.25f, 1f);
            colors.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(1f, 1f, 1f, 0.4f);
            colors.fadeDuration = 0.05f;
            button.colors = colors;

            return button;
        }

        private static Button NewArrowButton(string name, Transform parent, Font font, string symbol)
        {
            Button button = NewButton(name, parent, ButtonColor);
            SetSize(button.gameObject, 22f, 20f);

            Text label = NewText("Label", button.transform, font, BodySize, TextColor, TextAnchor.MiddleCenter, FontStyle.Bold);
            label.text = symbol;
            Stretch(label.rectTransform);

            return button;
        }

        private static RectTransform NewRect(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static Image NewImage(string name, Transform parent, Color color, bool raycast = false)
        {
            RectTransform rect = NewRect(name, parent);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = raycast;
            return image;
        }

        private static Text NewText(string name, Transform parent, Font font, int size, Color color,
            TextAnchor anchor, FontStyle style = FontStyle.Normal)
        {
            RectTransform rect = NewRect(name, parent);
            Text text = rect.gameObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.color = color;
            text.alignment = anchor;
            text.fontStyle = style;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.supportRichText = true;
            text.raycastTarget = false;
            return text;
        }

        private static VerticalLayoutGroup AddVertical(GameObject go, RectOffset padding, float spacing)
        {
            VerticalLayoutGroup layout = go.AddComponent<VerticalLayoutGroup>();
            layout.padding = padding;
            layout.spacing = spacing;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            return layout;
        }

        private static HorizontalLayoutGroup AddHorizontal(GameObject go, RectOffset padding, float spacing, TextAnchor alignment)
        {
            HorizontalLayoutGroup layout = go.AddComponent<HorizontalLayoutGroup>();
            layout.padding = padding;
            layout.spacing = spacing;
            layout.childAlignment = alignment;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            return layout;
        }

        private static void SetSize(GameObject go, float width = -1f, float height = -1f, float flexibleWidth = -1f)
        {
            LayoutElement element = go.GetComponent<LayoutElement>();
            if (element == null)
                element = go.AddComponent<LayoutElement>();

            if (width >= 0f) { element.minWidth = width; element.preferredWidth = width; }
            if (height >= 0f) { element.minHeight = height; element.preferredHeight = height; }
            if (flexibleWidth >= 0f) element.flexibleWidth = flexibleWidth;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        /// <summary>Dunkle Kontur, damit Text auch auf hellem Gras lesbar bleibt.</summary>
        private static void AddOutline(Text text)
        {
            Outline outline = text.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            outline.effectDistance = new Vector2(1f, -1f);
        }

        private static void Set(SerializedObject so, string field, Object value)
        {
            SerializedProperty property = so.FindProperty(field);

            if (property == null)
            {
                Debug.LogWarning($"InteractionPopupBuilder: Feld '{field}' nicht gefunden.");
                return;
            }

            property.objectReferenceValue = value;
        }

        /// <summary>Schrift des bisherigen Popups übernehmen (Pixel-Font), sonst Arial.</summary>
        private static Font FindFont(InteractionPopup old)
        {
            SerializedObject so = new SerializedObject(old);

            foreach (string field in new[] { "upgradeTitle", "interactionText", "upgradeDescription", "txtLevel" })
            {
                SerializedProperty property = so.FindProperty(field);
                Text text = property != null ? property.objectReferenceValue as Text : null;

                if (text != null && text.font != null)
                    return text.font;
            }

            return Font.CreateDynamicFontFromOSFont("Arial", 16);
        }

        private static void EnsureFolder(string folder)
        {
            string[] parts = folder.Split('/');
            string current = parts[0];

            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];

                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);

                current = next;
            }
        }
    }
}
#endif