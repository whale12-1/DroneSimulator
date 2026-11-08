using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

public enum SettingType
{
    Slider,
    Toggle,
    Dropdown,
    Key
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
    public string[] Options;
    public string ActionId;
    public int BindingIndex;
    public System.Func<int> GetIndex;
    public System.Action<int> SetIndex;
    public System.Func<float> GetFloat;
    public System.Action<float> SetFloat;
    public System.Func<bool> GetBool;
    public System.Action<bool> SetBool;
}

public class SettingsListBuilder : MonoBehaviour
{
    [SerializeField] private RectTransform content;
    [SerializeField] private float rowHeight = 56f;
    [SerializeField] private float sliderWidth = 260f;
    [Tooltip("Высота кликабельной зоны слайдера (вся полоса попадания), px")]
    [SerializeField] private float sliderHeight = 24f;
    [Tooltip("Толщина видимой дорожки слайдера, px")]
    [SerializeField] private float trackHeight = 10f;
    [Tooltip("Размер ползунка слайдера, px")]
    [SerializeField] private float handleSize = 14f;
    [SerializeField] private Color labelColor = new Color(0.85f, 0.87f, 0.9f, 1f);
    [SerializeField] private Color trackColor = new Color(0.2f, 0.2f, 0.22f, 1f);
    [SerializeField] private Color fillColor = new Color(0.3f, 0.55f, 0.85f, 1f);
    [SerializeField] private Color handleColor = new Color(0.95f, 0.95f, 0.95f, 1f);
    [SerializeField] private Color checkColor = new Color(0.3f, 0.85f, 0.4f, 1f);

    private Font labelFont;

    private string expandedId;
    private List<SettingDescriptor> lastDescriptors;

    private float EffectiveRowHeight => Mathf.Max(24f, rowHeight);
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
            else if (d.Type == SettingType.Key) row = CreateKeyRow(d);
            else row = CreateSliderRow(d);

            RectTransform rt = (RectTransform)row.transform;
            rt.SetParent(content, false);

            // VerticalLayoutGroup у Content не управляет высотой детей (childControlHeight = false),
            // поэтому высоту строки задаём явно. Иначе строка остаётся 100 px (дефолтный
            // RectTransform), LayoutElement.preferredHeight игнорируется и слайдеры выглядят толстыми.
            SetHeight(rt, EffectiveRowHeight);

            LayoutElement le = row.GetComponent<LayoutElement>();
            if (le == null) le = row.AddComponent<LayoutElement>();
            le.preferredHeight = EffectiveRowHeight;
            le.flexibleWidth = 1f;

