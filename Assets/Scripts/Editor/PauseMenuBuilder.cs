#if UNITY_EDITOR
using LevelSelector;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

#if UNITY_EDITOR
using System.Collections.Generic;
using LevelSelector;
using LevelSelector.Definitions;
using TowerDefense.Research;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace TowerDefense.EditorTools
{
    /// <summary>
    /// Victory-Screen im Stil des Pausemenüs.
    ///
    ///   Links:  Titel "Victory." (+ optionale Krone) und die Score-Aufschlüsselung (wie im Pausemenü)
    ///   Rechts: Highscore (+ NEW RECORD!), Spieler-Level mit XP-Balken und Vorschau des nächsten Perks,
    ///           Buttons "Back to Map" / "Restart"
    ///   Popup:  "NEW PERK" bei jedem Level-Up
    ///
    /// Legt außerdem Assets/Resources/PlayerProgression.asset an (falls nicht vorhanden) und trägt
    /// das neue Canvas im LevelManager als endScreen ein.
    /// Aufruf: Tools > UI > Victory-Screen neu aufbauen
    /// </summary>
    public static partial class PauseMenuBuilder
    {
        private const string VictoryRootName = "VictoryCanvas (neu)";
        private const string ProgressionFolder = "Assets/Resources";
        private const string ProgressionPath = ProgressionFolder + "/PlayerProgression.asset";

        [MenuItem("Tools/UI/Victory-Screen neu aufbauen")]
        private static void BuildVictory()
        {
            LevelManager levelManager = Object.FindFirstObjectByType<LevelManager>(FindObjectsInactive.Include);

            if (levelManager == null)
            {
                EditorUtility.DisplayDialog("Victory-Screen",
                    "In der Szene wurde kein LevelManager gefunden. Öffne die Level-Szene.", "OK");
                return;
            }

            Font font = FindFont();

            foreach (VictoryScreenUI old in Object.FindObjectsByType<VictoryScreenUI>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Undo.DestroyObjectImmediate(old.gameObject);
            }

            EnsureEventSystem();
            EnsureProgressionAsset();

            // Score-Prefabs werden mit dem Pausemenü geteilt
            ScoreRow scoreRowPrefab = CreateScoreRowPrefab(font);
            GameObject scoreHeaderPrefab = CreateScoreHeaderPrefab(font);
            GameObject scoreDividerPrefab = CreateScoreDividerPrefab();

            if (scoreRowPrefab == null)
            {
                EditorUtility.DisplayDialog("Victory-Screen",
                    "Die Prefab-Komponente ScoreRow konnte nicht erzeugt werden. Liegt ScoreRow.cs als eigene Datei im Projekt " +
                    "und gibt es Compile-Fehler in der Console?", "OK");
                return;
            }

            // ---------- Canvas ----------
            GameObject root = new GameObject(VictoryRootName, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(root, "Victory-Screen neu aufbauen");

            Canvas canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = PauseSortingOrder;

            CanvasScaler scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            root.AddComponent<GraphicRaycaster>();
            CanvasGroup canvasGroup = root.AddComponent<CanvasGroup>();
            VictoryScreenUI victory = root.AddComponent<VictoryScreenUI>();

            Image dim = NewImage("Dim", root.transform, DimBackColor, true);
            Stretch(dim.rectTransform);

            RectTransform main = NewRect("MainPanel", root.transform);
            Stretch(main);
            AddHorizontal(main.gameObject, ScreenPadding, 24f, TextAnchor.UpperCenter);

            RectTransform left = NewColumn("Left", main, 560f);
            RectTransform right = NewColumn("Right", main, 440f);

            // ---------- Links: Titel + Score ----------
            RectTransform titleRow = NewRect("TitleRow", left);
            AddHorizontal(titleRow.gameObject, new RectOffset(0, 0, 0, 0), 14f, TextAnchor.MiddleLeft);
            SetSize(titleRow.gameObject, height: 72f);

            // Krone: Sprite im Inspector zuweisen (ohne Sprite wird das Bild ausgeblendet)
            Image crown = NewImage("Crown", titleRow, Color.white);
            crown.preserveAspect = true;
            SetSize(crown.gameObject, 56f, 56f);

            Text title = NewText("Title", titleRow, font, TitleSize, TextColor, TextAnchor.MiddleLeft, FontStyle.Bold);
            title.text = "Victory.";

            RectTransform scoreCard = CreateCard("ScoreCard", left, out GameObject scoreOuter);
            SectionHeader(scoreCard, font, "Score");

            RectTransform scoreRows = NewRect("Rows", scoreCard);
            AddVertical(scoreRows.gameObject, new RectOffset(0, 0, 0, 0), 2f);

            ScorePanelUI scorePanel = scoreOuter.AddComponent<ScorePanelUI>();
            SerializedObject sc = new SerializedObject(scorePanel);
            Set(sc, "container", scoreRows);
            Set(sc, "rowPrefab", scoreRowPrefab);
            Set(sc, "headerPrefab", scoreHeaderPrefab);
            Set(sc, "dividerPrefab", scoreDividerPrefab);
            sc.ApplyModifiedPropertiesWithoutUndo();

            // ---------- Rechts: Highscore ----------
            RectTransform highscoreCard = CreateCard("HighscoreCard", right, out _);
            SectionHeader(highscoreCard, font, "Highscore");

            Text txtHighscore = NewText("Highscore", highscoreCard, font, BodySize, DimColor, TextAnchor.MiddleLeft);
            Text txtNewRecord = NewText("NewRecord", highscoreCard, font, BodySize, GoldColor, TextAnchor.MiddleLeft, FontStyle.Bold);
            txtNewRecord.text = "NEW RECORD!";

            // ---------- Rechts: Level ----------
            RectTransform levelCard = CreateCard("LevelCard", right, out GameObject levelOuter);
            SectionHeader(levelCard, font, "Level");

            RectTransform levelRow = NewRect("LevelRow", levelCard);
            AddHorizontal(levelRow.gameObject, new RectOffset(0, 0, 4, 0), 14f, TextAnchor.MiddleLeft);

            RectTransform levelLeft = NewRect("Left", levelRow);
            AddVertical(levelLeft.gameObject, new RectOffset(0, 0, 0, 0), 6f);
            SetSize(levelLeft.gameObject, flexibleWidth: 1f);

            Text txtLevel = NewText("Level", levelLeft, font, BodySize + 2, TextColor, TextAnchor.MiddleLeft, FontStyle.Bold);

            Image barBack = NewImage("Bar", levelLeft, BarBackColor);
            SetSize(barBack.gameObject, height: 24f);
            AddBorder(barBack.gameObject);

            // Füllung wird im Script über anchorMax.x gesteuert
            Image fill = NewImage("Fill", barBack.transform, SelectedColor);
            fill.rectTransform.anchorMin = new Vector2(0f, 0f);
            fill.rectTransform.anchorMax = new Vector2(0f, 1f);
            fill.rectTransform.offsetMin = new Vector2(0f, 2f);
            fill.rectTransform.offsetMax = new Vector2(0f, -2f);

            Text txtXp = NewText("BarText", barBack.transform, font, SmallSize, TextColor, TextAnchor.MiddleCenter, FontStyle.Bold);
            Stretch(txtXp.rectTransform);
            AddOutline(txtXp);

            Text txtXpGained = NewText("XpGained", levelLeft, font, SmallSize + 2, GoldColor, TextAnchor.MiddleLeft, FontStyle.Bold);

            // Vorschau des nächsten Perks
            Image nextBack = NewImage("NextPerk", levelRow, KeyColor);
            SetSize(nextBack.gameObject, 56f, 56f);
            AddBorder(nextBack.gameObject);
            Image nextIcon = NewImage("Icon", nextBack.transform, Color.white);
            nextIcon.preserveAspect = true;
            Stretch(nextIcon.rectTransform);
            nextIcon.rectTransform.offsetMin = new Vector2(6f, 6f);
            nextIcon.rectTransform.offsetMax = new Vector2(-6f, -6f);

            // ---------- Rechts: Buttons ----------
            Button btnMap = NewMenuButton(right, font, "Back to Map");
            Button btnRestart = NewMenuButton(right, font, "Try again");

            // ---------- Popup: NEW PERK ----------
            Image popupRoot = NewImage("PerkPopup", root.transform, new Color(0f, 0f, 0f, 0.7f), true);
            Stretch(popupRoot.rectTransform);

            // Klick irgendwo schließt das Popup (Klicks auf die Karte laufen zum Root-Button durch)
            Button popupButton = popupRoot.gameObject.AddComponent<Button>();
            popupButton.targetGraphic = popupRoot;
            popupButton.transition = Selectable.Transition.None;
            popupButton.navigation = new Navigation { mode = Navigation.Mode.None };

            VerticalLayoutGroup popupLayout = AddVertical(popupRoot.gameObject, new RectOffset(0, 0, 0, 0), 0f);
            popupLayout.childAlignment = TextAnchor.MiddleCenter;
            popupLayout.childForceExpandWidth = false;

            RectTransform popupCard = CreateCard("Card", popupRoot.transform, out GameObject popupOuter, new RectOffset(32, 32, 24, 26));
            SetSize(popupOuter, width: 600f, flexibleWidth: 0f);

            Text popupHeader = NewText("Header", popupCard, font, BodySize + 2, DimColor, TextAnchor.MiddleCenter, FontStyle.Bold);
            popupHeader.text = "NEW PERK";

            Image popupAccent = NewImage("Accent", popupCard, BorderColor);
            SetSize(popupAccent.gameObject, height: 2f);

            RectTransform popupRow = NewRect("Row", popupCard);
            AddHorizontal(popupRow.gameObject, new RectOffset(0, 0, 10, 10), 22f, TextAnchor.MiddleLeft);

            RectTransform popupText = NewRect("Text", popupRow);
            AddVertical(popupText.gameObject, new RectOffset(0, 0, 0, 0), 8f);
            SetSize(popupText.gameObject, flexibleWidth: 1f);

            Text txtPerkName = NewText("Name", popupText, font, BodySize + 6, GoldColor, TextAnchor.UpperLeft, FontStyle.Bold);
            Text txtPerkDescription = NewText("Description", popupText, font, SmallSize + 2, DimColor, TextAnchor.UpperLeft, wrap: true);
            txtPerkDescription.lineSpacing = 1.3f;

            Image perkIconBack = NewImage("IconBack", popupRow, KeyColor);
            SetSize(perkIconBack.gameObject, 104f, 104f);
            AddBorder(perkIconBack.gameObject);
            Image perkIcon = NewImage("Icon", perkIconBack.transform, Color.white);
            perkIcon.preserveAspect = true;
            Stretch(perkIcon.rectTransform);
            perkIcon.rectTransform.offsetMin = new Vector2(8f, 8f);
            perkIcon.rectTransform.offsetMax = new Vector2(-8f, -8f);

            Text popupHint = NewText("Hint", popupCard, font, SmallSize, DimColor, TextAnchor.MiddleCenter);
            popupHint.text = "Click to continue";

            // ---------- Verdrahten ----------
            SerializedObject vs = new SerializedObject(victory);
            Set(vs, "crownIcon", crown);
            Set(vs, "scorePanel", scorePanel);
            Set(vs, "txtHighscore", txtHighscore);
            Set(vs, "newRecordObject", txtNewRecord.gameObject);
            Set(vs, "levelCard", levelOuter);
            Set(vs, "txtLevel", txtLevel);
            Set(vs, "txtXpGained", txtXpGained);
            Set(vs, "txtXp", txtXp);
            Set(vs, "barFill", fill.rectTransform);
            Set(vs, "nextPerkRoot", nextBack.gameObject);
            Set(vs, "nextPerkIcon", nextIcon);
            Set(vs, "perkPopup", popupRoot.gameObject);
            Set(vs, "txtPerkName", txtPerkName);
            Set(vs, "txtPerkDescription", txtPerkDescription);
            Set(vs, "imgPerkIcon", perkIcon);
            Set(vs, "btnPerkContinue", popupButton);
            Set(vs, "btnMap", btnMap);
            Set(vs, "btnRestart", btnRestart);
            Set(vs, "canvasGroup", canvasGroup);

            // Map-Szene vom Pausemenü übernehmen, falls dort schon gesetzt
            PauseMenuUI pause = Object.FindFirstObjectByType<PauseMenuUI>(FindObjectsInactive.Include);
            if (pause != null)
            {
                SerializedProperty source = new SerializedObject(pause).FindProperty("mapSceneName");
                SerializedProperty target = vs.FindProperty("mapSceneName");
                if (source != null && target != null)
                    target.stringValue = source.stringValue;
            }

            vs.ApplyModifiedProperties();

            // LevelManager.endScreen = neues Victory-Canvas, alten Screen ausschalten
            SerializedObject lm = new SerializedObject(levelManager);
            SerializedProperty endProperty = lm.FindProperty("endScreen");

            if (endProperty != null)
            {
                GameObject oldEnd = endProperty.objectReferenceValue as GameObject;
                endProperty.objectReferenceValue = root;
                lm.ApplyModifiedProperties();

                if (oldEnd != null && oldEnd != root)
                {
                    Undo.RecordObject(oldEnd, "Alten Victory-Screen deaktivieren");
                    oldEnd.SetActive(false);
                }
            }

            popupRoot.gameObject.SetActive(false);
            root.SetActive(false);

            EditorSceneManager.MarkSceneDirty(root.scene);
            Selection.activeGameObject = root;

            Debug.Log("Victory-Screen aufgebaut und als LevelManager.endScreen eingetragen.\n" +
                      "Optional: Krone als Sprite im Feld 'Crown Icon' zuweisen. " +
                      $"Perks pro Stufe im Asset {ProgressionPath} eintragen.");
        }

        /// <summary>Legt das Progressions-Asset mit Beispielstufen an. Perks pro Stufe trägst du selbst ein.</summary>
        private static void EnsureProgressionAsset()
        {
            if (AssetDatabase.LoadAssetAtPath<PlayerProgressionDefinition>(ProgressionPath) != null)
                return;

            EnsureFolder(ProgressionFolder);

            PlayerProgressionDefinition asset = ScriptableObject.CreateInstance<PlayerProgressionDefinition>();
            asset.xpPerScorePoint = 0.25f;
            asset.levels = new List<ProgressionLevel>
            {
                new ProgressionLevel { xpRequired = 0 },
                new ProgressionLevel { xpRequired = 1500 },
                new ProgressionLevel { xpRequired = 2500 },
                new ProgressionLevel { xpRequired = 3500 },
                new ProgressionLevel { xpRequired = 5000 },
                new ProgressionLevel { xpRequired = 7000 },
            };

            AssetDatabase.CreateAsset(asset, ProgressionPath);
            AssetDatabase.SaveAssets();

            Debug.Log($"PlayerProgression-Asset angelegt: {ProgressionPath}. Trage dort pro Stufe den freizuschaltenden Perk ein.");
        }
    }
}
#endif


namespace TowerDefense.EditorTools
{
    /// <summary>
    /// Baut das komplette Pausemenü im Stil des InteractionPopup.
    ///
    ///   MainPanel     Links:  Titel, Continue / Settings / Restart / Quit, Wave-Browser
    ///                 Mitte:  Score-Aufschlüsselung (Basis | Trennlinie | Multiplikator je Perk und Mutator | Gesamt)
    ///                 Rechts: Loadout (Waffe, Perks, Mutatoren, Detailtext) und Quests
    ///   SettingsPanel gleiche Ränder wie das Hauptmenü, Tabs Video / Audio / Gameplay / Controls
    ///   EnemyInfoPanel  Klick auf eine Wave-Zeile
    ///   ConfirmDialog
    ///
    /// Aufruf: Tools > UI > Pausemenü neu aufbauen   (beliebig oft wiederholbar)
    /// </summary>
    public static partial class PauseMenuBuilder
    {
        // ---------- Stil (hier anpassen) ----------
        private const int TitleSize = 44;
        private const int HeaderSize = 16;
        private const int BodySize = 20;
        private const int SmallSize = 16;
        private const int ButtonSize = 22;

        private const float LeftWidth = 420f;
        private const float MiddleWidth = 520f;   // Score: Label + Multiplikator + Wert brauchen Platz
        private const float RightWidth = 440f;

        // Gleiche Ränder für Hauptmenü und Settings
        private static readonly RectOffset ScreenPadding = new RectOffset(60, 60, 90, 60);

        private const float SettingsPagesHeight = 380f;
        private const float RowHeight = 34f;

        private const float IconSize = 56f;

        // Muss über dem HUD-Canvas liegen, sonst scheinen HUD-Elemente durch das Menü.
        // Ein Fade-Canvas (SceneFadeIn) braucht eine höhere Ordnung als dieser Wert.
        private const int PauseSortingOrder = 500;
        private const int FpsSortingOrder = 600;

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
            WaveEnemyEntry waveEntryPrefab = CreateWaveEntryPrefab(font);
            GameObject statRowPrefab = CreateEnemyStatRowPrefab(font);
            GameObject statHeaderPrefab = CreateEnemyStatHeaderPrefab(font);
            ScoreRow scoreRowPrefab = CreateScoreRowPrefab(font);
            GameObject scoreHeaderPrefab = CreateScoreHeaderPrefab(font);
            GameObject scoreDividerPrefab = CreateScoreDividerPrefab();

            if (scoreRowPrefab == null || waveEntryPrefab == null)
            {
                EditorUtility.DisplayDialog("Pausemenü",
                    "Die Prefab-Komponenten ScoreRow bzw. WaveEnemyEntry konnten nicht erzeugt werden.\n\n" +
                    "Jede Klasse muss in einer eigenen Datei liegen (ScoreRow.cs, WaveEnemyEntry.cs, ...). " +
                    "Alte Dateien PauseExtras.cs und PauseEnemyUI.cs löschen und die Console auf Compile-Fehler prüfen.", "OK");
                return;
            }

            // ---------- Canvas ----------
            GameObject root = new GameObject(RootName, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(root, "Pausemenü neu aufbauen");

            Canvas canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = PauseSortingOrder;

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
            AddHorizontal(main.gameObject, ScreenPadding, 24f, TextAnchor.UpperCenter);

            RectTransform left = NewColumn("Left", main, LeftWidth);
            RectTransform middle = NewColumn("Middle", main, MiddleWidth);
            RectTransform right = NewColumn("Right", main, RightWidth);

            // ---------- Links: Menü + Waves ----------
            Text title = NewText("Title", left, font, TitleSize, TextColor, TextAnchor.MiddleLeft, FontStyle.Bold);
            title.text = "Paused";
            SetSize(title.gameObject, height: 72f);

            Button btnContinue = NewMenuButton(left, font, "Continue");
            Button btnSettings = NewMenuButton(left, font, "Settings");
            Button btnRestart = NewMenuButton(left, font, "Try again");
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

            RectTransform waveEntries = NewScroll("WaveScroll", waveCard, 300f, 4f);

            Image waveDivider = NewImage("Divider", waveCard, DividerColor);
            SetSize(waveDivider.gameObject, height: 1f);

            Text txtWaveSummary = NewText("Summary", waveCard, font, SmallSize + 2, GoldColor, TextAnchor.MiddleLeft, FontStyle.Bold);
            SetSize(txtWaveSummary.gameObject, height: 26f);

            // ---------- Mitte: Score ----------
            RectTransform scoreCard = CreateCard("ScoreCard", middle, out GameObject scoreOuter);
            SectionHeader(scoreCard, font, "Score");

            RectTransform scoreRows = NewRect("Rows", scoreCard);
            AddVertical(scoreRows.gameObject, new RectOffset(0, 0, 0, 0), 2f);

            ScorePanelUI scorePanel = scoreOuter.AddComponent<ScorePanelUI>();
            SerializedObject sc = new SerializedObject(scorePanel);
            Set(sc, "container", scoreRows);
            Set(sc, "rowPrefab", scoreRowPrefab);
            Set(sc, "headerPrefab", scoreHeaderPrefab);
            Set(sc, "dividerPrefab", scoreDividerPrefab);
            sc.ApplyModifiedPropertiesWithoutUndo();

            // ---------- Rechts: Loadout + Quests ----------
            RectTransform loadoutCard = CreateCard("LoadoutCard", right, out _);
            SectionHeader(loadoutCard, font, "Loadout");

            // Wie in LevelLoadoutUI: 1 Waffe, 5 Perks, 3 Mutatoren (LevelSelectorManager.StartLevel erzwingt das)
            RectTransform weaponRow = NewLoadoutGroup(loadoutCard, font, "Weapon");
            RectTransform perkRow = NewLoadoutGroup(loadoutCard, font, "Perks");
            RectTransform mutatorRow = NewLoadoutGroup(loadoutCard, font, "Mutators");

            Image loadoutDivider = NewImage("Divider", loadoutCard, DividerColor);
            SetSize(loadoutDivider.gameObject, height: 1f);

            Text detailTitle = NewText("DetailTitle", loadoutCard, font, BodySize, GoldColor, TextAnchor.UpperLeft, FontStyle.Bold);
            Text detailText = NewText("Detail", loadoutCard, font, SmallSize, DimColor, TextAnchor.UpperLeft, wrap: true);
            detailText.lineSpacing = 1.3f;
            MinHeight(detailText.gameObject, 96f);

            RectTransform questCard = CreateCard("QuestCard", right, out _);
            SectionHeader(questCard, font, "Quests");
            RectTransform questScroll = NewScroll("QuestScroll", questCard, 230f, 6f);

            // ======================================================
            // SETTINGS PANEL (gleiche Ränder wie MainPanel)
            // ======================================================
            RectTransform settingsRoot = NewRect("SettingsPanel", root.transform);
            Stretch(settingsRoot);
            VerticalLayoutGroup settingsLayout = AddVertical(settingsRoot.gameObject, ScreenPadding, 14f);
            settingsLayout.childAlignment = TextAnchor.UpperCenter;

            Text settingsTitle = NewText("Title", settingsRoot, font, TitleSize, TextColor, TextAnchor.MiddleLeft, FontStyle.Bold);
            settingsTitle.text = "Settings";
            SetSize(settingsTitle.gameObject, height: 72f);

            RectTransform settingsCard = CreateCard("Card", settingsRoot, out _, new RectOffset(24, 24, 16, 18));

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
            // ENEMY INFO PANEL
            // ======================================================
            Image enemyRoot = NewImage("EnemyInfoPanel", root.transform, new Color(0f, 0f, 0f, 0.6f), true);
            Stretch(enemyRoot.rectTransform);
            VerticalLayoutGroup enemyLayout = AddVertical(enemyRoot.gameObject, new RectOffset(0, 0, 0, 0), 0f);
            enemyLayout.childAlignment = TextAnchor.MiddleCenter;
            enemyLayout.childForceExpandWidth = false;

            RectTransform enemyCard = CreateCard("Card", enemyRoot.transform, out GameObject enemyOuter, new RectOffset(24, 24, 18, 20));
            SetSize(enemyOuter, width: 640f, flexibleWidth: 0f);

            RectTransform enemyHead = NewRect("Head", enemyCard);
            AddHorizontal(enemyHead.gameObject, new RectOffset(0, 0, 0, 0), 16f, TextAnchor.UpperLeft);

            Image enemyIconBack = NewImage("IconBack", enemyHead, KeyColor);
            SetSize(enemyIconBack.gameObject, 104f, 104f);
            AddBorder(enemyIconBack.gameObject);
            Image enemyIcon = NewImage("Icon", enemyIconBack.transform, Color.white);
            enemyIcon.preserveAspect = true;
            Stretch(enemyIcon.rectTransform);
            enemyIcon.rectTransform.offsetMin = new Vector2(8f, 8f);
            enemyIcon.rectTransform.offsetMax = new Vector2(-8f, -8f);

            RectTransform enemyHeadText = NewRect("Text", enemyHead);
            AddVertical(enemyHeadText.gameObject, new RectOffset(0, 0, 0, 0), 6f);
            SetSize(enemyHeadText.gameObject, flexibleWidth: 1f);

            Text enemyName = NewText("Name", enemyHeadText, font, TitleSize - 12, TextColor, TextAnchor.UpperLeft, FontStyle.Bold);
            Text enemyDescription = NewText("Description", enemyHeadText, font, SmallSize, DimColor, TextAnchor.UpperLeft, wrap: true);
            enemyDescription.lineSpacing = 1.3f;

            Image enemyAccent = NewImage("Accent", enemyCard, BorderColor);
            SetSize(enemyAccent.gameObject, height: 2f);

            RectTransform enemyStats = NewScroll("StatsScroll", enemyCard, 380f, 2f);

            Button enemyClose = NewMenuButton(enemyCard, font, "Close", ButtonSize, 44f);

            EnemyInfoPanel enemyPanel = enemyRoot.gameObject.AddComponent<EnemyInfoPanel>();
            SerializedObject ep = new SerializedObject(enemyPanel);
            Set(ep, "icon", enemyIcon);
            Set(ep, "txtName", enemyName);
            Set(ep, "txtDescription", enemyDescription);
            Set(ep, "statContainer", enemyStats);
            Set(ep, "statRowPrefab", statRowPrefab);
            Set(ep, "statHeaderPrefab", statHeaderPrefab);
            Set(ep, "btnClose", enemyClose);
            ep.ApplyModifiedProperties();

            // ======================================================
            // CONFIRM DIALOG
            // ======================================================
            Image dialogRoot = NewImage("ConfirmDialog", root.transform, new Color(0f, 0f, 0f, 0.6f), true);
            Stretch(dialogRoot.rectTransform);
            VerticalLayoutGroup dialogLayout = AddVertical(dialogRoot.gameObject, new RectOffset(0, 0, 0, 0), 0f);
            dialogLayout.childAlignment = TextAnchor.MiddleCenter;
            dialogLayout.childForceExpandWidth = false;

            RectTransform dialogCard = CreateCard("Card", dialogRoot.transform, out GameObject dialogOuter, new RectOffset(24, 24, 20, 22));
            SetSize(dialogOuter, width: 560f, flexibleWidth: 0f);

            Text dialogMessage = NewText("Message", dialogCard, font, BodySize + 2, TextColor, TextAnchor.MiddleCenter, wrap: true);
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
            // (sceneFader wird zur Laufzeit automatisch gefunden)
            // ======================================================
            SerializedObject pm = new SerializedObject(menu);
            Set(pm, "txtTitle", title);
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
            Set(pm, "waveEntryContainer", waveEntries);
            Set(pm, "waveEntryPrefab", waveEntryPrefab);
            Set(pm, "txtWaveSummary", txtWaveSummary);
            Set(pm, "enemyInfoPanel", enemyPanel);

            Set(pm, "weaponContainer", weaponRow);
            Set(pm, "perkContainer", perkRow);
            Set(pm, "mutatorContainer", mutatorRow);
            Set(pm, "loadoutButtonPrefab", loadoutButtonPrefab);
            CopyLoadoutColors(pm);
            Set(pm, "txtLoadoutDetailTitle", detailTitle);
            Set(pm, "txtLoadoutDetail", detailText);

            Set(pm, "questContainer", questScroll);
            Set(pm, "questPrefab", questPrefab);

            Set(pm, "scorePanel", scorePanel);
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

            // Death-Screen läuft jetzt über dieses Menü (Modus "Died"): alten Screen ausschalten
            SerializedProperty deathProperty = lm.FindProperty("deathScreen");
            GameObject oldDeath = deathProperty != null ? deathProperty.objectReferenceValue as GameObject : null;
            if (oldDeath != null && oldDeath != root)
            {
                Undo.RecordObject(oldDeath, "Alten Death-Screen deaktivieren");
                oldDeath.SetActive(false);
            }

            // Startzustand: Menü aus, Unterpanels aus
            settingsRoot.gameObject.SetActive(false);
            dialogRoot.gameObject.SetActive(false);
            enemyRoot.gameObject.SetActive(false);
            root.SetActive(false);

            EditorSceneManager.MarkSceneDirty(root.scene);
            Selection.activeGameObject = root;

            Debug.Log("Pausemenü aufgebaut und verdrahtet. Das alte Pause-Canvas ist deaktiviert und kann gelöscht werden.\n" +
                      $"Pause-Canvas Sorting Order: {PauseSortingOrder}. Ein Fade-Canvas muss höher liegen, damit der Fade über dem Menü sichtbar ist.\n" +
                      "Bitte prüfen: PauseMenuUI > 'Map Scene Name' und die Ziel-Szene in den Build Settings.");
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

            Text description = NewText("Description", inner.transform, font, SmallSize, DimColor, TextAnchor.UpperLeft, wrap: true);
            description.lineSpacing = 1.25f;

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

        /// <summary>Prefab mit dem vorhandenen LoadoutSelectionButton-Script (gleiche Felder wie dein btnPrefab).</summary>
        private static GameObject CreateLoadoutButtonPrefab()
        {
            EnsureFolder(PrefabFolder);

            GameObject root = new GameObject("LoadoutButton", typeof(RectTransform));
            SetSize(root, IconSize, IconSize);

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

            Image marker = NewImage("Selected", root.transform, GoldColor);
            marker.rectTransform.anchorMin = new Vector2(0f, 0f);
            marker.rectTransform.anchorMax = new Vector2(1f, 0f);
            marker.rectTransform.offsetMin = new Vector2(8f, 4f);
            marker.rectTransform.offsetMax = new Vector2(-8f, 8f);
            marker.gameObject.SetActive(false);

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

        private static WaveEnemyEntry CreateWaveEntryPrefab(Font font)
        {
            EnsureFolder(PrefabFolder);

            GameObject root = new GameObject("WaveEnemyEntry", typeof(RectTransform));
            Image background = root.AddComponent<Image>();
            background.color = ButtonColor;
            AddBorder(root);
            AddHorizontal(root, new RectOffset(8, 8, 6, 6), 10f, TextAnchor.MiddleLeft);
            MinHeight(root, 48f);

            Button button = root.AddComponent<Button>();
            button.targetGraphic = background;
            button.colors = ButtonColors();
            button.navigation = new Navigation { mode = Navigation.Mode.None };

            Image icon = NewImage("Icon", root.transform, Color.white);
            icon.preserveAspect = true;
            SetSize(icon.gameObject, 36f, 36f);

            Text label = NewText("Label", root.transform, font, BodySize - 2, TextColor, TextAnchor.MiddleLeft, FontStyle.Bold);
            SetSize(label.gameObject, flexibleWidth: 1f);

            Text gold = NewText("Gold", root.transform, font, SmallSize, GoldColor, TextAnchor.MiddleRight, FontStyle.Bold);
            SetSize(gold.gameObject, width: 90f);

            Image info = NewImage("Info", root.transform, KeyColor);
            SetSize(info.gameObject, 24f, 24f);
            AddBorder(info.gameObject);
            Text i = NewText("I", info.transform, font, SmallSize, DimColor, TextAnchor.MiddleCenter, FontStyle.Bold);
            i.text = "i";
            Stretch(i.rectTransform);

            WaveEnemyEntry entry = root.AddComponent<WaveEnemyEntry>();
            SerializedObject so = new SerializedObject(entry);
            Set(so, "icon", icon);
            Set(so, "txtLabel", label);
            Set(so, "txtGold", gold);
            so.ApplyModifiedPropertiesWithoutUndo();

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabFolder}/WaveEnemyEntry.prefab");
            Object.DestroyImmediate(root);
            return AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/WaveEnemyEntry.prefab").GetComponent<WaveEnemyEntry>();
        }

        private static GameObject CreateEnemyStatRowPrefab(Font font)
        {
            EnsureFolder(PrefabFolder);

            GameObject root = new GameObject("EnemyStatRow", typeof(RectTransform));
            AddHorizontal(root, new RectOffset(0, 0, 0, 0), 12f, TextAnchor.MiddleLeft);
            SetSize(root, height: 26f);

            Text label = NewText("Label", root.transform, font, SmallSize + 2, DimColor, TextAnchor.MiddleLeft);
            SetSize(label.gameObject, flexibleWidth: 1f);

            NewText("Value", root.transform, font, SmallSize + 2, TextColor, TextAnchor.MiddleRight, FontStyle.Bold);

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabFolder}/EnemyStatRow.prefab");
            Object.DestroyImmediate(root);
            return saved;
        }

        private static GameObject CreateEnemyStatHeaderPrefab(Font font)
        {
            EnsureFolder(PrefabFolder);

            GameObject root = new GameObject("EnemyStatHeader", typeof(RectTransform));
            AddVertical(root, new RectOffset(0, 0, 10, 0), 2f);

            NewText("Label", root.transform, font, HeaderSize, GoldColor, TextAnchor.UpperLeft, FontStyle.Bold);

            Image divider = NewImage("Divider", root.transform, DividerColor);
            SetSize(divider.gameObject, height: 1f);

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabFolder}/EnemyStatHeader.prefab");
            Object.DestroyImmediate(root);
            return saved;
        }

        // ---- Score ----

        private static ScoreRow CreateScoreRowPrefab(Font font)
        {
            EnsureFolder(PrefabFolder);

            GameObject root = new GameObject("ScoreRow", typeof(RectTransform));
            Image background = root.AddComponent<Image>();
            background.color = new Color(0f, 0f, 0f, 0f);     // wird beim Highlight eingefärbt
            background.raycastTarget = false;
            AddHorizontal(root, new RectOffset(6, 6, 2, 2), 8f, TextAnchor.MiddleLeft);
            MinHeight(root, 28f);

            Text label = NewText("Label", root.transform, font, SmallSize + 2, DimColor, TextAnchor.MiddleLeft);
            SetSize(label.gameObject, flexibleWidth: 1f);

            Text middle = NewText("Middle", root.transform, font, SmallSize + 2, DimColor, TextAnchor.MiddleRight, FontStyle.Bold);
            SetSize(middle.gameObject, width: 84f);

            Text value = NewText("Value", root.transform, font, SmallSize + 2, TextColor, TextAnchor.MiddleRight);
            SetSize(value.gameObject, width: 96f);

            ScoreRow row = root.AddComponent<ScoreRow>();
            SerializedObject so = new SerializedObject(row);
            Set(so, "background", background);
            Set(so, "txtLabel", label);
            Set(so, "txtMiddle", middle);
            Set(so, "txtValue", value);
            so.ApplyModifiedPropertiesWithoutUndo();

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabFolder}/ScoreRow.prefab");
            Object.DestroyImmediate(root);
            return AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/ScoreRow.prefab").GetComponent<ScoreRow>();
        }

        private static GameObject CreateScoreHeaderPrefab(Font font)
        {
            EnsureFolder(PrefabFolder);

            GameObject root = new GameObject("ScoreHeader", typeof(RectTransform));
            AddVertical(root, new RectOffset(6, 6, 6, 2), 0f);

            NewText("Label", root.transform, font, HeaderSize, DimColor, TextAnchor.MiddleLeft, FontStyle.Bold);

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabFolder}/ScoreHeader.prefab");
            Object.DestroyImmediate(root);
            return saved;
        }

        /// <summary>Trennlinie zwischen Basis-Werten und Multiplikatoren bzw. Ergebnis.</summary>
        private static GameObject CreateScoreDividerPrefab()
        {
            EnsureFolder(PrefabFolder);

            GameObject root = new GameObject("ScoreDivider", typeof(RectTransform));
            SetSize(root, height: 16f);

            Image line = NewImage("Line", root.transform, BorderColor);
            line.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            line.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            line.rectTransform.sizeDelta = new Vector2(0f, 2f);
            line.rectTransform.anchoredPosition = Vector2.zero;

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabFolder}/ScoreDivider.prefab");
            Object.DestroyImmediate(root);
            return saved;
        }

        private static GameObject CreateBindingRowPrefab(Font font)
        {
            EnsureFolder(PrefabFolder);

            GameObject root = new GameObject("BindingRow", typeof(RectTransform));
            AddHorizontal(root, new RectOffset(0, 0, 0, 0), 12f, TextAnchor.MiddleLeft);
            SetSize(root, height: RowHeight);

            // Wichtig: Das Label ist das ERSTE Text-Kind (SettingsPanel nutzt GetComponentInChildren<Text>)
            Text label = NewText("Label", root.transform, font, BodySize, TextColor, TextAnchor.MiddleLeft);
            SetSize(label.gameObject, flexibleWidth: 1f);

            Button key = NewButton("Key", root.transform, ButtonColor);
            SetSize(key.gameObject, 260f, 30f);
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
            canvas.sortingOrder = FpsSortingOrder;

            CanvasScaler scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            Text text = NewText("Fps", go.transform, font, SmallSize + 2, TextColor, TextAnchor.UpperRight, FontStyle.Bold);
            RectTransform rect = text.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-16f, -12f);
            rect.sizeDelta = new Vector2(240f, 30f);
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

        /// <summary>Übernimmt colorWeapon / colorPerk / colorMutator aus LevelLoadoutUI, falls in einer geöffneten Szene vorhanden.</summary>
        private static void CopyLoadoutColors(SerializedObject pm)
        {
            LevelLoadoutUI ui = Object.FindFirstObjectByType<LevelLoadoutUI>(FindObjectsInactive.Include);
            if (ui == null) return;

            SerializedObject source = new SerializedObject(ui);

            foreach (string field in new[] { "colorWeapon", "colorPerk", "colorMutator" })
            {
                SerializedProperty from = source.FindProperty(field);
                SerializedProperty to = pm.FindProperty(field);

                if (from != null && to != null && from.colorValue.a > 0.01f)
                    to.colorValue = from.colorValue;
            }
        }

        private static RectTransform NewPage(string name, Transform parent)
        {
            RectTransform page = NewRect(name, parent);
            Stretch(page);
            AddVertical(page.gameObject, new RectOffset(0, 0, 6, 0), 6f);
            return page;
        }

        private static RectTransform NewRow(string name, Transform parent, Font font, string label, float height = RowHeight)
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

            // Breit genug für "1920 x 1080 @ 60Hz" in der Pixelschrift, einzeilig
            value = NewText("Value", row, font, BodySize, TextColor, TextAnchor.MiddleCenter, FontStyle.Bold);
            SetSize(value.gameObject, width: 380f);

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
            SetSize(back.gameObject, 300f, 18f);
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

            // Breit genug für "1.00" (Pixelschrift), einzeilig
            valueText = NewText("Value", row, font, BodySize, TextColor, TextAnchor.MiddleRight, FontStyle.Bold);
            SetSize(valueText.gameObject, width: 90f);

            return slider;
        }

        private static Button NewResetRow(Transform parent, Font font, string label)
        {
            RectTransform row = NewRect("ResetRow", parent);
            AddHorizontal(row.gameObject, new RectOffset(0, 0, 8, 0), 0f, TextAnchor.MiddleRight);
            SetSize(row.gameObject, height: 46f);

            Button button = NewMenuButton(row, font, label, SmallSize + 2, 36f);
            SetSize(button.gameObject, width: 380f);
            return button;
        }

        // ------------------------------------------------------------------
        // Bausteine (gleiche Idee wie im InteractionPopupBuilder)
        // ------------------------------------------------------------------

        /// <summary>
        /// Spalte mit Mindestbreite. Zusätzlicher Platz wird im Verhältnis der Breiten verteilt,
        /// dadurch füllen die Spalten die Fläche innerhalb der Ränder (wie das bisherige Layout).
        /// </summary>
        private static RectTransform NewColumn(string name, Transform parent, float width)
        {
            RectTransform column = NewRect(name, parent);
            AddVertical(column.gameObject, new RectOffset(0, 0, 0, 0), 14f);
            SetSize(column.gameObject, width: width, flexibleWidth: width / 100f);
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

        /// <summary>
        /// Standard: einzeilig (kein Umbruch, Text läuft bei Platzmangel über, statt in die nächste Zeile zu rutschen).
        /// Mit wrap: true für Beschreibungen und Fließtext.
        /// </summary>
        private static Text NewText(string name, Transform parent, Font font, int size, Color color,
            TextAnchor anchor, FontStyle style = FontStyle.Normal, bool wrap = false)
        {
            RectTransform rect = NewRect(name, parent);
            Text text = rect.gameObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.color = color;
            text.alignment = anchor;
            text.fontStyle = style;
            text.horizontalOverflow = wrap ? HorizontalWrapMode.Wrap : HorizontalWrapMode.Overflow;
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


#if UNITY_EDITOR


namespace TowerDefense.EditorTools
{
    /// <summary>
    /// Baut den Loadout-Screen (Stil des Pausemenüs) in der Karten-Szene und ersetzt die alte LevelLoadoutUI:
    ///   - übernimmt Farben (colorWeapon/Perk/Mutator) und die Start-Button-Events des alten Screens
    ///     (ohne den StartLevel-Aufruf, den der neue Start-Button selbst macht)
    ///   - blendet die alte UI aus, ohne Objekte zu deaktivieren, auf denen deine Manager liegen
    /// Aufruf: Tools > UI > Loadout-Screen neu aufbauen
    /// </summary>
    public static partial class PauseMenuBuilder
    {
        private const string LoadoutRootName = "LoadoutCanvas (neu)";
        private const int LoadoutSortingOrder = 300;

        // Objekte, die diese Komponenten tragen, werden nie deaktiviert (nur ihre UI-Kinder)
        private static readonly string[] KeepAliveTypeNames =
        {
            "LevelSelectorManager", "LevelLoadoutManager", "LevelProgressManager", "ResearchManager",
            "LevelUnlocker", "SceneFadeIn", "EventSystem", "Camera", "AudioListener", "AudioSource"
        };

        [MenuItem("Tools/UI/Loadout-Screen neu aufbauen")]
        private static void BuildLoadoutMenu() => BuildLoadout();

        private static void BuildLoadout()
        {
            LevelSelectorManager selector = Object.FindFirstObjectByType<LevelSelectorManager>(FindObjectsInactive.Include);

            if (selector == null)
            {
                EditorUtility.DisplayDialog("Loadout-Screen",
                    "In der Szene wurde kein LevelSelectorManager gefunden. Öffne die Karten-/Level-Auswahl-Szene.", "OK");
                return;
            }

            Font font = FindFont();

            // Vorherige neue Version entfernen, dann alte (Legacy-)Komponenten einsammeln
            foreach (LevelLoadoutUI ui in Object.FindObjectsByType<LevelLoadoutUI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (ui.gameObject.name == LoadoutRootName)
                    Undo.DestroyObjectImmediate(ui.gameObject);
            }

            LevelLoadoutUI[] legacy = Object.FindObjectsByType<LevelLoadoutUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            EnsureEventSystem();

            // ---------- Prefabs ----------
            GameObject conditionIconPrefab = CreateConditionIconPrefab();
            GameObject questPrefab = CreateQuestPrefab(font, conditionIconPrefab);
            GameObject loadoutButtonPrefab = CreateLoadoutButtonPrefab();
            GameObject enemyIconPrefab = CreateEnemyIconPrefab();
            GameObject statRowPrefab = CreateEnemyStatRowPrefab(font);
            GameObject statHeaderPrefab = CreateEnemyStatHeaderPrefab(font);

            // ---------- Canvas ----------
            GameObject root = new GameObject(LoadoutRootName, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(root, "Loadout-Screen neu aufbauen");

            Canvas canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = LoadoutSortingOrder;

            CanvasScaler scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            root.AddComponent<GraphicRaycaster>();
            LevelLoadoutUI loadoutUi = root.AddComponent<LevelLoadoutUI>();

            RectTransform screen = NewRect("Screen", root.transform);
            Stretch(screen);
            CanvasGroup canvasGroup = screen.gameObject.AddComponent<CanvasGroup>();

            Image dim = NewImage("Dim", screen, DimBackColor, true);
            Stretch(dim.rectTransform);

            RectTransform main = NewRect("MainPanel", screen);
            Stretch(main);
            AddHorizontal(main.gameObject, ScreenPadding, 24f, TextAnchor.UpperCenter);

            RectTransform left = NewColumn("Left", main, 440f);
            RectTransform middle = NewColumn("Middle", main, 620f);
            RectTransform right = NewColumn("Right", main, 440f);

            // ======================================================
            // LINKS: Level, Spieler-Level, Gegner, Quests
            // ======================================================
            Text txtLevelName = NewText("LevelName", left, font, TitleSize - 4, TextColor, TextAnchor.MiddleLeft, FontStyle.Bold);
            SetSize(txtLevelName.gameObject, height: 72f);

            RectTransform levelCard = CreateCard("LevelCard", left, out _);
            SectionHeader(levelCard, font, "Mission");

            Text txtLevelDescription = NewText("Description", levelCard, font, SmallSize + 2, DimColor, TextAnchor.UpperLeft, wrap: true);
            txtLevelDescription.lineSpacing = 1.3f;
            MinHeight(txtLevelDescription.gameObject, 48f);

            Image levelDivider = NewImage("Divider", levelCard, DividerColor);
            SetSize(levelDivider.gameObject, height: 1f);

            RectTransform highscoreRow = NewRow("Highscore", levelCard, font, "Highscore");
            Text txtHighscore = NewText("Value", highscoreRow, font, BodySize, GoldColor, TextAnchor.MiddleRight, FontStyle.Bold);

            // Spieler-Level
            RectTransform playerRoot = NewRect("PlayerLevel", levelCard);
            AddVertical(playerRoot.gameObject, new RectOffset(0, 0, 4, 0), 6f);

            RectTransform playerRow = NewRect("Row", playerRoot);
            AddHorizontal(playerRow.gameObject, new RectOffset(0, 0, 0, 0), 8f, TextAnchor.MiddleLeft);
            SetSize(playerRow.gameObject, height: 28f);
            Text txtPlayerLevel = NewText("Level", playerRow, font, BodySize, TextColor, TextAnchor.MiddleLeft, FontStyle.Bold);
            SetSize(txtPlayerLevel.gameObject, flexibleWidth: 1f);
            Text txtPlayerXp = NewText("Xp", playerRow, font, SmallSize + 2, DimColor, TextAnchor.MiddleRight);

            Image playerBar = NewImage("Bar", playerRoot, BarBackColor);
            SetSize(playerBar.gameObject, height: 14f);
            AddBorder(playerBar.gameObject);
            Image playerFill = NewImage("Fill", playerBar.transform, SelectedColor);
            playerFill.rectTransform.anchorMin = new Vector2(0f, 0f);
            playerFill.rectTransform.anchorMax = new Vector2(0f, 1f);
            playerFill.rectTransform.offsetMin = new Vector2(0f, 2f);
            playerFill.rectTransform.offsetMax = new Vector2(0f, -2f);

            RectTransform nextRow = NewRect("Next", playerRoot);
            AddHorizontal(nextRow.gameObject, new RectOffset(0, 0, 4, 0), 10f, TextAnchor.MiddleLeft);
            Image nextBack = NewImage("IconBack", nextRow, KeyColor);
            SetSize(nextBack.gameObject, 40f, 40f);
            AddBorder(nextBack.gameObject);
            Image nextIcon = NewImage("Icon", nextBack.transform, Color.white);
            nextIcon.preserveAspect = true;
            Stretch(nextIcon.rectTransform);
            nextIcon.rectTransform.offsetMin = new Vector2(4f, 4f);
            nextIcon.rectTransform.offsetMax = new Vector2(-4f, -4f);
            Text txtNext = NewText("Text", nextRow, font, SmallSize, DimColor, TextAnchor.MiddleLeft, wrap: true);
            SetSize(txtNext.gameObject, flexibleWidth: 1f);

            // Gegner-Vorschau
            RectTransform enemyCard = CreateCard("EnemyCard", left, out GameObject enemyOuter);
            SectionHeader(enemyCard, font, "Enemies");
            RectTransform enemyGrid = NewRect("Icons", enemyCard);
            GridLayoutGroup enemyLayout = enemyGrid.gameObject.AddComponent<GridLayoutGroup>();
            enemyLayout.cellSize = new Vector2(52f, 52f);
            enemyLayout.spacing = new Vector2(6f, 6f);
            enemyLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            enemyLayout.constraintCount = 7;
            enemyLayout.childAlignment = TextAnchor.UpperLeft;
            MinHeight(enemyGrid.gameObject, 52f);
            Text enemyHint = NewText("Hint", enemyCard, font, SmallSize, DimColor, TextAnchor.UpperLeft);
            enemyHint.text = "Click an enemy to see its stats.";

            // Quests
            RectTransform questCard = CreateCard("QuestCard", left, out _);
            SectionHeader(questCard, font, "Quests");
            RectTransform questScroll = NewScroll("QuestScroll", questCard, 200f, 6f);

            // ======================================================
            // MITTE: Waffe, Perks, Mutatoren
            // ======================================================
            RectTransform weaponCard = CreateCard("WeaponCard", middle, out _);
            Text txtWeaponCounter = CounterHeader(weaponCard, font, "Weapon");
            RectTransform weaponGrid = NewSelectionGrid(weaponCard);

            RectTransform perkCard = CreateCard("PerkCard", middle, out _);
            Text txtPerkCounter = CounterHeader(perkCard, font, "Perks");
            RectTransform perkGrid = NewSelectionGrid(perkCard);

            RectTransform mutatorCard = CreateCard("MutatorCard", middle, out _);
            Text txtMutatorCounter = CounterHeader(mutatorCard, font, "Mutators");
            RectTransform mutatorGrid = NewSelectionGrid(mutatorCard);

            // ======================================================
            // RECHTS: Details, Zusammenfassung, Buttons
            // ======================================================
            RectTransform detailCard = CreateCard("DetailCard", right, out _);
            SectionHeader(detailCard, font, "Details");

            Text detailEmpty = NewText("Empty", detailCard, font, SmallSize + 2, DimColor, TextAnchor.UpperLeft, wrap: true);
            detailEmpty.text = "Select a weapon, perk or mutator to see its details.";
            MinHeight(detailEmpty.gameObject, 60f);

            RectTransform detailRoot = NewRect("Content", detailCard);
            AddVertical(detailRoot.gameObject, new RectOffset(0, 0, 0, 0), 8f);

            RectTransform detailHead = NewRect("Head", detailRoot);
            AddHorizontal(detailHead.gameObject, new RectOffset(0, 0, 0, 0), 14f, TextAnchor.UpperLeft);

            Image detailIconBack = NewImage("IconBack", detailHead, KeyColor);
            SetSize(detailIconBack.gameObject, 88f, 88f);
            AddBorder(detailIconBack.gameObject);
            Image detailIcon = NewImage("Icon", detailIconBack.transform, Color.white);
            detailIcon.preserveAspect = true;
            Stretch(detailIcon.rectTransform);
            detailIcon.rectTransform.offsetMin = new Vector2(8f, 8f);
            detailIcon.rectTransform.offsetMax = new Vector2(-8f, -8f);

            RectTransform detailHeadText = NewRect("Text", detailHead);
            AddVertical(detailHeadText.gameObject, new RectOffset(0, 0, 0, 0), 4f);
            SetSize(detailHeadText.gameObject, flexibleWidth: 1f);
            Text txtDetailName = NewText("Name", detailHeadText, font, BodySize + 2, GoldColor, TextAnchor.UpperLeft, FontStyle.Bold);
            Text txtDetailType = NewText("Type", detailHeadText, font, SmallSize, DimColor, TextAnchor.UpperLeft);
            Text txtDetailMultiplier = NewText("Multiplier", detailHeadText, font, SmallSize + 2, DimColor, TextAnchor.UpperLeft, FontStyle.Bold);

            Image detailDivider = NewImage("Divider", detailRoot, DividerColor);
            SetSize(detailDivider.gameObject, height: 1f);

            Text txtDetailBody = NewText("Body", detailRoot, font, SmallSize + 2, DimColor, TextAnchor.UpperLeft, wrap: true);
            txtDetailBody.lineSpacing = 1.3f;
            MinHeight(txtDetailBody.gameObject, 150f);

            RectTransform summaryCard = CreateCard("SummaryCard", right, out _);
            SectionHeader(summaryCard, font, "Loadout");

            RectTransform weaponSummaryRow = NewRow("Weapon", summaryCard, font, "Weapon");
            Text txtSummaryWeapon = NewText("Value", weaponSummaryRow, font, BodySize, TextColor, TextAnchor.MiddleRight, FontStyle.Bold);

            RectTransform multiplierRow = NewRow("Multiplier", summaryCard, font, "Score multiplier");
            Text txtSummaryMultiplier = NewText("Value", multiplierRow, font, BodySize, TextColor, TextAnchor.MiddleRight, FontStyle.Bold);

            Text txtStartHint = NewText("Hint", summaryCard, font, SmallSize + 2, DimColor, TextAnchor.MiddleCenter);
            SetSize(txtStartHint.gameObject, height: 28f);

            Button btnStart = NewMenuButton(summaryCard, font, "Start", ButtonSize + 4, 56f);
            btnStart.GetComponent<Image>().color = new Color(0.55f, 0.36f, 0.15f, 1f);
            btnStart.GetComponentInChildren<Text>().color = GoldColor;

            RectTransform smallButtons = NewRect("Buttons", summaryCard);
            AddHorizontal(smallButtons.gameObject, new RectOffset(0, 0, 0, 0), 10f, TextAnchor.MiddleCenter);
            SetSize(smallButtons.gameObject, height: 42f);
            Button btnClear = NewMenuButton(smallButtons, font, "Clear", SmallSize + 2, 42f);
            SetSize(btnClear.gameObject, flexibleWidth: 1f);
            Button btnBack = NewMenuButton(smallButtons, font, "Back", SmallSize + 2, 42f);
            SetSize(btnBack.gameObject, flexibleWidth: 1f);

            // ======================================================
            // Gegner-Panel (Overlay)
            // ======================================================
            EnemyInfoPanel enemyPanel = CreateEnemyInfoOverlay(screen, font, statRowPrefab, statHeaderPrefab);

            // ======================================================
            // Verdrahten
            // ======================================================
            SerializedObject so = new SerializedObject(loadoutUi);

            Set(so, "panel", screen.gameObject);
            Set(so, "canvasGroup", canvasGroup);

            Set(so, "txtLevelName", txtLevelName);
            Set(so, "txtLevelDescription", txtLevelDescription);
            Set(so, "txtHighscore", txtHighscore);

            Set(so, "playerLevelRoot", playerRoot.gameObject);
            Set(so, "txtPlayerLevel", txtPlayerLevel);
            Set(so, "txtPlayerXp", txtPlayerXp);
            Set(so, "playerBarFill", playerFill.rectTransform);
            Set(so, "nextUnlockRoot", nextRow.gameObject);
            Set(so, "nextUnlockIcon", nextIcon);
            Set(so, "txtNextUnlock", txtNext);

            Set(so, "enemyCard", enemyOuter);
            Set(so, "enemyContainer", enemyGrid);
            Set(so, "enemyIconPrefab", enemyIconPrefab);
            Set(so, "enemyInfoPanel", enemyPanel);

            Set(so, "weaponContainer", weaponGrid);
            Set(so, "perkContainer", perkGrid);
            Set(so, "mutatorContainer", mutatorGrid);
            Set(so, "questContainer", questScroll);
            Set(so, "txtWeaponCounter", txtWeaponCounter);
            Set(so, "txtPerkCounter", txtPerkCounter);
            Set(so, "txtMutatorCounter", txtMutatorCounter);

            Set(so, "detailEmpty", detailEmpty.gameObject);
            Set(so, "detailRoot", detailRoot.gameObject);
            Set(so, "detailIcon", detailIcon);
            Set(so, "txtDetailType", txtDetailType);
            Set(so, "txtDetailName", txtDetailName);
            Set(so, "txtDetailMultiplier", txtDetailMultiplier);
            Set(so, "txtDetailBody", txtDetailBody);

            Set(so, "txtSummaryWeapon", txtSummaryWeapon);
            Set(so, "txtSummaryMultiplier", txtSummaryMultiplier);
            Set(so, "txtStartHint", txtStartHint);
            Set(so, "btnStart", btnStart);
            Set(so, "btnClear", btnClear);
            Set(so, "btnBack", btnBack);

            Set(so, "questPrefab", questPrefab);
            Set(so, "btnPrefab", loadoutButtonPrefab);

            // ---------- Altes übernehmen: Farben, Start-Events ----------
            int hidden = 0;
            int copiedCalls = 0;

            foreach (LevelLoadoutUI old in legacy)
            {
                if (old == null || old == loadoutUi) continue;

                SerializedObject oldSo = new SerializedObject(old);

                foreach (string field in new[] { "colorWeapon", "colorPerk", "colorMutator" })
                {
                    SerializedProperty from = oldSo.FindProperty(field);
                    SerializedProperty to = so.FindProperty(field);

                    if (from != null && to != null && from.colorValue.a > 0.01f)
                        to.colorValue = from.colorValue;
                }

                SerializedProperty oldPanel = oldSo.FindProperty("panel");
                GameObject oldPanelObject = oldPanel != null ? oldPanel.objectReferenceValue as GameObject : null;
                hidden += HideLegacy(oldPanelObject);
            }

            // Start-Button des alten Screens: Szenenwechsel-Aufrufe übernehmen (StartLevel macht der neue Button selbst)
            Button oldStart = FindButtonByName("Btn_Start", root.transform);
            if (oldStart != null)
                copiedCalls = CopyClickCalls(oldStart, so.FindProperty("onStartConfirmed.m_PersistentCalls.m_Calls"), "StartLevel");

            so.ApplyModifiedProperties();

            // Alte Komponenten entfernen (gleiche Klasse: sonst gäbe es zwei Singletons)
            foreach (LevelLoadoutUI old in legacy)
            {
                if (old != null && old != loadoutUi)
                    Undo.DestroyObjectImmediate(old);
            }

            // Der alte Loadout-Screen hing am Selector-Canvas: Rest der alten UI ebenfalls ausblenden
            Canvas selectorCanvas = selector.GetComponentInParent<Canvas>();
            if (selectorCanvas != null)
                hidden += HideLegacy(selectorCanvas.rootCanvas.gameObject);

            enemyPanel.gameObject.SetActive(false);
            screen.gameObject.SetActive(false);

            EditorSceneManager.MarkSceneDirty(root.scene);
            Selection.activeGameObject = root;

            Debug.Log($"Loadout-Screen aufgebaut. {hidden} alte UI-Objekte ausgeblendet, {copiedCalls} Start-Aufrufe übernommen.\n" +
                      "Falls der Szenenwechsel bisher nicht am Start-Button hing: LevelLoadoutUI > 'On Start Confirmed' eintragen.\n" +
                      "Optional: LevelDefinition > 'Enemy Preview' füllen (Gegner-Vorschau).");
        }

        // ------------------------------------------------------------------
        // Bausteine nur für den Loadout-Screen
        // ------------------------------------------------------------------

        /// <summary>Titel links, Zähler ("2/5") rechts, darunter die Akzentlinie.</summary>
        private static Text CounterHeader(Transform parent, Font font, string title)
        {
            RectTransform row = NewRect("Header", parent);
            AddHorizontal(row.gameObject, new RectOffset(0, 0, 0, 0), 8f, TextAnchor.MiddleLeft);

            Text label = NewText("Title", row, font, HeaderSize, DimColor, TextAnchor.MiddleLeft, FontStyle.Bold);
            label.text = title.ToUpperInvariant();
            SetSize(label.gameObject, flexibleWidth: 1f);

            Text counter = NewText("Counter", row, font, HeaderSize + 2, DimColor, TextAnchor.MiddleRight, FontStyle.Bold);

            Image accent = NewImage("Accent", parent, BorderColor);
            SetSize(accent.gameObject, height: 2f);

            return counter;
        }

        private static RectTransform NewSelectionGrid(Transform parent)
        {
            RectTransform grid = NewRect("Items", parent);
            GridLayoutGroup layout = grid.gameObject.AddComponent<GridLayoutGroup>();
            layout.cellSize = new Vector2(64f, 64f);
            layout.spacing = new Vector2(6f, 6f);
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = 8;
            layout.childAlignment = TextAnchor.UpperLeft;
            MinHeight(grid.gameObject, 64f);
            return grid;
        }

        private static GameObject CreateEnemyIconPrefab()
        {
            EnsureFolder(PrefabFolder);

            GameObject root = new GameObject("EnemyIcon", typeof(RectTransform));
            Image background = root.AddComponent<Image>();
            background.color = KeyColor;
            AddBorder(root);
            SetSize(root, 52f, 52f);

            Button button = root.AddComponent<Button>();
            button.targetGraphic = background;
            button.colors = ButtonColors();
            button.navigation = new Navigation { mode = Navigation.Mode.None };

            Image icon = NewImage("Icon", root.transform, Color.white);
            icon.preserveAspect = true;
            Stretch(icon.rectTransform);
            icon.rectTransform.offsetMin = new Vector2(4f, 4f);
            icon.rectTransform.offsetMax = new Vector2(-4f, -4f);

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabFolder}/EnemyIcon.prefab");
            Object.DestroyImmediate(root);
            return saved;
        }

        /// <summary>Gleiches Gegner-Panel wie im Pausemenü, hier als Overlay im Loadout-Screen.</summary>
        private static EnemyInfoPanel CreateEnemyInfoOverlay(Transform parent, Font font, GameObject statRowPrefab, GameObject statHeaderPrefab)
        {
            Image enemyRoot = NewImage("EnemyInfoPanel", parent, new Color(0f, 0f, 0f, 0.6f), true);
            Stretch(enemyRoot.rectTransform);
            VerticalLayoutGroup layout = AddVertical(enemyRoot.gameObject, new RectOffset(0, 0, 0, 0), 0f);
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childForceExpandWidth = false;

            RectTransform card = CreateCard("Card", enemyRoot.transform, out GameObject outer, new RectOffset(24, 24, 18, 20));
            SetSize(outer, width: 640f, flexibleWidth: 0f);

            RectTransform head = NewRect("Head", card);
            AddHorizontal(head.gameObject, new RectOffset(0, 0, 0, 0), 16f, TextAnchor.UpperLeft);

            Image iconBack = NewImage("IconBack", head, KeyColor);
            SetSize(iconBack.gameObject, 104f, 104f);
            AddBorder(iconBack.gameObject);
            Image icon = NewImage("Icon", iconBack.transform, Color.white);
            icon.preserveAspect = true;
            Stretch(icon.rectTransform);
            icon.rectTransform.offsetMin = new Vector2(8f, 8f);
            icon.rectTransform.offsetMax = new Vector2(-8f, -8f);

            RectTransform headText = NewRect("Text", head);
            AddVertical(headText.gameObject, new RectOffset(0, 0, 0, 0), 6f);
            SetSize(headText.gameObject, flexibleWidth: 1f);

            Text name = NewText("Name", headText, font, TitleSize - 12, TextColor, TextAnchor.UpperLeft, FontStyle.Bold);
            Text description = NewText("Description", headText, font, SmallSize, DimColor, TextAnchor.UpperLeft, wrap: true);
            description.lineSpacing = 1.3f;

            Image accent = NewImage("Accent", card, BorderColor);
            SetSize(accent.gameObject, height: 2f);

            RectTransform stats = NewScroll("StatsScroll", card, 380f, 2f);
            Button close = NewMenuButton(card, font, "Close", ButtonSize, 44f);

            EnemyInfoPanel panel = enemyRoot.gameObject.AddComponent<EnemyInfoPanel>();
            SerializedObject so = new SerializedObject(panel);
            Set(so, "icon", icon);
            Set(so, "txtName", name);
            Set(so, "txtDescription", description);
            Set(so, "statContainer", stats);
            Set(so, "statRowPrefab", statRowPrefab);
            Set(so, "statHeaderPrefab", statHeaderPrefab);
            Set(so, "btnClose", close);
            so.ApplyModifiedPropertiesWithoutUndo();

            return panel;
        }

        // ------------------------------------------------------------------
        // Alte UI sicher ausblenden / Verlinkungen übernehmen
        // ------------------------------------------------------------------

        /// <summary>
        /// Deaktiviert ein altes UI-Objekt. Trägt es (oder ein Kind) einen deiner Manager, bleibt es aktiv:
        /// dann werden nur seine eigenen Grafiken ausgeschaltet und die Kinder einzeln behandelt.
        /// Gibt die Zahl der deaktivierten Objekte zurück.
        /// </summary>
        private static int HideLegacy(GameObject go)
        {
            if (go == null) return 0;

            if (!HostsKeepAlive(go))
            {
                if (!go.activeSelf) return 0;

                Undo.RecordObject(go, "Alte UI ausblenden");
                go.SetActive(false);
                return 1;
            }

            foreach (Graphic graphic in go.GetComponents<Graphic>())
            {
                Undo.RecordObject(graphic, "Alte UI ausblenden");
                graphic.enabled = false;
            }

            int count = 0;
            foreach (Transform child in go.transform)
                count += HideLegacy(child.gameObject);

            return count;
        }

        private static bool HostsKeepAlive(GameObject go)
        {
            foreach (Component component in go.GetComponentsInChildren<Component>(true))
            {
                if (component == null) continue;

                string typeName = component.GetType().Name;
                foreach (string keep in KeepAliveTypeNames)
                    if (typeName == keep)
                        return true;
            }

            return false;
        }

        private static Button FindButtonByName(string buttonName, Transform exclude)
        {
            foreach (Button button in Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (button.name == buttonName && !button.transform.IsChildOf(exclude))
                    return button;
            }

            return null;
        }

        /// <summary>Kopiert die Inspector-Aufrufe eines Buttons in ein UnityEvent und lässt eine Methode aus.</summary>
        /// <summary>Kopiert die Inspector-Aufrufe eines Buttons in ein UnityEvent und lässt eine Methode aus.</summary>
        private static int CopyClickCalls(Button source, SerializedProperty destinationCalls, string skipMethod)
        {
            SerializedObject sourceObject = new SerializedObject(source);
            SerializedProperty sourceCalls = sourceObject.FindProperty("m_OnClick.m_PersistentCalls.m_Calls");

            if (sourceCalls == null || destinationCalls == null)
                return 0;

            destinationCalls.ClearArray();
            int copied = 0;

            for (int i = 0; i < sourceCalls.arraySize; i++)
            {
                SerializedProperty sourceCall = sourceCalls.GetArrayElementAtIndex(i);
                string methodName = sourceCall.FindPropertyRelative("m_MethodName").stringValue;

                if (methodName == skipMethod)
                    continue;

                int destinationIndex = destinationCalls.arraySize;
                destinationCalls.InsertArrayElementAtIndex(destinationIndex);

                SerializedProperty destinationCall = destinationCalls.GetArrayElementAtIndex(destinationIndex);

                CopyProperty(sourceCall, destinationCall, "m_Target");
                CopyProperty(sourceCall, destinationCall, "m_TargetAssemblyTypeName");
                CopyProperty(sourceCall, destinationCall, "m_MethodName");
                CopyProperty(sourceCall, destinationCall, "m_Mode");
                CopyProperty(sourceCall, destinationCall, "m_Arguments");
                CopyProperty(sourceCall, destinationCall, "m_CallState");

                copied++;
            }

            return copied;
        }

        private static void CopyProperty(SerializedProperty source, SerializedProperty destination, string propertyName)
        {
            SerializedProperty sourceProperty = source.FindPropertyRelative(propertyName);
            SerializedProperty destinationProperty = destination.FindPropertyRelative(propertyName);

            if (sourceProperty == null || destinationProperty == null)
                return;

            destinationProperty.serializedObject.CopyFromSerializedProperty(sourceProperty);
        }
        /// <summary>Schrift aus irgendeinem Text der Szene (Pixelschrift), die nicht die Standardschrift ist.</summary>
        private static Font FindSceneFont()
        {
            foreach (Text text in Object.FindObjectsByType<Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Font font = text.font;
                if (font == null) continue;

                string fontName = font.name;
                if (fontName.Contains("Arial") || fontName.Contains("LegacyRuntime")) continue;

                return font;
            }

            return null;
        }
    }
}
#endif

#if UNITY_EDITOR

namespace TowerDefense.EditorTools
{
    /// <summary>
    /// Hauptmenü + begehbare Übersichtskarte (Stil des Pausemenüs).
    ///
    ///   MainMenuCanvas   Play / Research / Settings / Quit (ESC = Pause der Karte)
    ///   Overworld        Held (WASD), ein Level-Knoten pro LevelDefinition im Projekt, Platzhalter-Boden, Kamera-Follow
    ///   OverworldUI      Popup über dem Knoten in Reichweite: Name, Highscore, Quests, "Press [E] to select"
    ///
    /// Übernimmt deine bestehende Szene:
    ///   - ResearchCanvas bleibt unangetastet, "Research" blendet es ein
    ///   - alte Hauptmenü-UI und alte Level-Auswahl werden ausgeblendet (Objekte mit deinen Managern bleiben aktiv)
    ///   - Button-Aufrufe, die das alte Hauptmenü wieder einschalten wollten, werden auf das neue umgeleitet
    ///
    /// Aufruf: Tools > UI > Hauptmenü + Übersichtskarte neu aufbauen
    ///         oder Tools > UI > Alles für die Karten-Szene (inkl. Loadout-Screen)
    /// </summary>
    public static partial class PauseMenuBuilder
    {
        private const string MenuRootName = "MainMenuCanvas (neu)";
        private const string OverworldRootName = "Overworld (neu)";
        private const string OverworldUiName = "OverworldUI (neu)";
        private const int MenuSortingOrder = 100;
        private const int OverworldUiSortingOrder = 20;

        [MenuItem("Tools/UI/Hauptmenü + Übersichtskarte neu aufbauen")]
        private static void BuildMainMenuMenu() => BuildMainMenuAndOverworld();

        [MenuItem("Tools/UI/Alles für die Karten-Szene (Hauptmenü, Karte, Loadout)")]
        private static void BuildEverythingForMap()
        {
            BuildLoadout();
            BuildMainMenuAndOverworld();
        }

        private static void BuildMainMenuAndOverworld()
        {
            Font font = FindFont();

            // ---------- Vorherige Version entfernen ----------
            foreach (MainMenuUI old in Object.FindObjectsByType<MainMenuUI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Undo.DestroyObjectImmediate(old.gameObject);

            foreach (LevelNodePopup old in Object.FindObjectsByType<LevelNodePopup>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Undo.DestroyObjectImmediate(old.gameObject);

            GameObject previousOverworld = FindSceneRoot(OverworldRootName);
            if (previousOverworld != null)
                Undo.DestroyObjectImmediate(previousOverworld);

            EnsureEventSystem();

            // ---------- Bestehende Szene erkennen ----------
            GameObject oldMenu = FindOldMenuCanvas();
            GameObject research = FindSceneRoot("ResearchCanvas");

            // ---------- Prefabs ----------
            GameObject bindingRowPrefab = CreateBindingRowPrefab(font);
            GameObject nodeQuestRowPrefab = CreateNodeQuestRowPrefab(font);

            // ======================================================
            // MAIN MENU CANVAS
            // ======================================================
            GameObject root = new GameObject(MenuRootName, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(root, "Hauptmenü neu aufbauen");

            Canvas canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = MenuSortingOrder;

            CanvasScaler scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            root.AddComponent<GraphicRaycaster>();
            MainMenuUI menu = root.AddComponent<MainMenuUI>();

            RectTransform menuPanel = NewRect("MenuPanel", root.transform);
            Stretch(menuPanel);

            // Abdunklung: die Karte bleibt dahinter sichtbar
            Image menuDim = NewImage("Dim", menuPanel, new Color(DimBackColor.r, DimBackColor.g, DimBackColor.b, 0.62f), true);
            Stretch(menuDim.rectTransform);

            RectTransform column = NewRect("Column", menuPanel);
            column.anchorMin = new Vector2(0f, 0f);
            column.anchorMax = new Vector2(0f, 1f);
            column.pivot = new Vector2(0f, 0.5f);
            column.sizeDelta = new Vector2(480f, -160f);
            column.anchoredPosition = new Vector2(100f, 0f);
            VerticalLayoutGroup columnLayout = AddVertical(column.gameObject, new RectOffset(0, 0, 0, 0), 14f);
            columnLayout.childAlignment = TextAnchor.MiddleLeft;

            Text title = NewText("Title", column, font, TitleSize + 24, TextColor, TextAnchor.MiddleLeft, FontStyle.Bold);
            title.text = Application.productName;
            SetSize(title.gameObject, height: 110f);

            Image titleAccent = NewImage("Accent", column, BorderColor);
            SetSize(titleAccent.gameObject, height: 3f);

            RectTransform spacer = NewRect("Spacer", column);
            SetSize(spacer.gameObject, height: 24f);

            Button btnPlay = NewMenuButton(column, font, "Play", ButtonSize + 2, 56f);
            Button btnResearch = NewMenuButton(column, font, "Research", ButtonSize + 2, 56f);
            Button btnSettings = NewMenuButton(column, font, "Settings", ButtonSize + 2, 56f);
            Button btnQuit = NewMenuButton(column, font, "Quit", ButtonSize + 2, 56f);

            Text txtProfile = NewText("Profile", column, font, SmallSize + 2, DimColor, TextAnchor.MiddleLeft);
            SetSize(txtProfile.gameObject, height: 40f);

            Text version = NewText("Version", menuPanel, font, SmallSize, DimColor, TextAnchor.LowerRight);
            version.text = "v" + Application.version;
            version.rectTransform.anchorMin = version.rectTransform.anchorMax = new Vector2(1f, 0f);
            version.rectTransform.pivot = new Vector2(1f, 0f);
            version.rectTransform.anchoredPosition = new Vector2(-30f, 24f);
            version.rectTransform.sizeDelta = new Vector2(300f, 30f);

            SettingsPanel settings = CreateSettingsPanel(root.transform, font, bindingRowPrefab);
            ConfirmDialog quitDialog = CreateConfirmDialog(root.transform, font);

            // ======================================================
            // ÜBERSICHTSKARTE + POPUP
            // ======================================================
            int nodeCount = BuildOverworld(font, nodeQuestRowPrefab);

            // ======================================================
            // MainMenuUI verdrahten
            // ======================================================
            SerializedObject so = new SerializedObject(menu);
            Set(so, "menuPanel", menuPanel.gameObject);
            Set(so, "settingsPanel", settings);
            Set(so, "confirmDialog", quitDialog);
            Set(so, "researchCanvas", research);
            Set(so, "btnPlay", btnPlay);
            Set(so, "txtPlay", btnPlay.GetComponentInChildren<Text>());
            Set(so, "btnResearch", btnResearch);
            Set(so, "btnSettings", btnSettings);
            Set(so, "btnQuit", btnQuit);
            Set(so, "txtProfile", txtProfile);
            so.ApplyModifiedProperties();

            // ======================================================
            // Alte UI übernehmen / ausblenden
            // ======================================================
            int retargeted = oldMenu != null ? RetargetOldMenuCalls(oldMenu, root) : 0;
            int hidden = 0;

            if (oldMenu != null)
                hidden += HideLegacy(oldMenu);

            LevelSelectorManager selector = Object.FindFirstObjectByType<LevelSelectorManager>(FindObjectsInactive.Include);
            Canvas selectorCanvas = selector != null ? selector.GetComponentInParent<Canvas>() : null;
            if (selectorCanvas != null && !IsNewUi(selectorCanvas.rootCanvas.gameObject))
                hidden += HideLegacy(selectorCanvas.rootCanvas.gameObject);

            settings.gameObject.SetActive(false);
            quitDialog.gameObject.SetActive(false);

            EditorSceneManager.MarkSceneDirty(root.scene);
            Selection.activeGameObject = root;

            Debug.Log($"Hauptmenü + Übersichtskarte aufgebaut: {nodeCount} Level-Knoten, {hidden} alte UI-Objekte ausgeblendet, " +
                      $"{retargeted} Button-Aufrufe auf das neue Menü umgeleitet.\n" +
                      (research == null ? "Kein 'ResearchCanvas' gefunden: der Research-Button bleibt ausgeblendet.\n" : "") +
                      (oldMenu == null ? "Kein altes Hauptmenü ('Canvas' im Szenen-Root) gefunden. Wähle es vorher aus, wenn es anders heißt.\n" : "") +
                      "Der Held ist ein Platzhalter-Kreis: Sprites im OverworldHero zuweisen (Idle/Walk). " +
                      "Der graue Platzhalter-Boden kann gelöscht werden, sobald deine Map-Tiles stehen.\n" +
                      $"Canvas-Sorting: Menü {MenuSortingOrder}, Loadout {LoadoutSortingOrder}. Das Fade-Canvas muss höher liegen.");
        }

        // ------------------------------------------------------------------
        // Übersichtskarte
        // ------------------------------------------------------------------

        private static int BuildOverworld(Font font, GameObject questRowPrefab)
        {
            Sprite knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            Sprite square = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

            GameObject overworld = new GameObject(OverworldRootName);
            Undo.RegisterCreatedObjectUndo(overworld, "Übersichtskarte aufbauen");

            // Platzhalter-Boden (hinter allem, damit deine Tilemap darüber liegt)
            GameObject ground = new GameObject("Placeholder Ground (löschen, sobald die Map steht)");
            ground.transform.SetParent(overworld.transform);
            SpriteRenderer groundRenderer = ground.AddComponent<SpriteRenderer>();
            groundRenderer.sprite = square;
            groundRenderer.drawMode = SpriteDrawMode.Sliced;
            groundRenderer.size = new Vector2(140f, 80f);
            groundRenderer.color = new Color(0.30f, 0.46f, 0.26f);
            groundRenderer.sortingOrder = -200;

            // Level aus dem Projekt
            List<LevelDefinition> levels = new List<LevelDefinition>();
            foreach (string guid in AssetDatabase.FindAssets("t:LevelDefinition"))
            {
                LevelDefinition level = AssetDatabase.LoadAssetAtPath<LevelDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (level != null) levels.Add(level);
            }
            levels.Sort((a, b) => a.levelIndex.CompareTo(b.levelIndex));

            Vector3[] positions = new Vector3[levels.Count];
            for (int i = 0; i < positions.Length; i++)
                positions[i] = new Vector3(i * 8f, Mathf.Sin(i * 1.3f) * 3f, 0f);

            // Gepunkteter Weg zwischen den Knoten
            GameObject pathRoot = new GameObject("Path");
            pathRoot.transform.SetParent(overworld.transform);

            for (int i = 0; i < positions.Length - 1; i++)
            {
                int dots = Mathf.Max(2, Mathf.FloorToInt(Vector3.Distance(positions[i], positions[i + 1]) / 0.9f));

                for (int k = 1; k < dots; k++)
                {
                    GameObject dot = new GameObject("Dot");
                    dot.transform.SetParent(pathRoot.transform);
                    dot.transform.position = Vector3.Lerp(positions[i], positions[i + 1], k / (float)dots);
                    dot.transform.localScale = Vector3.one * 0.25f;

                    SpriteRenderer dotRenderer = dot.AddComponent<SpriteRenderer>();
                    dotRenderer.sprite = knob;
                    dotRenderer.color = new Color(0.82f, 0.74f, 0.55f, 0.85f);
                    dotRenderer.sortingOrder = -50;
                }
            }

            // Level-Knoten
            GameObject nodesRoot = new GameObject("LevelNodes");
            nodesRoot.transform.SetParent(overworld.transform);

            Font labelFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            for (int i = 0; i < levels.Count; i++)
            {
                LevelDefinition level = levels[i];

                GameObject nodeObject = new GameObject($"LevelNode {level.levelIndex} ({level.levelName})");
                nodeObject.transform.SetParent(nodesRoot.transform);
                nodeObject.transform.position = positions[i];

                GameObject markerObject = new GameObject("Marker");
                markerObject.transform.SetParent(nodeObject.transform, false);
                markerObject.transform.localScale = Vector3.one * 1.6f;

                SpriteRenderer markerRenderer = markerObject.AddComponent<SpriteRenderer>();
                markerRenderer.sprite = knob;
                markerRenderer.sortingOrder = 5;

                if (labelFont != null)
                {
                    GameObject labelObject = new GameObject("Label");
                    labelObject.transform.SetParent(nodeObject.transform, false);

                    TextMesh label = labelObject.AddComponent<TextMesh>();
                    label.font = labelFont;
                    label.text = level.levelIndex.ToString();
                    label.anchor = TextAnchor.MiddleCenter;
                    label.alignment = TextAlignment.Center;
                    label.characterSize = 0.18f;
                    label.fontSize = 64;
                    label.color = Color.black;

                    MeshRenderer labelRenderer = labelObject.GetComponent<MeshRenderer>();
                    labelRenderer.sharedMaterial = labelFont.material;
                    labelRenderer.sortingOrder = 6;
                }

                LevelNode node = nodeObject.AddComponent<LevelNode>();
                SerializedObject nodeSo = new SerializedObject(node);
                Set(nodeSo, "level", level);
                Set(nodeSo, "marker", markerRenderer);
                nodeSo.ApplyModifiedPropertiesWithoutUndo();
            }

            // Held (Platzhalter-Kreis: Sprites im OverworldHero zuweisen)
            GameObject heroObject = new GameObject("Hero (Karte)");
            heroObject.transform.SetParent(overworld.transform);
            heroObject.transform.position = positions.Length > 0 ? positions[0] + new Vector3(-3.5f, -1f, 0f) : Vector3.zero;
            heroObject.transform.localScale = Vector3.one * 0.9f;

            SpriteRenderer heroRenderer = heroObject.AddComponent<SpriteRenderer>();
            heroRenderer.sprite = knob;
            heroRenderer.color = new Color(1f, 0.78f, 0.29f);
            heroRenderer.sortingOrder = 10;

            OverworldHero hero = heroObject.AddComponent<OverworldHero>();
            SerializedObject heroSo = new SerializedObject(hero);
            Set(heroSo, "spriteRenderer", heroRenderer);
            heroSo.ApplyModifiedPropertiesWithoutUndo();

            // Kamera folgt dem Helden
            Camera camera = Camera.main != null ? Camera.main : Object.FindFirstObjectByType<Camera>(FindObjectsInactive.Include);

            if (camera == null)
                Debug.LogWarning("Keine Kamera in der Szene gefunden: OverworldCamera nicht hinzugefügt.");
            else if (camera.GetComponent<OverworldCamera>() == null)
                Undo.AddComponent<OverworldCamera>(camera.gameObject);

            BuildNodePopup(font, questRowPrefab);

            return levels.Count;
        }

        /// <summary>Popup über dem Level-Knoten in Reichweite, im Stil des InteractionPopup.</summary>
        private static void BuildNodePopup(Font font, GameObject questRowPrefab)
        {
            GameObject uiRoot = new GameObject(OverworldUiName, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(uiRoot, "Karten-Popup aufbauen");

            Canvas canvas = uiRoot.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = OverworldUiSortingOrder;

            CanvasScaler scaler = uiRoot.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            LevelNodePopup popup = uiRoot.AddComponent<LevelNodePopup>();

            RectTransform content = CreateCard("Popup", uiRoot.transform, out GameObject outer, new RectOffset(16, 16, 12, 14));

            RectTransform rootRect = (RectTransform)outer.transform;
            rootRect.anchorMin = rootRect.anchorMax = new Vector2(0.5f, 0.5f);
            rootRect.pivot = new Vector2(0.5f, 0f);
            rootRect.sizeDelta = new Vector2(420f, 0f);

            ContentSizeFitter fitter = outer.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            Text txtName = NewText("Name", content, font, BodySize + 4, GoldColor, TextAnchor.UpperLeft, FontStyle.Bold);
            Text txtHighscore = NewText("Highscore", content, font, SmallSize + 2, DimColor, TextAnchor.UpperLeft);

            Image accent = NewImage("Accent", content, BorderColor);
            SetSize(accent.gameObject, height: 2f);

            RectTransform quests = NewRect("Quests", content);
            AddVertical(quests.gameObject, new RectOffset(0, 0, 0, 0), 4f);

            Image divider = NewImage("Divider", content, DividerColor);
            SetSize(divider.gameObject, height: 1f);

            // "Press [E] to select"
            RectTransform prompt = NewRect("Prompt", content);
            AddHorizontal(prompt.gameObject, new RectOffset(0, 0, 0, 0), 8f, TextAnchor.MiddleCenter);
            SetSize(prompt.gameObject, height: 34f);

            Text press = NewText("Press", prompt, font, BodySize, TextColor, TextAnchor.MiddleCenter);
            press.text = "Press";
            AddOutline(press);

            Image keyBack = NewImage("Key", prompt, KeyColor);
            SetSize(keyBack.gameObject, 30f, 30f);
            AddBorder(keyBack.gameObject);
            Text txtKey = NewText("Text", keyBack.transform, font, BodySize - 2, TextColor, TextAnchor.MiddleCenter, FontStyle.Bold);
            Stretch(txtKey.rectTransform);
            txtKey.text = "E";

            Text action = NewText("Action", prompt, font, BodySize, TextColor, TextAnchor.MiddleCenter);
            action.text = "to select";
            AddOutline(action);

            Text locked = NewText("Locked", content, font, SmallSize + 2, new Color(0.90f, 0.35f, 0.30f),
                TextAnchor.MiddleCenter, FontStyle.Bold, wrap: true);

            SerializedObject so = new SerializedObject(popup);
            Set(so, "root", rootRect);
            Set(so, "txtName", txtName);
            Set(so, "txtHighscore", txtHighscore);
            Set(so, "questContainer", quests);
            Set(so, "questRowPrefab", questRowPrefab);
            Set(so, "promptRoot", prompt.gameObject);
            Set(so, "txtKey", txtKey);
            Set(so, "lockedRoot", locked.gameObject);
            Set(so, "txtLocked", locked);
            so.ApplyModifiedPropertiesWithoutUndo();

            outer.SetActive(false);
        }

        private static GameObject CreateNodeQuestRowPrefab(Font font)
        {
            EnsureFolder(PrefabFolder);

            GameObject go = new GameObject("NodeQuestRow", typeof(RectTransform));
            Text text = go.AddComponent<Text>();
            text.font = font;
            text.fontSize = SmallSize + 2;
            text.color = TextColor;
            text.alignment = TextAnchor.UpperLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.supportRichText = true;
            text.raycastTarget = false;

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(go, $"{PrefabFolder}/NodeQuestRow.prefab");
            Object.DestroyImmediate(go);
            return saved;
        }

        // ------------------------------------------------------------------
        // Settings + Dialog (gleiche Bausteine wie im Pausemenü, ohne PauseMenuUI)
        // ------------------------------------------------------------------

        private static SettingsPanel CreateSettingsPanel(Transform parent, Font font, GameObject bindingRowPrefab)
        {
            RectTransform settingsRoot = NewRect("SettingsPanel", parent);
            Stretch(settingsRoot);

            // Eigene Abdunklung (zählt nicht im Layout)
            Image dim = NewImage("Dim", settingsRoot, DimBackColor, true);
            dim.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            Stretch(dim.rectTransform);

            VerticalLayoutGroup layout = AddVertical(settingsRoot.gameObject, ScreenPadding, 14f);
            layout.childAlignment = TextAnchor.UpperCenter;

            Text settingsTitle = NewText("Title", settingsRoot, font, TitleSize, TextColor, TextAnchor.MiddleLeft, FontStyle.Bold);
            settingsTitle.text = "Settings";
            SetSize(settingsTitle.gameObject, height: 72f);

            RectTransform card = CreateCard("Card", settingsRoot, out _, new RectOffset(24, 24, 16, 18));

            RectTransform tabRow = NewRect("Tabs", card);
            AddHorizontal(tabRow.gameObject, new RectOffset(0, 0, 0, 0), 6f, TextAnchor.MiddleCenter);
            SetSize(tabRow.gameObject, height: 42f);

            string[] tabNames = { "Video", "Audio", "Gameplay", "Controls" };
            Button[] tabButtons = new Button[tabNames.Length];
            for (int i = 0; i < tabNames.Length; i++)
            {
                tabButtons[i] = NewMenuButton(tabRow, font, tabNames[i], SmallSize + 2, 42f);
                SetSize(tabButtons[i].gameObject, flexibleWidth: 1f);
            }

            Image tabDivider = NewImage("Divider", card, DividerColor);
            SetSize(tabDivider.gameObject, height: 1f);

            RectTransform pages = NewRect("Pages", card);
            SetSize(pages.gameObject, height: SettingsPagesHeight);

            RectTransform pageVideo = NewPage("Video", pages);
            NewArrowRow(pageVideo, font, "Resolution", out Button resPrev, out Text txtResolution, out Button resNext);
            Toggle toggleFullscreen = NewToggleRow(pageVideo, font, "Fullscreen");
            Toggle toggleVSync = NewToggleRow(pageVideo, font, "V-Sync");
            Toggle togglePost = NewToggleRow(pageVideo, font, "Post Processing");
            NewArrowRow(pageVideo, font, "Anti Aliasing", out Button aaPrev, out Text txtAa, out Button aaNext);
            Slider sliderRenderScale = NewSliderRow(pageVideo, font, "Render Scale", out Text txtRenderScale);
            Button resetVideo = NewResetRow(pageVideo, font, "Reset Video Settings");

            RectTransform pageAudio = NewPage("Audio", pages);
            Slider sliderMaster = NewSliderRow(pageAudio, font, "Master", out Text txtMaster);
            Slider sliderGameplay = NewSliderRow(pageAudio, font, "Gameplay", out Text txtGameplay);
            Slider sliderMusic = NewSliderRow(pageAudio, font, "Music", out Text txtMusic);
            Slider sliderEnvironment = NewSliderRow(pageAudio, font, "Environment", out Text txtEnvironment);
            Button resetAudio = NewResetRow(pageAudio, font, "Reset Audio Settings");

            RectTransform pageGameplay = NewPage("Gameplay", pages);
            Toggle toggleShake = NewToggleRow(pageGameplay, font, "Screen Shake");
            Toggle toggleDamage = NewToggleRow(pageGameplay, font, "Damage Numbers");
            Toggle toggleHealthBars = NewToggleRow(pageGameplay, font, "Health Bars");
            Toggle toggleAutoPause = NewToggleRow(pageGameplay, font, "Pause on Focus Loss");
            Toggle toggleFps = NewToggleRow(pageGameplay, font, "Show FPS");
            Button resetGameplay = NewResetRow(pageGameplay, font, "Reset Gameplay Settings");

            RectTransform pageControls = NewPage("Controls", pages);
            RectTransform bindings = NewRect("Bindings", pageControls);
            AddVertical(bindings.gameObject, new RectOffset(0, 0, 0, 0), 6f);
            Button resetControls = NewResetRow(pageControls, font, "Reset Controls");

            Image backDivider = NewImage("Divider", card, DividerColor);
            SetSize(backDivider.gameObject, height: 1f);

            Button btnBack = NewMenuButton(card, font, "Back", ButtonSize, 44f);

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

            Set(sp, "btnBack", btnBack);   // Zurück läuft über SettingsPanel.BackRequested (MainMenuUI)
            sp.ApplyModifiedPropertiesWithoutUndo();

            return settings;
        }

        private static ConfirmDialog CreateConfirmDialog(Transform parent, Font font)
        {
            Image dialogRoot = NewImage("ConfirmDialog", parent, new Color(0f, 0f, 0f, 0.6f), true);
            Stretch(dialogRoot.rectTransform);

            VerticalLayoutGroup layout = AddVertical(dialogRoot.gameObject, new RectOffset(0, 0, 0, 0), 0f);
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childForceExpandWidth = false;

            RectTransform card = CreateCard("Card", dialogRoot.transform, out GameObject outer, new RectOffset(24, 24, 20, 22));
            SetSize(outer, width: 560f, flexibleWidth: 0f);

            Text message = NewText("Message", card, font, BodySize + 2, TextColor, TextAnchor.MiddleCenter, wrap: true);
            MinHeight(message.gameObject, 80f);

            RectTransform buttons = NewRect("Buttons", card);
            AddHorizontal(buttons.gameObject, new RectOffset(0, 0, 0, 0), 16f, TextAnchor.MiddleCenter);
            SetSize(buttons.gameObject, height: 46f);

            Button yes = NewMenuButton(buttons, font, "Yes", ButtonSize, 46f);
            SetSize(yes.gameObject, flexibleWidth: 1f);
            Button no = NewMenuButton(buttons, font, "No", ButtonSize, 46f);
            SetSize(no.gameObject, flexibleWidth: 1f);

            ConfirmDialog dialog = dialogRoot.gameObject.AddComponent<ConfirmDialog>();
            SerializedObject so = new SerializedObject(dialog);
            Set(so, "txtMessage", message);
            Set(so, "btnYes", yes);
            Set(so, "btnNo", no);
            so.ApplyModifiedPropertiesWithoutUndo();

            return dialog;
        }

        // ------------------------------------------------------------------
        // Bestehende Szene
        // ------------------------------------------------------------------

        private static bool IsNewUi(GameObject go) => go != null && go.name.EndsWith("(neu)");

        private static GameObject FindSceneRoot(string rootName)
        {
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
                if (root.name == rootName)
                    return root;

            return null;
        }

        /// <summary>Alte Hauptmenü-UI: das ausgewählte Canvas, sonst das Szenen-Root-Objekt "Canvas".</summary>
        private static GameObject FindOldMenuCanvas()
        {
            GameObject selected = Selection.activeGameObject;

            if (selected != null)
            {
                Canvas selectedCanvas = selected.GetComponentInParent<Canvas>();
                GameObject selectedRoot = selectedCanvas != null ? selectedCanvas.rootCanvas.gameObject : null;

                if (selectedRoot != null && !IsNewUi(selectedRoot) && selectedRoot.name != "ResearchCanvas")
                    return selectedRoot;
            }

            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (root.name == "Canvas" && root.GetComponent<Canvas>() != null)
                    return root;
            }

            return null;
        }

        /// <summary>
        /// Buttons außerhalb des alten Menüs (z. B. "Zurück" im ResearchCanvas), die das alte Menü per SetActive(true)
        /// wieder einschalten wollten, zeigen danach auf das neue Menü. SetActive(false)-Aufrufe bleiben unverändert.
        /// </summary>
        private static int RetargetOldMenuCalls(GameObject oldMenu, GameObject newRoot)
        {
            int count = 0;

            foreach (Button button in Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (button.transform.IsChildOf(oldMenu.transform) || button.transform.IsChildOf(newRoot.transform))
                    continue;

                SerializedObject so = new SerializedObject(button);
                SerializedProperty calls = so.FindProperty("m_OnClick.m_PersistentCalls.m_Calls");
                if (calls == null) continue;

                bool changed = false;

                for (int i = 0; i < calls.arraySize; i++)
                {
                    SerializedProperty call = calls.GetArrayElementAtIndex(i);
                    SerializedProperty target = call.FindPropertyRelative("m_Target");

                    if (target.objectReferenceValue != oldMenu) continue;
                    if (call.FindPropertyRelative("m_MethodName").stringValue != "SetActive") continue;
                    if (!call.FindPropertyRelative("m_Arguments.m_BoolArgument").boolValue) continue;

                    target.objectReferenceValue = newRoot;
                    changed = true;
                    count++;
                }

                if (changed)
                    so.ApplyModifiedProperties();
            }

            return count;
        }
    }
}
#endif



#if UNITY_EDITOR


namespace TowerDefense.EditorTools
{
    /// <summary>
    /// Baut die Forschungs-Ansicht (Skilltree) im Stil des Pausemenüs.
    ///
    ///   Links    Titel, Forschungspunkte, Liste aller Türme
    ///   Mitte    Skilltree des gewählten Turms (scrollbar, Zoom nicht nötig)
    ///   Rechts   Details, Unlock/Research-Button, Zurück, Reset
    ///
    /// Das Canvas heißt "ResearchCanvas" und startet deaktiviert: MainMenuUI findet es über diesen Namen,
    /// blendet es bei "Research" ein und erkennt das Zurück (Canvas wird deaktiviert) automatisch.
    /// Ein vorhandenes altes ResearchCanvas wird in "ResearchCanvas (alt)" umbenannt und ausgeschaltet.
    /// Liegt der ResearchManager darunter, bleibt das Objekt aktiv (sonst würde der Manager verschwinden).
    ///
    /// Aufruf: Tools > UI > Research-Skilltree neu aufbauen   (danach ggf. "Hauptmenü + Übersichtskarte" erneut ausführen,
    /// damit MainMenuUI das neue Canvas referenziert)
    /// </summary>
    public static partial class PauseMenuBuilder
    {
        private const string ResearchRootName = "ResearchCanvas";
        private const int ResearchSortingOrder = 200;

        private const float ResearchNodeSize = 76f;

        [MenuItem("Tools/UI/Research-Skilltree neu aufbauen")]
        private static void BuildResearch()
        {
            Font font = FindFont();

            // Altes Canvas wegräumen (außer es enthält den Manager)
            GameObject old = FindSceneRoot(ResearchRootName);
            if (old != null)
            {
                bool hasManager = old.GetComponentInChildren<ResearchManager>(true) != null;

                Undo.RecordObject(old, "Altes ResearchCanvas umbenennen");
                old.name = ResearchRootName + " (alt)";

                if (!hasManager)
                    old.SetActive(false);
                else
                    Debug.LogWarning("Research-Skilltree: Das alte ResearchCanvas enthält den ResearchManager und bleibt aktiv. " +
                                     "Verschiebe den Manager auf ein eigenes Objekt und lösche das alte Canvas.");
            }

            EnsureEventSystem();

            ResearchNodeView nodePrefab = CreateResearchNodePrefab(font);
            ResearchTowerButton towerButtonPrefab = CreateResearchTowerButtonPrefab(font);

            // ---------- Canvas ----------
            GameObject root = new GameObject(ResearchRootName, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(root, "Research-Skilltree neu aufbauen");

            Canvas canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = ResearchSortingOrder;

            CanvasScaler scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            root.AddComponent<GraphicRaycaster>();
            ResearchTreeUI tree = root.AddComponent<ResearchTreeUI>();

            Image dim = NewImage("Dim", root.transform, DimBackColor, true);
            Stretch(dim.rectTransform);

            RectTransform main = NewRect("MainPanel", root.transform);
            Stretch(main);
            AddHorizontal(main.gameObject, ScreenPadding, 24f, TextAnchor.UpperCenter);

            RectTransform left = NewColumn("Left", main, 360f);
            RectTransform middle = NewColumn("Middle", main, 760f);
            RectTransform right = NewColumn("Right", main, 440f);

            // ---------- Links: Titel, Punkte, Turm-Liste ----------
            Text title = NewText("Title", left, font, TitleSize, TextColor, TextAnchor.MiddleLeft, FontStyle.Bold);
            title.text = "Research";
            SetSize(title.gameObject, height: 72f);

            RectTransform pointsCard = CreateCard("PointsCard", left, out _);
            Text txtPoints = NewText("Points", pointsCard, font, BodySize, GoldColor, TextAnchor.MiddleLeft, FontStyle.Bold);
            SetSize(txtPoints.gameObject, height: 30f);

            RectTransform listCard = CreateCard("TowerCard", left, out _);
            SectionHeader(listCard, font, "Towers");
            RectTransform towerList = NewScroll("TowerScroll", listCard, 560f, 4f);

            // ---------- Mitte: Baum ----------
            RectTransform treeCard = CreateCard("TreeCard", middle, out _);
            SectionHeader(treeCard, font, "Upgrades");
            RectTransform treeContent = NewTreeScroll("TreeScroll", treeCard, 700f);

            Text keyHints = NewText("KeyHints", treeCard, font, SmallSize, DimColor, TextAnchor.MiddleLeft, wrap: true);
            MinHeight(keyHints.gameObject, 22f);

            // ---------- Rechts: Details ----------
            RectTransform detailCard = CreateCard("DetailCard", right, out _);

            Text txtTitle = NewText("Title", detailCard, font, BodySize + 6, GoldColor, TextAnchor.UpperLeft, FontStyle.Bold, wrap: true);
            Text txtSubtitle = NewText("Subtitle", detailCard, font, SmallSize, DimColor, TextAnchor.UpperLeft, wrap: true);

            Image accent = NewImage("Accent", detailCard, BorderColor);
            SetSize(accent.gameObject, height: 2f);

            Text txtDescription = NewText("Description", detailCard, font, SmallSize + 2, TextColor, TextAnchor.UpperLeft, wrap: true);
            txtDescription.lineSpacing = 1.3f;
            MinHeight(txtDescription.gameObject, 90f);

            Image statDivider = NewImage("Divider", detailCard, DividerColor);
            SetSize(statDivider.gameObject, height: 1f);

            Text txtStats = NewText("Stats", detailCard, font, SmallSize + 2, DimColor, TextAnchor.UpperLeft, wrap: true);
            txtStats.lineSpacing = 1.25f;
            MinHeight(txtStats.gameObject, 150f);

            Text txtHint = NewText("Hint", detailCard, font, SmallSize, new Color(0.90f, 0.35f, 0.30f), TextAnchor.MiddleLeft, wrap: true);
            MinHeight(txtHint.gameObject, 24f);

            Button btnAction = NewMenuButton(detailCard, font, "Research", ButtonSize, 50f);
            Text txtAction = btnAction.GetComponentInChildren<Text>();

            Button btnBack = NewMenuButton(right, font, "Back");
            Button btnReset = NewMenuButton(right, font, "Reset research", SmallSize + 2, 40f);

            ConfirmDialog confirm = CreateConfirmDialog(root.transform, font);

            // ---------- Verdrahten ----------
            SerializedObject so = new SerializedObject(tree);

            Set(so, "towerListContainer", towerList);
            Set(so, "towerButtonPrefab", towerButtonPrefab);

            Set(so, "treeContent", treeContent);
            Set(so, "nodePrefab", nodePrefab);
            Set(so, "pathLabelPrefab", CreatePathLabelPrefab(font));

            Set(so, "txtTitle", txtTitle);
            Set(so, "txtSubtitle", txtSubtitle);
            Set(so, "txtDescription", txtDescription);
            Set(so, "txtStats", txtStats);
            Set(so, "txtHint", txtHint);
            Set(so, "btnAction", btnAction);
            Set(so, "txtAction", txtAction);

            Set(so, "txtScreenTitle", title);
            Set(so, "txtTowersHeader", listCard.Find("Header").GetComponent<Text>());
            Set(so, "txtUpgradesHeader", treeCard.Find("Header").GetComponent<Text>());
            Set(so, "txtKeyHints", keyHints);
            Set(so, "txtPoints", txtPoints);
            Set(so, "btnBack", btnBack);
            Set(so, "btnReset", btnReset);
            Set(so, "confirmDialog", confirm);
            Set(so, "closeTarget", root);
            so.ApplyModifiedProperties();

            root.SetActive(false);

            EditorSceneManager.MarkSceneDirty(root.scene);
            Selection.activeGameObject = root;

            Debug.Log("Research-Skilltree aufgebaut (ResearchCanvas, startet deaktiviert).\n" +
                      "1) Im ResearchManager prüfen: Tower Prefabs, 'Starting Unlocked Towers', Kosten.\n" +
                      "2) 'Hauptmenü + Übersichtskarte' erneut ausführen, damit MainMenuUI das neue Canvas findet " +
                      "(oder 'Research Canvas' im MainMenuUI von Hand zuweisen).\n" +
                      "3) Beschreibung/Basiswerte der Türme stehen in TowerResearchInfo (ResearchManager.LoadTowersFromPrefabs).");
        }

        // ------------------------------------------------------------------
        // Prefabs
        // ------------------------------------------------------------------

        private static ResearchNodeView CreateResearchNodePrefab(Font font)
        {
            EnsureFolder(PrefabFolder);

            GameObject root = new GameObject("ResearchNode", typeof(RectTransform));
            ((RectTransform)root.transform).sizeDelta = new Vector2(ResearchNodeSize, ResearchNodeSize);

            // Auswahlrahmen (größer als der Knoten, liegt dahinter)
            Image selected = NewImage("Selected", root.transform, Color.white);
            selected.rectTransform.anchorMin = Vector2.zero;
            selected.rectTransform.anchorMax = Vector2.one;
            selected.rectTransform.offsetMin = new Vector2(-6f, -6f);
            selected.rectTransform.offsetMax = new Vector2(6f, 6f);
            selected.color = SelectedColor;

            Image border = NewImage("Border", root.transform, BorderColor);
            Stretch(border.rectTransform);

            Image background = NewImage("Background", border.transform, ButtonColor, true);
            Stretch(background.rectTransform);
            background.rectTransform.offsetMin = new Vector2(3f, 3f);
            background.rectTransform.offsetMax = new Vector2(-3f, -3f);

            Image icon = NewImage("Icon", background.transform, Color.white);
            icon.preserveAspect = true;
            Stretch(icon.rectTransform);
            icon.rectTransform.offsetMin = new Vector2(8f, 8f);
            icon.rectTransform.offsetMax = new Vector2(-8f, -8f);

            // Sperr-Overlay
            Image lockOverlay = NewImage("Lock", background.transform, new Color(0f, 0f, 0f, 0.55f));
            Stretch(lockOverlay.rectTransform);
            Text lockText = NewText("Label", lockOverlay.transform, font, SmallSize, DimColor, TextAnchor.MiddleCenter, FontStyle.Bold);
            lockText.text = "LOCKED";
            Stretch(lockText.rectTransform);

            // Marker "erforscht" (Haken unten rechts)
            Image marker = NewImage("Researched", root.transform, GoldColor);
            marker.rectTransform.anchorMin = marker.rectTransform.anchorMax = new Vector2(1f, 0f);
            marker.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            marker.rectTransform.sizeDelta = new Vector2(24f, 24f);
            marker.rectTransform.anchoredPosition = new Vector2(-4f, 4f);
            AddBorder(marker.gameObject);
            Text check = NewText("Check", marker.transform, font, SmallSize, KeyColor, TextAnchor.MiddleCenter, FontStyle.Bold);
            check.text = "✓";
            Stretch(check.rectTransform);

            // Stufen-Nummer oben links
            Text badge = NewText("Badge", root.transform, font, SmallSize, TextColor, TextAnchor.UpperLeft, FontStyle.Bold);
            badge.rectTransform.anchorMin = badge.rectTransform.anchorMax = new Vector2(0f, 1f);
            badge.rectTransform.pivot = new Vector2(0f, 1f);
            badge.rectTransform.anchoredPosition = new Vector2(6f, -4f);
            badge.rectTransform.sizeDelta = new Vector2(30f, 20f);
            AddOutline(badge);

            // Kosten unter dem Knoten
            Text cost = NewText("Cost", root.transform, font, SmallSize, GoldColor, TextAnchor.UpperCenter, FontStyle.Bold);
            cost.rectTransform.anchorMin = new Vector2(0f, 0f);
            cost.rectTransform.anchorMax = new Vector2(1f, 0f);
            cost.rectTransform.pivot = new Vector2(0.5f, 1f);
            cost.rectTransform.anchoredPosition = new Vector2(0f, -8f);
            cost.rectTransform.sizeDelta = new Vector2(40f, 22f);
            AddOutline(cost);

            Button button = root.AddComponent<Button>();
            button.targetGraphic = background;
            button.colors = ButtonColors();
            button.navigation = new Navigation { mode = Navigation.Mode.None };

            ResearchNodeView view = root.AddComponent<ResearchNodeView>();
            SerializedObject so = new SerializedObject(view);
            Set(so, "border", border);
            Set(so, "background", background);
            Set(so, "icon", icon);
            Set(so, "lockOverlay", lockOverlay.gameObject);
            Set(so, "lockLabel", lockText);
            Set(so, "researchedMarker", marker.gameObject);
            Set(so, "selectedFrame", selected.gameObject);
            Set(so, "badge", badge);
            Set(so, "costText", cost);
            Set(so, "button", button);
            so.ApplyModifiedPropertiesWithoutUndo();

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabFolder}/ResearchNode.prefab");
            Object.DestroyImmediate(root);
            return saved.GetComponent<ResearchNodeView>();
        }

        private static ResearchTowerButton CreateResearchTowerButtonPrefab(Font font)
        {
            EnsureFolder(PrefabFolder);

            GameObject root = new GameObject("ResearchTowerButton", typeof(RectTransform));
            Image background = root.AddComponent<Image>();
            background.color = ButtonColor;
            AddBorder(root);
            AddHorizontal(root, new RectOffset(8, 8, 6, 6), 10f, TextAnchor.MiddleLeft);
            MinHeight(root, 56f);

            Button button = root.AddComponent<Button>();
            button.targetGraphic = background;
            button.colors = ButtonColors();
            button.navigation = new Navigation { mode = Navigation.Mode.None };

            Image icon = NewImage("Icon", root.transform, Color.white);
            icon.preserveAspect = true;
            SetSize(icon.gameObject, 40f, 40f);

            RectTransform texts = NewRect("Texts", root.transform);
            AddVertical(texts.gameObject, new RectOffset(0, 0, 0, 0), 0f);
            SetSize(texts.gameObject, flexibleWidth: 1f);

            Text name = NewText("Name", texts, font, BodySize - 2, TextColor, TextAnchor.MiddleLeft, FontStyle.Bold);
            Text status = NewText("Status", texts, font, SmallSize, DimColor, TextAnchor.MiddleLeft);

            // Auswahl-Balken links, ignoriert das Layout
            Image marker = NewImage("Selected", root.transform, SelectedColor);
            marker.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            marker.rectTransform.anchorMin = new Vector2(0f, 0f);
            marker.rectTransform.anchorMax = new Vector2(0f, 1f);
            marker.rectTransform.pivot = new Vector2(0f, 0.5f);
            marker.rectTransform.sizeDelta = new Vector2(4f, 0f);
            marker.rectTransform.anchoredPosition = Vector2.zero;

            ResearchTowerButton view = root.AddComponent<ResearchTowerButton>();
            SerializedObject so = new SerializedObject(view);
            Set(so, "icon", icon);
            Set(so, "txtName", name);
            Set(so, "txtStatus", status);
            Set(so, "selectedMarker", marker.gameObject);
            Set(so, "button", button);
            so.ApplyModifiedPropertiesWithoutUndo();

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabFolder}/ResearchTowerButton.prefab");
            Object.DestroyImmediate(root);
            return saved.GetComponent<ResearchTowerButton>();
        }

        private static Text CreatePathLabelPrefab(Font font)
        {
            EnsureFolder(PrefabFolder);

            GameObject root = new GameObject("ResearchPathLabel", typeof(RectTransform));
            Text label = root.AddComponent<Text>();
            label.font = font;
            label.fontSize = SmallSize;
            label.fontStyle = FontStyle.Bold;
            label.color = DimColor;
            label.alignment = TextAnchor.LowerLeft;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.raycastTarget = false;
            ((RectTransform)root.transform).sizeDelta = new Vector2(240f, 24f);

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabFolder}/ResearchPathLabel.prefab");
            Object.DestroyImmediate(root);
            return saved.GetComponent<Text>();
        }

        // ------------------------------------------------------------------
        // Bausteine
        // ------------------------------------------------------------------

        /// <summary>
        /// Scrollbereich in beide Richtungen für den Baum. Der Content hat feste Größe (setzt ResearchTreeUI)
        /// und ist oben links verankert, die Knoten werden per anchoredPosition platziert.
        /// </summary>
        private static RectTransform NewTreeScroll(string name, Transform parent, float height)
        {
            Image root = NewImage(name, parent, new Color(0f, 0f, 0f, 0.18f), true);
            SetSize(root.gameObject, height: height);
            AddBorder(root.gameObject);

            RectTransform viewport = NewRect("Viewport", root.transform);
            Stretch(viewport);
            viewport.gameObject.AddComponent<RectMask2D>();

            RectTransform content = NewRect("Content", viewport);
            content.anchorMin = content.anchorMax = new Vector2(0f, 1f);
            content.pivot = new Vector2(0f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = new Vector2(1200f, 600f);

            ScrollRect scroll = root.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = true;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;

            return content;
        }
    }
}
#endif