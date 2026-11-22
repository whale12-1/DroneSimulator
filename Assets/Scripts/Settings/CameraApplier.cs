using UnityEngine;
using UnityEngine.Rendering.Universal;

public static class CameraApplier
{
    private static bool droneSwitchSubscribed;

    public static void Refresh()
    {
        SettingsService service = SettingsService.Instance;
        if (service == null || service.Data == null || service.Data.graphics == null) return;

        DroneSimulator.Settings.GraphicsSettings graphics = service.Data.graphics;
        Camera[] cameras = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include);

        for (int i = 0; i < cameras.Length; i++)
            ApplyToCamera(cameras[i], graphics);

        SubscribeDroneSwitch();
    }

    private static void ApplyToCamera(Camera cam, DroneSimulator.Settings.GraphicsSettings graphics)
    {
        if (cam == null) return;

        if (cam.GetComponentInParent<CharacterController>() != null)
        {
            ApplyFpsCamera(cam, graphics);
            return;
        }

        if (IsDroneCamera(cam)) ApplyDroneCamera(cam, graphics);
    }

    private static bool IsDroneCamera(Camera cam)
    {
        if (cam.GetComponent<FPVZoom>() != null) return true;
        if (cam.GetComponent<DroneCameraModule>() != null) return true;
        return cam.GetComponentInParent<DroneFlightController>() != null;
    }

    private static void ApplyFpsCamera(Camera cam, DroneSimulator.Settings.GraphicsSettings graphics)
    {
        float farClip = graphics.bunkerFarClip > 0f ? graphics.bunkerFarClip : cam.farClipPlane;
        if (!Mathf.Approximately(cam.farClipPlane, farClip)) cam.farClipPlane = farClip;

        UniversalAdditionalCameraData data = cam.GetUniversalAdditionalCameraData();
        if (data != null && data.renderPostProcessing != graphics.bunkerPostProcessing)
            data.renderPostProcessing = graphics.bunkerPostProcessing;
    }

    // The far horizon mesh reaches a 10 km radius; a drone at the terrain corner can
    // look up to ~11.4 km, so the drone camera must never go below 12 km.
    private const float MinDroneFarClip = 12000f;
    private const float MaxDroneFarClip = 30000f;

    private static void ApplyDroneCamera(Camera cam, DroneSimulator.Settings.GraphicsSettings graphics)
    {
        float farClip = Mathf.Clamp(graphics.droneFarClip, MinDroneFarClip, MaxDroneFarClip);
        if (!Mathf.Approximately(cam.farClipPlane, farClip)) cam.farClipPlane = farClip;

        UniversalAdditionalCameraData data = cam.GetUniversalAdditionalCameraData();
        if (data == null) return;

        bool renderShadows = graphics.droneShadowDistance > 0f;
        if (data.renderShadows != renderShadows) data.renderShadows = renderShadows;

        AntialiasingMode antialiasing = (AntialiasingMode)Mathf.Clamp(graphics.droneAntiAliasing, 0, 3);
        if (data.antialiasing != antialiasing) data.antialiasing = antialiasing;
    }

    private static void SubscribeDroneSwitch()
    {
        if (droneSwitchSubscribed) return;

        GameEvents.DroneSwitched += OnDroneSwitched;
        droneSwitchSubscribed = true;
    }

    private static void OnDroneSwitched(GameObject drone)
    {
        if (drone == null) return;

        SettingsService service = SettingsService.Instance;
        if (service == null || service.Data == null || service.Data.graphics == null) return;

        Camera[] cameras = drone.GetComponentsInChildren<Camera>(true);
        for (int i = 0; i < cameras.Length; i++)
            ApplyToCamera(cameras[i], service.Data.graphics);
    }
}
