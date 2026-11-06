using UnityEngine;

public static class HudStyleService
{
    public const float DefaultScale = 1f;
    public const float DefaultTransparency = 1f;
    public const float DefaultCrosshairSize = 14f;

    private static SettingsService Service
    {
        get
        {
            SettingsService service = SettingsService.Instance;
            if (service == null || service.Data == null || service.Data.gameplay == null) return null;
            return service;
        }
    }

    public static float Scale
    {
        get
        {
            SettingsService service = Service;
            if (service == null) return DefaultScale;
            return Mathf.Clamp(service.Data.gameplay.hudScale, 0.5f, 2f);
        }
    }

    public static float Transparency
    {
        get
        {
            SettingsService service = Service;
            if (service == null) return DefaultTransparency;
            return Mathf.Clamp01(service.Data.gameplay.hudTransparency);
        }
    }

    public static Color ApplyAlpha(Color color)
    {
        color.a *= Transparency;
        return color;
    }

    public static Color Opaque(Color color)
    {
        color.a = 1f;
        return color;
    }

    public static float CrosshairSize(float fallback)
    {
        SettingsService service = Service;
        if (service == null) return Mathf.Max(1f, fallback);
        return Mathf.Max(1f, service.Data.gameplay.crosshairSize);
    }

    public static Matrix4x4 Begin()
    {
        Matrix4x4 previous = GUI.matrix;

        float scale = Scale;
        if (Mathf.Approximately(scale, 1f)) return previous;

        Vector2 pivot = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        GUIUtility.ScaleAroundPivot(new Vector2(scale, scale), pivot);

        return previous;
    }

    public static void End(Matrix4x4 previous)
    {
        GUI.matrix = previous;
    }
}
