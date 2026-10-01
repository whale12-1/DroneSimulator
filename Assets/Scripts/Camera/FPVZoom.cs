using UnityEngine;

public class FPVZoom : MonoBehaviour
{
    [Header("Профиль Дрона")]
    [SerializeField] private DroneConfig config;

    private Camera cam;
    private int currentZoomIndex = 0;

    private void Awake()
    {
        cam = GetComponent<Camera>();

        if (cam != null)
        {
            float defaultFOV = GetZoomLevel(0);
            cam.fieldOfView = defaultFOV;
        }
    }

    private void Update()
    {
        if (cam == null) return;

        // Если зум отключен для этого дрона — плавный сброс к дефолтному FOV
        if (config != null && !config.allowZoom)
        {
            float baseFOV = GetZoomLevel(0);
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, baseFOV, Time.deltaTime * GetZoomSpeed());
            return;
        }

        KeyCode zoomInKey = (config != null) ? config.zoomInKey : KeyCode.Z;
        KeyCode zoomOutKey = (config != null) ? config.zoomOutKey : KeyCode.X;

        int maxIndex = GetZoomLevelsCount() - 1;

        // Увеличение кратности (уменьшение FOV)
        if (Input.GetKeyDown(zoomInKey))
        {
            if (currentZoomIndex < maxIndex)
            {
                currentZoomIndex++;
            }
        }

        // Уменьшение кратности (увеличение FOV)
        if (Input.GetKeyDown(zoomOutKey))
        {
            if (currentZoomIndex > 0)
            {
                currentZoomIndex--;
            }
        }

        // Плавный переход к целевому FOV
        float targetFOV = GetZoomLevel(currentZoomIndex);
        cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFOV, Time.deltaTime * GetZoomSpeed());
    }

    private float GetZoomLevel(int index)
    {
        if (config != null && config.zoomLevels != null && config.zoomLevels.Length > 0)
        {
            int clampedIndex = Mathf.Clamp(index, 0, config.zoomLevels.Length - 1);
            return config.zoomLevels[clampedIndex];
        }

        // Дефолтные значения при отсутствии конфига
        float[] fallbackLevels = new float[] { 100f, 50f, 25f, 10f };
        return fallbackLevels[Mathf.Clamp(index, 0, fallbackLevels.Length - 1)];
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