using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class SettingsPanelPrefabBuilder
{
    private const string PrefabFolder = "Assets/Prefabs/UI";
    private const string PrefabPath = PrefabFolder + "/SettingsPanel.prefab";
    private const int UiLayer = 5;

    private static Sprite uiSprite;
    private static Font uiFont;

    [MenuItem("Tools/DroneSimulator/Create SettingsPanel Prefab")]
    public static void BuildPrefab()
    {
        uiSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        GameObject root = CreateRect("SettingsPanel", null);

        RectTransform rootRT = (RectTransform)root.transform;
        Stretch(rootRT);

        Image rootImage = root.AddComponent<Image>();
        rootImage.color = new Color(0.09f, 0.10f, 0.13f, 0.96f);

        CanvasGroup canvasGroup = root.AddComponent<CanvasGroup>();

        SettingsPanel panel = root.AddComponent<SettingsPanel>();

        GameObject header = BuildHeader(root.transform);
        Button closeButton = BuildCloseButton(header.transform);
        Button[] tabButtons = BuildTabs(root.transform);
        SettingsListBuilder listBuilder = BuildScrollView(root.transform);

        SerializedObject panelSO = new SerializedObject(panel);
        SerializedProperty tabsProperty = panelSO.FindProperty("tabButtons");
        tabsProperty.arraySize = tabButtons.Length;
        for (int i = 0; i < tabButtons.Length; i++)
            tabsProperty.GetArrayElementAtIndex(i).objectReferenceValue = tabButtons[i];

        panelSO.FindProperty("listBuilder").objectReferenceValue = listBuilder;
        panelSO.FindProperty("closeButton").objectReferenceValue = closeButton;
        panelSO.FindProperty("hideOnStart").boolValue = true;
        panelSO.FindProperty("group").objectReferenceValue = canvasGroup;
        panelSO.ApplyModifiedPropertiesWithoutUndo();

        SerializedObject builderSO = new SerializedObject(listBuilder);
        builderSO.FindProperty("content").objectReferenceValue = listBuilder.transform;
        builderSO.ApplyModifiedPropertiesWithoutUndo();

        EnsureFolder();

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool success);
        Object.DestroyImmediate(root);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (success) Debug.Log($"[SettingsPanelPrefabBuilder] Префаб создан: {PrefabPath}");
        else Debug.LogError($"[SettingsPanelPrefabBuilder] Не удалось создать префаб: {PrefabPath}");
    }

    private static GameObject BuildHeader(Transform parent)
    {
        GameObject header = CreateRect("Header", parent);
        RectTransform rt = (RectTransform)header.transform;
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = new Vector2(0f, -72f);
        rt.offsetMax = Vector2.zero;

        Image image = header.AddComponent<Image>();
        ApplySprite(image, new Color(0.14f, 0.16f, 0.20f, 1f));

        GameObject title = CreateRect("Title", header.transform);
        RectTransform titleRT = (RectTransform)title.transform;
        titleRT.anchorMin = Vector2.zero;
        titleRT.anchorMax = Vector2.one;
        titleRT.offsetMin = new Vector2(24f, 0f);
        titleRT.offsetMax = new Vector2(-80f, 0f);
        AddText(title, "НАСТРОЙКИ", 30, TextAnchor.MiddleLeft, Color.white);

        return header;
    }

    private static Button BuildCloseButton(Transform parent)
    {
        GameObject go = CreateRect("CloseButton", parent);
        RectTransform rt = (RectTransform)go.transform;
        rt.anchorMin = new Vector2(1f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 1f);
        rt.sizeDelta = new Vector2(64f, 64f);
        rt.anchoredPosition = new Vector2(-16f, -4f);

        Image image = go.AddComponent<Image>();
        ApplySprite(image, Color.white);

        Button button = go.AddComponent<Button>();
        button.targetGraphic = image;

        GameObject label = CreateRect("Text", go.transform);
        Stretch((RectTransform)label.transform);
        AddText(label, "X", 24, TextAnchor.MiddleCenter, Color.white);

        return button;
    }

    private static Button[] BuildTabs(Transform parent)
    {
        GameObject tabs = CreateRect("Tabs", parent);
        RectTransform rt = (RectTransform)tabs.transform;
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = new Vector2(16f, -164f);
        rt.offsetMax = new Vector2(-16f, -108f);

        HorizontalLayoutGroup layout = tabs.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(0, 0, 0, 0);
        layout.spacing = 8f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = true;

        string[] names = { "Tab_Graphics", "Tab_Controls", "Tab_Audio", "Tab_Gameplay" };
        string[] labels = { "Графика", "Управление", "Звук", "Интерфейс" };

        Button[] buttons = new Button[names.Length];
        for (int i = 0; i < names.Length; i++)
        {
            GameObject tab = CreateRect(names[i], tabs.transform);

            Image image = tab.AddComponent<Image>();
            ApplySprite(image, Color.white);

            Button button = tab.AddComponent<Button>();
            button.targetGraphic = image;

            GameObject label = CreateRect("Text", tab.transform);
            Stretch((RectTransform)label.transform);
            AddText(label, labels[i], 20, TextAnchor.MiddleCenter, Color.white);

            buttons[i] = button;
        }

        return buttons;
    }

    private static SettingsListBuilder BuildScrollView(Transform parent)
    {
        GameObject scrollGo = CreateRect("ScrollView", parent);
        RectTransform scrollRT = (RectTransform)scrollGo.transform;
        scrollRT.anchorMin = Vector2.zero;
        scrollRT.anchorMax = Vector2.one;
        scrollRT.offsetMin = new Vector2(16f, 16f);
        scrollRT.offsetMax = new Vector2(-16f, -148f);

        Image scrollImage = scrollGo.AddComponent<Image>();
        scrollImage.color = new Color(0.07f, 0.08f, 0.10f, 0.90f);

        ScrollRect scroll = scrollGo.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 30f;

        GameObject viewport = CreateRect("Viewport", scrollGo.transform);
        RectTransform viewportRT = (RectTransform)viewport.transform;
        viewportRT.anchorMin = Vector2.zero;
        viewportRT.anchorMax = Vector2.one;
        viewportRT.offsetMin = new Vector2(0f, 0f);
        viewportRT.offsetMax = new Vector2(-20f, 0f);
        viewport.AddComponent<RectMask2D>();
        scroll.viewport = viewportRT;

        GameObject content = CreateRect("Content", viewport.transform);
        RectTransform contentRT = (RectTransform)content.transform;
        contentRT.anchorMin = new Vector2(0f, 1f);
        contentRT.anchorMax = new Vector2(1f, 1f);
        contentRT.pivot = new Vector2(0.5f, 1f);
        contentRT.offsetMin = Vector2.zero;
        contentRT.offsetMax = Vector2.zero;
        contentRT.sizeDelta = new Vector2(0f, 600f);

        VerticalLayoutGroup layout = content.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(8, 8, 8, 8);
        layout.spacing = 8f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = content.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        SettingsListBuilder builder = content.AddComponent<SettingsListBuilder>();
        scroll.content = contentRT;

        GameObject scrollbarGo = CreateRect("Scrollbar Vertical", scrollGo.transform);
        RectTransform scrollbarRT = (RectTransform)scrollbarGo.transform;
        scrollbarRT.anchorMin = new Vector2(1f, 0f);
        scrollbarRT.anchorMax = new Vector2(1f, 1f);
        scrollbarRT.pivot = new Vector2(1f, 0.5f);
        scrollbarRT.sizeDelta = new Vector2(16f, 0f);
        scrollbarRT.anchoredPosition = Vector2.zero;

        Image scrollbarImage = scrollbarGo.AddComponent<Image>();
        ApplySprite(scrollbarImage, new Color(0.20f, 0.20f, 0.22f, 1f));

        Scrollbar scrollbar = scrollbarGo.AddComponent<Scrollbar>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        scrollbar.targetGraphic = scrollbarImage;
        scrollbar.size = 0.5f;

        GameObject sliding = CreateRect("Sliding Area", scrollbarGo.transform);
        Stretch((RectTransform)sliding.transform);

        GameObject handle = CreateRect("Handle", sliding.transform);
        Stretch((RectTransform)handle.transform);

        Image handleImage = handle.AddComponent<Image>();
        ApplySprite(handleImage, new Color(0.35f, 0.55f, 0.85f, 1f));

        scrollbar.handleRect = (RectTransform)handle.transform;

        scroll.verticalScrollbar = scrollbar;
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;

        return builder;
    }

    private static GameObject CreateRect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.layer = UiLayer;

        if (parent != null)
            go.transform.SetParent(parent, false);

        return go;
    }

    private static void ApplySprite(Image image, Color color)
    {
        image.color = color;

        if (uiSprite == null) return;

        image.sprite = uiSprite;
        image.type = Image.Type.Sliced;
    }

    private static Text AddText(GameObject go, string text, int size, TextAnchor anchor, Color color)
    {
        Text label = go.AddComponent<Text>();
        label.font = uiFont;
        label.text = text;
        label.fontSize = size;
        label.alignment = anchor;
        label.color = color;
        label.raycastTarget = false;
        return label;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static void EnsureFolder()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
            AssetDatabase.CreateFolder("Assets", "Prefabs");

        if (!AssetDatabase.IsValidFolder(PrefabFolder))
            AssetDatabase.CreateFolder("Assets/Prefabs", "UI");
    }
}
