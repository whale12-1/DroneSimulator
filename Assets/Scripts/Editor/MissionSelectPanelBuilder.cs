using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Приводит MissionSelectPanel.prefab и MissionCard.prefab к единому тактическому стилю:
/// непрозрачный терминал на весь экран, вертикальный список широких карточек 840x150,
/// панель деталей 860 px, единый красный акцент #DB143D.
/// Правка in-place: Header и MissionDetailsPanel не пересоздаются, ссылки сцен
/// (MissionSelectScreen.detailsPanel / cardsContainer) сохраняются.
/// </summary>
public static class MissionSelectPanelBuilder
{
    private const string PanelPrefabPath = "Assets/Prefabs/UI/MissionSelectPanel.prefab";
    private const string CardPrefabPath = "Assets/Prefabs/UI/MissionCard.prefab";

    private static Sprite buttonSprite;
    private static Sprite knobSprite;
    private static Sprite toggleOnSprite;
    private static Sprite lockSprite;
    private static Sprite pixelSprite;

    [MenuItem("Tools/DroneSimulator/Mission UI/Rebuild Prefabs")]
    public static void RebuildPrefabs()
    {
        BuildPanel();
        BuildCard();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    [MenuItem("Tools/DroneSimulator/Mission UI/Apply All")]
    public static void ApplyAll()
    {
        MissionContentBuilder.Apply();
        RebuildPrefabs();
    }

    private static void CacheSprites()
    {
        buttonSprite = TacticalUIBuilder.LoadSprite("UI Starter Pack_1");
        knobSprite = TacticalUIBuilder.LoadSprite("UI Starter Pack_22");
        toggleOnSprite = TacticalUIBuilder.LoadSprite("UI Starter Pack_20");
        lockSprite = TacticalUIBuilder.LoadSprite("UI Starter Pack_50");
        pixelSprite = TacticalUIBuilder.LoadSprite("UI Starter Pack_28");
    }

    private static void BuildPanel()
    {
        CacheSprites();

        GameObject root = PrefabUtility.LoadPrefabContents(PanelPrefabPath);
        if (root == null)
        {
            Debug.LogError($"[MissionSelectPanelBuilder] Не найден префаб: {PanelPrefabPath}");
            return;
        }

        try
        {
            // Корень: непрозрачная заливка (иначе сквозь панель видно обои и иконку рабочего стола).
            Image rootImage = TacticalUIBuilder.EnsureImage(root, null, TacticalUITheme.PanelFill, Image.Type.Simple);
            rootImage.raycastTarget = true;

            GameObject frame = TacticalUIBuilder.EnsureChild(root.transform, "Frame");
            frame.transform.SetAsLastSibling();
            TacticalUIBuilder.Stretch(TacticalUIBuilder.RT(frame));
            Image oldFrameImage = frame.GetComponent<Image>();
            if (oldFrameImage != null) Object.DestroyImmediate(oldFrameImage);
            TacticalUIBuilder.BuildTacticalFrame(frame.transform, new Color(1f, 1f, 1f, 0.2f),
                TacticalUITheme.AccentRed, 2f, 36f, 3f, 0f);

            GameObject header = TacticalUIBuilder.EnsureChild(root.transform, "Header");
            BuildHeader(header);

            GameObject scroll = TacticalUIBuilder.EnsureChild(root.transform, "Scroll View");
            BuildScrollView(scroll);

            GameObject details = TacticalUIBuilder.EnsureChild(root.transform, "MissionDetailsPanel");
            BuildDetails(details);

            AssignReferences(root, header, details);
            EnsureContentReference(scroll);

            PrefabUtility.SaveAsPrefabAsset(root, PanelPrefabPath, out bool success);
            if (success) Debug.Log($"[MissionSelectPanelBuilder] Префаб панели обновлён: {PanelPrefabPath}");
            else Debug.LogError($"[MissionSelectPanelBuilder] Не удалось сохранить префаб: {PanelPrefabPath}");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void BuildHeader(GameObject header)
    {
        RectTransform rt = TacticalUIBuilder.RT(header);
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.offsetMin = new Vector2(80f, -150f);
        rt.offsetMax = new Vector2(-80f, -40f);

        Image background = header.GetComponent<Image>();
        if (background != null)
        {
            background.sprite = null;
            background.color = new Color(0f, 0f, 0f, 0f);
            background.raycastTarget = false;
        }

        GameObject titleGO = TacticalUIBuilder.EnsureChild(header.transform, "Title");
        SetTopLeft(titleGO, 0f, 8f, 1000f, 46f);
        TextMeshProUGUI title = TacticalUIBuilder.EnsureText(titleGO, "ВЫБОР МИССИИ", TacticalUITheme.FontTitle,
            Color.white, TextAlignmentOptions.BottomLeft);
        title.fontStyle = FontStyles.Bold;
        title.characterSpacing = 2f;

        GameObject subtitleGO = TacticalUIBuilder.EnsureChild(header.transform, "Subtitle");
        SetTopLeft(subtitleGO, 0f, 58f, 1000f, 18f);
        TacticalUIBuilder.EnsureText(subtitleGO, "ОПЕРАТИВНЫЙ ТЕРМИНАЛ", 13f,
            TacticalUITheme.TextSecondary, TextAlignmentOptions.BottomLeft);

        GameObject scoreGO = TacticalUIBuilder.EnsureChild(header.transform, "TotalScore");
        TacticalUIBuilder.SetTopRight(TacticalUIBuilder.RT(scoreGO), 200f, 30f, 320f, 26f);
        TacticalUIBuilder.EnsureText(scoreGO, "ОБЩИЙ СЧЁТ: 0", 15f,
            TacticalUITheme.TextCaption, TextAlignmentOptions.MidlineRight);

        GameObject ruleGO = TacticalUIBuilder.EnsureChild(header.transform, "Rule");
        RectTransform ruleRT = TacticalUIBuilder.RT(ruleGO);
        ruleRT.anchorMin = new Vector2(0f, 0f);
        ruleRT.anchorMax = new Vector2(1f, 0f);
        ruleRT.pivot = new Vector2(0.5f, 0f);
        ruleRT.sizeDelta = new Vector2(0f, 2f);
        ruleRT.anchoredPosition = Vector2.zero;
        Image rule = TacticalUIBuilder.EnsureImage(ruleGO, null, TacticalUITheme.Divider, Image.Type.Simple);
        rule.raycastTarget = false;

        GameObject backGO = TacticalUIBuilder.EnsureChild(header.transform, "BackButton");
        TacticalUIBuilder.SetTopRight(TacticalUIBuilder.RT(backGO), 0f, 26f, 160f, 56f);
        Image backImage = TacticalUIBuilder.EnsureImage(backGO, buttonSprite, TacticalUITheme.AccentRed);
        TacticalUIBuilder.EnsureButton(backGO, backImage);

        TextMeshProUGUI backLabel = backGO.GetComponentInChildren<TextMeshProUGUI>(true);
        if (backLabel != null)
        {
            RectTransform labelRT = (RectTransform)backLabel.transform;
            labelRT.anchorMin = Vector2.zero;
            labelRT.anchorMax = Vector2.one;
            labelRT.offsetMin = Vector2.zero;
            labelRT.offsetMax = Vector2.zero;

            backLabel.font = TacticalUIBuilder.LoadFont();
            backLabel.text = "НАЗАД";
            backLabel.fontSize = 22f;
            backLabel.color = Color.white;
            backLabel.alignment = TextAlignmentOptions.Center;
            backLabel.raycastTarget = false;
        }
    }

    private static void BuildScrollView(GameObject scroll)
    {
        RectTransform rt = TacticalUIBuilder.RT(scroll);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(80f, 80f);
        rt.offsetMax = new Vector2(-980f, -186f);

        Image background = TacticalUIBuilder.EnsureImage(scroll, null, TacticalUITheme.MissionListFill, Image.Type.Simple);
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

        // Legacy Image-based Mask на Viewport несовместим с прозрачной графикой:
        // материал маски включает UNITY_UI_ALPHACLIP, поэтому при alpha = 0 он не
        // пишет stencil, и все карточки списка отсекаются (выглядели «чёрным
        // прямоугольником» — клики при этом работали). Клипать должен только RectMask2D.
        Mask legacyMask = viewport.GetComponent<Mask>();
        if (legacyMask != null) Object.DestroyImmediate(legacyMask);

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
        contentRT.offsetMin = new Vector2(12f, 0f);
        contentRT.offsetMax = new Vector2(-12f, 0f);
        contentRT.sizeDelta = new Vector2(0f, 0f);

        // HorizontalLayoutGroup и VerticalLayoutGroup взаимоисключающие: старую
        // горизонтальную раскладку нужно снять до добавления вертикальной.
        HorizontalLayoutGroup oldLayout = content.GetComponent<HorizontalLayoutGroup>();
        if (oldLayout != null) Object.DestroyImmediate(oldLayout);

        VerticalLayoutGroup layout = content.GetComponent<VerticalLayoutGroup>();
        if (layout == null) layout = content.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(0, 0, 0, 0);
        layout.spacing = 12f;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = content.GetComponent<ContentSizeFitter>();
        if (fitter == null) fitter = content.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        GameObject oldHorizontal = TacticalUIBuilder.FindChild(scroll.transform, "Scrollbar Horizontal");
        if (oldHorizontal != null) Object.DestroyImmediate(oldHorizontal);

        GameObject scrollbarObject = TacticalUIBuilder.EnsureChild(scroll.transform, "Scrollbar Vertical");
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
        scrollbar.size = 0.4f;

        GameObject sliding = TacticalUIBuilder.EnsureChild(scrollbarObject.transform, "Sliding Area");
        TacticalUIBuilder.Stretch(TacticalUIBuilder.RT(sliding));

        GameObject handle = TacticalUIBuilder.EnsureChild(sliding.transform, "Handle");
        TacticalUIBuilder.Stretch(TacticalUIBuilder.RT(handle));
        Image handleImage = TacticalUIBuilder.EnsureImage(handle, pixelSprite, TacticalUITheme.Steel, Image.Type.Simple);
        handleImage.raycastTarget = true;
        scrollbar.handleRect = TacticalUIBuilder.RT(handle);

        scrollRect.viewport = viewportRT;
        scrollRect.content = contentRT;
        scrollRect.verticalScrollbar = scrollbar;
        scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        scrollRect.horizontalScrollbar = null;
    }

    private static void EnsureContentReference(GameObject scroll)
    {
        MissionSelectScreen screen = scroll.transform.root.GetComponent<MissionSelectScreen>();
        if (screen == null) return;

        Transform content = scroll.transform.Find("Viewport/Content");
        if (content == null) return;

        SerializedObject serialized = new SerializedObject(screen);
        SerializedProperty property = serialized.FindProperty("cardsContainer");
        if (property != null) property.objectReferenceValue = content;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void BuildDetails(GameObject details)
    {
        RectTransform rt = TacticalUIBuilder.RT(details);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(980f, 80f);
        rt.offsetMax = new Vector2(-80f, -186f);

        Image plate = TacticalUIBuilder.EnsureImage(details, null, TacticalUITheme.PanelFillSoft, Image.Type.Simple);
        plate.raycastTarget = true;

        GameObject emptyState = TacticalUIBuilder.EnsureChild(details.transform, "EmptyState");
        TacticalUIBuilder.Stretch(TacticalUIBuilder.RT(emptyState), 24f);
        TextMeshProUGUI emptyLabel = TacticalUIBuilder.EnsureText(emptyState, "ВЫБЕРИТЕ ОПЕРАЦИЮ\nСПИСОК МИССИЙ СЛЕВА",
            16f, new Color(1f, 1f, 1f, 0.45f), TextAlignmentOptions.Center);
        emptyLabel.fontStyle = FontStyles.Bold;

        GameObject content = TacticalUIBuilder.EnsureChild(details.transform, "Content");
        TacticalUIBuilder.Stretch(TacticalUIBuilder.RT(content));

        GameObject banner = TacticalUIBuilder.EnsureChild(content.transform, "Banner");
        SetTopLeft(banner, 24f, 20f, 812f, 150f);
        TacticalUIBuilder.EnsureImage(banner, null, new Color(0.1f, 0.11f, 0.13f, 1f), Image.Type.Simple).raycastTarget = false;

        GameObject bannerCode = TacticalUIBuilder.EnsureChild(content.transform, "BannerCode");
        SetTopLeft(bannerCode, 36f, 30f, 480f, 18f);
        TacticalUIBuilder.EnsureText(bannerCode, "ID: MISSION", 12f, TacticalUITheme.TextSecondary, TextAlignmentOptions.MidlineLeft);

        GameObject techLine = TacticalUIBuilder.EnsureChild(content.transform, "TechLine");
        TacticalUIBuilder.SetTopRight(TacticalUIBuilder.RT(techLine), 36f, 30f, 400f, 18f);
        TacticalUIBuilder.EnsureText(techLine, "ТЕХНИКА: — · БОРТОВ: 0", 12f, TacticalUITheme.TextSecondary, TextAlignmentOptions.MidlineRight);

        GameObject statusIcon = TacticalUIBuilder.EnsureChild(content.transform, "StatusIcon");
        SetTopLeft(statusIcon, 24f, 186f, 20f, 20f);
        TacticalUIBuilder.EnsureImage(statusIcon, knobSprite, TacticalUITheme.StatusPending, Image.Type.Simple).raycastTarget = false;

        GameObject statusLabel = TacticalUIBuilder.EnsureChild(content.transform, "StatusLabel");
        SetTopLeft(statusLabel, 52f, 182f, 420f, 24f);
        TextMeshProUGUI statusText = TacticalUIBuilder.EnsureText(statusLabel, "НЕ ВЫПОЛНЕНО", 14f,
            Color.white, TextAlignmentOptions.MidlineLeft);
        statusText.fontStyle = FontStyles.Bold;

        GameObject rewardLabel = TacticalUIBuilder.EnsureChild(content.transform, "RewardLabel");
        TacticalUIBuilder.SetTopRight(TacticalUIBuilder.RT(rewardLabel), 36f, 182f, 320f, 24f);
        TacticalUIBuilder.EnsureText(rewardLabel, "НАГРАДА +100", 12f, TacticalUITheme.TextSecondary, TextAlignmentOptions.MidlineRight);

        GameObject title = TacticalUIBuilder.EnsureChild(content.transform, "Title");
        SetTopLeft(title, 24f, 214f, 812f, 62f);
        TextMeshProUGUI titleText = TacticalUIBuilder.EnsureText(title, "НАЗВАНИЕ", 26f, Color.white, TextAlignmentOptions.TopLeft);
        titleText.fontStyle = FontStyles.Bold;
        titleText.overflowMode = TextOverflowModes.Ellipsis;

        GameObject rule = TacticalUIBuilder.EnsureChild(content.transform, "Rule");
        SetTopLeft(rule, 24f, 284f, 812f, 2f);
        TacticalUIBuilder.EnsureImage(rule, null, TacticalUITheme.Divider, Image.Type.Simple).raycastTarget = false;

        GameObject descriptionLabel = TacticalUIBuilder.EnsureChild(content.transform, "DescriptionLabel");
        SetTopLeft(descriptionLabel, 24f, 294f, 812f, 16f);
        TacticalUIBuilder.EnsureText(descriptionLabel, "ОПИСАНИЕ", TacticalUITheme.FontCaption, TacticalUITheme.TextCaption, TextAlignmentOptions.MidlineLeft);

        GameObject description = TacticalUIBuilder.EnsureChild(content.transform, "Description");
        SetTopLeft(description, 24f, 314f, 812f, 110f);
        TextMeshProUGUI descriptionText = TacticalUIBuilder.EnsureText(description, "Описание операции не задано.",
            TacticalUITheme.FontBody, new Color(1f, 1f, 1f, 0.85f), TextAlignmentOptions.TopLeft);
        descriptionText.overflowMode = TextOverflowModes.Ellipsis;

        GameObject statsLabel = TacticalUIBuilder.EnsureChild(content.transform, "StatsLabel");
        SetTopLeft(statsLabel, 24f, 436f, 812f, 16f);
        TacticalUIBuilder.EnsureText(statsLabel, "СТАТИСТИКА", TacticalUITheme.FontCaption, TacticalUITheme.TextCaption, TextAlignmentOptions.MidlineLeft);

        BuildStatTile(content, "TimeTile", 24f, 456f, "ЛУЧШЕЕ ВРЕМЯ");
        BuildStatTile(content, "ScoreTile", 464f, 456f, "СЧЁТ");
        BuildStatTile(content, "CompletionsTile", 24f, 540f, "ПРОХОЖДЕНИЙ");
        BuildStatTile(content, "TargetsTile", 464f, 540f, "ЦЕЛЕЙ");

        GameObject note = TacticalUIBuilder.EnsureChild(content.transform, "Note");
        SetTopLeft(note, 24f, 618f, 812f, 18f);
        TacticalUIBuilder.EnsureText(note, "Результат появится после первого прохождения", 13f,
            TacticalUITheme.TextCaption, TextAlignmentOptions.MidlineLeft);

        GameObject goalsLabel = TacticalUIBuilder.EnsureChild(content.transform, "GoalsLabel");
        SetTopLeft(goalsLabel, 24f, 642f, 812f, 16f);
        TacticalUIBuilder.EnsureText(goalsLabel, "ЦЕЛИ ОПЕРАЦИИ", TacticalUITheme.FontCaption, TacticalUITheme.TextCaption, TextAlignmentOptions.MidlineLeft);

        for (int i = 0; i < 3; i++)
        {
            float y = 664f + i * 22f;

            GameObject marker = TacticalUIBuilder.EnsureChild(content.transform, $"GoalMarker{i}");
            SetTopLeft(marker, 26f, y + 4f, 10f, 10f);
            TacticalUIBuilder.EnsureImage(marker, knobSprite, new Color(1f, 1f, 1f, 0.22f), Image.Type.Simple).raycastTarget = false;

            GameObject line = TacticalUIBuilder.EnsureChild(content.transform, $"GoalLine{i}");
            SetTopLeft(line, 44f, y, 792f, 18f);
            TacticalUIBuilder.EnsureText(line, "—", 13f, new Color(1f, 1f, 1f, 0.8f), TextAlignmentOptions.MidlineLeft);
        }

        GameObject launch = TacticalUIBuilder.EnsureChild(content.transform, "LaunchButton");
        SetTopLeft(launch, 24f, 736f, 812f, 64f);
        Image launchImage = TacticalUIBuilder.EnsureImage(launch, buttonSprite, TacticalUITheme.AccentRed);
        TacticalUIBuilder.EnsureButton(launch, launchImage);
        launchImage.raycastTarget = true;

        GameObject launchLabel = TacticalUIBuilder.EnsureChild(launch.transform, "Label");
        TacticalUIBuilder.Stretch(TacticalUIBuilder.RT(launchLabel), 8f);
        TextMeshProUGUI launchText = TacticalUIBuilder.EnsureText(launchLabel, "ЗАПУСТИТЬ МИССИЮ", 22f,
            Color.white, TextAlignmentOptions.Center);
        launchText.fontStyle = FontStyles.Bold;
    }

    private static void BuildStatTile(GameObject content, string name, float x, float y, string caption)
    {
        GameObject tile = TacticalUIBuilder.EnsureChild(content.transform, name);
        SetTopLeft(tile, x, y, 394f, 72f);
        TacticalUIBuilder.EnsureImage(tile, null, new Color(1f, 1f, 1f, 0.06f), Image.Type.Simple).raycastTarget = false;

        GameObject label = TacticalUIBuilder.EnsureChild(tile.transform, "Label");
        SetTopLeft(label, 12f, 10f, 370f, 14f);
        TacticalUIBuilder.EnsureText(label, caption, TacticalUITheme.FontCaption, TacticalUITheme.TextCaption, TextAlignmentOptions.MidlineLeft);

        GameObject value = TacticalUIBuilder.EnsureChild(tile.transform, "Value");
        SetTopLeft(value, 12f, 28f, 370f, 32f);
        TacticalUIBuilder.EnsureText(value, "—", 22f, Color.white, TextAlignmentOptions.MidlineLeft);
    }

    private static void BuildCard()
    {
        CacheSprites();

        GameObject root = PrefabUtility.LoadPrefabContents(CardPrefabPath);
        if (root == null)
        {
            Debug.LogError($"[MissionSelectPanelBuilder] Не найден префаб: {CardPrefabPath}");
            return;
        }

        try
        {
            Image rootImage = TacticalUIBuilder.EnsureImage(root, null, TacticalUITheme.MissionCardFill, Image.Type.Simple);
            rootImage.raycastTarget = true;
            TacticalUIBuilder.EnsureButton(root, rootImage);

            LayoutElement element = root.GetComponent<LayoutElement>();
            if (element == null) element = root.AddComponent<LayoutElement>();
            element.preferredWidth = TacticalUITheme.MissionCardWidth;
            element.preferredHeight = TacticalUITheme.MissionCardHeight;
            element.flexibleWidth = 0f;
            element.minWidth = 0f;
            element.minHeight = 0f;

            ((RectTransform)root.transform).sizeDelta = new Vector2(
                TacticalUITheme.MissionCardWidth, TacticalUITheme.MissionCardHeight);

            GameObject preview = TacticalUIBuilder.EnsureChild(root.transform, "Preview");
            RectTransform previewRT = TacticalUIBuilder.RT(preview);
            previewRT.anchorMin = new Vector2(0f, 0.5f);
            previewRT.anchorMax = new Vector2(0f, 0.5f);
            previewRT.pivot = new Vector2(0f, 0.5f);
            previewRT.anchoredPosition = new Vector2(14f, 0f);
            previewRT.sizeDelta = new Vector2(200f, 126f);

            Image previewImage = preview.GetComponent<Image>();
            if (previewImage == null) previewImage = preview.AddComponent<Image>();
            previewImage.type = Image.Type.Simple;
            previewImage.preserveAspect = false;
            previewImage.raycastTarget = false;
            preview.transform.SetAsFirstSibling();

            GameObject nameText = TacticalUIBuilder.EnsureChild(root.transform, "NameText");
            SetTopLeft(nameText, 230f, 22f, 590f, 30f);
            TextMeshProUGUI nameLabel = TacticalUIBuilder.EnsureText(nameText, "МИССИЯ", 22f,
                Color.white, TextAlignmentOptions.MidlineLeft);
            nameLabel.fontStyle = FontStyles.Bold;
            nameLabel.overflowMode = TextOverflowModes.Ellipsis;

            GameObject metaText = TacticalUIBuilder.EnsureChild(root.transform, "MetaText");
            SetTopLeft(metaText, 230f, 56f, 590f, 20f);
            TacticalUIBuilder.EnsureText(metaText, "1 цель · без лимита", 14f,
                TacticalUITheme.TextSecondary, TextAlignmentOptions.MidlineLeft);

            GameObject badge = TacticalUIBuilder.EnsureChild(root.transform, "isCompleted");
            TacticalUIBuilder.SetTopRight(TacticalUIBuilder.RT(badge), 12f, 12f, 28f, 28f);
            Image badgeImage = TacticalUIBuilder.EnsureImage(badge, toggleOnSprite, Color.white, Image.Type.Simple);
            badgeImage.raycastTarget = false;
            Transform oldCheck = badge.transform.Find("Check");
            if (oldCheck != null) Object.DestroyImmediate(oldCheck.gameObject);

            GameObject locked = TacticalUIBuilder.EnsureChild(root.transform, "LockedOverlay");
            TacticalUIBuilder.Stretch(TacticalUIBuilder.RT(locked));
            TacticalUIBuilder.EnsureImage(locked, null, new Color(0f, 0f, 0f, 0.72f), Image.Type.Simple).raycastTarget = false;

            GameObject lockIcon = TacticalUIBuilder.EnsureChild(locked.transform, "Image");
            TacticalUIBuilder.SetCenter(TacticalUIBuilder.RT(lockIcon), 40f, 48f, new Vector2(0f, 2f));
            TacticalUIBuilder.EnsureImage(lockIcon, lockSprite, Color.white, Image.Type.Simple).raycastTarget = false;

            GameObject caption = TacticalUIBuilder.EnsureChild(locked.transform, "Caption");
            TacticalUIBuilder.SetCenter(TacticalUIBuilder.RT(caption), 260f, 20f, new Vector2(0f, -42f));
            TextMeshProUGUI captionText = TacticalUIBuilder.EnsureText(caption, "ЗАБЛОКИРОВАНО", 13f,
                Color.white, TextAlignmentOptions.Center);
            captionText.fontStyle = FontStyles.Bold;

            GameObject selected = TacticalUIBuilder.EnsureChild(root.transform, "SelectedFrame");
            TacticalUIBuilder.Stretch(TacticalUIBuilder.RT(selected));
            BuildSelectedFrame(selected);
            selected.transform.SetAsLastSibling();
            selected.SetActive(false);

            NormalizeCardOrder(root);

            MissionCardView view = root.GetComponent<MissionCardView>();
            if (view != null)
            {
                SerializedObject serialized = new SerializedObject(view);
                SetRef(serialized, "previewImage", previewImage);
                SetRef(serialized, "nameLabel", nameLabel);
                SetRef(serialized, "metaText", metaText.GetComponent<TMP_Text>());
                SetRef(serialized, "lockedOverlay", locked);
                SetRef(serialized, "completedBadge", badge);
                SetRef(serialized, "selectedFrame", selected);
                SetRef(serialized, "button", root.GetComponent<Button>());
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            PrefabUtility.SaveAsPrefabAsset(root, CardPrefabPath, out bool success);
            if (success) Debug.Log($"[MissionSelectPanelBuilder] Префаб карточки обновлён: {CardPrefabPath}");
            else Debug.LogError($"[MissionSelectPanelBuilder] Не удалось сохранить префаб: {CardPrefabPath}");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    /// <summary>
    /// Порядок отрисовки карточки: превью → название → метаданные → бейдж «пройдено» →
    /// затемнение блокировки → красная рамка выбора. EnsureChild не переставляет уже
    /// существующих детей, поэтому порядок задаётся явно (иначе MetaText рисуется
    /// поверх LockedOverlay и остаётся ярким на заблокированной карточке).
    /// </summary>
    private static void NormalizeCardOrder(GameObject root)
    {
        string[] order =
        {
            "Preview", "NameText", "MetaText", "isCompleted", "LockedOverlay", "SelectedFrame"
        };

        for (int i = 0; i < order.Length; i++)
        {
            Transform child = root.transform.Find(order[i]);
            if (child != null) child.SetSiblingIndex(i);
        }
    }

    private static void BuildSelectedFrame(GameObject frame)
    {
        CreateBar(frame.transform, "Top", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 3f));
        CreateBar(frame.transform, "Bottom", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0f, 3f));
        CreateBar(frame.transform, "Left", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(3f, 0f));
        CreateBar(frame.transform, "Right", new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(3f, 0f));
    }

    private static void CreateBar(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 position, Vector2 size)
    {
        GameObject go = TacticalUIBuilder.EnsureChild(parent, name);
        RectTransform rect = TacticalUIBuilder.RT(go);
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.sizeDelta = size;
        rect.anchoredPosition = position;

        TacticalUIBuilder.EnsureImage(go, null, TacticalUITheme.AccentRed, Image.Type.Simple).raycastTarget = false;
    }

    private static void AssignReferences(GameObject root, GameObject header, GameObject details)
    {
        MissionSelectScreen screen = root.GetComponent<MissionSelectScreen>();
        if (screen == null) return;

        SerializedObject serialized = new SerializedObject(screen);
        SetRef(serialized, "detailsPanel", details.GetComponent<MissionDetailsPanel>());

        GameObject subtitle = TacticalUIBuilder.FindChild(header.transform, "Subtitle");
        SetRef(serialized, "headerSubtitleLabel", subtitle != null ? subtitle.GetComponent<TMP_Text>() : null);

        GameObject score = TacticalUIBuilder.FindChild(header.transform, "TotalScore");
        SetRef(serialized, "totalScoreLabel", score != null ? score.GetComponent<TMP_Text>() : null);

        GameObject scroll = TacticalUIBuilder.FindChild(root.transform, "Scroll View");
        Transform content = scroll != null ? scroll.transform.Find("Viewport/Content") : null;
        SetRef(serialized, "cardsContainer", content);

        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetTopLeft(GameObject go, float x, float y, float width, float height)
    {
        RectTransform rt = TacticalUIBuilder.RT(go);
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x, -y);
        rt.sizeDelta = new Vector2(width, height);
    }

    private static void SetRef(SerializedObject serialized, string field, Object value)
    {
        SerializedProperty property = serialized.FindProperty(field);
        if (property == null)
        {
            Debug.LogError($"[MissionSelectPanelBuilder] Не найдено поле '{field}' в {serialized.targetObject.GetType().Name}");
            return;
        }

        property.objectReferenceValue = value;
    }
}