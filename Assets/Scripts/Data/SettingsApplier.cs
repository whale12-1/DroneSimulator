using DroneSimulator.Settings;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public static class SettingsApplier
{
    public static void Apply(SettingsData data)
    {
        if (data == null) return;

        ApplyDisplay(data.graphics);
        ApplyQuality(data.graphics);
        ApplyAudio(data.audio);
        ApplyCamera(data.graphics);
    }

    private static void ApplyDisplay(GraphicsSettings g)
    {
        FullScreenMode mode = g.fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
        if (Screen.fullScreenMode != mode) Screen.fullScreenMode = mode;

        int vSync = g.vSync ? 1 : 0;
        if (QualitySettings.vSyncCount != vSync) QualitySettings.vSyncCount = vSync;

        int target = (g.vSync || g.fpsLimit <= 0) ? -1 : g.fpsLimit;
        if (Application.targetFrameRate != target) Application.targetFrameRate = target;
    }

    private static void ApplyQuality(GraphicsSettings g)
    {
        int maxLevel = Mathf.Max(0, QualitySettings.names.Length - 1);
        int level = Mathf.Clamp(g.qualityLevel, 0, maxLevel);
        if (QualitySettings.GetQualityLevel() != level) QualitySettings.SetQualityLevel(level, true);

        if (!Mathf.Approximately(QualitySettings.lodBias, g.bunkerLodBias))
            QualitySettings.lodBias = g.bunkerLodBias;

        if (QualitySettings.globalTextureMipmapLimit != g.bunkerTextureQuality)
            QualitySettings.globalTextureMipmapLimit = g.bunkerTextureQuality;
    }

    private static void ApplyAudio(DroneSimulator.Settings.AudioSettings a)
    {
        AudioListener.volume = Mathf.Clamp01(a.masterVolume);
    }

    private static void ApplyCamera(GraphicsSettings g)
    {
        Camera cam = Camera.main;
        if (cam == null) cam = UnityEngine.Object.FindAnyObjectByType<Camera>();
        if (cam == null) return;

        if (cam.GetComponentInParent<CharacterController>() == null) return;

        float farClip = g.bunkerFarClip > 0f ? g.bunkerFarClip : cam.farClipPlane;
        if (!Mathf.Approximately(cam.farClipPlane, farClip)) cam.farClipPlane = farClip;

        UniversalAdditionalCameraData data = cam.GetUniversalAdditionalCameraData();
        if (data != null && data.renderPostProcessing != g.bunkerPostProcessing)
            data.renderPostProcessing = g.bunkerPostProcessing;
    }
}
