using System.Collections.Generic;
using DroneSimulator.Settings;
using UnityEngine;

public static class SettingsApplier
{
    public static readonly string[] WindowModeLabels = { "Полный экран", "Без рамки", "Окно" };

    private static int lastWindowMode = int.MinValue;
    private static int lastResolutionIndex = int.MinValue;

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
        PostFxRouter.Ensure().ApplyMotionBlur(data.graphics.bunkerMotionBlur);
        CameraApplier.Refresh();
    }

    public static void ResetScreenCache()
    {
        lastWindowMode = int.MinValue;
        lastResolutionIndex = int.MinValue;
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

    private static void ApplyScreen(GraphicsSettings graphics)
    {
        if (lastWindowMode == graphics.windowMode && lastResolutionIndex == graphics.resolutionIndex) return;

        lastWindowMode = graphics.windowMode;
        lastResolutionIndex = graphics.resolutionIndex;

        FullScreenMode mode = ToFullScreenMode(graphics.windowMode);
        Resolution[] unique = GetUniqueResolutions();

        int index = graphics.resolutionIndex;
        bool hasTarget = index >= 0 && index < unique.Length;

        if (hasTarget)
        {
            Resolution target = unique[index];
            Screen.SetResolution(target.width, target.height, mode);
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

        int vSync = graphics.vSync ? 1 : 0;
        if (QualitySettings.vSyncCount != vSync) QualitySettings.vSyncCount = vSync;

        int target = (graphics.vSync || graphics.fpsLimit <= 0) ? -1 : graphics.fpsLimit;
        if (Application.targetFrameRate != target) Application.targetFrameRate = target;

        if (!Mathf.Approximately(QualitySettings.lodBias, graphics.bunkerLodBias))
            QualitySettings.lodBias = graphics.bunkerLodBias;

        if (QualitySettings.globalTextureMipmapLimit != graphics.bunkerTextureQuality)
            QualitySettings.globalTextureMipmapLimit = graphics.bunkerTextureQuality;
    }
}
