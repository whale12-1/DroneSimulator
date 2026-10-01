using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class NightVisionController : MonoBehaviour
{
    [Header("Профиль Дрона")]
    [SerializeField] private DroneConfig config;

    [Header("Ссылки")]
    [SerializeField] private Volume globalVolume;
    [SerializeField] private Light irSpotlight;

    public bool isNightVisionActive = false;
    private ColorAdjustments colorAdjustments;

    private void Awake()
    {
        // Авто-поиск Global Volume, если поле пустое
        if (globalVolume == null)
        {
            globalVolume = FindFirstObjectByType<Volume>();
        }

        // Авто-создание ИК-фонаря, если он не назначен
        if (irSpotlight == null)
        {
            GameObject lightObj = new GameObject("IR_Spotlight");
            lightObj.transform.SetParent(transform, false);
            irSpotlight = lightObj.AddComponent<Light>();
            irSpotlight.type = LightType.Spot;
        }

        // Применяем параметры ИК-подсветки из конфига
        if (irSpotlight != null && config != null)
        {
            irSpotlight.range = config.irRange;
            irSpotlight.spotAngle = config.irSpotAngle;
            irSpotlight.intensity = config.irIntensity;
        }

        // Получаем или создаем компонент ColorAdjustments в профиле
        if (globalVolume != null && globalVolume.profile != null)
        {
            if (!globalVolume.profile.TryGet(out colorAdjustments))
            {
                colorAdjustments = globalVolume.profile.Add<ColorAdjustments>(true);
            }
        }

        SetNightVisionState(false);
    }

    private void Update()
    {
        // Если ПНВ отключен в конфиге текущего дрона — выключаем и игнорируем ввод
        if (config != null && !config.hasNightVision)
        {
            if (isNightVisionActive) SetNightVisionState(false);
            return;
        }

        KeyCode toggleKey = (config != null) ? config.nvToggleKey : KeyCode.N;

        if (Input.GetKeyDown(toggleKey))
        {
            SetNightVisionState(!isNightVisionActive);
        }
    }

    private void SetNightVisionState(bool active)
    {
        isNightVisionActive = active;

        if (colorAdjustments != null)
        {
            colorAdjustments.active = active;
            colorAdjustments.colorFilter.overrideState = active;
            colorAdjustments.colorFilter.value = (config != null) ? config.nvColor : new Color(0.1f, 1.0f, 0.2f);

            colorAdjustments.postExposure.overrideState = active;
            colorAdjustments.postExposure.value = (config != null) ? config.nvPostExposure : 1.8f;
        }

        if (irSpotlight != null)
        {
            irSpotlight.enabled = active;
        }
    }
}