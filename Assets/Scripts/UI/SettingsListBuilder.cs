using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum SettingType
{
    Slider,
    Toggle,
    Dropdown
}

public class SettingDescriptor
{
    public string Id;
    public SettingType Type;
    public string Title;
    public float MinValue;
    public float MaxValue;
    public bool WholeNumbers;
    public string ValueFormat;
    public bool RequiresRelease;
    // true — изменение требует "тяжёлого" применения (Screen / QualitySettings /
    // runtime URP-ассет / камеры). Для настроек управления/звука/геймплея
    // достаточно ApplyInstant, иначе ApplyHeavy вызывался на каждый тик слайдера.
    public bool AppliesHeavy;
    public string[] Options;
    public System.Func<int> GetIndex;
    public System.Action<int> SetIndex;
    public System.Func<float> GetFloat;
    public System.Action<float> SetFloat;
    public System.Func<bool> GetBool;
    public System.Action<bool> SetBool;
}

/// <summary>
/// Процедурная сборка строк настроек в едином тактическом стиле главного меню:
/// TMP (LiberationSans SDF), спрайты HUF Pack, красный акцент только на опасных действиях.
/// Разметка строки: подпись (растягивается) + значение (88 px) + контрол (320 px).
/// </summary>
public class SettingsListBuilder : MonoBehaviour
{
    [SerializeField] private RectTransform content;

    [Header("Типографика")]
    [SerializeField] private TMP_FontAsset fontAsset;

    [Header("Спрайты HUF Pack")]
    [SerializeField] private Sprite sliderTrackSprite;
    [SerializeField] private Sprite sliderFillSprite;
    [SerializeField] private Sprite sliderHandleSprite;
    [SerializeField] private Sprite toggleOnSprite;
    [SerializeField] private Sprite toggleOffSprite;
    [SerializeField] private Sprite dropdownPlateSprite;
    [SerializeField] private Sprite dropdownArrowSprite;

    [Header("Метрики строки")]
    [SerializeField] private float rowHeight = TacticalUITheme.RowHeight;
    [SerializeField] private float sliderWidth = 320f;
    [SerializeField] private float sliderHeight = 24f;
    [SerializeField] private float trackHeight = 8f;
    [SerializeField] private float handleSize = 16f;
    [SerializeField] private float dropdownHeight = 40f;
    [SerializeField] private float valueWidth = 88f;
    [SerializeField] private float toggleSize = 32f;
    [SerializeField] private float controlSpacing = 16f;

    [Header("Цвета")]
    [SerializeField] private Color labelColor = new Color(1f, 1f, 1f, 0.9f);
    [SerializeField] private Color valueColor = new Color(1f, 1f, 1f, 0.62f);
    [SerializeField] private Color trackColor = new Color(0.18f, 0.19f, 0.2f, 1f);
    [SerializeField] private Color fillColor = new Color(0.72f, 0.75f, 0.78f, 1f);
    [SerializeField] private Color handleColor = new Color(0.95f, 0.95f, 0.95f, 1f);
    [SerializeField] private Color dropdownColor = new Color(0.16f, 0.17f, 0.19f, 1f);
    [SerializeField] private Color dropdownTextColor = Color.white;
    [SerializeField] private Color optionColor = new Color(0.1f, 0.11f, 0.12f, 0.98f);
    [SerializeField] private Color selectedOptionColor = new Color(0.22f, 0.23f, 0.25f, 1f);

    private string expandedId;
    private List<SettingDescriptor> lastDescriptors;

    private float EffectiveRowHeight => Mathf.Max(28f, rowHeight);
    private float EffectiveSliderHeight => sliderHeight > 0f ? sliderHeight : 24f;

    public void Build(List<SettingDescriptor> descriptors)
    {
        Clear();
        if (content == null || descriptors == null) return;

        lastDescriptors = descriptors;

        if (!string.IsNullOrEmpty(expandedId) && !HasDescriptor(descriptors, expandedId))
            expandedId = null;

        foreach (SettingDescriptor d in descriptors)
        {
            if (d == null) continue;

            GameObject row;
            if (d.Type == SettingType.Toggle) row = CreateToggleRow(d);
            else if (d.Type == SettingType.Dropdown) row = CreateDropdownRow(d);
            else row = CreateSliderRow(d);

            RectTransform rt = (RectTransform)row.transform;
            rt.SetParent(content, false);

            // VerticalLayoutGroup у Content не управляет высотой детей (childControlHeight = false),
            // поэтому высоту строки задаём явно.
            SetHeight(rt, EffectiveRowHeight);

            LayoutElement le = row.GetComponent<LayoutElement>();
            if (le == null) le = row.AddComponent<LayoutElement>();
            le.preferredHeight = EffectiveRowHeight;
            le.flexibleWidth = 1f;

            if (d.Type == SettingType.Dropdown && d.Id == expandedId) AddOptionRows(d);
        }

        // Кнопки строк (дропдауны и их опции) создаются здесь — подписываем их на клик.
        UISoundService.Attach(content.gameObject);
    }

