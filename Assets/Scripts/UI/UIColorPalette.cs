using UnityEngine;

public static class UIColorPalette
{
    public static readonly Color PanelBackground = FromHex("#1E2024", 0.90f);
    public static readonly Color PanelSurface = FromHex("#2A2D33", 0.90f);
    public static readonly Color PanelHeader = FromHex("#17191C", 0.95f);
    public static readonly Color Overlay = FromHex("#000000", 0.62f);
    public static readonly Color Accent = FromHex("#D94A4A", 1f);
    public static readonly Color AccentHover = FromHex("#E46363", 1f);
    public static readonly Color AccentPressed = FromHex("#A93737", 1f);
    public static readonly Color Success = FromHex("#73CC6B", 1f);
    public static readonly Color Failure = FromHex("#E67373", 1f);
    public static readonly Color TextPrimary = FromHex("#FFFFFF", 1f);
    public static readonly Color TextSecondary = FromHex("#A0A5B0", 1f);
    public static readonly Color Divider = FromHex("#FFFFFF", 0.16f);
    public static readonly Color Transparent = new Color(0f, 0f, 0f, 0f);

    public static Color FromHex(string hex, float alpha = 1f)
    {
        Color color;
        if (!ColorUtility.TryParseHtmlString(hex, out color))
        {
            color = Color.magenta;
        }

        color.a = Mathf.Clamp01(alpha);
        return color;
    }

    public static Color WithAlpha(Color color, float alpha)
    {
        color.a = Mathf.Clamp01(alpha);
        return color;
    }

    public static Color Lighten(Color color, float amount)
    {
        Color result = Color.Lerp(color, Color.white, Mathf.Clamp01(amount));
        result.a = color.a;
        return result;
    }

    public static Color Darken(Color color, float amount)
    {
        Color result = Color.Lerp(color, Color.black, Mathf.Clamp01(amount));
        result.a = color.a;
        return result;
    }

    public static float Saturation(Color color)
    {
        float h, s, v;
        Color.RGBToHSV(color, out h, out s, out v);
        return s;
    }

    public static float Hue(Color color)
    {
        float h, s, v;
        Color.RGBToHSV(color, out h, out s, out v);
        return h;
    }

    public static bool IsAcidic(Color color)
    {
        float h, s, v;
        Color.RGBToHSV(color, out h, out s, out v);
        return s >= 0.45f && v >= 0.45f;
    }

    public static bool IsNeutral(Color color)
    {
        return Saturation(color) < 0.22f;
    }

    public static Color SemanticiseSurface(Color color)
    {
        if (!IsAcidic(color)) return color;

        float hue = Hue(color);
        Color mapped;

        if (hue >= 0.18f && hue <= 0.46f) mapped = Success;
        else if (hue >= 0.46f && hue <= 0.72f) mapped = TextSecondary;
        else mapped = Accent;

        mapped.a = color.a;
        return mapped;
    }

    public static Color SemanticiseText(Color color)
    {
        if (!IsAcidic(color)) return color;

        float hue = Hue(color);
        Color mapped;

        if (hue >= 0.18f && hue <= 0.46f) mapped = Success;
        else if (hue >= 0.46f && hue <= 0.72f) mapped = TextSecondary;
        else if (hue >= 0.86f || hue <= 0.06f) mapped = Failure;
        else mapped = Accent;

        mapped.a = color.a;
        return mapped;
    }
}
