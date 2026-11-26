using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// HUD магазина бункера в стилистике ComputerOS: тёмные плашки, красный акцент,
/// LiberationSans SDF, подписи капсом. Собирается в рантайме на отдельном
/// Canvas (ScreenSpaceOverlay, order 200), чтобы не трогать иерархию InteractionCanvas.
/// Виден только пока открыт магазин (Show/Hide из DroneShopController).
/// </summary>
public class DroneShopUI : MonoBehaviour
{
    [SerializeField] private TMP_FontAsset fontAsset;

    /// <summary>Кнопка «◄» / клавиша A / стрелка влево.</summary>
    public System.Action PrevRequested;

    /// <summary>Кнопка «►» / клавиша D / стрелка вправо.</summary>
    public System.Action NextRequested;

    /// <summary>Кнопка «Сброс ракурса» / клавиша R.</summary>
    public System.Action ResetRequested;

    /// <summary>Кнопка «Назад» / клавиша Esc.</summary>
    public System.Action CloseRequested;

    private CanvasGroup group;
    private TMP_Text counterLabel;
    private TMP_Text navCounterLabel;
    private TMP_Text nameLabel;
    private TMP_Text roleLabel;
    private TMP_Text specsLabel;
    private TMP_Text theoryLabel;

    private void Awake()
    {
        if (fontAsset == null) fontAsset = TMP_Settings.defaultFontAsset;
        Build();
        Hide();
    }

    // ------------------------------------------------------------ публичное API

    public void ShowEntry(DroneShopEntry entry, int index, int total)
    {
        if (entry == null) return;

        if (nameLabel != null) nameLabel.text = entry.displayName.ToUpperInvariant();
        if (roleLabel != null) roleLabel.text = string.IsNullOrEmpty(entry.role) ? string.Empty : entry.role.ToUpperInvariant();
        if (specsLabel != null) specsLabel.text = BuildSpecsText(entry.specs);
        if (theoryLabel != null) theoryLabel.text = entry.theory;
        if (counterLabel != null) counterLabel.text = (index + 1) + " / " + Mathf.Max(1, total);
        if (navCounterLabel != null) navCounterLabel.text = (index + 1) + " / " + Mathf.Max(1, total);
    }

    public void Show()
    {
        if (group == null) return;
        group.alpha = 1f;
        group.interactable = true;
        group.blocksRaycasts = true;
    }

    public void Hide()
    {
        if (group == null) return;
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;
    }

    private static string BuildSpecsText(string[] specs)
    {
        if (specs == null || specs.Length == 0) return string.Empty;

        System.Text.StringBuilder builder = new System.Text.StringBuilder();
        for (int i = 0; i < specs.Length; i++)
        {
            if (string.IsNullOrEmpty(specs[i])) continue;
            if (builder.Length > 0) builder.Append('\n');
            builder.Append("•  ").Append(specs[i]);
        }

        return builder.ToString();
    }

    // ------------------------------------------------------------ сборка разметки