    public void Clear()
    {
        if (content == null) return;

        // Destroy откладывается до конца кадра, поэтому строки сначала отвязываем
        // от Content: иначе при повторной сборке в том же кадре (сброс настроек,
        // выбор пункта дропдауна) в контейнере остаются дубликаты.
        for (int i = content.childCount - 1; i >= 0; i--)
        {
            Transform child = content.GetChild(i);
            child.SetParent(null, false);
            Destroy(child.gameObject);
        }
    }

    // ---------------------------------------------------------------- строки

    private GameObject CreateSliderRow(SettingDescriptor d)
    {
        GameObject row = CreateRow(d.Id + "_Row");

        TextMeshProUGUI label = CreateLabel(row.transform, d.Title, TacticalUITheme.FontLabel, labelColor);
        CreateFlexibleLabelLayout(label, 220f);

        TextMeshProUGUI value = CreateLabel(row.transform, Format(d, d.GetFloat != null ? d.GetFloat() : 0f), TacticalUITheme.FontValue, valueColor);
        value.alignment = TextAlignmentOptions.MidlineRight;
        SetHeight((RectTransform)value.transform, EffectiveRowHeight);
        LayoutElement valueLE = value.gameObject.AddComponent<LayoutElement>();
        valueLE.preferredWidth = valueWidth;
        valueLE.minWidth = valueWidth;

        GameObject slider = CreateSliderControl(row.transform, d, label, value);
        LayoutElement sliderLE = slider.AddComponent<LayoutElement>();
        sliderLE.preferredWidth = sliderWidth;
        sliderLE.preferredHeight = EffectiveSliderHeight;
        sliderLE.minWidth = sliderWidth;

        return row;
    }

    private GameObject CreateToggleRow(SettingDescriptor d)
    {
        GameObject row = CreateRow(d.Id + "_Row");

        TextMeshProUGUI label = CreateLabel(row.transform, d.Title, TacticalUITheme.FontLabel, labelColor);
        CreateFlexibleLabelLayout(label, 220f);

        GameObject toggleGO = new GameObject(d.Id + "_Toggle", typeof(RectTransform));
        toggleGO.transform.SetParent(row.transform, false);
        ((RectTransform)toggleGO.transform).sizeDelta = new Vector2(toggleSize, toggleSize);

        Image bg = toggleGO.AddComponent<Image>();
        ApplySprite(bg, toggleOffSprite, Color.white);

        GameObject checkGO = new GameObject("Checkmark", typeof(RectTransform));
        checkGO.transform.SetParent(toggleGO.transform, false);
        Stretch((RectTransform)checkGO.transform);

        Image check = checkGO.AddComponent<Image>();
        ApplySprite(check, toggleOnSprite, Color.white);
        check.raycastTarget = false;

        Toggle toggle = toggleGO.AddComponent<Toggle>();
        toggle.targetGraphic = bg;
        toggle.graphic = check;
        toggle.transition = Selectable.Transition.ColorTint;
        toggle.colors = TacticalUITheme.ButtonColorBlock();
        toggle.toggleTransition = Toggle.ToggleTransition.Fade;
        toggle.isOn = d.GetBool != null && d.GetBool();
        toggle.onValueChanged.AddListener(v =>
        {
            if (d.SetBool != null) d.SetBool(v);
            UISoundService.PlayClick();
            OnChanged(d, true);
        });

        LayoutElement toggleLE = toggleGO.AddComponent<LayoutElement>();
        toggleLE.preferredWidth = toggleSize;
        toggleLE.preferredHeight = toggleSize;
        toggleLE.minWidth = toggleSize;

        return row;
    }

