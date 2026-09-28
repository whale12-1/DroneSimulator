using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class NightVisionController : MonoBehaviour
{
    [Header("Ссылки")]
    [SerializeField] private Volume globalVolume;
    [SerializeField] private Light irSpotlight;

    [Header("Клавиша Включения")]
    [SerializeField] private KeyCode toggleKey = KeyCode.N;

    public bool isNightVisionActive = false;
    private ColorAdjustments colorAdjustments;

    private void Awake()
    {
        // Авто-поиск Global Volume, если поле пустое
        if (globalVolume == null)
        {
            globalVolume = FindAnyObjectByType<Volume>();
        }

        // Авто-создание ИК-фонаря, если он не назначен
        if (irSpotlight == null)
        {
            GameObject lightObj = new GameObject("IR_Spotlight");
            lightObj.transform.SetParent(transform, false);
            irSpotlight = lightObj.AddComponent<Light>();
            irSpotlight.type = LightType.Spot;
            irSpotlight.range = 300f;
            irSpotlight.spotAngle = 60f;
            irSpotlight.intensity = 5f;
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
            colorAdjustments.colorFilter.value = new Color(0.1f, 1.0f, 0.2f); // Зелёный спектр ПНВ
            colorAdjustments.postExposure.overrideState = active;
            colorAdjustments.postExposure.value = 1.8f;
        }

        if (irSpotlight != null)
        {
            irSpotlight.enabled = active;
        }
    }
}