using System.Collections.Generic;
using DroneSimulator.Settings;
using UnityEngine;

public static class SettingsApplier
{
    public static readonly string[] WindowModeLabels = { "Полный экран", "Без рамки", "Окно" };

    private static int lastWindowMode = int.MinValue;
    private static int lastResolutionWidth = int.MinValue;
    private static int lastResolutionHeight = int.MinValue;

    public static void Apply(SettingsData data)
    {
        if (data == null) return;

        ApplyInstant(data);
        ApplyHeavy(data);
    }

    public static void ApplyInstant(SettingsData data)
    {
        if (data == null) return;

        AudioApplier.Apply(data.audio);
    }

    public static void ApplyHeavy(SettingsData data)
    {
        if (data == null) return;

        ApplyScreen(data.graphics);
        ApplyQualityState(data.graphics);
        GraphicsApplier.Apply(data.graphics);

        // Motion Blur — настройка бункера. PostFxRouter создаёт глобальные Volume,
        // живущие DontDestroyOnLoad, поэтому без проверки скоупа эффект попадал бы
        // и на камеры дронов (у них renderPostProcessing включён в префабах).
        PostFxRouter.Ensure().ApplyMotionBlur(!SettingsScope.IsMission && data.graphics.bunkerMotionBlur);
        CameraApplier.Refresh();
    }

    public static void ResetScreenCache()
    {
        lastWindowMode = int.MinValue;
        lastResolutionWidth = int.MinValue;
        lastResolutionHeight = int.MinValue;
    }

    public static string[] GetResolutionOptions()
    {
        Resolution[] unique = GetUniqueResolutions();
        string[] options = new string[unique.Length + 1];
        options[0] = "Текущее";

        for (int i = 0; i < unique.Length; i++)
            options[i + 1] = unique[i].width + " × " + unique[i].height;

        return options;
    }

    public static Resolution[] GetUniqueResolutions()
    {
        Resolution[] all = Screen.resolutions;
        List<Resolution> unique = new List<Resolution>();

        for (int i = 0; i < all.Length; i++)
        {
            bool exists = false;

            for (int j = 0; j < unique.Count; j++)
            {
                if (unique[j].width == all[i].width && unique[j].height == all[i].height)
                {
                    exists = true;
                    break;
                }
            }

            if (!exists) unique.Add(all[i]);
        }

        return unique.ToArray();
    }

    // Индекс для дропдауна: 0 = "Текущее", 1..N = позиция в GetUniqueResolutions().
    public static int FindResolutionOptionIndex(GraphicsSettings graphics)
    {
        if (graphics == null) return 0;
        if (graphics.resolutionWidth <= 0 || graphics.resolutionHeight <= 0) return 0;

        Resolution[] unique = GetUniqueResolutions();
        for (int i = 0; i < unique.Length; i++)
        {
            if (unique[i].width == graphics.resolutionWidth && unique[i].height == graphics.resolutionHeight)
                return i + 1;
        }

        // Сохранённое разрешение отсутствует в текущем списке (сменился монитор/драйвер).
        return 0;
    }

    // Записывает в данные выбранный пункт дропдауна (0 = не менять разрешение).
    public static void SetResolutionOption(GraphicsSettings graphics, int optionIndex)
    {
        if (graphics == null) return;

        if (optionIndex <= 0)
        {
            graphics.resolutionWidth = 0;
            graphics.resolutionHeight = 0;
            return;
        }

        Resolution[] unique = GetUniqueResolutions();
        int index = Mathf.Clamp(optionIndex - 1, 0, Mathf.Max(0, unique.Length - 1));
        if (unique.Length == 0) return;

        graphics.resolutionWidth = unique[index].width;
        graphics.resolutionHeight = unique[index].height;
    }

    private static void ApplyScreen(GraphicsSettings graphics)
    {
        FullScreenMode mode = ToFullScreenMode(graphics.windowMode);

        // windowMode — единственный источник истины; fullscreen остаётся
        // legacy-зеркалом для старых файлов настроек и внешнего кода.
        graphics.fullscreen = mode != FullScreenMode.Windowed;

        int width = Mathf.Max(0, graphics.resolutionWidth);
        int height = Mathf.Max(0, graphics.resolutionHeight);

        if (lastWindowMode == graphics.windowMode && lastResolutionWidth == width && lastResolutionHeight == height)
            return;

        lastWindowMode = graphics.windowMode;
        lastResolutionWidth = width;
        lastResolutionHeight = height;

        if (width > 0 && height > 0)
        {
            Screen.SetResolution(width, height, mode);
            return;
        }

        if (Screen.fullScreenMode != mode) Screen.fullScreenMode = mode;
    }

    private static FullScreenMode ToFullScreenMode(int windowMode)
    {
        switch (windowMode)
        {
            case 1:
                return FullScreenMode.MaximizedWindow;
            case 2:
                return FullScreenMode.Windowed;
            default:
                return FullScreenMode.FullScreenWindow;
        }
    }

    private static void ApplyQualityState(GraphicsSettings graphics)
    {
        int maxLevel = Mathf.Max(0, QualitySettings.names.Length - 1);
        int level = Mathf.Clamp(graphics.qualityLevel, 0, maxLevel);
        if (QualitySettings.GetQualityLevel() != level) QualitySettings.SetQualityLevel(level, true);

        // "Лимит побеждает": Application.targetFrameRate игнорируется драйвером при
        // включённом VSync, поэтому при fpsLimit > 0 VSync принудительно выключается.
        bool limitActive = graphics.fpsLimit > 0;
        int vSync = limitActive ? 0 : (graphics.vSync ? 1 : 0);
        if (QualitySettings.vSyncCount != vSync) QualitySettings.vSyncCount = vSync;

        int target = limitActive ? graphics.fpsLimit : -1;
        if (Application.targetFrameRate != target) Application.targetFrameRate = target;

        if (!Mathf.Approximately(QualitySettings.lodBias, graphics.bunkerLodBias))
            QualitySettings.lodBias = graphics.bunkerLodBias;

        if (QualitySettings.globalTextureMipmapLimit != graphics.bunkerTextureQuality)
            QualitySettings.globalTextureMipmapLimit = graphics.bunkerTextureQuality;
    }
}