    private GameObject CreateDropdownRow(SettingDescriptor d)
    {
        GameObject row = CreateRow(d.Id + "_Row");

        TextMeshProUGUI label = CreateLabel(row.transform, d.Title, TacticalUITheme.FontLabel, labelColor);
        CreateFlexibleLabelLayout(label, 220f);

        GameObject buttonObject = new GameObject(d.Id + "_Button", typeof(RectTransform));
        buttonObject.transform.SetParent(row.transform, false);
        ((RectTransform)buttonObject.transform).sizeDelta = new Vector2(sliderWidth, dropdownHeight);

        Image image = buttonObject.AddComponent<Image>();
        ApplySprite(image, dropdownPlateSprite, dropdownColor);

        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.ColorTint;
        button.colors = TacticalUITheme.ButtonColorBlock();

        GameObject textObject = new GameObject("Text", typeof(RectTransform));
        textObject.transform.SetParent(buttonObject.transform, false);
        Stretch((RectTransform)textObject.transform);

        TextMeshProUGUI value = textObject.AddComponent<TextMeshProUGUI>();
        ConfigureText(value, GetOptionLabel(d), TacticalUITheme.FontLabel, dropdownTextColor, TextAlignmentOptions.MidlineLeft);
        value.margin = new Vector4(16f, 0f, 36f, 0f);

        GameObject arrowObject = new GameObject("Arrow", typeof(RectTransform));
        arrowObject.transform.SetParent(buttonObject.transform, false);
        RectTransform arrowRT = (RectTransform)arrowObject.transform;
        arrowRT.anchorMin = new Vector2(1f, 0.5f);
        arrowRT.anchorMax = new Vector2(1f, 0.5f);
        arrowRT.pivot = new Vector2(1f, 0.5f);
        arrowRT.sizeDelta = new Vector2(16f, 10f);
        arrowRT.anchoredPosition = new Vector2(-14f, 0f);

        Image arrow = arrowObject.AddComponent<Image>();
        ApplySprite(arrow, dropdownArrowSprite, new Color(1f, 1f, 1f, 0.8f));
        arrow.raycastTarget = false;

        LayoutElement buttonLE = buttonObject.AddComponent<LayoutElement>();
        buttonLE.preferredWidth = sliderWidth;
        buttonLE.preferredHeight = dropdownHeight;
        buttonLE.minWidth = sliderWidth;

        string id = d.Id;
        button.onClick.AddListener(() => ToggleDropdown(id));

        return row;
    }

    private void AddOptionRows(SettingDescriptor d)
    {
        if (d.Options == null || d.Options.Length == 0) return;

        int current = d.GetIndex != null ? Mathf.Clamp(d.GetIndex(), 0, d.Options.Length - 1) : -1;

        for (int i = 0; i < d.Options.Length; i++)
        {
            int index = i;

            GameObject row = new GameObject(d.Id + "_Option" + i, typeof(RectTransform));
            row.transform.SetParent(content, false);

            RectTransform rowRT = (RectTransform)row.transform;
            SetHeight(rowRT, dropdownHeight);

            LayoutElement rowLE = row.AddComponent<LayoutElement>();
            rowLE.preferredHeight = dropdownHeight;
            rowLE.flexibleWidth = 1f;

            // Опции выравниваем по правому краю — под кнопкой дропдауна, а не на всю ширину строки.
            GameObject buttonObject = new GameObject("Option", typeof(RectTransform));
            buttonObject.transform.SetParent(row.transform, false);
            RectTransform buttonRT = (RectTransform)buttonObject.transform;
            buttonRT.anchorMin = new Vector2(1f, 0f);
            buttonRT.anchorMax = new Vector2(1f, 1f);
            buttonRT.pivot = new Vector2(1f, 0.5f);
            buttonRT.sizeDelta = new Vector2(sliderWidth, 0f);
            buttonRT.anchoredPosition = Vector2.zero;

            Image image = buttonObject.AddComponent<Image>();
            ApplySprite(image, dropdownPlateSprite, i == current ? selectedOptionColor : optionColor);

            Button button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;
            button.colors = TacticalUITheme.ButtonColorBlock();

            if (i == current)
            {
                GameObject accent = new GameObject("Accent", typeof(RectTransform));
                accent.transform.SetParent(buttonObject.transform, false);
                RectTransform accentRT = (RectTransform)accent.transform;
                accentRT.anchorMin = new Vector2(0f, 0f);
                accentRT.anchorMax = new Vector2(0f, 1f);
                accentRT.pivot = new Vector2(0f, 0.5f);
                accentRT.sizeDelta = new Vector2(3f, 0f);
                accentRT.anchoredPosition = Vector2.zero;

                Image accentImage = accent.AddComponent<Image>();
                accentImage.color = TacticalUITheme.AccentRed;
                accentImage.raycastTarget = false;
            }

            GameObject textObject = new GameObject("Text", typeof(RectTransform));
            textObject.transform.SetParent(buttonObject.transform, false);
            Stretch((RectTransform)textObject.transform);

            TextMeshProUGUI optionText = textObject.AddComponent<TextMeshProUGUI>();
            ConfigureText(optionText, d.Options[i], TacticalUITheme.FontValue,
                i == current ? Color.white : valueColor, TextAlignmentOptions.MidlineLeft);
            optionText.margin = new Vector4(18f, 0f, 12f, 0f);
            optionText.overflowMode = TextOverflowModes.Ellipsis;

            SettingDescriptor descriptor = d;
            button.onClick.AddListener(() =>
            {
                if (descriptor.SetIndex != null) descriptor.SetIndex(index);
                expandedId = null;
                OnChanged(descriptor, true);
                Build(lastDescriptors);
            });
        }
    }

