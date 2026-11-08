using UnityEngine;

public class FPVZoom : MonoBehaviour
{
    [Header("Профиль Дрона")]
    [SerializeField] private DroneConfig config;

    private Camera cam;
    private int currentZoomIndex = 0;
    private float currentFov;
    private float fovOffset;

    private static readonly float[] FallbackLevels = new float[] { 100f, 50f, 25f, 10f };

    private void Awake()
    {
        cam = GetComponent<Camera>();
        currentFov = GetZoomLevel(0);

        if (cam != null) cam.fieldOfView = currentFov;
    }

    private void Update()
    {
        if (cam == null) return;

        // Если зум отключен для этого дрона — плавный сброс к дефолтному FOV
        if (config != null && !config.allowZoom)
        {
            float baseFOV = GetZoomLevel(0);
            currentFov = Mathf.Lerp(currentFov, baseFOV, Time.deltaTime * GetZoomSpeed());
        }
        else
        {
            int maxIndex = GetZoomLevelsCount() - 1;
            int zoomStep = GameInput.ZoomStep;

            // Увеличение кратности (уменьшение FOV)
            if (zoomStep > 0)
            {
                if (currentZoomIndex < maxIndex)
                {
                    currentZoomIndex++;
                }
            }

            // Уменьшение кратности (увеличение FOV)
            if (zoomStep < 0)
            {
                if (currentZoomIndex > 0)
                {
                    currentZoomIndex--;
                }
            }

            // Плавный переход к целевому FOV
            float targetFOV = GetZoomLevel(currentZoomIndex);
            currentFov = Mathf.Lerp(currentFov, targetFOV, Time.deltaTime * GetZoomSpeed());
        }

        cam.fieldOfView = Mathf.Max(1f, currentFov + fovOffset);
    }

    public void SetFovOffset(float offset)
    {
        fovOffset = offset;
    }

    private float GetZoomLevel(int index)
    {
        float reference = GetStoredZoomLevel(0);
        float stored = GetStoredZoomLevel(index);

        if (reference <= 0.001f) return stored;

        return stored * (GetBaseFov() / reference);
    }

    private float GetStoredZoomLevel(int index)
    {
        if (config != null && config.zoomLevels != null && config.zoomLevels.Length > 0)
        {
            int clampedIndex = Mathf.Clamp(index, 0, config.zoomLevels.Length - 1);
            return config.zoomLevels[clampedIndex];
        }

        return FallbackLevels[Mathf.Clamp(index, 0, FallbackLevels.Length - 1)];
    }

    private float GetBaseFov()
    {
        SettingsService service = SettingsService.Instance;
        if (service != null && service.Data != null && service.Data.graphics != null)
            return Mathf.Clamp(service.Data.graphics.droneFov, 30f, 140f);

        return GetStoredZoomLevel(0);
    }

    private int GetZoomLevelsCount()
    {
        if (config != null && config.zoomLevels != null && config.zoomLevels.Length > 0)
        {
            return config.zoomLevels.Length;
        }
        return 4;
    }

    private float GetZoomSpeed()
    {
        return (config != null) ? config.zoomSpeed : 10f;
    }
}