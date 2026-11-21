using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class NightVisionController : MonoBehaviour
{
    [Header("Профиль Дрона")]
    [SerializeField] private DroneConfig config;

    [Header("Ссылки")]
    [Tooltip("ИК-фонарь (Spotlight), который светит впереди дрона")]
    [SerializeField] private Light irSpotlight;
    [Tooltip("Имя Full Screen Pass Renderer Feature в URP Asset")]
    [SerializeField] private string rendererFeatureName = "Night Vision Feature";

    public bool isNightVisionActive = false;

    private ScriptableRendererFeature nvFeature;

    private void Awake()
    {
        // 1. Безопасный поиск фичи в текущем URP пайплайне
        var urpAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (urpAsset != null)
        {
            // Получаем список рендереров (обычно это один PC_Renderer)
            var rendererDataList = typeof(UniversalRenderPipelineAsset)
                .GetField("m_RendererDataList", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .GetValue(urpAsset) as ScriptableRendererData[];

            if (rendererDataList != null && rendererDataList.Length > 0)
            {
                // Ищем фичу по имени
                foreach (var feature in rendererDataList[0].rendererFeatures)
                {
                    if (feature.name == rendererFeatureName)
                    {
                        nvFeature = feature;
                        break;
                    }
                }
            }
        }

        if (nvFeature == null)
        {
            Debug.LogError($"[NV] Не удалось найти Renderer Feature с именем '{rendererFeatureName}' в URP Asset!");
        }

        // 2. Настройка ИК-фонаря из конфига
        if (irSpotlight != null && config != null)
        {
            irSpotlight.range = config.irRange;
            irSpotlight.spotAngle = config.irSpotAngle;
            irSpotlight.intensity = config.irIntensity;
            // ИК-свет невидим для обычного глаза, но мы делаем его цветным для ПНВ
            irSpotlight.color = Color.white;
        }
    }

    private void Start()
    {
        // Гарантированно выключаем ПНВ на старте
        SetNightVisionState(false);
    }

    private void Update()
    {
        // Если в конфиге ПНВ запрещен, игнорируем ввод
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

        // Включаем/выключаем сам пост-эффект
        if (nvFeature != null)
        {
            nvFeature.SetActive(active);
        }

        // Включаем/выключаем ИК-фонарь (он нужен для реального усиления света)
        if (irSpotlight != null)
        {
            irSpotlight.enabled = active;
        }
    }

    // Архитектурная защита: если дрон выключается (переключаемся на другой), 
    // ПНВ гарантированно выключается, чтобы не "протечь" на камеру другого дрона.
    private void OnDisable()
    {
        if (isNightVisionActive)
        {
            SetNightVisionState(false);
        }
    }
}