    private void ToggleDropdown(string id)
    {
        expandedId = expandedId == id ? null : id;

        if (lastDescriptors != null) Build(lastDescriptors);
    }

    // ---------------------------------------------------------------- слайдер

    private GameObject CreateSliderControl(Transform parent, SettingDescriptor d, TMP_Text label, TMP_Text valueLabel)
    {
        float height = EffectiveSliderHeight;
        float track = Mathf.Clamp(trackHeight, 2f, height);
        float trackInset = Mathf.Max(0f, (height - track) * 0.5f);
        float handlePx = Mathf.Clamp(handleSize, 6f, height);

        GameObject root = new GameObject(d.Id + "_Slider", typeof(RectTransform));
        root.transform.SetParent(parent, false);
        RectTransform rootRT = (RectTransform)root.transform;
        rootRT.sizeDelta = new Vector2(sliderWidth, height);

        // Прозрачная зона захвата на всю высоту слайдера: иначе по тонкой дорожке
        // (8 px) тяжело попасть мышью, а клик мимо дорожки не считался бы кликом по Slider.
        GameObject hitGO = new GameObject("Hit Area", typeof(RectTransform));
        hitGO.transform.SetParent(root.transform, false);
        Image hit = hitGO.AddComponent<Image>();
        hit.color = new Color(0f, 0f, 0f, 0f);
        hit.raycastTarget = true;
        Stretch((RectTransform)hitGO.transform);

        GameObject bgGO = new GameObject("Background", typeof(RectTransform));
        bgGO.transform.SetParent(root.transform, false);
        Image bg = bgGO.AddComponent<Image>();
        ApplySprite(bg, sliderTrackSprite, trackColor);
        Stretch((RectTransform)bgGO.transform);
        RectTransform bgRT = (RectTransform)bgGO.transform;
        bgRT.offsetMin = new Vector2(0f, trackInset);
        bgRT.offsetMax = new Vector2(0f, -trackInset);

        GameObject fillArea = new GameObject("Fill Area", typeof(RectTransform));
        fillArea.transform.SetParent(root.transform, false);
        SetSliderArea((RectTransform)fillArea.transform);

        GameObject fillGO = new GameObject("Fill", typeof(RectTransform));
        fillGO.transform.SetParent(fillArea.transform, false);
        Image fill = fillGO.AddComponent<Image>();
        ApplySprite(fill, sliderFillSprite, fillColor);
        Stretch((RectTransform)fillGO.transform);
        RectTransform fillRT = (RectTransform)fillGO.transform;
        fillRT.offsetMin = new Vector2(0f, trackInset);
        fillRT.offsetMax = new Vector2(0f, -trackInset);

        GameObject handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
        handleArea.transform.SetParent(root.transform, false);
        SetSliderArea((RectTransform)handleArea.transform);

        GameObject handleGO = new GameObject("Handle", typeof(RectTransform));
        handleGO.transform.SetParent(handleArea.transform, false);
        Image handle = handleGO.AddComponent<Image>();
        ApplySprite(handle, sliderHandleSprite, handleColor);
        RectTransform handleRT = (RectTransform)handleGO.transform;
        handleRT.anchorMin = new Vector2(0.5f, 0.5f);
        handleRT.anchorMax = new Vector2(0.5f, 0.5f);
        handleRT.pivot = new Vector2(0.5f, 0.5f);
        // Slider растягивает handle по вертикали (anchors 0..1), поэтому высоту задаём
        // отрицательной delta: height + sizeDelta.y = размер ползунка.
        handleRT.sizeDelta = new Vector2(handlePx, -(height - handlePx));

        Slider slider = root.AddComponent<Slider>();
        slider.fillRect = (RectTransform)fillGO.transform;
        slider.handleRect = handleRT;
        slider.targetGraphic = handle;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = d.MinValue;
        slider.maxValue = d.MaxValue;
        slider.wholeNumbers = d.WholeNumbers;
        slider.value = d.GetFloat != null ? d.GetFloat() : 0f;

        label.text = d.Title;
        valueLabel.text = Format(d, slider.value);

        slider.onValueChanged.AddListener(v =>
        {
            if (d.SetFloat != null) d.SetFloat(v);
            valueLabel.text = Format(d, v);
            OnChanged(d, false);
        });

        SliderReleaseNotifier notifier = root.AddComponent<SliderReleaseNotifier>();
        notifier.OnRelease = () =>
        {
            UISoundService.PlayClick();
            OnChanged(d, true);
        };

        return root;
    }

