using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Общие утилиты Editor-билдеров UI: загрузка спрайтов HUF Pack, шрифта LiberationSans SDF,
/// создание/поиск объектов и единая стилизация кнопок. Все билдеры работают in-place
/// (PrefabUtility.LoadPrefabContents -> SaveAsPrefabAsset), чтобы не менять fileID и не
/// рвать ссылки сцен на компоненты префабов.
/// </summary>
public static class TacticalUIBuilder
{
    public const string AtlasPath = "Assets/Strategic Warfare UI Starter Pack/UI Kit/UI-png/HUF Pack.png";
    public const string FontPath = "Assets/UnityTechnologies/ParticlePack/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";

    private static Dictionary<string, Sprite> sprites;
    private static TMP_FontAsset font;

    // ---------------------------------------------------------------- ассеты

    public static Sprite LoadSprite(string spriteName)
    {
        if (sprites == null)
        {
            sprites = new Dictionary<string, Sprite>();
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(AtlasPath);

            foreach (Object asset in assets)
            {
                Sprite sprite = asset as Sprite;
                if (sprite != null && !sprites.ContainsKey(sprite.name))
                    sprites.Add(sprite.name, sprite);
            }
        }

        if (sprites.TryGetValue(spriteName, out Sprite result)) return result;

        Debug.LogError($"[TacticalUIBuilder] Не найден спрайт '{spriteName}' в {AtlasPath}");
        return null;
    }

    public static TMP_FontAsset LoadFont()
    {
        if (font == null)
            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);

        if (font == null) font = TMP_Settings.defaultFontAsset;

        if (font == null)
            Debug.LogError("[TacticalUIBuilder] Не найден шрифт LiberationSans SDF");

