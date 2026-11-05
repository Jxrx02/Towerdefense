#if UNITY_EDITOR
using LevelSelector;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TowerDefense.EditorTools
{
    /// <summary>
    /// Baut das komplette Pausemenü im Stil des InteractionPopup:
    ///
    ///   MainPanel     Links:  Titel, Continue / Settings / Restart / Quit, Wave-Browser
    ///                 Mitte:  Loadout (Waffe, Perks, Mutatoren + Detailtext), Quests
    ///                 Rechts: Score-Zusammensetzung
    ///   SettingsPanel Tabs Video / Audio / Gameplay / Controls (inkl. Rebinding-Liste)
    ///   ConfirmDialog "Wirklich beenden?"
    ///
    /// Erzeugt zusätzlich die Prefabs (Loadout-Icon, Quest, Quest-Bedingungs-Icon, Binding-Zeile),
    /// verdrahtet alle Felder von PauseMenuUI / SettingsPanel / ConfirmDialog / PauseHotkey und
    /// trägt das neue Canvas im LevelManager als pauseCanvas ein. Das alte Pause-Canvas wird deaktiviert.
    ///
    /// Aufruf: Tools > UI > Pausemenü neu aufbauen   (kann beliebig oft wiederholt werden)
    /// </summary>
    public static class PauseMenuBuilder
    {
        // ---------- Stil (hier anpassen) ----------
        private const int TitleSize = 44;
        private const int HeaderSize = 16;
        private const int BodySize = 20;
        private const int SmallSize = 16;
        private const int ButtonSize = 22;

        private const float LeftWidth = 420f;
        private const float MiddleWidth = 440f;
        private const float RightWidth = 340f;
        private const float SettingsWidth = 900f;
        private const float SettingsPagesHeight = 440f;

        private const float IconSize = 56f;

        private const string PrefabFolder = "Assets/UI/Generated";
        private const string RootName = "PauseCanvas (neu)";

        // Gleiche Palette wie InteractionPopupBuilder
        private static readonly Color BorderColor   = new Color(0.62f, 0.46f, 0.27f, 0.70f);
        private static readonly Color PanelColor    = new Color(0.12f, 0.09f, 0.07f, 0.95f);
        private static readonly Color TextColor     = new Color(0.96f, 0.92f, 0.82f);
        private static readonly Color DimColor      = new Color(0.74f, 0.67f, 0.55f);
        private static readonly Color GoldColor     = new Color(1f, 0.78f, 0.29f);
        private static readonly Color BarBackColor  = new Color(0.16f, 0.12f, 0.09f, 1f);
        private static readonly Color XpColor       = new Color(0.62f, 0.80f, 0.38f);
        private static readonly Color ButtonColor   = new Color(0.27f, 0.20f, 0.14f, 1f);
        private static readonly Color KeyColor      = new Color(0.17f, 0.13f, 0.10f, 1f);
        private static readonly Color DividerColor  = new Color(0.62f, 0.46f, 0.27f, 0.35f);
        private static readonly Color SelectedColor = new Color(0.82f, 0.55f, 0.20f, 1f);
        private static readonly Color DimBackColor  = new Color(0.04f, 0.03f, 0.02f, 0.82f);

        // ------------------------------------------------------------------

        [MenuItem("Tools/UI/Pausemenü neu aufbauen")]
        private static void Build()
        {
            LevelManager levelManager = Object.FindFirstObjectByType<LevelManager>(FindObjectsInactive.Include);

            if (levelManager == null)
            {
                EditorUtility.DisplayDialog("Pausemenü",
                    "In der Szene wurde kein LevelManager gefunden. Öffne die Level-Szene.", "OK");
                return;
            }

            Font font = FindFont();

            // Vorherige Version entfernen, damit der Aufruf wiederholbar ist
            foreach (PauseMenuUI old in Object.FindObjectsByType<PauseMenuUI>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Undo.DestroyObjectImmediate(old.gameObject);
            }

            EnsureEventSystem();

            // ---------- Prefabs ----------
            GameObject conditionIconPrefab = CreateConditionIconPrefab();
            GameObject questPrefab = CreateQuestPrefab(font, conditionIconPrefab);
            GameObject loadoutButtonPrefab = CreateLoadoutButtonPrefab();
            GameObject bindingRowPrefab = CreateBindingRowPrefab(font);

            // ---------- Canvas ----------
            GameObject root = new GameObject(RootName, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(root, "Pausemenü neu aufbauen");

            Canvas canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            CanvasScaler scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            root.AddComponent<GraphicRaycaster>();
            CanvasGroup canvasGroup = root.AddComponent<CanvasGroup>();
            PauseMenuUI menu = root.AddComponent<PauseMenuUI>();

            Image dim = NewImage("Dim", root.transform, DimBackColor, true);
            Stretch(dim.rectTransform);

            // ======================================================
            // MAIN PANEL
            // ======================================================
            RectTransform main = NewRect("MainPanel", root.transform);
            Stretch(main);
            HorizontalLayoutGroup mainLayout = AddHorizontal(main.gameObject, new RectOffset(60, 60, 90, 60), 24f, TextAnchor.UpperCenter);
            mainLayout.childForceExpandHeight = false;

            // ---------- Linke Spalte ----------
            RectTransform left = NewColumn("Left", main, LeftWidth);

            Text title = NewText("Title", left, font, TitleSize, TextColor, TextAnchor.MiddleLeft, FontStyle.Bold);
            title.text = "Paused";
            title.horizontalOverflow = HorizontalWrapMode.Overflow;
            SetSize(title.gameObject, height: 72f);

            Button btnContinue = NewMenuButton(left, font, "Continue");
            Button btnSettings = NewMenuButton(left, font, "Settings");
            Button btnRestart = NewMenuButton(left, font, "Restart");
            Button btnQuit = NewMenuButton(left, font, "Quit to Map");

            RectTransform waveCard = CreateCard("WaveCard", left, out _);
            SectionHeader(waveCard, font, "Waves");

            RectTransform waveHeader = NewRect("WaveHeader", waveCard);
            AddHorizontal(waveHeader.gameObject, new RectOffset(0, 0, 0, 0), 8f, TextAnchor.MiddleCenter);
            SetSize(waveHeader.gameObject, height: 36f);

            Button btnPrevWave = NewArrowButton("Prev", waveHeader, font, "<");
            Text txtWaveTitle = NewText("Title", waveHeader, font, BodySize, TextColor, TextAnchor.MiddleCenter, FontStyle.Bold);
            SetSize(txtWaveTitle.gameObject, flexibleWidth: 1f);
            Button btnNextWave = NewArrowButton("Next", waveHeader, font, ">");

            RectTransform waveScroll = NewScroll("WaveScroll", waveCard, 330f);
            Text txtWaveInfo = NewText("Info", waveScroll, font, SmallSize, DimColor, TextAnchor.UpperLeft);
            txtWaveInfo.lineSpacing = 1.3f;

            // ---------- Mittlere Spalte ----------
            RectTransform middle = NewColumn("Middle", main, MiddleWidth);

            RectTransform loadoutCard = CreateCard("LoadoutCard", middle, out _);
            SectionHeader(loadoutCard, font, "Loadout");

            // Wie in LevelLoadoutUI: 1 Waffe, 5 Perks, 3 Mutatoren (LevelSelectorManager.StartLevel erzwingt das)
            RectTransform weaponRow = NewLoadoutGroup(loadoutCard, font, "Weapon");
            RectTransform perkRow = NewLoadoutGroup(loadoutCard, font, "Perks");
            RectTransform mutatorRow = NewLoadoutGroup(loadoutCard, font, "Mutators");

            Image loadoutDivider = NewImage("Divider", loadoutCard, DividerColor);
            SetSize(loadoutDivider.gameObject, height: 1f);

            Text detailTitle = NewText("DetailTitle", loadoutCard, font, BodySize, GoldColor, TextAnchor.UpperLeft, FontStyle.Bold);
            Text detailText = NewText("Detail", loadoutCard, font, SmallSize, DimColor, TextAnchor.UpperLeft);
            detailText.lineSpacing = 1.3f;
            MinHeight(detailText.gameObject, 96f);

            RectTransform questCard = CreateCard("QuestCard", middle, out _);
            SectionHeader(questCard, font, "Quests");
            RectTransform questScroll = NewScroll("QuestScroll", questCard, 250f, 6f);

            // ---------- Rechte Spalte ----------
            RectTransform right = NewColumn("Right", main, RightWidth);

            RectTransform scoreCard = CreateCard("ScoreCard", right, out _);
            SectionHeader(scoreCard, font, "Score");

            RectTransform scoreRow = NewRect("ScoreRow", scoreCard);
            AddHorizontal(scoreRow.gameObject, new RectOffset(0, 0, 0, 0), 10f, TextAnchor.UpperLeft);

            Text scoreLabels = NewText("Labels", scoreRow, font, SmallSize + 2, DimColor, TextAnchor.UpperLeft);
            scoreLabels.lineSpacing = 1.5f;
            SetSize(scoreLabels.gameObject, flexibleWidth: 1f);

            Text scoreValues = NewText("Values", scoreRow, font, SmallSize + 2, TextColor, TextAnchor.UpperRight);
            scoreValues.lineSpacing = 1.5f;
            SetSize(scoreValues.gameObject, width: 110f);

            // ======================================================
            // SETTINGS PANEL
            // ======================================================
            RectTransform settingsRoot = NewRect("SettingsPanel", root.transform);
            Stretch(settingsRoot);
            VerticalLayoutGroup settingsLayout = AddVertical(settingsRoot.gameObject, new RectOffset(0, 0, 0, 0), 0f);
            settingsLayout.childAlignment = TextAnchor.MiddleCenter;
            settingsLayout.childForceExpandWidth = false;
            settingsLayout.childForceExpandHeight = false;

            RectTransform settingsCard = CreateCard("Card", settingsRoot, out GameObject settingsOuter, new RectOffset(24, 24, 18, 20));
            SetSize(settingsOuter, width: SettingsWidth);

            Text settingsTitle = NewText("Title", settingsCard, font, TitleSize - 6, TextColor, TextAnchor.MiddleLeft, FontStyle.Bold);
            settingsTitle.text = "Settings";
            SetSize(settingsTitle.gameObject, height: 56f);

            RectTransform tabRow = NewRect("Tabs", settingsCard);
            AddHorizontal(tabRow.gameObject, new RectOffset(0, 0, 0, 0), 6f, TextAnchor.MiddleCenter);
            SetSize(tabRow.gameObject, height: 42f);

            string[] tabNames = { "Video", "Audio", "Gameplay", "Controls" };
            Button[] tabButtons = new Button[tabNames.Length];
            for (int i = 0; i < tabNames.Length; i++)
            {
                tabButtons[i] = NewMenuButton(tabRow, font, tabNames[i], SmallSize + 2, 42f);
                SetSize(tabButtons[i].gameObject, flexibleWidth: 1f);
            }

            Image tabDivider = NewImage("Divider", settingsCard, DividerColor);
            SetSize(tabDivider.gameObject, height: 1f);

            RectTransform pages = NewRect("Pages", settingsCard);
            SetSize(pages.gameObject, height: SettingsPagesHeight);

            // ---- Video ----
            RectTransform pageVideo = NewPage("Video", pages);
            NewArrowRow(pageVideo, font, "Resolution", out Button resPrev, out Text txtResolution, out Button resNext);
            Toggle toggleFullscreen = NewToggleRow(pageVideo, font, "Fullscreen");
            Toggle toggleVSync = NewToggleRow(pageVideo, font, "V-Sync");
            Toggle togglePost = NewToggleRow(pageVideo, font, "Post Processing");
            NewArrowRow(pageVideo, font, "Anti Aliasing", out Button aaPrev, out Text txtAa, out Button aaNext);
            Slider sliderRenderScale = NewSliderRow(pageVideo, font, "Render Scale", out Text txtRenderScale);
            Button resetVideo = NewResetRow(pageVideo, font, "Reset Video Settings");

            // ---- Audio ----
            RectTransform pageAudio = NewPage("Audio", pages);
            Slider sliderMaster = NewSliderRow(pageAudio, font, "Master", out Text txtMaster);
            Slider sliderGameplay = NewSliderRow(pageAudio, font, "Gameplay", out Text txtGameplay);
            Slider sliderMusic = NewSliderRow(pageAudio, font, "Music", out Text txtMusic);
            Slider sliderEnvironment = NewSliderRow(pageAudio, font, "Environment", out Text txtEnvironment);
            Button resetAudio = NewResetRow(pageAudio, font, "Reset Audio Settings");

            // ---- Gameplay ----
            RectTransform pageGameplay = NewPage("Gameplay", pages);
            Toggle toggleShake = NewToggleRow(pageGameplay, font, "Screen Shake");
            Toggle toggleDamage = NewToggleRow(pageGameplay, font, "Damage Numbers");
            Toggle toggleHealthBars = NewToggleRow(pageGameplay, font, "Health Bars");
            Toggle toggleAutoPause = NewToggleRow(pageGameplay, font, "Pause on Focus Loss");
            Toggle toggleFps = NewToggleRow(pageGameplay, font, "Show FPS");
            Button resetGameplay = NewResetRow(pageGameplay, font, "Reset Gameplay Settings");

            // ---- Controls ----
            RectTransform pageControls = NewPage("Controls", pages);
            RectTransform bindings = NewRect("Bindings", pageControls);
            AddVertical(bindings.gameObject, new RectOffset(0, 0, 0, 0), 6f);
            Button resetControls = NewResetRow(pageControls, font, "Reset Controls");

            Image backDivider = NewImage("Divider", settingsCard, DividerColor);
            SetSize(backDivider.gameObject, height: 1f);

            Button btnBack = NewMenuButton(settingsCard, font, "Back", ButtonSize, 44f);

            // ======================================================
            // CONFIRM DIALOG
            // ======================================================
            Image dialogRoot = NewImage("ConfirmDialog", root.transform, new Color(0f, 0f, 0f, 0.6f), true);
            Stretch(dialogRoot.rectTransform);
            VerticalLayoutGroup dialogLayout = AddVertical(dialogRoot.gameObject, new RectOffset(0, 0, 0, 0), 0f);
            dialogLayout.childAlignment = TextAnchor.MiddleCenter;
            dialogLayout.childForceExpandWidth = false;
            dialogLayout.childForceExpandHeight = false;

            RectTransform dialogCard = CreateCard("Card", dialogRoot.transform, out GameObject dialogOuter, new RectOffset(24, 24, 20, 22));
            SetSize(dialogOuter, width: 560f);

            Text dialogMessage = NewText("Message", dialogCard, font, BodySize + 2, TextColor, TextAnchor.MiddleCenter);
            MinHeight(dialogMessage.gameObject, 80f);

            RectTransform dialogButtons = NewRect("Buttons", dialogCard);
            AddHorizontal(dialogButtons.gameObject, new RectOffset(0, 0, 0, 0), 16f, TextAnchor.MiddleCenter);
            SetSize(dialogButtons.gameObject, height: 46f);

            Button dialogYes = NewMenuButton(dialogButtons, font, "Yes", ButtonSize, 46f);
            SetSize(dialogYes.gameObject, flexibleWidth: 1f);
            Button dialogNo = NewMenuButton(dialogButtons, font, "No", ButtonSize, 46f);
            SetSize(dialogNo.gameObject, flexibleWidth: 1f);

            ConfirmDialog confirm = dialogRoot.gameObject.AddComponent<ConfirmDialog>();

            // ======================================================
            // SettingsPanel-Komponente
            // ======================================================
            SettingsPanel settings = settingsRoot.gameObject.AddComponent<SettingsPanel>();

            SerializedObject sp = new SerializedObject(settings);
            SetArray(sp, "tabButtons", tabButtons);
            SetArray(sp, "tabPanels", new Object[] { pageVideo.gameObject, pageAudio.gameObject, pageGameplay.gameObject, pageControls.gameObject });

            Set(sp, "btnResPrev", resPrev);
            Set(sp, "btnResNext", resNext);
            Set(sp, "txtResolution", txtResolution);
            Set(sp, "toggleFullscreen", toggleFullscreen);
            Set(sp, "toggleVSync", toggleVSync);
            Set(sp, "togglePost", togglePost);
            Set(sp, "btnAaPrev", aaPrev);
            Set(sp, "btnAaNext", aaNext);
            Set(sp, "txtAa", txtAa);
            Set(sp, "sliderRenderScale", sliderRenderScale);
            Set(sp, "txtRenderScale", txtRenderScale);
            Set(sp, "btnResetVideo", resetVideo);

            Set(sp, "sliderMaster", sliderMaster);
            Set(sp, "sliderGameplay", sliderGameplay);
            Set(sp, "sliderMusic", sliderMusic);
            Set(sp, "sliderEnvironment", sliderEnvironment);
            Set(sp, "txtMaster", txtMaster);
            Set(sp, "txtGameplay", txtGameplay);
            Set(sp, "txtMusic", txtMusic);
            Set(sp, "txtEnvironment", txtEnvironment);
            Set(sp, "btnResetAudio", resetAudio);

            Set(sp, "toggleShake", toggleShake);
            Set(sp, "toggleDamageNumbers", toggleDamage);
            Set(sp, "toggleHealthBars", toggleHealthBars);
            Set(sp, "toggleAutoPause", toggleAutoPause);
            Set(sp, "toggleFps", toggleFps);
            Set(sp, "btnResetGameplay", resetGameplay);

            Set(sp, "bindingContainer", bindings);
            Set(sp, "bindingRowPrefab", bindingRowPrefab);
            Set(sp, "btnResetControls", resetControls);

            Set(sp, "btnBack", btnBack);
            Set(sp, "pauseMenu", menu);
            sp.ApplyModifiedProperties();

            // ======================================================
            // ConfirmDialog-Komponente
            // ======================================================
            SerializedObject cd = new SerializedObject(confirm);
            Set(cd, "txtMessage", dialogMessage);
            Set(cd, "btnYes", dialogYes);
            Set(cd, "btnNo", dialogNo);
            cd.ApplyModifiedProperties();

            // ======================================================
            // PauseMenuUI verdrahten
            // ======================================================
            SerializedObject pm = new SerializedObject(menu);
            Set(pm, "btnContinue", btnContinue);
            Set(pm, "btnSettings", btnSettings);
            Set(pm, "btnRestart", btnRestart);
            Set(pm, "btnQuit", btnQuit);
            Set(pm, "confirmDialog", confirm);
            Set(pm, "canvasGroup", canvasGroup);

            Set(pm, "mainPanel", main.gameObject);
            Set(pm, "settingsPanel", settings);

            Set(pm, "btnPrevWave", btnPrevWave);
            Set(pm, "btnNextWave", btnNextWave);
            Set(pm, "txtWaveTitle", txtWaveTitle);
            Set(pm, "txtWaveInfo", txtWaveInfo);

            Set(pm, "weaponContainer", weaponRow);
            Set(pm, "perkContainer", perkRow);
            Set(pm, "mutatorContainer", mutatorRow);
            Set(pm, "loadoutButtonPrefab", loadoutButtonPrefab);
            CopyLoadoutColors(pm);
            Set(pm, "txtLoadoutDetailTitle", detailTitle);
            Set(pm, "txtLoadoutDetail", detailText);

            Set(pm, "questContainer", questScroll);
            Set(pm, "questPrefab", questPrefab);

            Set(pm, "txtScoreLabels", scoreLabels);
            Set(pm, "txtScoreValues", scoreValues);
            pm.ApplyModifiedProperties();

            // ======================================================
            // LevelManager + PauseHotkey + FPS-Anzeige
            // ======================================================
            PauseHotkey hotkey = levelManager.GetComponent<PauseHotkey>();
            if (hotkey == null)
                hotkey = Undo.AddComponent<PauseHotkey>(levelManager.gameObject);

            SerializedObject hk = new SerializedObject(hotkey);
            SerializedProperty fpsProp = hk.FindProperty("txtFps");
            if (fpsProp != null && fpsProp.objectReferenceValue == null)
                fpsProp.objectReferenceValue = CreateFpsText(font);
            Set(hk, "pauseCanvas", root);
            hk.ApplyModifiedProperties();

            SerializedObject lm = new SerializedObject(levelManager);
            GameObject oldPause = levelManager.pauseCanvas;
            Set(lm, "pauseCanvas", root);
            lm.ApplyModifiedProperties();

            if (oldPause != null && oldPause != root)
            {
                Undo.RecordObject(oldPause, "Altes Pausemenü deaktivieren");
                oldPause.SetActive(false);
            }

            // Startzustand: Menü aus, Unterpanels aus
            settingsRoot.gameObject.SetActive(false);
            dialogRoot.gameObject.SetActive(false);
            root.SetActive(false);

            EditorSceneManager.MarkSceneDirty(root.scene);
            Selection.activeGameObject = root;

            Debug.Log("Pausemenü aufgebaut und verdrahtet. Das alte Pause-Canvas ist deaktiviert und kann gelöscht werden.\n" +
                      "Bitte noch prüfen: PauseMenuUI > 'Map Scene Name' (Standard: LevelSelector) und die Ziel-Szene in den Build Settings.\n" +
                      "Optional: GameSettings.Mixer per Script setzen, falls du einen AudioMixer nutzt.");
        }

        // ------------------------------------------------------------------
        // Prefabs
        // ------------------------------------------------------------------

        private static GameObject CreateConditionIconPrefab()
        {
            EnsureFolder(PrefabFolder);

            GameObject go = new GameObject("QuestConditionIcon", typeof(RectTransform));
            Image image = go.AddComponent<Image>();
            image.preserveAspect = true;
            image.color = Color.white;
            image.raycastTarget = false;
            SetSize(go, 28f, 28f);

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(go, $"{PrefabFolder}/QuestConditionIcon.prefab");
            Object.DestroyImmediate(go);
            return saved;
        }

        private static GameObject CreateQuestPrefab(Font font, GameObject conditionIconPrefab)
        {
            EnsureFolder(PrefabFolder);

            // Rahmen + Inhalt, wie die Karten des Popups
            GameObject root = new GameObject("PauseQuest", typeof(RectTransform));
            Image border = root.AddComponent<Image>();
            border.color = BorderColor;
            AddVertical(root, new RectOffset(1, 1, 1, 1), 0f);

            Image inner = NewImage("Content", root.transform, KeyColor, true);
            AddVertical(inner.gameObject, new RectOffset(10, 10, 8, 8), 4f);

            RectTransform nameRow = NewRect("NameRow", inner.transform);
            AddHorizontal(nameRow.gameObject, new RectOffset(0, 0, 0, 0), 8f, TextAnchor.MiddleLeft);

            Text questName = NewText("Name", nameRow, font, BodySize, TextColor, TextAnchor.MiddleLeft, FontStyle.Bold);
            SetSize(questName.gameObject, flexibleWidth: 1f);

            RectTransform conditions = NewRect("Conditions", nameRow);
            AddHorizontal(conditions.gameObject, new RectOffset(0, 0, 0, 0), 4f, TextAnchor.MiddleRight);

            Text description = NewText("Description", inner.transform, font, SmallSize, DimColor, TextAnchor.UpperLeft);
            description.lineSpacing = 1.25f;

            // Erledigt-Overlay: grüne Tönung, zählt nicht im Layout
            Image overlay = NewImage("CompletedOverlay", inner.transform, new Color(XpColor.r, XpColor.g, XpColor.b, 0.16f));
            overlay.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            Stretch(overlay.rectTransform);

            Text done = NewText("Label", overlay.transform, font, HeaderSize, XpColor, TextAnchor.LowerRight, FontStyle.Bold);
            done.text = "COMPLETED";
            Stretch(done.rectTransform);
            done.rectTransform.offsetMin = new Vector2(8f, 6f);
            done.rectTransform.offsetMax = new Vector2(-10f, -6f);
            overlay.gameObject.SetActive(false);

            QuestUI ui = root.AddComponent<QuestUI>();
            SerializedObject so = new SerializedObject(ui);
            Set(so, "questName", questName);
            Set(so, "questDescription", description);
            Set(so, "conditionContainer", conditions);
            Set(so, "conditionIconPrefab", conditionIconPrefab);
            Set(so, "completedOverlay", overlay.gameObject);
            so.ApplyModifiedPropertiesWithoutUndo();

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabFolder}/PauseQuest.prefab");
            Object.DestroyImmediate(root);
            return saved;
        }

        /// <summary>
        /// Prefab mit dem vorhandenen LoadoutSelectionButton-Script (gleiche Felder wie dein btnPrefab):
        /// border = Auswahlrahmen (Root-Image), background = Typfarbe, icon, selectedObject = Marker.
        /// </summary>
        private static GameObject CreateLoadoutButtonPrefab()
        {
            EnsureFolder(PrefabFolder);

            GameObject root = new GameObject("LoadoutButton", typeof(RectTransform));
            SetSize(root, IconSize, IconSize);

            // Auswahlrahmen: LoadoutSelectionButton schaltet border.enabled je nach Auswahl
            Image border = root.AddComponent<Image>();
            border.color = SelectedColor;
            border.raycastTarget = false;

            Image background = NewImage("Background", root.transform, ButtonColor, true);
            Stretch(background.rectTransform);
            background.rectTransform.offsetMin = new Vector2(2f, 2f);
            background.rectTransform.offsetMax = new Vector2(-2f, -2f);

            Image icon = NewImage("Icon", background.transform, Color.white);
            icon.preserveAspect = true;
            Stretch(icon.rectTransform);
            icon.rectTransform.offsetMin = new Vector2(6f, 6f);
            icon.rectTransform.offsetMax = new Vector2(-6f, -6f);

            // Marker unten am Button
            Image marker = NewImage("Selected", root.transform, GoldColor);
            marker.rectTransform.anchorMin = new Vector2(0f, 0f);
            marker.rectTransform.anchorMax = new Vector2(1f, 0f);
            marker.rectTransform.offsetMin = new Vector2(8f, 4f);
            marker.rectTransform.offsetMax = new Vector2(-8f, 8f);
            marker.gameObject.SetActive(false);

            // Button muss auf dem Root liegen: LoadoutSelectionButton.Awake nutzt GetComponent<Button>()
            Button button = root.AddComponent<Button>();
            button.targetGraphic = background;
            button.colors = ButtonColors();
            button.navigation = new Navigation { mode = Navigation.Mode.None };

            LoadoutSelectionButton selection = root.AddComponent<LoadoutSelectionButton>();
            SerializedObject so = new SerializedObject(selection);
            Set(so, "border", border);
            Set(so, "background", background);
            Set(so, "icon", icon);
            Set(so, "selectedObject", marker.gameObject);
            so.ApplyModifiedPropertiesWithoutUndo();

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabFolder}/LoadoutButton.prefab");
            Object.DestroyImmediate(root);
            return saved;
        }

        private static GameObject CreateBindingRowPrefab(Font font)
        {
            EnsureFolder(PrefabFolder);

            GameObject root = new GameObject("BindingRow", typeof(RectTransform));
            AddHorizontal(root, new RectOffset(0, 0, 0, 0), 12f, TextAnchor.MiddleLeft);
            SetSize(root, height: 34f);

            // Wichtig: Das Label ist das ERSTE Text-Kind (SettingsPanel nutzt GetComponentInChildren<Text>)
            Text label = NewText("Label", root.transform, font, BodySize, TextColor, TextAnchor.MiddleLeft);
            SetSize(label.gameObject, flexibleWidth: 1f);

            Button key = NewButton("Key", root.transform, ButtonColor);
            SetSize(key.gameObject, 220f, 32f);
            AddBorder(key.gameObject);

            Text keyText = NewText("Text", key.transform, font, SmallSize + 2, GoldColor, TextAnchor.MiddleCenter, FontStyle.Bold);
            Stretch(keyText.rectTransform);

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabFolder}/BindingRow.prefab");
            Object.DestroyImmediate(root);
            return saved;
        }

        private static Text CreateFpsText(Font font)
        {
            GameObject go = new GameObject("FpsCanvas", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(go, "FPS-Anzeige");

            Canvas canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 90;

            CanvasScaler scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            Text text = NewText("Fps", go.transform, font, SmallSize + 2, TextColor, TextAnchor.UpperRight, FontStyle.Bold);
            RectTransform rect = text.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-16f, -12f);
            rect.sizeDelta = new Vector2(200f, 30f);
            AddOutline(text);
            text.gameObject.SetActive(false);

            return text;
        }

        // ------------------------------------------------------------------
        // Zeilen / Seiten der Settings
        // ------------------------------------------------------------------

        /// <summary>Beschriftung + Reihe für Loadout-Buttons. Gibt die Reihe zurück.</summary>
        private static RectTransform NewLoadoutGroup(Transform parent, Font font, string caption)
        {
            RectTransform group = NewRect(caption + "Group", parent);
            AddVertical(group.gameObject, new RectOffset(0, 0, 0, 0), 4f);

            Text label = NewText("Caption", group, font, SmallSize, DimColor, TextAnchor.UpperLeft);
            label.text = caption;

            RectTransform row = NewRect("Row", group);
            AddHorizontal(row.gameObject, new RectOffset(0, 0, 0, 0), 6f, TextAnchor.UpperLeft);
            MinHeight(row.gameObject, IconSize);

            return row;
        }

        /// <summary>
        /// Übernimmt colorWeapon / colorPerk / colorMutator aus LevelLoadoutUI, falls sie in einer
        /// geöffneten Szene liegt (in der Regel die Map-Szene, dann bleiben die Standardfarben).
        /// </summary>
        private static void CopyLoadoutColors(SerializedObject pm)
        {
            LevelLoadoutUI ui = Object.FindFirstObjectByType<LevelLoadoutUI>(FindObjectsInactive.Include);
            if (ui == null) return;

            SerializedObject source = new SerializedObject(ui);

            foreach (string field in new[] { "colorWeapon", "colorPerk", "colorMutator" })
            {
                SerializedProperty from = source.FindProperty(field);
                SerializedProperty to = pm.FindProperty(field);

                // Alpha 0 = im Inspector nie gesetzt
                if (from != null && to != null && from.colorValue.a > 0.01f)
                    to.colorValue = from.colorValue;
            }
        }

        private static RectTransform NewPage(string name, Transform parent)
        {
            RectTransform page = NewRect(name, parent);
            Stretch(page);
            AddVertical(page.gameObject, new RectOffset(0, 0, 6, 0), 8f);
            return page;
        }

        private static RectTransform NewRow(string name, Transform parent, Font font, string label, float height = 38f)
        {
            RectTransform row = NewRect(name, parent);
            AddHorizontal(row.gameObject, new RectOffset(0, 0, 0, 0), 12f, TextAnchor.MiddleLeft);
            SetSize(row.gameObject, height: height);

            Text text = NewText("Label", row, font, BodySize, TextColor, TextAnchor.MiddleLeft);
            text.text = label;
            SetSize(text.gameObject, flexibleWidth: 1f);

            return row;
        }

        private static void NewArrowRow(Transform parent, Font font, string label,
            out Button prev, out Text value, out Button next)
        {
            RectTransform row = NewRow(label, parent, font, label);

            prev = NewArrowButton("Prev", row, font, "<");

            value = NewText("Value", row, font, BodySize, TextColor, TextAnchor.MiddleCenter, FontStyle.Bold);
            SetSize(value.gameObject, width: 250f);

            next = NewArrowButton("Next", row, font, ">");
        }

        private static Toggle NewToggleRow(Transform parent, Font font, string label)
        {
            RectTransform row = NewRow(label, parent, font, label);

            Image background = NewImage("Toggle", row, ButtonColor, true);
            SetSize(background.gameObject, 28f, 28f);
            AddBorder(background.gameObject);

            Image check = NewImage("Check", background.transform, SelectedColor);
            Stretch(check.rectTransform);
            check.rectTransform.offsetMin = new Vector2(5f, 5f);
            check.rectTransform.offsetMax = new Vector2(-5f, -5f);

            Toggle toggle = background.gameObject.AddComponent<Toggle>();
            toggle.targetGraphic = background;
            toggle.graphic = check;
            toggle.isOn = true;
            toggle.navigation = new Navigation { mode = Navigation.Mode.None };
            toggle.colors = ButtonColors();

            return toggle;
        }

        private static Slider NewSliderRow(Transform parent, Font font, string label, out Text valueText)
        {
            RectTransform row = NewRow(label, parent, font, label);

            Image back = NewImage("Slider", row, BarBackColor, true);
            SetSize(back.gameObject, 250f, 18f);
            AddBorder(back.gameObject);

            RectTransform fillArea = NewRect("Fill Area", back.transform);
            Stretch(fillArea);
            fillArea.offsetMin = new Vector2(4f, 4f);
            fillArea.offsetMax = new Vector2(-4f, -4f);

            Image fill = NewImage("Fill", fillArea, SelectedColor);
            Stretch(fill.rectTransform);

            RectTransform slideArea = NewRect("Handle Slide Area", back.transform);
            Stretch(slideArea);
            slideArea.offsetMin = new Vector2(8f, 0f);
            slideArea.offsetMax = new Vector2(-8f, 0f);

            Image handle = NewImage("Handle", slideArea, TextColor, true);
            handle.rectTransform.anchorMin = new Vector2(0f, 0f);
            handle.rectTransform.anchorMax = new Vector2(0f, 1f);
            handle.rectTransform.sizeDelta = new Vector2(16f, 0f);

            Slider slider = back.gameObject.AddComponent<Slider>();
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 1f;
            slider.navigation = new Navigation { mode = Navigation.Mode.None };

            valueText = NewText("Value", row, font, BodySize, TextColor, TextAnchor.MiddleRight, FontStyle.Bold);
            SetSize(valueText.gameObject, width: 64f);

            return slider;
        }

        private static Button NewResetRow(Transform parent, Font font, string label)
        {
            RectTransform row = NewRect("ResetRow", parent);
            AddHorizontal(row.gameObject, new RectOffset(0, 0, 8, 0), 0f, TextAnchor.MiddleRight);
            SetSize(row.gameObject, height: 44f);

            Button button = NewMenuButton(row, font, label, SmallSize + 2, 36f);
            SetSize(button.gameObject, width: 300f);
            return button;
        }

        // ------------------------------------------------------------------
        // Bausteine (gleiche Idee wie im InteractionPopupBuilder)
        // ------------------------------------------------------------------

        private static RectTransform NewColumn(string name, Transform parent, float width)
        {
            RectTransform column = NewRect(name, parent);
            AddVertical(column.gameObject, new RectOffset(0, 0, 0, 0), 14f);
            SetSize(column.gameObject, width: width);
            return column;
        }

        /// <summary>Karte mit 1 px Rahmen. Gibt den Inhaltsbereich zurück, 'outer' ist die ganze Karte.</summary>
        private static RectTransform CreateCard(string name, Transform parent, out GameObject outer, RectOffset padding = null)
        {
            Image border = NewImage(name, parent, BorderColor, true);
            AddVertical(border.gameObject, new RectOffset(1, 1, 1, 1), 0f);
            outer = border.gameObject;

            Image inner = NewImage("Content", border.transform, PanelColor, true);
            AddVertical(inner.gameObject, padding ?? new RectOffset(16, 16, 12, 16), 8f);

            return inner.rectTransform;
        }

        private static void SectionHeader(Transform parent, Font font, string text)
        {
            Text header = NewText("Header", parent, font, HeaderSize, DimColor, TextAnchor.UpperLeft, FontStyle.Bold);
            header.text = text.ToUpperInvariant();

            Image accent = NewImage("Accent", parent, BorderColor);
            SetSize(accent.gameObject, height: 2f);
        }

        /// <summary>Scrollbereich mit festem Viewport. Gibt den Content zurück (vertikale Layout-Gruppe).</summary>
        private static RectTransform NewScroll(string name, Transform parent, float height, float spacing = 4f)
        {
            Image root = NewImage(name, parent, new Color(0f, 0f, 0f, 0f), true);   // fängt das Mausrad auch über leeren Flächen
            SetSize(root.gameObject, height: height);

            RectTransform viewport = NewRect("Viewport", root.transform);
            Stretch(viewport);
            viewport.gameObject.AddComponent<RectMask2D>();

            RectTransform content = NewRect("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = Vector2.zero;

            VerticalLayoutGroup layout = AddVertical(content.gameObject, new RectOffset(0, 0, 0, 0), spacing);
            layout.childAlignment = TextAnchor.UpperLeft;

            ContentSizeFitter fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            ScrollRect scroll = root.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;

            return content;
        }

        private static Button NewMenuButton(Transform parent, Font font, string label, int size = ButtonSize, float height = 46f)
        {
            Button button = NewButton(label, parent, ButtonColor);
            AddBorder(button.gameObject);
            SetSize(button.gameObject, height: height);

            Text text = NewText("Label", button.transform, font, size, TextColor, TextAnchor.MiddleCenter, FontStyle.Bold);
            text.text = label;
            Stretch(text.rectTransform);

            return button;
        }

        private static Button NewArrowButton(string name, Transform parent, Font font, string symbol)
        {
            Button button = NewButton(name, parent, ButtonColor);
            AddBorder(button.gameObject);
            SetSize(button.gameObject, 40f, 32f);

            Text label = NewText("Label", button.transform, font, BodySize, TextColor, TextAnchor.MiddleCenter, FontStyle.Bold);
            label.text = symbol;
            Stretch(label.rectTransform);

            return button;
        }

        private static ColorBlock ButtonColors()
        {
            ColorBlock colors = ColorBlock.defaultColorBlock;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.25f, 1.25f, 1.25f, 1f);
            colors.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(1f, 1f, 1f, 0.4f);
            colors.fadeDuration = 0.05f;
            return colors;
        }

        private static Button MakeButton(Image image)
        {
            Button button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.colors = ButtonColors();

            // Keine Tastaturnavigation: Leertaste (Welle starten) darf keinen markierten Button auslösen
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            return button;
        }

        private static Button NewButton(string name, Transform parent, Color color)
        {
            Image image = NewImage(name, parent, color, true);
            return MakeButton(image);
        }

        /// <summary>1 px Rahmen in Bronze über eine Outline-Komponente.</summary>
        private static void AddBorder(GameObject go)
        {
            Outline outline = go.AddComponent<Outline>();
            outline.effectColor = BorderColor;
            outline.effectDistance = new Vector2(1f, -1f);
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

        /// <summary>Nur Mindesthöhe (der Inhalt darf größer werden, z. B. bei langen Texten).</summary>
        private static void MinHeight(GameObject go, float height)
        {
            LayoutElement element = go.GetComponent<LayoutElement>();
            if (element == null)
                element = go.AddComponent<LayoutElement>();

            element.minHeight = height;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void AddOutline(Text text)
        {
            Outline outline = text.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            outline.effectDistance = new Vector2(1f, -1f);
        }

        // ------------------------------------------------------------------
        // Serialisierung / Hilfen
        // ------------------------------------------------------------------

        private static void Set(SerializedObject so, string field, Object value)
        {
            SerializedProperty property = so.FindProperty(field);

            if (property == null)
            {
                Debug.LogWarning($"PauseMenuBuilder: Feld '{field}' in {so.targetObject.GetType().Name} nicht gefunden.");
                return;
            }

            property.objectReferenceValue = value;
        }

        private static void SetArray(SerializedObject so, string field, Object[] values)
        {
            SerializedProperty property = so.FindProperty(field);

            if (property == null)
            {
                Debug.LogWarning($"PauseMenuBuilder: Feld '{field}' in {so.targetObject.GetType().Name} nicht gefunden.");
                return;
            }

            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }

        /// <summary>Schrift des InteractionPopup bzw. der Loadout-UI übernehmen (Pixel-Font), sonst die Standardschrift.</summary>
        private static Font FindFont()
        {
            InteractionPopup popup = Object.FindFirstObjectByType<InteractionPopup>(FindObjectsInactive.Include);
            Font font = FontFrom(popup, "upgradeTitle", "interactionText", "upgradeDescription", "txtLevel");
            if (font != null) return font;

            LevelLoadoutUI loadoutUi = Object.FindFirstObjectByType<LevelLoadoutUI>(FindObjectsInactive.Include);
            font = FontFrom(loadoutUi, "textTitelLevel", "textDescription", "textWeaponsSelected");
            if (font != null) return font;

            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return font != null ? font : Font.CreateDynamicFontFromOSFont("Arial", 16);
        }

        private static Font FontFrom(Object source, params string[] fields)
        {
            if (source == null) return null;

            SerializedObject so = new SerializedObject(source);

            foreach (string field in fields)
            {
                SerializedProperty property = so.FindProperty(field);
                Text text = property != null ? property.objectReferenceValue as Text : null;

                if (text != null && text.font != null)
                    return text.font;
            }

            return null;
        }

        private static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>(FindObjectsInactive.Include) != null)
                return;

            GameObject go = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            Undo.RegisterCreatedObjectUndo(go, "EventSystem");
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