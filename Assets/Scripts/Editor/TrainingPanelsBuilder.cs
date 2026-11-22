using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Приводит модальные панели PreparingDemo к стилю Tactical Metal
/// (как MainMenu / Bunker(main)): затемнение 0.62, металлическая плита HUF Pack,
/// тонкая рамка с красными уголками #DB143D, кнопки 460x60 с единым ColorBlock,
/// шрифт LiberationSans SDF и иерархия размеров 30 / 24 / 17 / 15.
/// Работает in-place в открытой сцене PreparingDemo, корни панелей сохраняются.
/// </summary>
public static class TrainingPanelsBuilder
{
    private const float PauseWidth = 560f;
    private const float PauseHeight = 620f;
    private const float DebriefWidth = 720f;
    private const float DebriefHeight = 640f;

    [MenuItem("Tools/DroneSimulator/Mission UI/Rebuild Training Panels")]
    public static void Rebuild()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.name != "PreparingDemo")
        {
            Debug.LogWarning("[TrainingPanels] Откройте сцену PreparingDemo — билдер работает только с ней.");
            return;
        }

        GameObject canvas = FindInactive("Canvas");
        MissionUIManager ui = Object.FindAnyObjectByType<MissionUIManager>(FindObjectsInactive.Include);
        DebriefController debrief = Object.FindAnyObjectByType<DebriefController>(FindObjectsInactive.Include);
        TrainingManager training = Object.FindAnyObjectByType<TrainingManager>(FindObjectsInactive.Include);

        if (canvas == null || ui == null || debrief == null || training == null)
        {
            Debug.LogError("[TrainingPanels] Не найдены Canvas / MissionUIManager / DebriefController / TrainingManager.");
            return;
        }

        Sprite plateSprite = TacticalUIBuilder.LoadSprite("UI Starter Pack_0");
        Sprite buttonSprite = TacticalUIBuilder.LoadSprite("UI Starter Pack_1");

        GameObject pause = FindInactive("PausePanel");
        BuildModal(pause, "ПАУЗА", "ОПЕРАЦИЯ ПРИОСТАНОВЛЕНА", "Esc — продолжить · Настройки — параметры",
            new[] { "continue", "settings", "exit" },
            new[] { "Продолжить", "Настройки", "Выйти в бункер" },
            new[] { false, false, true },
            PauseWidth, PauseHeight, plateSprite, buttonSprite, 0f);

        GameObject crash = FindInactive("CrashPanel");
        BuildModal(crash, "БОРТ ПОТЕРЯН", "СВЯЗЬ С БОРТОМ ПРЕРВАНА", "Перезапуск вернёт миссию к началу",
            new[] { "return", "exit" },
            new[] { "Перезапустить", "Выйти в бункер" },
            new[] { false, true },
            PauseWidth, PauseHeight, plateSprite, buttonSprite, 0f);

        GameObject debriefPanel = FindInactive("DebriefPanel");
        BuildDebrief(debriefPanel, plateSprite, buttonSprite);

        StyleInstruction(plateSprite, training, ui);

        GameObject fadeText = FindInactive("FadeText");
        if (fadeText != null)
        {
            TextMeshProUGUI label = fadeText.GetComponent<TextMeshProUGUI>();
            if (label != null)
            {
                label.alignment = TextAlignmentOptions.Center;
                label.color = TacticalUITheme.TextPrimary;
                label.fontSize = 64f;
            }
        }

        GameObject subtitle = FindInactive("SubtitleText");
        if (subtitle != null)
        {
            TextMeshProUGUI label = subtitle.GetComponent<TextMeshProUGUI>();
            if (label != null)
            {
                label.fontSize = 24f;
                label.alignment = TextAlignmentOptions.Center;
                label.color = TacticalUITheme.TextPrimary;
            }
        }

        SerializedObject uiSo = new SerializedObject(ui);
        uiSo.FindProperty("pausePanel").objectReferenceValue = pause;
        uiSo.FindProperty("crashPanel").objectReferenceValue = crash;
        uiSo.FindProperty("instructionText").objectReferenceValue = FindInactive("InstructionText");
        if (pause != null) uiSo.FindProperty("settingsButton").objectReferenceValue =
            FindDeep(pause, "settings")?.GetComponent<Button>();
        uiSo.ApplyModifiedPropertiesWithoutUndo();

        SerializedObject debriefSo = new SerializedObject(debrief);
        debriefSo.FindProperty("panel").objectReferenceValue = debriefPanel;
        debriefSo.FindProperty("titleText").objectReferenceValue = FindDeep(debriefPanel, "DebriefTitle")?.GetComponent<TMP_Text>();
        debriefSo.FindProperty("statsText").objectReferenceValue = FindDeep(debriefPanel, "DebriefStats")?.GetComponent<TMP_Text>();
        debriefSo.FindProperty("returnToMenuButton").objectReferenceValue = FindDeep(debriefPanel, "ReturnButton")?.GetComponent<Button>();
        debriefSo.FindProperty("retryButton").objectReferenceValue = FindDeep(debriefPanel, "RetryButton")?.GetComponent<Button>();
        debriefSo.FindProperty("mainMenuButton").objectReferenceValue = FindDeep(debriefPanel, "MainMenuButton")?.GetComponent<Button>();
        debriefSo.ApplyModifiedPropertiesWithoutUndo();

        SerializedObject trainingSo = new SerializedObject(training);
        trainingSo.FindProperty("instructionText").objectReferenceValue =
            FindDeep(FindInactive("InstructionText"), "Label")?.GetComponent<TMP_Text>();
        trainingSo.FindProperty("subtitleText").objectReferenceValue = FindInactive("SubtitleText")?.GetComponent<TMP_Text>();
        trainingSo.ApplyModifiedPropertiesWithoutUndo();

        // Гигиена кликов: убираем невидимые полноэкранные перехватчики лучей
        // и проверяем, что в сцене есть EventSystem + GraphicRaycaster.
        HardenClickThrough(canvas);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[TrainingPanels] Панели PreparingDemo пересобраны в стиле Tactical Metal.");
    }

    /// <summary>
    /// Гигиена кликов мышью. Билдер не должен оставлять декоративные полноэкранные
    /// слои, которые перехватывают лучи: ScreenFader лежит выше панелей, а его
    /// FadeOut() включает blocksRaycasts — панель под ним становится некликабельной.
    /// Здесь же проверяется наличие EventSystem/GraphicRaycaster, без которых
    /// мышь не работает в принципе.
    /// </summary>
    private static void HardenClickThrough(GameObject canvas)
    {
        if (canvas == null) return;

        Transform canvasT = canvas.transform;

        // 1) ScreenFader — под модальными панелями и полностью «прозрачный» для лучей.
        //    raycastTarget=false гасит слой даже когда ScreenFader.FadeOut() включил
        //    blocksRaycasts у CanvasGroup.
        Transform fader = canvasT.Find("ScreenFader");
        if (fader != null)
        {
            foreach (Graphic g in fader.GetComponentsInChildren<Graphic>(true))
                g.raycastTarget = false;

            CanvasGroup group = fader.GetComponent<CanvasGroup>();
            if (group != null)
            {
                group.blocksRaycasts = false;
                group.interactable = false;
            }

            // sibling 0 — InstructionText; фейдер идёт сразу за ним и ниже панелей.
            fader.SetSiblingIndex(1);
        }

        // 2) Настройки открываются поверх паузы — всегда последний сиблинг.
        Transform settings = canvasT.Find("SettingsPanel");
        if (settings != null)
            settings.SetSiblingIndex(canvasT.childCount - 1);

        // 3) Внутри панелей кликабельны только Image самих кнопок и скрим корня.
        string[] panelNames = { "PausePanel", "CrashPanel", "DebriefPanel" };
        foreach (string panelName in panelNames)
        {
            Transform panel = canvasT.Find(panelName);
            if (panel == null) continue;

            foreach (Graphic g in panel.GetComponentsInChildren<Graphic>(true))
            {
                bool isButtonArt = g.GetComponent<Button>() != null;
                g.raycastTarget = isButtonArt || g.transform == panel;
            }

            foreach (Button b in panel.GetComponentsInChildren<Button>(true))
            {
                b.interactable = true;
                if (b.targetGraphic == null)
                    b.targetGraphic = b.GetComponent<Image>();
            }
        }

        // 4) Диагностика окружения — иначе баг вернётся молча.
        if (canvas.GetComponent<GraphicRaycaster>() == null)
            Debug.LogError("[TrainingPanels] На Canvas нет GraphicRaycaster — клики мышью работать не будут.");

        if (Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            Debug.LogError("[TrainingPanels] В сцене нет EventSystem — клики мышью работать не будут.");
        else if (Object.FindAnyObjectByType<UnityEngine.InputSystem.UI.InputSystemUIInputModule>() == null)
            Debug.LogError("[TrainingPanels] На EventSystem нет InputSystemUIInputModule — клики мышью работать не будут.");
    }

    private static void BuildModal(GameObject root, string title, string subtitle, string footer,
        string[] names, string[] labels, bool[] dangerous, float width, float height,
        Sprite plateSprite, Sprite buttonSprite, float buttonsTop)
    {
        if (root == null) { Debug.LogError("[TrainingPanels] Панель не найдена."); return; }

        ClearChildren(root.transform);
        PrepareRoot(root);

        GameObject plate = TacticalUIBuilder.CreateRect("Plate", root.transform);
        TacticalUIBuilder.SetCenter((RectTransform)plate.transform, width, height, Vector2.zero);
        TacticalUIBuilder.EnsureImage(plate, plateSprite, TacticalUITheme.PanelFill, Image.Type.Sliced);
        TacticalUIBuilder.BuildTacticalFrame(plate.transform, TacticalUITheme.Divider, TacticalUITheme.AccentRed);

        AddTitle(plate.transform, "Title", title, 36f, width - 80f);
        AddText(plate.transform, "Subtitle", subtitle, 88f, width - 80f, TacticalUITheme.FontBody, TacticalUITheme.TextSecondary);

        Image rule = TacticalUIBuilder.EnsureBar(plate.transform, "AccentRule", TacticalUITheme.AccentRed);
        TacticalUIBuilder.SetTopCenter((RectTransform)rule.transform, 124f, width - 120f, 3f);

        float top = buttonsTop <= 0f ? 170f : buttonsTop;
        for (int i = 0; i < names.Length; i++)
        {
            AddButton(plate.transform, names[i], labels[i], top, dangerous[i], buttonSprite);
            top += 72f;
        }

        if (!string.IsNullOrEmpty(footer))
            AddText(plate.transform, "Footer", footer, 0f, width - 80f,
                TacticalUITheme.FontCaption, TacticalUITheme.TextCaption, true);
    }

    private static void BuildDebrief(GameObject root, Sprite plateSprite, Sprite buttonSprite)
    {
        if (root == null) { Debug.LogError("[TrainingPanels] DebriefPanel не найден."); return; }

        ClearChildren(root.transform);
        PrepareRoot(root);

        GameObject plate = TacticalUIBuilder.CreateRect("Plate", root.transform);
        TacticalUIBuilder.SetCenter((RectTransform)plate.transform, DebriefWidth, DebriefHeight, Vector2.zero);
        TacticalUIBuilder.EnsureImage(plate, plateSprite, TacticalUITheme.PanelFill, Image.Type.Sliced);
        TacticalUIBuilder.BuildTacticalFrame(plate.transform, TacticalUITheme.Divider, TacticalUITheme.AccentRed);

        AddTitle(plate.transform, "DebriefTitle", "МИССИЯ ВЫПОЛНЕНА", 36f, DebriefWidth - 80f);

        Image rule = TacticalUIBuilder.EnsureBar(plate.transform, "AccentRule", TacticalUITheme.AccentRed);
        TacticalUIBuilder.SetTopCenter((RectTransform)rule.transform, 88f, DebriefWidth - 120f, 3f);

        GameObject statsGO = TacticalUIBuilder.EnsureChild(plate.transform, "DebriefStats");
        TacticalUIBuilder.SetTopCenter((RectTransform)statsGO.transform, 106f, DebriefWidth - 120f, 170f);
        TextMeshProUGUI stats = TacticalUIBuilder.EnsureText(statsGO,
            "Время: —\nЦелей уничтожено: —\nНаграда: —",
            TacticalUITheme.FontLabel + 1f, TacticalUITheme.TextSecondary, TextAlignmentOptions.Center);
        stats.textWrappingMode = TextWrappingModes.Normal;

        AddButton(plate.transform, "RetryButton", "ПОВТОРИТЬ", 300f, false, buttonSprite);
        AddButton(plate.transform, "ReturnButton", "ВЕРНУТЬСЯ В БУНКЕР", 372f, false, buttonSprite);
        AddButton(plate.transform, "MainMenuButton", "В ГЛАВНОЕ МЕНЮ", 444f, true, buttonSprite);

        AddText(plate.transform, "Footer", "Esc — вернуться в бункер", 0f, DebriefWidth - 80f,
            TacticalUITheme.FontCaption, TacticalUITheme.TextCaption, true);
    }

    private static void PrepareRoot(GameObject root)
    {
        RectTransform rt = (RectTransform)root.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.localPosition = Vector3.zero;
        rt.localScale = Vector3.one;

        Image scrim = TacticalUIBuilder.EnsureImage(root, null, TacticalUITheme.Scrim, Image.Type.Simple);
        scrim.raycastTarget = true;
    }

    private static void AddTitle(Transform plate, string name, string text, float top, float width)
    {
        GameObject go = TacticalUIBuilder.EnsureChild(plate, name);
        TacticalUIBuilder.SetTopCenter((RectTransform)go.transform, top, width, 44f);

        TextMeshProUGUI label = TacticalUIBuilder.EnsureText(go, text, TacticalUITheme.FontModalTitle,
            TacticalUITheme.TextPrimary, TextAlignmentOptions.Center);
        label.fontStyle = FontStyles.Bold;
        label.textWrappingMode = TextWrappingModes.NoWrap;
    }

    private static void AddText(Transform plate, string name, string text, float top, float width,
        float size, Color color, bool bottom = false)
    {
        GameObject go = TacticalUIBuilder.EnsureChild(plate, name);
        RectTransform rt = (RectTransform)go.transform;

        if (bottom) TacticalUIBuilder.SetBottomCenter(rt, 24f, width, 22f);
        else TacticalUIBuilder.SetTopCenter(rt, top, width, 24f);

        TextMeshProUGUI label = TacticalUIBuilder.EnsureText(go, text, size, color, TextAlignmentOptions.Center);
        label.textWrappingMode = TextWrappingModes.NoWrap;
    }

    private static void AddButton(Transform plate, string name, string label, float top, bool dangerous, Sprite buttonSprite)
    {
        GameObject go = TacticalUIBuilder.EnsureChild(plate, name);
        TacticalUIBuilder.SetTopCenter((RectTransform)go.transform, top, 460f, TacticalUITheme.PrimaryButtonHeight);

        Image background = TacticalUIBuilder.EnsureImage(go, buttonSprite,
            dangerous ? TacticalUITheme.AccentRed : Color.white, Image.Type.Sliced);
        background.raycastTarget = true;

        TacticalUIBuilder.EnsureButton(go, background);

        GameObject labelGO = TacticalUIBuilder.EnsureChild(go.transform, "Text (TMP)");
        TacticalUIBuilder.Stretch((RectTransform)labelGO.transform);

        TextMeshProUGUI text = TacticalUIBuilder.EnsureText(labelGO, label, TacticalUITheme.FontButton,
            TacticalUITheme.TextPrimary, TextAlignmentOptions.Center);
        text.textWrappingMode = TextWrappingModes.NoWrap;
    }

    private static void StyleInstruction(Sprite plateSprite, TrainingManager training, MissionUIManager ui)
    {
        GameObject root = FindInactive("InstructionText");
        if (root == null) return;

        // Сначала убираем с корня TMP и Outline: Image и TMP не могут жить
        // на одном GameObject (конфликт за CanvasRenderer).
        Outline rootOutline = root.GetComponent<Outline>();
        if (rootOutline != null) Object.DestroyImmediate(rootOutline, true);

        TextMeshProUGUI oldLabel = root.GetComponent<TextMeshProUGUI>();
        if (oldLabel != null) Object.DestroyImmediate(oldLabel, true);

        RectTransform rt = (RectTransform)root.transform;
        rt.anchorMin = new Vector2(1f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 1f);
        rt.anchoredPosition = new Vector2(-40f, -40f);
        rt.sizeDelta = new Vector2(460f, 176f);

        Image plate = TacticalUIBuilder.EnsureImage(root, plateSprite, new Color(0.03f, 0.035f, 0.04f, 0.92f), Image.Type.Sliced);
        if (plate != null) plate.raycastTarget = false;

        TacticalUIBuilder.BuildTacticalFrame(root.transform, TacticalUITheme.Divider, TacticalUITheme.AccentRed, 1.5f, 18f, 2f);

        Image accent = TacticalUIBuilder.EnsureBar(root.transform, "AccentBar", TacticalUITheme.AccentRed);
        RectTransform accentRT = (RectTransform)accent.transform;
        accentRT.anchorMin = new Vector2(0f, 0f);
        accentRT.anchorMax = new Vector2(0f, 1f);
        accentRT.pivot = new Vector2(0f, 0.5f);
        accentRT.offsetMin = new Vector2(0f, 0f);
        accentRT.offsetMax = new Vector2(4f, 0f);

        // Текст живёт в дочернем Label и рисуется поверх плашки.
        GameObject labelGO = TacticalUIBuilder.EnsureChild(root.transform, "Label");
        RectTransform labelRT = (RectTransform)labelGO.transform;
        TacticalUIBuilder.Stretch(labelRT);
        labelRT.offsetMin = new Vector2(24f, 14f);
        labelRT.offsetMax = new Vector2(-18f, -14f);

        TextMeshProUGUI label = TacticalUIBuilder.EnsureText(labelGO, "ИНСТРУКТОР:", 18f,
            TacticalUITheme.TextPrimary, TextAlignmentOptions.TopLeft);
        label.textWrappingMode = TextWrappingModes.Normal;

        Outline outline = labelGO.GetComponent<Outline>();
        if (outline == null) outline = labelGO.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.6f);
        outline.effectDistance = new Vector2(1f, -1f);

        if (training != null)
        {
            SerializedObject so = new SerializedObject(training);
            so.FindProperty("instructionText").objectReferenceValue = label;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        if (ui != null)
        {
            SerializedObject so = new SerializedObject(ui);
            so.FindProperty("instructionText").objectReferenceValue = root;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static void ClearChildren(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
            Object.DestroyImmediate(parent.GetChild(i).gameObject, true);
    }

    private static GameObject FindInactive(string name)
    {
        foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
            if (t.name == name) return t.gameObject;

        return null;
    }

    private static Transform FindDeep(GameObject root, string name)
    {
        if (root == null) return null;

        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == name) return t;

        return null;
    }
}
