using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class NightVisionController : MonoBehaviour
{
    [Header("������� �����")]
    [SerializeField] private DroneConfig config;

    [Header("������")]
    [Tooltip("��-������ (Spotlight), ������� ������ ������� �����")]
    [SerializeField] private Light irSpotlight;
    [Tooltip("��� Full Screen Pass Renderer Feature � URP Asset")]
    [SerializeField] private string rendererFeatureName = "Night Vision Feature";

    public bool isNightVisionActive = false;

    private ScriptableRendererFeature nvFeature;

    private void Awake()
    {
        // 1. ���������� ����� ���� � ������� URP ���������
        var urpAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (urpAsset != null)
        {
            // �������� ������ ���������� (������ ��� ���� PC_Renderer)
            var rendererDataList = typeof(UniversalRenderPipelineAsset)
                .GetField("m_RendererDataList", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .GetValue(urpAsset) as ScriptableRendererData[];

            if (rendererDataList != null && rendererDataList.Length > 0)
            {
                // ���� ���� �� �����
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
            Debug.LogError($"[NV] �� ������� ����� Renderer Feature � ������ '{rendererFeatureName}' � URP Asset!");
        }

        // 2. ��������� ��-������ �� �������
        if (irSpotlight != null && config != null)
        {
            irSpotlight.range = config.irRange;
            irSpotlight.spotAngle = config.irSpotAngle;
            irSpotlight.intensity = config.irIntensity;
            // ��-���� ������� ��� �������� �����, �� �� ������ ��� ������� ��� ���
            irSpotlight.color = Color.white;
        }
    }

    private void Start()
    {
        // �������������� ��������� ��� �� ������
        SetNightVisionState(false);
    }

    private void Update()
    {
        if (Time.timeScale <= 0f) return; // пауза: клавиша N не переключает ПНВ

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

        // ��������/��������� ��� ����-������
        if (nvFeature != null)
        {
            nvFeature.SetActive(active);
        }

        // ��������/��������� ��-������ (�� ����� ��� ��������� �������� �����)
        if (irSpotlight != null)
        {
            irSpotlight.enabled = active;
        }
    }

    // ������������� ������: ���� ���� ����������� (������������� �� ������), 
    // ��� �������������� �����������, ����� �� "�������" �� ������ ������� �����.
    private void OnDisable()
    {
        if (isNightVisionActive)
        {
            SetNightVisionState(false);
        }
    }
}