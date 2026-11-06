using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

public enum SettingType
{
    Slider,
    Toggle
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

    private float EffectiveRowHeight => Mathf.Max(24f, rowHeight);
    private float EffectiveSliderHeight => sliderHeight > 0f ? sliderHeight : 24f;

    public void Build(List<SettingDescriptor> descriptors)
    {
        Clear();
        if (content == null || descriptors == null) return;

        foreach (SettingDescriptor d in descriptors)
        {
            if (d == null) continue;

            GameObject row = d.Type == SettingType.Toggle ? CreateToggleRow(d) : CreateSliderRow(d);
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
            OnChanged();
        });

        LayoutElement toggleLE = toggleGO.AddComponent<LayoutElement>();
        toggleLE.preferredWidth = 28f;
        toggleLE.preferredHeight = 28f;
        toggleLE.minWidth = 28f;

        return row;
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
            OnChanged();
        });

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

    private void OnChanged()
    {
        if (SettingsService.Instance != null)
        {
            SettingsService.Instance.ApplyAll();
            SettingsService.Instance.MarkDirty();
        }
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
