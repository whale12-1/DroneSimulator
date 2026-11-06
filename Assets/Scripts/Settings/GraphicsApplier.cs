using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class GraphicsApplier
{
    private static UniversalRenderPipelineAsset runtimeAsset;
    private static RenderPipelineAsset originalQualityAsset;
    private static FieldInfo softShadowsField;
    private static bool softShadowsFieldResolved;
    private static bool warningLogged;
    private static bool quittingSubscribed;

    public static bool HasRuntimeAsset => runtimeAsset != null;

    public static void Apply(DroneSimulator.Settings.GraphicsSettings graphics)
    {
        if (graphics == null) return;

        EnsureRuntimeAsset();
        if (runtimeAsset == null) return;

        runtimeAsset.renderScale = Mathf.Clamp(graphics.renderScale, 0.1f, 1f);
        runtimeAsset.shadowDistance = Mathf.Clamp(GetShadowDistance(graphics), 0f, 500f);
        runtimeAsset.shadowCascadeCount = Mathf.Clamp(graphics.bunkerShadowQuality, 1, 4);
        runtimeAsset.msaaSampleCount = NormalizeMsaa(graphics.bunkerMsaa);
        SetSoftShadows(runtimeAsset, graphics.bunkerSoftShadows);
        runtimeAsset.mainLightShadowmapResolution = NormalizeShadowResolution(graphics.bunkerShadowResolution);

        if (QualitySettings.renderPipeline != runtimeAsset)
            QualitySettings.renderPipeline = runtimeAsset;
    }

    private static void EnsureRuntimeAsset()
    {
        if (runtimeAsset != null) return;

        RenderPipelineAsset current = GraphicsSettings.currentRenderPipeline;
        UniversalRenderPipelineAsset source = current as UniversalRenderPipelineAsset;

        if (source == null)
            source = GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;

        if (source == null)
        {
            if (!warningLogged)
            {
                warningLogged = true;
                Debug.LogWarning("[GraphicsApplier] URP-ассет не найден. Настройки графики (render scale, тени, MSAA) не применяются.");
            }

            return;
        }

        originalQualityAsset = QualitySettings.renderPipeline;
        runtimeAsset = Object.Instantiate(source);
        runtimeAsset.name = source.name + " (Runtime)";
        QualitySettings.renderPipeline = runtimeAsset;

        if (!quittingSubscribed)
        {
            Application.quitting += Shutdown;
            quittingSubscribed = true;
        }
    }

    private static void Shutdown()
    {
        if (runtimeAsset == null) return;

        if (QualitySettings.renderPipeline == runtimeAsset)
            QualitySettings.renderPipeline = originalQualityAsset;

        runtimeAsset = null;
    }

    private static float GetShadowDistance(DroneSimulator.Settings.GraphicsSettings graphics)
    {
        return SettingsScope.IsMission ? graphics.droneShadowDistance : graphics.bunkerShadowDistance;
    }

    private static void SetSoftShadows(UniversalRenderPipelineAsset asset, bool enabled)
    {
        if (!softShadowsFieldResolved)
        {
            softShadowsFieldResolved = true;
            softShadowsField = typeof(UniversalRenderPipelineAsset).GetField("m_SoftShadowsSupported", BindingFlags.Instance | BindingFlags.NonPublic);
        }

        if (softShadowsField == null) return;

        object current = softShadowsField.GetValue(asset);
        if (current is bool currentValue && currentValue == enabled) return;

        softShadowsField.SetValue(asset, enabled);
    }

    private static int NormalizeMsaa(int samples)
    {
        if (samples <= 1) return 1;

        int normalized = Mathf.ClosestPowerOfTwo(samples);
        return Mathf.Clamp(normalized, 1, 8);
    }

    private static int NormalizeShadowResolution(int resolution)
    {
        int normalized = Mathf.ClosestPowerOfTwo(Mathf.Max(256, resolution));
        return Mathf.Clamp(normalized, 256, 4096);
    }
}