    private void Build()
    {
        GameObject canvasObject = new GameObject("DroneShopCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
        canvasObject.layer = 5;
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;
        canvas.pixelPerfect = false;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        group = canvasObject.GetComponent<CanvasGroup>();

        RectTransform root = (RectTransform)canvasObject.transform;
        BuildTopBar(root);
        BuildInfoPanel(root);
        BuildBottomBar(root);

        UISoundService.Attach(canvasObject);
    }

    private void BuildTopBar(RectTransform root)
    {
        Image bar = CreatePlate(root, "TopBar", TacticalUITheme.PanelFill,
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 76f));

        TMP_Text title = CreateLabel(bar.transform, "Title", "МАГАЗИН ДРОНОВ  ·  ТЕРМИНАЛ СНАБЖЕНИЯ БУНКЕРА", 20f, TacticalUITheme.TextPrimary, TextAlignmentOptions.MidlineLeft);
        Place(rt(title), new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(44f, 0f), new Vector2(1100f, 0f));

        counterLabel = CreateLabel(bar.transform, "Counter", "1 / 1", 20f, TacticalUITheme.AccentRed, TextAlignmentOptions.MidlineRight);
        Place(rt(counterLabel), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(-44f, 0f), new Vector2(260f, 0f));

        CreatePlate(bar.transform, "AccentRule", TacticalUITheme.AccentRed,
            new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0f, 4f));
    }

    private void BuildInfoPanel(RectTransform root)
    {
        Image panel = CreatePlate(root, "InfoPanel", TacticalUITheme.PanelFillSoft,
            new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-40f, -100f), new Vector2(700f, 820f));

        Image accent = CreatePlate(panel.transform, "AccentRule", TacticalUITheme.AccentRed,
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 4f));
        accent.raycastTarget = false;

        nameLabel = CreateLabel(panel.transform, "DroneName", "ДРОН", 29f, TacticalUITheme.TextPrimary, TextAlignmentOptions.MidlineLeft);
        PlaceTopStretch(rt(nameLabel), 28f, 30f, 28f, 44f);

        roleLabel = CreateLabel(panel.transform, "DroneRole", string.Empty, 15f, TacticalUITheme.TextSecondary, TextAlignmentOptions.MidlineLeft);
        PlaceTopStretch(rt(roleLabel), 28f, 82f, 28f, 26f);

        CreateDivider(panel.transform, "Divider_1", 120f);

        TMP_Text specsHeader = CreateLabel(panel.transform, "SpecsHeader", "ТАКТИКО-ТЕХНИЧЕСКИЕ ХАРАКТЕРИСТИКИ", 12f, TacticalUITheme.TextCaption, TextAlignmentOptions.MidlineLeft);
        PlaceTopStretch(rt(specsHeader), 28f, 136f, 28f, 26f);

        specsLabel = CreateLabel(panel.transform, "Specs", string.Empty, 16f, TacticalUITheme.TextPrimary, TextAlignmentOptions.TopLeft);
        PlaceTopStretch(rt(specsLabel), 28f, 168f, 28f, 230f);

        CreateDivider(panel.transform, "Divider_2", 412f);

        TMP_Text theoryHeader = CreateLabel(panel.transform, "TheoryHeader", "ТЕОРИЯ И ПРАКТИКА ПРИМЕНЕНИЯ", 12f, TacticalUITheme.AccentRed, TextAlignmentOptions.MidlineLeft);
        PlaceTopStretch(rt(theoryHeader), 28f, 428f, 28f, 26f);

        theoryLabel = CreateLabel(panel.transform, "Theory", string.Empty, 14f, TacticalUITheme.TextSecondary, TextAlignmentOptions.TopLeft);
        PlaceTopStretch(rt(theoryLabel), 28f, 462f, 28f, 330f);
    }

    private void BuildBottomBar(RectTransform root)
    {
        Image bar = CreatePlate(root, "BottomBar", TacticalUITheme.PanelFill,
            new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0f, 168f));

        CreatePlate(bar.transform, "TopRule", TacticalUITheme.Divider,
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 2f));

        CreateButton(bar.transform, "Btn_Prev", "<", new Vector2(-180f, 96f), new Vector2(96f, 60f),
            TacticalUITheme.ControlPlate, TacticalUITheme.TextPrimary, 34f, () => Raise(PrevRequested));

        CreateButton(bar.transform, "Btn_Next", ">", new Vector2(180f, 96f), new Vector2(96f, 60f),
            TacticalUITheme.ControlPlate, TacticalUITheme.TextPrimary, 34f, () => Raise(NextRequested));

        navCounterLabel = CreateLabel(bar.transform, "NavCounter", "1 / 1", 22f, TacticalUITheme.Steel, TextAlignmentOptions.Center);
        Place(rt(navCounterLabel), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 96f), new Vector2(140f, 60f));

        CreateButton(bar.transform, "Btn_Reset", "СБРОСИТЬ РАКУРС", new Vector2(-620f, 96f), new Vector2(250f, 60f),
            TacticalUITheme.ControlPlate, TacticalUITheme.TextPrimary, 16f, () => Raise(ResetRequested));

        CreateButton(bar.transform, "Btn_Back", "НАЗАД", new Vector2(620f, 96f), new Vector2(200f, 60f),
            TacticalUITheme.AccentRed, Color.white, 18f, () => Raise(CloseRequested));

        TMP_Text hint = CreateLabel(bar.transform, "Hint",
            "ЛКМ + мышь — вращать 360°   |   A / D или  <  /  >  — сменить дрон   |   R — сброс ракурса   |   Esc — назад",
            14f, TacticalUITheme.TextCaption, TextAlignmentOptions.Center);
        Place(rt(hint), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 26f), new Vector2(1500f, 26f));
    }

    // ------------------------------------------------------------ утилиты разметки

    private static void Raise(System.Action action)
    {
        if (action != null) action();
    }

    private static RectTransform CreateRect(string objectName, Transform parent)
    {
        GameObject go = new GameObject(objectName, typeof(RectTransform));
        go.layer = 5;

        RectTransform rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        return rect;
    }

    private static Image CreatePlate(Transform parent, string objectName, Color color, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 size)
    {
        GameObject go = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.layer = 5;

        RectTransform rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        Image image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private TMP_Text CreateLabel(Transform parent, string objectName, string text, float fontSize, Color color, TextAlignmentOptions alignment)
    {
        GameObject go = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer));
        go.layer = 5;

        RectTransform rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);

        TextMeshProUGUI label = go.AddComponent<TextMeshProUGUI>();
        label.font = fontAsset != null ? fontAsset : TMP_Settings.defaultFontAsset;
        label.text = text;
        label.fontSize = fontSize;
        label.color = color;
        label.alignment = alignment;
        label.textWrappingMode = TextWrappingModes.Normal;
        label.overflowMode = TextOverflowModes.Overflow;
        label.raycastTarget = false;
        return label;
    }

    private static void CreateDivider(Transform parent, string objectName, float top)
    {
        Image divider = CreatePlate(parent, objectName, TacticalUITheme.Divider,
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -top), new Vector2(-56f, 2f));
        divider.raycastTarget = false;
    }

    private void CreateButton(Transform parent, string objectName, string caption, Vector2 anchoredPosition, Vector2 size, Color plateColor, Color textColor, float fontSize, System.Action onClick)
    {
        GameObject go = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.layer = 5;

        RectTransform rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        Image image = go.GetComponent<Image>();
        image.color = plateColor;
        image.raycastTarget = true;

        Button button = go.GetComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.ColorTint;
        button.colors = TacticalUITheme.ButtonColorBlock();
        if (onClick != null) button.onClick.AddListener(() => onClick());

        TMP_Text label = CreateLabel(rect, "Text (TMP)", caption, fontSize, textColor, TextAlignmentOptions.Center);
        RectTransform labelRect = rt(label);
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
    }

    private static RectTransform rt(Component component)
    {
        return (RectTransform)component.transform;
    }

    private static void Place(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 size)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
    }

    private static void PlaceTopStretch(RectTransform rect, float left, float top, float right, float height)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(left, -top);
        rect.sizeDelta = new Vector2(-(left + right), height);
    }
}
