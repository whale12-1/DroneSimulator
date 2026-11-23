using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Приводит SettingsPanel.prefab к единому тактическому стилю главного меню.
/// Правка выполняется in-place (LoadPrefabContents -> SaveAsPrefabAsset), чтобы
/// сохранить fileID компонентов и не порвать ссылки сцен MainMenu/Bunker(main).
/// Повторный запуск идемпотентен: объекты ищутся по имени, дубликаты не создаются.
/// </summary>
public static class SettingsPanelPrefabBuilder
{
    private const string PrefabFolder = "Assets/Prefabs/UI";
    private const string PrefabPath = PrefabFolder + "/SettingsPanel.prefab";
    private const int UiLayer = 5;

    private static Sprite buttonSprite;
    private static Sprite closeSprite;
    private static Sprite trackSprite;
    private static Sprite fillSprite;
    private static Sprite handleSprite;
    private static Sprite toggleOnSprite;
    private static Sprite toggleOffSprite;
    private static Sprite dropdownSprite;
    private static Sprite arrowSprite;

    [MenuItem("Tools/DroneSimulator/Create SettingsPanel Prefab")]
    public static void BuildPrefab()
    {
        CacheSprites();

        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
            CreateEmptyPrefab();

        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        if (root == null)
        {
            Debug.LogError($"[SettingsPanelPrefabBuilder] Не удалось открыть префаб: {PrefabPath}");
            return;
        }

        try
        {
            Apply(root);

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool success);
            if (success) Debug.Log($"[SettingsPanelPrefabBuilder] Префаб обновлён: {PrefabPath}");
            else Debug.LogError($"[SettingsPanelPrefabBuilder] Не удалось сохранить префаб: {PrefabPath}");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static void CacheSprites()
    {
        buttonSprite = TacticalUIBuilder.LoadSprite("UI Starter Pack_1");
        closeSprite = TacticalUIBuilder.LoadSprite("UI Starter Pack_38");
        trackSprite = TacticalUIBuilder.LoadSprite("UI Starter Pack_49");
        fillSprite = TacticalUIBuilder.LoadSprite("UI Starter Pack_49");
        handleSprite = TacticalUIBuilder.LoadSprite("UI Starter Pack_22");
        toggleOnSprite = TacticalUIBuilder.LoadSprite("UI Starter Pack_20");
        toggleOffSprite = TacticalUIBuilder.LoadSprite("UI Starter Pack_21");
        dropdownSprite = TacticalUIBuilder.LoadSprite("UI Starter Pack_2");
        arrowSprite = TacticalUIBuilder.LoadSprite("UI Starter Pack_39");
    }

    private static void CreateEmptyPrefab()
    {
        GameObject root = new GameObject("SettingsPanel", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        root.layer = UiLayer;
        EnsureFolder();
        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool success);
        Object.DestroyImmediate(root);

        if (!success) Debug.LogError($"[SettingsPanelPrefabBuilder] Не удалось создать префаб: {PrefabPath}");
    }

    private static void Apply(GameObject root)
    {
        RectTransform rootRT = TacticalUIBuilder.RT(root);
        TacticalUIBuilder.Stretch(rootRT);
        rootRT.localPosition = new Vector3(rootRT.localPosition.x, rootRT.localPosition.y, 0f);

        // Подложка: затемняет сцену/видео и перехватывает клики вне модального окна.
        Image scrim = TacticalUIBuilder.EnsureImage(root, null, TacticalUITheme.Scrim, Image.Type.Simple);
        scrim.raycastTarget = true;

        CanvasGroup group = root.GetComponent<CanvasGroup>();
        if (group == null) group = root.AddComponent<CanvasGroup>();

        SettingsPanel panel = root.GetComponent<SettingsPanel>();
        if (panel == null) panel = root.AddComponent<SettingsPanel>();

        // Модальная металлическая плашка 1400x920 по центру экрана.
        GameObject plate = TacticalUIBuilder.EnsureChild(root.transform, "Plate");
        plate.transform.SetAsFirstSibling();
        TacticalUIBuilder.SetCenter(TacticalUIBuilder.RT(plate),
            TacticalUITheme.SettingsModalWidth, TacticalUITheme.SettingsModalHeight, Vector2.zero);
        // Плоская заливка: спрайт-плашка 568x434 при растяжении на 1400x920
        // даёт внутренние декоративные линии, которые пересекают строки настроек.
        TacticalUIBuilder.EnsureImage(plate, null, TacticalUITheme.PanelFill, Image.Type.Simple).raycastTarget = true;

        GameObject header = MoveUnder(root.transform, plate.transform, "Header");
        GameObject tabs = MoveUnder(root.transform, plate.transform, "Tabs");
        GameObject scroll = MoveUnder(root.transform, plate.transform, "ScrollView");

        // Рамка: тонкий контур + красные угловые скобки. Спрайт #7 растягивается
        // с внутренними декоративными линиями, которые проходят поверх строк.
        GameObject frame = TacticalUIBuilder.EnsureChild(plate.transform, "Frame");
        // Рамка рисуется поверх шапки/контента, но состоит только из тонких линий
        // по краю и угловых скобок, поэтому ничего не перекрывает.
        frame.transform.SetAsLastSibling();
        TacticalUIBuilder.Stretch(TacticalUIBuilder.RT(frame));
        Image oldFrameImage = frame.GetComponent<Image>();
        if (oldFrameImage != null) Object.DestroyImmediate(oldFrameImage);
        TacticalUIBuilder.BuildTacticalFrame(frame.transform, new Color(1f, 1f, 1f, 0.18f),
            TacticalUITheme.AccentRed, 2f, 28f, 3f, 0f);

        BuildHeader(header);
        BuildTabs(tabs);
        AssignReferences(panel, root, tabs, scroll);
        BuildScrollView(scroll);
    }

    private static GameObject MoveUnder(Transform root, Transform plate, string name)
    {
        GameObject inPlate = TacticalUIBuilder.FindChild(plate, name);
        GameObject inRoot = TacticalUIBuilder.FindChild(root, name);

        if (inPlate != null)
        {
            if (inRoot != null) Object.DestroyImmediate(inRoot);
            return inPlate;
        }

        if (inRoot != null)
        {
            inRoot.transform.SetParent(plate, false);
            return inRoot;
        }

        return TacticalUIBuilder.CreateRect(name, plate);
    }

    private static void BuildHeader(GameObject header)
    {
        RectTransform rt = TacticalUIBuilder.RT(header);
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = new Vector2(0f, -80f);
        rt.offsetMax = Vector2.zero;

        Image background = TacticalUIBuilder.EnsureImage(header, null,
            new Color(0.085f, 0.09f, 0.1f, 1f), Image.Type.Simple);
        background.raycastTarget = true;

        // Заголовок не должен заезжать под кнопки «Сброс» и «X».
        GameObject titleGO = TacticalUIBuilder.EnsureChild(header.transform, "Title");
        RectTransform titleRT = TacticalUIBuilder.RT(titleGO);
        titleRT.anchorMin = new Vector2(0f, 0f);
        titleRT.anchorMax = new Vector2(1f, 1f);
        titleRT.offsetMin = new Vector2(32f, 0f);
        titleRT.offsetMax = new Vector2(-264f, 0f);
        TextMeshProUGUI title = TacticalUIBuilder.EnsureText(titleGO, "НАСТРОЙКИ", TacticalUITheme.FontModalTitle,
            Color.white, TextAlignmentOptions.MidlineLeft);
        title.fontStyle = FontStyles.Bold;
        title.characterSpacing = 2f;

        GameObject close = TacticalUIBuilder.EnsureChild(header.transform, "CloseButton");
        TacticalUIBuilder.SetTopRight(TacticalUIBuilder.RT(close), 32f, 16f, 48f, 48f);
        Image closeImage = TacticalUIBuilder.EnsureImage(close, buttonSprite, TacticalUITheme.AccentRed);
        TacticalUIBuilder.EnsureButton(close, closeImage);
        TacticalUIBuilder.RemoveChildrenNamed(close.transform, "Text");

        GameObject icon = TacticalUIBuilder.EnsureChild(close.transform, "Icon");
        TacticalUIBuilder.SetCenter(TacticalUIBuilder.RT(icon), 24f, 24f, Vector2.zero);
        Image iconImage = TacticalUIBuilder.EnsureImage(icon, closeSprite, Color.white, Image.Type.Simple);
        iconImage.raycastTarget = false;

        GameObject reset = TacticalUIBuilder.EnsureChild(header.transform, "ResetButton");
        TacticalUIBuilder.SetTopRight(TacticalUIBuilder.RT(reset), 96f, 18f, 168f, 44f);
        Image resetImage = TacticalUIBuilder.EnsureImage(reset, buttonSprite, TacticalUITheme.AccentRed);
        TacticalUIBuilder.EnsureButton(reset, resetImage);

        GameObject resetLabel = TacticalUIBuilder.EnsureChild(reset.transform, "Text");
        TacticalUIBuilder.Stretch(TacticalUIBuilder.RT(resetLabel));
        TacticalUIBuilder.EnsureText(resetLabel, "Сброс", TacticalUITheme.FontButtonSecondary, Color.white, TextAlignmentOptions.Center);

        Image rule = TacticalUIBuilder.EnsureBar(header.transform, "AccentRule", TacticalUITheme.AccentRed);
        RectTransform ruleRT = TacticalUIBuilder.RT(rule.gameObject);
        ruleRT.anchorMin = new Vector2(0f, 0f);
        ruleRT.anchorMax = new Vector2(1f, 0f);
        ruleRT.pivot = new Vector2(0.5f, 0f);
        ruleRT.sizeDelta = new Vector2(0f, 3f);
        ruleRT.anchoredPosition = Vector2.zero;
        rule.transform.SetAsLastSibling();
    }

    private static void BuildTabs(GameObject tabs)
    {
        RectTransform rt = TacticalUIBuilder.RT(tabs);
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = new Vector2(32f, -144f);
        rt.offsetMax = new Vector2(-32f, -92f);

        HorizontalLayoutGroup layout = tabs.GetComponent<HorizontalLayoutGroup>();
        if (layout == null) layout = tabs.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(0, 0, 0, 0);
        layout.spacing = 8f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = true;

        string[] names = { "Tab_Graphics", "Tab_Controls", "Tab_Audio", "Tab_Gameplay" };
        string[] labels = { "Графика", "Управление", "Звук", "Интерфейс" };

        for (int i = 0; i < names.Length; i++)
        {
            bool selected = i == 0;
            GameObject tab = TacticalUIBuilder.EnsureChild(tabs.transform, names[i]);
            RectTransform tabRT = TacticalUIBuilder.RT(tab);
            tabRT.anchorMin = new Vector2(0f, 0f);
            tabRT.anchorMax = new Vector2(0f, 1f);
            tabRT.sizeDelta = new Vector2(330f, 0f);

            Image image = TacticalUIBuilder.EnsureImage(tab, buttonSprite,
                selected ? Color.white : TacticalUITheme.TabIdle);
            image.raycastTarget = true;
            TacticalUIBuilder.EnsureButton(tab, image);

            GameObject labelGO = TacticalUIBuilder.EnsureChild(tab.transform, "Text");
            TacticalUIBuilder.Stretch(TacticalUIBuilder.RT(labelGO));
            TextMeshProUGUI label = TacticalUIBuilder.EnsureText(labelGO, labels[i],
                TacticalUITheme.FontButtonSecondary,
                selected ? Color.white : new Color(1f, 1f, 1f, 0.72f),
                TextAlignmentOptions.Center);
            label.fontStyle = selected ? FontStyles.Bold : FontStyles.Normal;

            Image accent = TacticalUIBuilder.EnsureBar(tab.transform, "Accent", TacticalUITheme.AccentRed);
            RectTransform accentRT = TacticalUIBuilder.RT(accent.gameObject);
            accentRT.anchorMin = new Vector2(0f, 0f);
            accentRT.anchorMax = new Vector2(1f, 0f);
            accentRT.pivot = new Vector2(0.5f, 0f);
            accentRT.sizeDelta = new Vector2(0f, 3f);
            accentRT.anchoredPosition = Vector2.zero;
            accent.gameObject.SetActive(selected);
        }
    }

    private static SettingsPanel AssignReferences(SettingsPanel panel, GameObject root, GameObject tabs, GameObject scroll)
    {
        SerializedObject serialized = new SerializedObject(panel);

        SerializedProperty tabButtons = serialized.FindProperty("tabButtons");
        if (tabButtons != null)
        {
            tabButtons.arraySize = 4;
            string[] names = { "Tab_Graphics", "Tab_Controls", "Tab_Audio", "Tab_Gameplay" };

            for (int i = 0; i < names.Length; i++)
            {
                GameObject tab = TacticalUIBuilder.FindChild(tabs.transform, names[i]);
                Button button = tab != null ? tab.GetComponent<Button>() : null;
                tabButtons.GetArrayElementAtIndex(i).objectReferenceValue = button;
            }
        }

        GameObject closeObject = TacticalUIBuilder.FindChild(FindHeader(root, tabs).transform, "CloseButton");
        GameObject resetObject = TacticalUIBuilder.FindChild(FindHeader(root, tabs).transform, "ResetButton");

        SetRef(serialized, "closeButton", closeObject != null ? closeObject.GetComponent<Button>() : null);
        SetRef(serialized, "resetButton", resetObject != null ? resetObject.GetComponent<Button>() : null);
        SetRef(serialized, "listBuilder", scroll.GetComponentInChildren<SettingsListBuilder>(true));
        SetRef(serialized, "listScroll", scroll.GetComponent<ScrollRect>());
        SetRef(serialized, "group", root.GetComponent<CanvasGroup>());

        SerializedProperty hide = serialized.FindProperty("hideOnStart");
        if (hide != null) hide.boolValue = true;

        // Цвета вкладок заданы в префабе явно: старые значения (синий 0.3/0.5/0.8)
        // оставались в сериализованных данных и перебивали новые дефолты класса.
        SerializedProperty selectedTab = serialized.FindProperty("selectedTabColor");
        if (selectedTab != null) selectedTab.colorValue = Color.white;

        SerializedProperty normalTab = serialized.FindProperty("normalTabColor");
        if (normalTab != null) normalTab.colorValue = TacticalUITheme.TabIdle;

        serialized.ApplyModifiedPropertiesWithoutUndo();
        return panel;
    }

    private static GameObject FindHeader(GameObject root, GameObject tabs)
    {
        Transform parent = tabs.transform.parent;
        GameObject header = parent != null ? TacticalUIBuilder.FindChild(parent, "Header") : null;
        return header != null ? header : root;
    }

    private static void SetRef(SerializedObject serialized, string field, Object value)
    {
        SerializedProperty property = serialized.FindProperty(field);
        if (property == null)
        {
            Debug.LogError($"[SettingsPanelPrefabBuilder] Не найдено поле '{field}' в {serialized.targetObject.GetType().Name}");
            return;
        }

        property.objectReferenceValue = value;
    }

    private static void BuildScrollView(GameObject scroll)
    {
        RectTransform rt = TacticalUIBuilder.RT(scroll);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(32f, 24f);
        rt.offsetMax = new Vector2(-32f, -156f);

        // Фон списка не нужен: строки лежат прямо на теле модальной плашки.
        Image background = TacticalUIBuilder.EnsureImage(scroll, null, new Color(0f, 0f, 0f, 0f), Image.Type.Simple);
        background.raycastTarget = true;

        ScrollRect scrollRect = scroll.GetComponent<ScrollRect>();
        if (scrollRect == null) scrollRect = scroll.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 30f;
        scrollRect.inertia = true;

        GameObject viewport = TacticalUIBuilder.EnsureChild(scroll.transform, "Viewport");
        RectTransform viewportRT = TacticalUIBuilder.RT(viewport);
        viewportRT.anchorMin = Vector2.zero;
        viewportRT.anchorMax = Vector2.one;
        viewportRT.pivot = new Vector2(0.5f, 0.5f);
        viewportRT.offsetMin = Vector2.zero;
        viewportRT.offsetMax = new Vector2(-20f, 0f);

        Image viewportImage = viewport.GetComponent<Image>();
        if (viewportImage != null)
        {
            viewportImage.color = new Color(0f, 0f, 0f, 0f);
            viewportImage.raycastTarget = false;
        }

        if (viewport.GetComponent<RectMask2D>() == null) viewport.AddComponent<RectMask2D>();

        GameObject content = TacticalUIBuilder.EnsureChild(viewport.transform, "Content");
        RectTransform contentRT = TacticalUIBuilder.RT(content);
        contentRT.anchorMin = new Vector2(0f, 1f);
        contentRT.anchorMax = new Vector2(1f, 1f);
        contentRT.pivot = new Vector2(0.5f, 1f);
        contentRT.offsetMin = new Vector2(32f, 0f);
        contentRT.offsetMax = new Vector2(-32f, 0f);
        contentRT.sizeDelta = new Vector2(0f, 0f);

        VerticalLayoutGroup layout = content.GetComponent<VerticalLayoutGroup>();
        if (layout == null) layout = content.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(0, 0, 0, 0);
        layout.spacing = TacticalUITheme.RowSpacing;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = content.GetComponent<ContentSizeFitter>();
        if (fitter == null) fitter = content.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        SettingsListBuilder builder = content.GetComponent<SettingsListBuilder>();
        if (builder == null) builder = content.AddComponent<SettingsListBuilder>();

        scrollRect.viewport = viewportRT;
        scrollRect.content = contentRT;

        BuildScrollbar(scrollRect);
        ConfigureBuilder(builder, contentRT);
    }

    private static void BuildScrollbar(ScrollRect scrollRect)
    {
        GameObject scrollbarObject = TacticalUIBuilder.EnsureChild(scrollRect.transform, "Scrollbar Vertical");
        RectTransform scrollbarRT = TacticalUIBuilder.RT(scrollbarObject);
        scrollbarRT.anchorMin = new Vector2(1f, 0f);
        scrollbarRT.anchorMax = new Vector2(1f, 1f);
        scrollbarRT.pivot = new Vector2(1f, 0.5f);
        scrollbarRT.sizeDelta = new Vector2(8f, 0f);
        scrollbarRT.anchoredPosition = new Vector2(-6f, 0f);

        Image scrollbarImage = TacticalUIBuilder.EnsureImage(scrollbarObject, null,
            new Color(1f, 1f, 1f, 0.06f), Image.Type.Simple);
        scrollbarImage.raycastTarget = true;

        Scrollbar scrollbar = scrollbarObject.GetComponent<Scrollbar>();
        if (scrollbar == null) scrollbar = scrollbarObject.AddComponent<Scrollbar>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        scrollbar.targetGraphic = scrollbarImage;
        scrollbar.size = 0.35f;

        GameObject sliding = TacticalUIBuilder.EnsureChild(scrollbarObject.transform, "Sliding Area");
        TacticalUIBuilder.Stretch(TacticalUIBuilder.RT(sliding));

        GameObject handle = TacticalUIBuilder.EnsureChild(sliding.transform, "Handle");
        TacticalUIBuilder.Stretch(TacticalUIBuilder.RT(handle));
        Image handleImage = TacticalUIBuilder.EnsureImage(handle,
            TacticalUIBuilder.LoadSprite("UI Starter Pack_28"), TacticalUITheme.Steel, Image.Type.Simple);
        handleImage.raycastTarget = true;
        scrollbar.handleRect = TacticalUIBuilder.RT(handle);

        scrollRect.verticalScrollbar = scrollbar;
        scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        scrollRect.horizontalScrollbar = null;
    }

    private static void ConfigureBuilder(SettingsListBuilder builder, RectTransform content)
    {
        SerializedObject serialized = new SerializedObject(builder);

        SetRef(serialized, "content", content);
        SetRef(serialized, "fontAsset", TacticalUIBuilder.LoadFont());
        SetRef(serialized, "sliderTrackSprite", trackSprite);
        SetRef(serialized, "sliderFillSprite", fillSprite);
        SetRef(serialized, "sliderHandleSprite", handleSprite);
        SetRef(serialized, "toggleOnSprite", toggleOnSprite);
        SetRef(serialized, "toggleOffSprite", toggleOffSprite);
        SetRef(serialized, "dropdownPlateSprite", dropdownSprite);
        SetRef(serialized, "dropdownArrowSprite", arrowSprite);

        SetFloat(serialized, "rowHeight", TacticalUITheme.RowHeight);
        SetFloat(serialized, "sliderWidth", 320f);
        SetFloat(serialized, "sliderHeight", 24f);
        SetFloat(serialized, "trackHeight", 8f);
        SetFloat(serialized, "handleSize", 16f);
        SetFloat(serialized, "dropdownHeight", 40f);
        SetFloat(serialized, "valueWidth", 88f);
        SetFloat(serialized, "toggleSize", 32f);
        SetFloat(serialized, "controlSpacing", 16f);

        SetColor(serialized, "labelColor", new Color(1f, 1f, 1f, 0.9f));
        SetColor(serialized, "valueColor", TacticalUITheme.TextSecondary);
        SetColor(serialized, "trackColor", TacticalUITheme.TrackDark);
        SetColor(serialized, "fillColor", TacticalUITheme.Steel);
        SetColor(serialized, "handleColor", new Color(0.95f, 0.95f, 0.95f, 1f));
        SetColor(serialized, "dropdownColor", TacticalUITheme.ControlPlate);
        SetColor(serialized, "dropdownTextColor", Color.white);
        SetColor(serialized, "optionColor", new Color(0.1f, 0.11f, 0.12f, 0.98f));
        SetColor(serialized, "selectedOptionColor", TacticalUITheme.ControlPlateHover);

        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetFloat(SerializedObject serialized, string field, float value)
    {
        SerializedProperty property = serialized.FindProperty(field);
        if (property != null) property.floatValue = value;
    }

    private static void SetColor(SerializedObject serialized, string field, Color value)
    {
        SerializedProperty property = serialized.FindProperty(field);
        if (property != null) property.colorValue = value;
    }

    private static void EnsureFolder()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
            AssetDatabase.CreateFolder("Assets", "Prefabs");

        if (!AssetDatabase.IsValidFolder(PrefabFolder))
            AssetDatabase.CreateFolder("Assets/Prefabs", "UI");
    }
}