        return font;
    }

    // ---------------------------------------------------------------- объекты

    public static GameObject CreateRect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.layer = 5;
        if (parent != null) go.transform.SetParent(parent, false);
        return go;
    }

    public static GameObject FindChild(Transform parent, string name)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name == name) return child.gameObject;
        }

        return null;
    }

    public static GameObject EnsureChild(Transform parent, string name)
    {
        GameObject existing = FindChild(parent, name);
        return existing != null ? existing : CreateRect(name, parent);
    }

    public static RectTransform RT(GameObject go) => (RectTransform)go.transform;

    // ---------------------------------------------------------------- изображения и текст

    public static Image EnsureImage(GameObject go, Sprite sprite, Color color, Image.Type type = Image.Type.Sliced)
    {
        Image image = go.GetComponent<Image>();
        if (image == null) image = go.AddComponent<Image>();

        image.sprite = sprite;
        image.type = sprite != null ? type : Image.Type.Simple;
        image.color = color;
        return image;
    }

    public static Image EnsureImageChild(Transform parent, string name, Sprite sprite, Color color, Image.Type type = Image.Type.Sliced)
    {
        GameObject go = EnsureChild(parent, name);
        RectTransform rt = RT(go);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return EnsureImage(go, sprite, color, type);
    }

    public static TextMeshProUGUI EnsureText(GameObject go, string text, float size, Color color, TextAlignmentOptions alignment)
    {
        // Legacy UnityEngine.UI.Text рисуется поверх TMP — убираем его полностью.
        Text legacy = go.GetComponent<Text>();
        if (legacy != null) Object.DestroyImmediate(legacy, true);

        TextMeshProUGUI label = go.GetComponent<TextMeshProUGUI>();
        if (label == null)
        {
            label = go.AddComponent<TextMeshProUGUI>();
            if (go.GetComponent<CanvasRenderer>() == null) go.AddComponent<CanvasRenderer>();
        }

        label.font = LoadFont();
        label.text = text;
        label.fontSize = size;
        label.color = color;
        label.alignment = alignment;
        label.raycastTarget = false;
        // По умолчанию оставляем перенос строк: описания миссий и подсказки
        // должны переноситься, а не вылезать за границы блока. Однострочные
        // подписи помечаются NoWrap точечно в билдерах.
        label.textWrappingMode = TextWrappingModes.Normal;
        return label;
    }

    public static Button EnsureButton(GameObject go, Graphic targetGraphic)
    {
        Button button = go.GetComponent<Button>();
        if (button == null) button = go.AddComponent<Button>();

        button.targetGraphic = targetGraphic;
        button.transition = Selectable.Transition.ColorTint;
        button.colors = TacticalUITheme.ButtonColorBlock();
        return button;
    }

    // ---------------------------------------------------------------- разметка

    public static void Stretch(RectTransform rt, float padding = 0f)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(padding, padding);
        rt.offsetMax = new Vector2(-padding, -padding);
    }

    public static void SetTopLeft(RectTransform rt, float x, float y, float width, float height)
    {
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x, -y);
        rt.sizeDelta = new Vector2(width, height);
    }

    public static void SetTopRight(RectTransform rt, float right, float top, float width, float height)
    {
        rt.anchorMin = new Vector2(1f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 1f);
        rt.anchoredPosition = new Vector2(-right, -top);
        rt.sizeDelta = new Vector2(width, height);
    }

    public static void SetTopCenter(RectTransform rt, float top, float width, float height, float x = 0f)
    {
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(x, -top);
        rt.sizeDelta = new Vector2(width, height);
    }

    public static void SetCenter(RectTransform rt, float width, float height, Vector2 offset)
    {
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = offset;
        rt.sizeDelta = new Vector2(width, height);
    }

    public static void SetBottomCenter(RectTransform rt, float bottom, float width, float height, float x = 0f)
    {
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(x, bottom);
        rt.sizeDelta = new Vector2(width, height);
    }

    public static void SetBottomRight(RectTransform rt, float right, float bottom, float width, float height)
    {
        rt.anchorMin = new Vector2(1f, 0f);
        rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(1f, 0f);
        rt.anchoredPosition = new Vector2(-right, bottom);
        rt.sizeDelta = new Vector2(width, height);
    }

    /// <summary>Плоская тонкая полоса (красный акцент, разделитель).</summary>
    public static Image EnsureBar(Transform parent, string name, Color color)
    {
        GameObject go = EnsureChild(parent, name);
        Image image = EnsureImage(go, null, color, Image.Type.Simple);
        image.raycastTarget = false;
        return image;
    }

    /// <summary>
    /// Чистая тактическая рамка без внутренних линий: 1-2 px контур + красные
    /// угловые скобки. Используется вместо растянутого спрайта-рамки HUF Pack,
    /// который на больших размерах даёт декоративные линии поверх контента.
    /// </summary>
    public static void BuildTacticalFrame(Transform parent, Color lineColor, Color accentColor,
        float thickness = 2f, float cornerLength = 28f, float cornerThickness = 3f, float inset = 0f)
    {
        RectTransform rt = parent as RectTransform;
        if (rt == null) return;

        CreateEdge(parent, "FrameTop", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
            new Vector2(inset, -inset - thickness), new Vector2(-inset, -inset), lineColor);
        CreateEdge(parent, "FrameBottom", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f),
            new Vector2(inset, inset), new Vector2(-inset, inset + thickness), lineColor);
        CreateEdge(parent, "FrameLeft", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f),
            new Vector2(inset, inset), new Vector2(inset + thickness, -inset), lineColor);
        CreateEdge(parent, "FrameRight", new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f),
            new Vector2(-inset - thickness, inset), new Vector2(-inset, -inset), lineColor);

        BuildCorner(parent, "CornerTL", new Vector2(0f, 1f), new Vector2(1f, -1f), inset, cornerLength, cornerThickness, accentColor);
        BuildCorner(parent, "CornerTR", new Vector2(1f, 1f), new Vector2(-1f, -1f), inset, cornerLength, cornerThickness, accentColor);
        BuildCorner(parent, "CornerBL", new Vector2(0f, 0f), new Vector2(1f, 1f), inset, cornerLength, cornerThickness, accentColor);
        BuildCorner(parent, "CornerBR", new Vector2(1f, 0f), new Vector2(-1f, 1f), inset, cornerLength, cornerThickness, accentColor);
    }

    private static void CreateEdge(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
        Vector2 offsetMin, Vector2 offsetMax, Color color)
    {
        GameObject go = EnsureChild(parent, name);
        RectTransform rt = RT(go);
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;
        EnsureImage(go, null, color, Image.Type.Simple).raycastTarget = false;
    }

    private static void BuildCorner(Transform parent, string name, Vector2 anchor, Vector2 direction,
        float inset, float length, float thickness, Color color)
    {
        GameObject group = EnsureChild(parent, name);
        RectTransform groupRT = RT(group);
        groupRT.anchorMin = anchor;
        groupRT.anchorMax = anchor;
        groupRT.pivot = anchor;
        groupRT.sizeDelta = new Vector2(length, length);
        groupRT.anchoredPosition = new Vector2(direction.x * inset, direction.y * inset);

        // direction.x > 0 — угол слева, direction.y < 0 — угол сверху.
        int edgeY = direction.y < 0f ? 1 : 0;
        int edgeX = direction.x > 0f ? 0 : 1;

        GameObject horizontal = EnsureChild(group.transform, "H");
        RectTransform h = RT(horizontal);
        h.anchorMin = new Vector2(0f, edgeY);
        h.anchorMax = new Vector2(1f, edgeY);
        h.pivot = new Vector2(0.5f, edgeY);
        h.sizeDelta = new Vector2(0f, thickness);
        h.anchoredPosition = Vector2.zero;
        EnsureImage(horizontal, null, color, Image.Type.Simple).raycastTarget = false;

        GameObject vertical = EnsureChild(group.transform, "V");
        RectTransform v = RT(vertical);
        v.anchorMin = new Vector2(edgeX, 0f);
        v.anchorMax = new Vector2(edgeX, 1f);
        v.pivot = new Vector2(edgeX, 0.5f);
        v.sizeDelta = new Vector2(thickness, 0f);
        v.anchoredPosition = Vector2.zero;
        EnsureImage(vertical, null, color, Image.Type.Simple).raycastTarget = false;
    }

    public static void RemoveChildrenNamed(Transform parent, string name)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            Transform child = parent.GetChild(i);
            if (child.name == name) Object.DestroyImmediate(child.gameObject);
        }
    }
}