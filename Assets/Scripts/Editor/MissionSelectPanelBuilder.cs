using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class MissionSelectPanelBuilder
{
    private const string PanelPrefabPath = "Assets/Prefabs/UI/MissionSelectPanel.prefab";
    private const string CardPrefabPath = "Assets/Prefabs/UI/MissionCard.prefab";
    private const int UiLayer = 5;

    private static Sprite plateSprite;
    private static Sprite buttonSprite;
    private static Image.Type buttonImageType = Image.Type.Simple;
    private static Sprite tileSprite;
    private static Sprite dotSprite;
    private static Sprite checkSprite;
    private static TMP_FontAsset fontAsset;

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

    private static void BuildPanel()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PanelPrefabPath);
        if (root == null)
        {
            Debug.LogError($"[MissionSelectPanelBuilder] Не найден префаб: {PanelPrefabPath}");
            return;
        }

        try
        {
            Image rootImage = root.GetComponent<Image>();
            plateSprite = rootImage != null ? rootImage.sprite : null;
            tileSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            dotSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            checkSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Checkmark.psd");
            fontAsset = ResolveFont(root.transform);

            Button backButton = FindButton(root.transform, "BackButton");
            if (backButton == null)
            {
                Debug.LogError("[MissionSelectPanelBuilder] В префабе не найдена кнопка BackButton");
                return;
            }

            Image backImage = backButton.GetComponent<Image>();
            buttonSprite = backImage != null ? backImage.sprite : null;
            buttonImageType = backImage != null ? backImage.type : Image.Type.Simple;

            Transform oldHeader = root.transform.Find("Header");
            if (oldHeader != null)
            {
                if (backButton.transform.IsChildOf(oldHeader))
                    backButton.transform.SetParent(root.transform, false);

                Object.DestroyImmediate(oldHeader.gameObject);
            }

            Transform oldDetails = root.transform.Find("MissionDetailsPanel");
            if (oldDetails != null)
                Object.DestroyImmediate(oldDetails.gameObject);

            RectTransform scrollView = root.transform.Find("Scroll View") as RectTransform;
            if (scrollView != null)
            {
                scrollView.anchorMin = Vector2.zero;
                scrollView.anchorMax = Vector2.one;
                scrollView.offsetMin = new Vector2(96f, 120f);
                scrollView.offsetMax = new Vector2(-750f, -240f);
            }

            GameObject header = BuildHeader(root.transform, backButton);
            MissionDetailsPanel details = BuildDetails(root.transform);

            MissionSelectScreen screen = root.GetComponent<MissionSelectScreen>();
            if (screen != null)
            {
                SerializedObject serialized = new SerializedObject(screen);
                SetRef(serialized, "detailsPanel", details);
                SetRef(serialized, "headerSubtitleLabel", FindText(header.transform, "Subtitle"));
                SetRef(serialized, "totalScoreLabel", FindText(header.transform, "TotalScore"));
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            PrefabUtility.SaveAsPrefabAsset(root, PanelPrefabPath, out bool success);

            if (success) Debug.Log($"[MissionSelectPanelBuilder] Префаб панели обновлён: {PanelPrefabPath}");
            else Debug.LogError($"[MissionSelectPanelBuilder] Не удалось сохранить префаб: {PanelPrefabPath}");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static GameObject BuildHeader(Transform parent, Button backButton)
    {
        GameObject header = CreateRect("Header", parent);
        RectTransform headerRect = (RectTransform)header.transform;
        headerRect.anchorMin = new Vector2(0f, 1f);
        headerRect.anchorMax = new Vector2(1f, 1f);
        headerRect.pivot = new Vector2(0.5f, 1f);
        headerRect.sizeDelta = new Vector2(-192f, 100f);
        headerRect.anchoredPosition = new Vector2(0f, -40f);

        TextMeshProUGUI title = CreateText(Block(header.transform, "Title", 0f, 8f, 900f, 46f), "ВЫБОР МИССИИ", 30f, new Color(0.12f, 0.13f, 0.16f, 1f), TextAlignmentOptions.BottomLeft);
        title.fontStyle = FontStyles.Bold;

        CreateText(Block(header.transform, "Subtitle", 0f, 58f, 900f, 18f), "ОПЕРАТИВНЫЙ ТЕРМИНАЛ", 13f, new Color(0.18f, 0.19f, 0.21f, 0.9f), TextAlignmentOptions.BottomLeft);

        RectTransform scoreRect = BlockRight(header.transform, "TotalScore", 176f, 28f, 320f, 26f);
        CreateText(scoreRect, "ОБЩИЙ СЧЁТ: 0", 15f, new Color(0.17f, 0.18f, 0.2f, 0.95f), TextAlignmentOptions.MidlineRight);

        RectTransform ruleRect = Block(header.transform, "Rule", 0f, 98f, 0f, 2f);
        ruleRect.anchorMin = new Vector2(0f, 0f);
        ruleRect.anchorMax = new Vector2(1f, 0f);
        ruleRect.pivot = new Vector2(0.5f, 0f);
        ruleRect.sizeDelta = new Vector2(0f, 2f);
        ruleRect.anchoredPosition = Vector2.zero;

        Image ruleImage = AddImage(ruleRect, new Color(0f, 0f, 0f, 0.22f), tileSprite, Image.Type.Sliced);

        backButton.transform.SetParent(header.transform, false);
        RectTransform backRect = (RectTransform)backButton.transform;
        backRect.anchorMin = new Vector2(1f, 1f);
        backRect.anchorMax = new Vector2(1f, 1f);
        backRect.pivot = new Vector2(1f, 1f);
        backRect.sizeDelta = new Vector2(160f, 60f);
        backRect.anchoredPosition = new Vector2(0f, -26f);

        TextMeshProUGUI backLabel = backButton.GetComponentInChildren<TextMeshProUGUI>(true);
        if (backLabel != null)
        {
            backLabel.text = "НАЗАД";
            backLabel.fontSize = 22f;
            backLabel.color = Color.white;
        }

        return header;
    }

    private static MissionDetailsPanel BuildDetails(Transform parent)
    {
        GameObject details = CreateRect("MissionDetailsPanel", parent);
        RectTransform detailsRect = (RectTransform)details.transform;
        detailsRect.anchorMin = Vector2.zero;
        detailsRect.anchorMax = Vector2.one;
        detailsRect.offsetMin = new Vector2(1206f, 120f);
        detailsRect.offsetMax = new Vector2(-96f, -240f);

        Image plate = AddImage(detailsRect, new Color(0.05f, 0.06f, 0.08f, 0.82f), plateSprite, Image.Type.Sliced);
        plate.raycastTarget = true;

        CanvasGroup group = details.AddComponent<CanvasGroup>();
        group.alpha = 1f;
        group.interactable = true;
        group.blocksRaycasts = true;

        MissionDetailsPanel panel = details.AddComponent<MissionDetailsPanel>();

        GameObject emptyState = CreateRect("EmptyState", details.transform);
        Stretch((RectTransform)emptyState.transform, 24f);
        TextMeshProUGUI emptyLabel = CreateText((RectTransform)emptyState.transform, "ВЫБЕРИТЕ ОПЕРАЦИЮ\nСПИСОК МИССИЙ СЛЕВА", 16f, new Color(1f, 1f, 1f, 0.45f), TextAlignmentOptions.Center);
        emptyLabel.fontStyle = FontStyles.Bold;

        Transform content = CreateRect("Content", details.transform).transform;
        Stretch((RectTransform)content, 0f);

        RectTransform bannerRect = Block(content, "Banner", 20f, 20f, 578f, 110f);
        Image banner = AddImage(bannerRect, new Color(0.14f, 0.15f, 0.17f, 1f), plateSprite, Image.Type.Sliced);
        banner.raycastTarget = false;

        TextMeshProUGUI bannerCode = CreateText(Block(content, "BannerCode", 32f, 30f, 554f, 16f), "ID: MISSION", 11f, new Color(1f, 1f, 1f, 0.55f), TextAlignmentOptions.MidlineLeft);

        RectTransform statusIconRect = Block(content, "StatusIcon", 20f, 146f, 20f, 20f);
        Image statusIcon = AddImage(statusIconRect, new Color(0.92f, 0.72f, 0.25f, 1f), dotSprite, Image.Type.Simple);
        statusIcon.raycastTarget = false;

        TextMeshProUGUI statusLabel = CreateText(Block(content, "StatusLabel", 48f, 142f, 270f, 24f), "НЕ ВЫПОЛНЕНО", 14f, Color.white, TextAlignmentOptions.MidlineLeft);
        statusLabel.fontStyle = FontStyles.Bold;

        TextMeshProUGUI rewardLabel = CreateText(BlockRight(content, "RewardLabel", 20f, 142f, 240f, 24f), "НАГРАДА +100", 12f, new Color(1f, 1f, 1f, 0.7f), TextAlignmentOptions.MidlineRight);

        TextMeshProUGUI titleLabel = CreateText(Block(content, "Title", 20f, 174f, 578f, 62f), "НАЗВАНИЕ", 26f, Color.white, TextAlignmentOptions.TopLeft);
        titleLabel.fontStyle = FontStyles.Bold;
        titleLabel.overflowMode = TextOverflowModes.Ellipsis;

        RectTransform ruleRect = Block(content, "Rule", 20f, 242f, 578f, 2f);
        AddImage(ruleRect, new Color(1f, 1f, 1f, 0.16f), tileSprite, Image.Type.Sliced).raycastTarget = false;

        CreateText(Block(content, "DescriptionLabel", 20f, 252f, 578f, 16f), "ОПИСАНИЕ", 11f, new Color(1f, 1f, 1f, 0.55f), TextAlignmentOptions.MidlineLeft);

        TextMeshProUGUI descriptionLabel = CreateText(Block(content, "Description", 20f, 272f, 578f, 100f), "Описание операции не задано.", 15f, new Color(1f, 1f, 1f, 0.85f), TextAlignmentOptions.TopLeft);
        descriptionLabel.overflowMode = TextOverflowModes.Ellipsis;

        CreateText(Block(content, "StatsLabel", 20f, 380f, 578f, 16f), "СТАТИСТИКА", 11f, new Color(1f, 1f, 1f, 0.55f), TextAlignmentOptions.MidlineLeft);

        TMP_Text timeValue = BuildStatTile(content, "TimeTile", 20f, 400f, "ЛУЧШЕЕ ВРЕМЯ");
        TMP_Text scoreValue = BuildStatTile(content, "ScoreTile", 315f, 400f, "СЧЁТ");
        TMP_Text completionsValue = BuildStatTile(content, "CompletionsTile", 20f, 468f, "ПРОХОЖДЕНИЙ");
        TMP_Text targetsValue = BuildStatTile(content, "TargetsTile", 315f, 468f, "ЦЕЛЕЙ");

        TextMeshProUGUI noteText = CreateText(Block(content, "Note", 20f, 536f, 578f, 16f), "Результат появится после первого прохождения", 11f, new Color(1f, 1f, 1f, 0.5f), TextAlignmentOptions.MidlineLeft);

        CreateText(Block(content, "GoalsLabel", 20f, 558f, 578f, 16f), "ЦЕЛИ ОПЕРАЦИИ", 11f, new Color(1f, 1f, 1f, 0.55f), TextAlignmentOptions.MidlineLeft);

        TMP_Text[] goalLines = new TMP_Text[3];
        Image[] goalMarkers = new Image[3];

        for (int i = 0; i < 3; i++)
        {
            float row = 578f + i * 20f;

            RectTransform markerRect = Block(content, $"GoalMarker{i}", 22f, row + 4f, 10f, 10f);
            goalMarkers[i] = AddImage(markerRect, new Color(1f, 1f, 1f, 0.22f), dotSprite, Image.Type.Simple);
            goalMarkers[i].raycastTarget = false;

            goalLines[i] = CreateText(Block(content, $"GoalLine{i}", 40f, row, 558f, 18f), "—", 13f, new Color(1f, 1f, 1f, 0.8f), TextAlignmentOptions.MidlineLeft);
        }

        TextMeshProUGUI techLine = CreateText(Block(content, "TechLine", 32f, 108f, 554f, 16f), "ТЕХНИКА: — · БОРТОВ: 0", 11f, new Color(1f, 1f, 1f, 0.6f), TextAlignmentOptions.MidlineRight);

        RectTransform launchRect = Block(content, "LaunchButton", 20f, 640f, 578f, 64f);
        Image launchImage = AddImage(launchRect, new Color(0.8627452f, 0.1921569f, 0.1960784f, 1f), buttonSprite, buttonImageType);
        launchImage.raycastTarget = true;

        Button launchButton = launchRect.gameObject.AddComponent<Button>();
        launchButton.targetGraphic = launchImage;
        launchButton.colors = ColorBlock.defaultColorBlock;

        TextMeshProUGUI launchLabel = CreateStretchedText(launchRect, "Label", "ЗАПУСТИТЬ МИССИЮ", 22f, Color.white, TextAlignmentOptions.Center, 8f);
        launchLabel.fontStyle = FontStyles.Bold;

        SerializedObject serialized = new SerializedObject(panel);
        SetRef(serialized, "group", group);
        SetRef(serialized, "emptyState", emptyState);
        SetRef(serialized, "contentRoot", content.gameObject);
        SetRef(serialized, "bannerImage", banner);
        SetRef(serialized, "bannerCodeText", bannerCode);
        SetRef(serialized, "statusIcon", statusIcon);
        SetRef(serialized, "statusLabel", statusLabel);
        SetRef(serialized, "rewardLabel", rewardLabel);
        SetRef(serialized, "titleLabel", titleLabel);
        SetRef(serialized, "descriptionLabel", descriptionLabel);
        SetRef(serialized, "timeValue", timeValue);
        SetRef(serialized, "scoreValue", scoreValue);
        SetRef(serialized, "completionsValue", completionsValue);
        SetRef(serialized, "targetsValue", targetsValue);
        SetRef(serialized, "noteText", noteText);
        SetRef(serialized, "goalLines", goalLines);
        SetRef(serialized, "goalMarkers", goalMarkers);
        SetRef(serialized, "techLine", techLine);
        SetRef(serialized, "launchButton", launchButton);
        SetRef(serialized, "launchLabel", launchLabel);
        serialized.ApplyModifiedPropertiesWithoutUndo();

        return panel;
    }

    private static TMP_Text BuildStatTile(Transform parent, string name, float x, float y, string caption)
    {
        RectTransform tile = Block(parent, name, x, y, 283f, 60f);
        AddImage(tile, new Color(1f, 1f, 1f, 0.07f), tileSprite, Image.Type.Sliced).raycastTarget = false;

        CreateText(Block(tile, "Label", 10f, 8f, 263f, 14f), caption, 11f, new Color(1f, 1f, 1f, 0.55f), TextAlignmentOptions.MidlineLeft);

        return CreateText(Block(tile, "Value", 10f, 22f, 263f, 30f), "—", 22f, Color.white, TextAlignmentOptions.MidlineLeft);
    }

    private static void BuildCard()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(CardPrefabPath);
        if (root == null)
        {
            Debug.LogError($"[MissionSelectPanelBuilder] Не найден префаб: {CardPrefabPath}");
            return;
        }

        try
        {
            fontAsset = ResolveFont(root.transform);

            GameObject preview = FindChild(root.transform, "Preview");
            if (preview != null)
            {
                RectTransform previewRect = (RectTransform)preview.transform;
                previewRect.offsetMin = new Vector2(10f, 58f);
                previewRect.offsetMax = new Vector2(-10f, -10f);
            }

            GameObject nameText = FindChild(root.transform, "NameText");
            if (nameText != null)
            {
                RectTransform nameRect = (RectTransform)nameText.transform;
                nameRect.anchorMin = new Vector2(0f, 0f);
                nameRect.anchorMax = new Vector2(1f, 0f);
                nameRect.pivot = new Vector2(0.5f, 0f);
                nameRect.sizeDelta = new Vector2(-16f, 26f);
                nameRect.anchoredPosition = new Vector2(0f, 28f);

                TextMeshProUGUI label = nameText.GetComponent<TextMeshProUGUI>();
                if (label != null)
                {
                    label.fontSize = 18f;
                    label.fontStyle = FontStyles.Bold;
                    label.alignment = TextAlignmentOptions.Midline;
                    label.color = new Color(1f, 1f, 1f, 1f);
                }
            }

            GameObject metaText = FindChild(root.transform, "MetaText");
            if (metaText == null)
            {
                RectTransform metaRect = Block(root.transform, "MetaText", 8f, 8f, 384f, 18f);
                metaRect.anchorMin = new Vector2(0f, 0f);
                metaRect.anchorMax = new Vector2(1f, 0f);
                metaRect.pivot = new Vector2(0.5f, 0f);
                metaRect.sizeDelta = new Vector2(-16f, 18f);
                metaRect.anchoredPosition = new Vector2(0f, 6f);

                TextMeshProUGUI meta = CreateText(metaRect, "1 цель · без лимита", 12f, new Color(0.19607843f, 0.19607843f, 0.19607843f, 0.75f), TextAlignmentOptions.Midline);
                metaText = meta.gameObject;
            }

            TextMeshProUGUI metaLabel = metaText.GetComponent<TextMeshProUGUI>();
            if (metaLabel != null)
                metaLabel.color = new Color(1f, 1f, 1f, 0.75f);

            GameObject badge = FindChild(root.transform, "isCompleted");
            if (badge != null)
            {
                RectTransform badgeRect = (RectTransform)badge.transform;
                badgeRect.anchorMin = new Vector2(1f, 1f);
                badgeRect.anchorMax = new Vector2(1f, 1f);
                badgeRect.pivot = new Vector2(1f, 1f);
                badgeRect.sizeDelta = new Vector2(34f, 34f);
                badgeRect.anchoredPosition = new Vector2(-8f, -8f);

                Image badgeImage = badge.GetComponent<Image>();
                if (badgeImage != null)
                {
                    badgeImage.sprite = dotSprite;
                    badgeImage.type = Image.Type.Simple;
                    badgeImage.preserveAspect = true;
                    badgeImage.color = new Color(0f, 0f, 0f, 0.62f);
                    badgeImage.raycastTarget = false;
                }

                GameObject check = FindChild(badge.transform, "Check");
                if (check == null)
                {
                    RectTransform checkRect = AddChildRect(badge.transform, "Check");
                    checkRect.anchorMin = new Vector2(0.5f, 0.5f);
                    checkRect.anchorMax = new Vector2(0.5f, 0.5f);
                    checkRect.pivot = new Vector2(0.5f, 0.5f);
                    checkRect.sizeDelta = new Vector2(22f, 22f);
                    checkRect.anchoredPosition = Vector2.zero;

                    Image checkImage = AddImage(checkRect, Color.white, checkSprite, Image.Type.Simple);
                    checkImage.preserveAspect = true;
                    checkImage.raycastTarget = false;
                }
            }

            GameObject lockedOverlay = FindChild(root.transform, "LockedOverlay");
            if (lockedOverlay != null)
            {
                RectTransform overlayRect = (RectTransform)lockedOverlay.transform;
                overlayRect.anchorMin = Vector2.zero;
                overlayRect.anchorMax = Vector2.one;
                overlayRect.offsetMin = Vector2.zero;
                overlayRect.offsetMax = Vector2.zero;

                Image scrim = lockedOverlay.GetComponent<Image>();
                if (scrim == null) scrim = lockedOverlay.AddComponent<Image>();
                scrim.sprite = null;
                scrim.color = new Color(0f, 0f, 0f, 0.62f);
                scrim.raycastTarget = false;

                GameObject lockIcon = FindChild(lockedOverlay.transform, "Image");
                if (lockIcon != null)
                {
                    RectTransform lockRect = (RectTransform)lockIcon.transform;
                    lockRect.anchorMin = new Vector2(0.5f, 0.5f);
                    lockRect.anchorMax = new Vector2(0.5f, 0.5f);
                    lockRect.pivot = new Vector2(0.5f, 0.5f);
                    lockRect.sizeDelta = new Vector2(56f, 56f);
                    lockRect.anchoredPosition = new Vector2(0f, 10f);
                }

                GameObject caption = FindChild(lockedOverlay.transform, "Caption");
                if (caption == null)
                {
                    RectTransform captionRect = AddChildRect(lockedOverlay.transform, "Caption");
                    captionRect.anchorMin = new Vector2(0.5f, 0.5f);
                    captionRect.anchorMax = new Vector2(0.5f, 0.5f);
                    captionRect.pivot = new Vector2(0.5f, 0.5f);
                    captionRect.sizeDelta = new Vector2(220f, 18f);
                    captionRect.anchoredPosition = new Vector2(0f, -44f);

                    CreateText(captionRect, "ЗАБЛОКИРОВАНО", 11f, Color.white, TextAlignmentOptions.Center).fontStyle = FontStyles.Bold;
                    caption = captionRect.gameObject;
                }
            }

            GameObject selectedFrame = FindChild(root.transform, "SelectedFrame");
            if (selectedFrame == null)
                selectedFrame = BuildSelectedFrame(root.transform);

            selectedFrame.transform.SetAsLastSibling();
            selectedFrame.SetActive(false);

            MissionCardView view = root.GetComponent<MissionCardView>();
            if (view != null)
            {
                SerializedObject serialized = new SerializedObject(view);
                SetRef(serialized, "previewImage", preview != null ? preview.GetComponent<Image>() : null);
                SetRef(serialized, "nameLabel", FindText(root.transform, "NameText"));
                SetRef(serialized, "metaText", FindText(root.transform, "MetaText"));
                SetRef(serialized, "lockedOverlay", lockedOverlay);
                SetRef(serialized, "completedBadge", badge);
                SetRef(serialized, "selectedFrame", selectedFrame);
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

    private static GameObject BuildSelectedFrame(Transform parent)
    {
        GameObject frame = CreateRect("SelectedFrame", parent);
        Stretch((RectTransform)frame.transform, 0f);

        Color color = new Color(0.8627452f, 0.1921569f, 0.1960784f, 1f);

        CreateBar(frame.transform, "Top", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 0f), new Vector2(0f, 3f), color);
        CreateBar(frame.transform, "Bottom", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 0f), new Vector2(0f, 3f), color);
        CreateBar(frame.transform, "Left", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(3f, 0f), color);
        CreateBar(frame.transform, "Right", new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(3f, 0f), color);

        return frame;
    }

    private static void CreateBar(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 position, Vector2 size, Color color)
    {
        RectTransform rect = AddChildRect(parent, name);
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.sizeDelta = size;
        rect.anchoredPosition = position;

        AddImage(rect, color, null, Image.Type.Simple).raycastTarget = false;
    }

    private static RectTransform Block(Transform parent, string name, float x, float y, float width, float height)
    {
        RectTransform rect = AddChildRect(parent, name);
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
        return rect;
    }

    private static RectTransform BlockRight(Transform parent, string name, float right, float top, float width, float height)
    {
        RectTransform rect = AddChildRect(parent, name);
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = new Vector2(-right, -top);
        rect.sizeDelta = new Vector2(width, height);
        return rect;
    }

    private static TextMeshProUGUI CreateText(RectTransform rect, string text, float size, Color color, TextAlignmentOptions alignment)
    {
        TextMeshProUGUI label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        EnsureCanvasRenderer(rect.gameObject);

        label.font = fontAsset;
        label.text = text;
        label.fontSize = size;
        label.color = color;
        label.alignment = alignment;
        label.raycastTarget = false;
        label.margin = Vector4.zero;

        return label;
    }

    private static TextMeshProUGUI CreateStretchedText(RectTransform parent, string name, string text, float size, Color color, TextAlignmentOptions alignment, float padding)
    {
        RectTransform rect = AddChildRect(parent, name);
        Stretch(rect, padding);

        return CreateText(rect, text, size, color, alignment);
    }

    private static Image AddImage(RectTransform rect, Color color, Sprite sprite, Image.Type type)
    {
        Image image = rect.gameObject.AddComponent<Image>();
        EnsureCanvasRenderer(rect.gameObject);

        image.sprite = sprite;
        image.type = type;
        image.color = color;

        return image;
    }

    private static GameObject CreateRect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.layer = UiLayer;

        if (parent != null)
            go.transform.SetParent(parent, false);

        return go;
    }

    private static RectTransform AddChildRect(Transform parent, string name)
    {
        return (RectTransform)CreateRect(name, parent).transform;
    }

    private static void Stretch(RectTransform rect, float padding)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(padding, padding);
        rect.offsetMax = new Vector2(-padding, -padding);
    }

    private static void EnsureCanvasRenderer(GameObject go)
    {
        if (go.GetComponent<CanvasRenderer>() == null)
            go.AddComponent<CanvasRenderer>();
    }

    private static GameObject FindChild(Transform parent, string name)
    {
        Transform[] children = parent.GetComponentsInChildren<Transform>(true);

        foreach (Transform child in children)
        {
            if (child.name == name)
                return child.gameObject;
        }

        return null;
    }

    private static Button FindButton(Transform parent, string name)
    {
        Button[] buttons = parent.GetComponentsInChildren<Button>(true);

        foreach (Button button in buttons)
        {
            if (button.name == name)
                return button;
        }

        return null;
    }

    private static TextMeshProUGUI FindText(Transform parent, string name)
    {
        TextMeshProUGUI[] texts = parent.GetComponentsInChildren<TextMeshProUGUI>(true);

        foreach (TextMeshProUGUI text in texts)
        {
            if (text.name == name)
                return text;
        }

        return null;
    }

    private static TMP_FontAsset ResolveFont(Transform root)
    {
        TextMeshProUGUI[] texts = root.GetComponentsInChildren<TextMeshProUGUI>(true);

        foreach (TextMeshProUGUI text in texts)
        {
            if (text.font != null)
                return text.font;
        }

        if (TMP_Settings.defaultFontAsset != null)
            return TMP_Settings.defaultFontAsset;

        return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/UnityTechnologies/ParticlePack/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
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

    private static void SetRef(SerializedObject serialized, string field, Object[] values)
    {
        SerializedProperty property = serialized.FindProperty(field);

        if (property == null)
        {
            Debug.LogError($"[MissionSelectPanelBuilder] Не найдено поле '{field}' в {serialized.targetObject.GetType().Name}");
            return;
        }

        property.arraySize = values.Length;

        for (int i = 0; i < values.Length; i++)
            property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    }
}