            if (d.Type == SettingType.Dropdown && d.Id == expandedId) AddOptionRows(d);
        }
    }

    public void Clear()
    {
        if (content == null) return;
        for (int i = content.childCount - 1; i >= 0; i--)
            Destroy(content.GetChild(i).gameObject);
    }

    private Font GetLabelFont()
    {
        if (labelFont == null)
        {
            labelFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (labelFont == null) labelFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        return labelFont;
    }

    private GameObject CreateSliderRow(SettingDescriptor d)
    {
        GameObject row = new GameObject(d.Id + "_Row", typeof(RectTransform));
        ConfigureRowLayout(row);

        Text label = CreateLabel(row.transform, d.Title);
        SetHeight((RectTransform)label.transform, EffectiveRowHeight);
        LayoutElement labelLE = label.gameObject.AddComponent<LayoutElement>();
        labelLE.flexibleWidth = 1f;
        labelLE.minWidth = 180f;

        GameObject slider = CreateSliderControl(row.transform, d, label);
        LayoutElement sliderLE = slider.AddComponent<LayoutElement>();
        sliderLE.preferredWidth = sliderWidth;
        sliderLE.preferredHeight = EffectiveSliderHeight;

        return row;
    }

    private GameObject CreateToggleRow(SettingDescriptor d)
    {
        GameObject row = new GameObject(d.Id + "_Row", typeof(RectTransform));
        ConfigureRowLayout(row);

        Text label = CreateLabel(row.transform, d.Title);
        SetHeight((RectTransform)label.transform, EffectiveRowHeight);
        LayoutElement labelLE = label.gameObject.AddComponent<LayoutElement>();
        labelLE.flexibleWidth = 1f;
        labelLE.minWidth = 180f;

        GameObject toggleGO = new GameObject(d.Id + "_Toggle", typeof(RectTransform));
        toggleGO.transform.SetParent(row.transform, false);

        // LayoutElement.preferredHeight не применяется (childControlHeight = false),
        // поэтому размер чекбокса задаём через sizeDelta.
        ((RectTransform)toggleGO.transform).sizeDelta = new Vector2(28f, 28f);

        Image bg = toggleGO.AddComponent<Image>();
        bg.color = trackColor;

        GameObject checkGO = new GameObject("Checkmark", typeof(RectTransform));
        checkGO.transform.SetParent(toggleGO.transform, false);
        Text check = checkGO.AddComponent<Text>();
        check.font = GetLabelFont();
        check.text = "✓";
        check.fontSize = 20;
        check.alignment = TextAnchor.MiddleCenter;
        check.color = checkColor;
        check.raycastTarget = false;
        Stretch((RectTransform)checkGO.transform);

        Toggle toggle = toggleGO.AddComponent<Toggle>();
        toggle.targetGraphic = bg;
        toggle.graphic = check;
        toggle.isOn = d.GetBool != null ? d.GetBool() : false;
        toggle.onValueChanged.AddListener(v =>
        {
            if (d.SetBool != null) d.SetBool(v);
            OnChanged(d, true);
        });

        LayoutElement toggleLE = toggleGO.AddComponent<LayoutElement>();
        toggleLE.preferredWidth = 28f;
        toggleLE.preferredHeight = 28f;
        toggleLE.minWidth = 28f;

        return row;
    }

    private GameObject CreateDropdownRow(SettingDescriptor d)
    {
        GameObject row = new GameObject(d.Id + "_Row", typeof(RectTransform));
        ConfigureRowLayout(row);

        Text label = CreateLabel(row.transform, d.Title);
        SetHeight((RectTransform)label.transform, EffectiveRowHeight);
        LayoutElement labelLE = label.gameObject.AddComponent<LayoutElement>();
        labelLE.flexibleWidth = 1f;
        labelLE.minWidth = 180f;

        GameObject buttonObject = new GameObject(d.Id + "_Button", typeof(RectTransform));
        buttonObject.transform.SetParent(row.transform, false);

        Image image = buttonObject.AddComponent<Image>();
        image.color = trackColor;

        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;

        GameObject textObject = new GameObject("Text", typeof(RectTransform));
        textObject.transform.SetParent(buttonObject.transform, false);
        Stretch((RectTransform)textObject.transform);

        Text value = textObject.AddComponent<Text>();
        value.font = GetLabelFont();
        value.text = GetOptionLabel(d);
        value.fontSize = 16;
        value.alignment = TextAnchor.MiddleCenter;
        value.color = labelColor;
        value.raycastTarget = false;

        LayoutElement buttonLE = buttonObject.AddComponent<LayoutElement>();
        buttonLE.preferredWidth = sliderWidth;
        buttonLE.preferredHeight = EffectiveRowHeight;

        string id = d.Id;
        button.onClick.AddListener(() => ToggleDropdown(id));

        return row;
    }

    private GameObject CreateKeyRow(SettingDescriptor d)
    {
        GameObject row = new GameObject(d.Id + "_Row", typeof(RectTransform));
        ConfigureRowLayout(row);

        Text label = CreateLabel(row.transform, d.Title);
        SetHeight((RectTransform)label.transform, EffectiveRowHeight);
        LayoutElement labelLE = label.gameObject.AddComponent<LayoutElement>();
        labelLE.flexibleWidth = 1f;
        labelLE.minWidth = 180f;

        GameObject buttonObject = new GameObject(d.Id + "_Key", typeof(RectTransform));
        buttonObject.transform.SetParent(row.transform, false);

        Image image = buttonObject.AddComponent<Image>();
        image.color = trackColor;

        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;

        GameObject textObject = new GameObject("Text", typeof(RectTransform));
        textObject.transform.SetParent(buttonObject.transform, false);
        Stretch((RectTransform)textObject.transform);

        Text value = textObject.AddComponent<Text>();
        value.font = GetLabelFont();
        value.fontSize = 16;
        value.alignment = TextAnchor.MiddleCenter;
        value.color = labelColor;
        value.raycastTarget = false;

        LayoutElement buttonLE = buttonObject.AddComponent<LayoutElement>();
        buttonLE.preferredWidth = sliderWidth;
        buttonLE.preferredHeight = EffectiveRowHeight;

        KeyBindingRow keyRow = buttonObject.AddComponent<KeyBindingRow>();
        keyRow.Setup(d.ActionId, d.BindingIndex, value, () => OnChanged(d, true));

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
            ConfigureRowLayout(row);

            RectTransform rowRT = (RectTransform)row.transform;
            rowRT.SetParent(content, false);
            SetHeight(rowRT, EffectiveRowHeight);

            LayoutElement rowLE = row.AddComponent<LayoutElement>();
            rowLE.preferredHeight = EffectiveRowHeight;
            rowLE.flexibleWidth = 1f;

            GameObject buttonObject = new GameObject("Option", typeof(RectTransform));
            buttonObject.transform.SetParent(row.transform, false);

            Image image = buttonObject.AddComponent<Image>();
            image.color = i == current ? fillColor : trackColor;

            Button button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;

            GameObject textObject = new GameObject("Text", typeof(RectTransform));
            textObject.transform.SetParent(buttonObject.transform, false);
            Stretch((RectTransform)textObject.transform);

            Text optionText = textObject.AddComponent<Text>();
            optionText.font = GetLabelFont();
            optionText.text = "   " + d.Options[i];
            optionText.fontSize = 16;
            optionText.alignment = TextAnchor.MiddleLeft;
            optionText.color = labelColor;
            optionText.raycastTarget = false;

            LayoutElement buttonLE = buttonObject.AddComponent<LayoutElement>();
            buttonLE.flexibleWidth = 1f;
            buttonLE.preferredHeight = EffectiveRowHeight;

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
        return d.Options[index] + "  ▾";
    }

    private GameObject CreateSliderControl(Transform parent, SettingDescriptor d, Text label)
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
        // (10 px) тяжело попасть мышью, а клик мимо дорожки не считался бы кликом по Slider.
        GameObject hitGO = new GameObject("Hit Area", typeof(RectTransform));
        hitGO.transform.SetParent(root.transform, false);
        Image hit = hitGO.AddComponent<Image>();
        hit.color = new Color(0f, 0f, 0f, 0f);
        hit.raycastTarget = true;
        Stretch((RectTransform)hitGO.transform);

        GameObject bgGO = new GameObject("Background", typeof(RectTransform));
        bgGO.transform.SetParent(root.transform, false);
        Image bg = bgGO.AddComponent<Image>();
        bg.color = trackColor;
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
        fill.color = fillColor;
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
        handle.color = handleColor;
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
        label.text = BuildLabel(d, slider.value);
        slider.onValueChanged.AddListener(v =>
        {
            if (d.SetFloat != null) d.SetFloat(v);
            label.text = BuildLabel(d, v);
            OnChanged(d, false);
        });

        SliderReleaseNotifier notifier = root.AddComponent<SliderReleaseNotifier>();
        notifier.OnRelease = () => OnChanged(d, true);

        return root;
    }

    private void ConfigureRowLayout(GameObject row)
    {
        HorizontalLayoutGroup hlg = row.AddComponent<HorizontalLayoutGroup>();
        hlg.padding = new RectOffset(0, 0, 4, 4);
        hlg.spacing = 12f;
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.childControlWidth = true;
        hlg.childControlHeight = false;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;
    }

    private Text CreateLabel(Transform parent, string text)
    {
        GameObject go = new GameObject("Label", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Text t = go.AddComponent<Text>();
        t.font = GetLabelFont();
        t.text = text;
        t.fontSize = 18;
        t.alignment = TextAnchor.MiddleLeft;
        t.color = labelColor;
        t.raycastTarget = false;
        return t;
    }

    private void OnChanged(SettingDescriptor descriptor, bool released)
    {
        SettingsService service = SettingsService.Instance;
        if (service == null) return;

        service.ApplyInstant();

        if (descriptor == null || !descriptor.RequiresRelease || released)
            service.ApplyHeavy();

        service.MarkDirty();
    }

    private string BuildLabel(SettingDescriptor d, float value)
    {
        string fmt = string.IsNullOrEmpty(d.ValueFormat) ? (d.WholeNumbers ? "F0" : "F2") : d.ValueFormat;
        return d.Title + ": " + value.ToString(fmt, CultureInfo.InvariantCulture);
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