    // ---------------------------------------------------------------- утилиты разметки

    private GameObject CreateRow(string name)
    {
        GameObject row = new GameObject(name, typeof(RectTransform));
        HorizontalLayoutGroup hlg = row.AddComponent<HorizontalLayoutGroup>();
        hlg.padding = new RectOffset(0, 0, 0, 0);
        hlg.spacing = controlSpacing;
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.childControlWidth = true;
        hlg.childControlHeight = false;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;
        return row;
    }

    private static void CreateFlexibleLabelLayout(TextMeshProUGUI label, float minWidth)
    {
        SetHeight((RectTransform)label.transform, TacticalUITheme.RowHeight);
        LayoutElement le = label.gameObject.AddComponent<LayoutElement>();
        le.flexibleWidth = 1f;
        le.minWidth = minWidth;
    }

    private TextMeshProUGUI CreateLabel(Transform parent, string text, float size, Color color)
    {
        GameObject go = new GameObject("Label", typeof(RectTransform));
        go.transform.SetParent(parent, false);

        TextMeshProUGUI label = go.AddComponent<TextMeshProUGUI>();
        ConfigureText(label, text, size, color, TextAlignmentOptions.MidlineLeft);
        return label;
    }

    private void ConfigureText(TMP_Text text, string value, float size, Color color, TextAlignmentOptions alignment)
    {
        text.font = GetFont();
        text.text = value;
        text.fontSize = size;
        text.color = color;
        text.alignment = alignment;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Ellipsis;
    }

    private TMP_FontAsset GetFont()
    {
        if (fontAsset != null) return fontAsset;
        return TMP_Settings.defaultFontAsset;
    }

    private static void ApplySprite(Image image, Sprite sprite, Color color)
    {
        image.sprite = sprite;
        image.type = sprite != null ? Image.Type.Sliced : Image.Type.Simple;
        image.color = color;
    }

    private static bool HasDescriptor(List<SettingDescriptor> descriptors, string id)
    {
        for (int i = 0; i < descriptors.Count; i++)
        {
            if (descriptors[i] != null && descriptors[i].Id == id) return true;
        }

        return false;
    }

    private static string GetOptionLabel(SettingDescriptor d)
    {
        if (d.Options == null || d.Options.Length == 0) return "—";

        int index = d.GetIndex != null ? Mathf.Clamp(d.GetIndex(), 0, d.Options.Length - 1) : 0;
        return d.Options[index];
    }

    private void OnChanged(SettingDescriptor descriptor, bool released)
    {
        SettingsService service = SettingsService.Instance;
        if (service == null) return;

        service.ApplyInstant();

        // ApplyHeavy запускаем только для дескрипторов, помеченных AppliesHeavy,
        // и (если требуется) только по отпусканию ползунка.
        if (descriptor == null)
            service.ApplyHeavy();
        else if (descriptor.AppliesHeavy && (!descriptor.RequiresRelease || released))
            service.ApplyHeavy();

        service.MarkDirty();
    }

    private static string Format(SettingDescriptor d, float value)
    {
        string fmt = string.IsNullOrEmpty(d.ValueFormat) ? (d.WholeNumbers ? "F0" : "F2") : d.ValueFormat;
        return value.ToString(fmt, CultureInfo.InvariantCulture);
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static void SetHeight(RectTransform rt, float height)
    {
        Vector2 size = rt.sizeDelta;
        rt.sizeDelta = new Vector2(size.x, height);
    }

    private static void SetSliderArea(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = new Vector2(10f, 0f);
        rt.offsetMax = new Vector2(-10f, 0f);
    }
}
