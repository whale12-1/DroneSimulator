using UnityEngine;
using UnityEngine.Rendering.Universal;

public class NightVisionController : MonoBehaviour
{
    [SerializeField] private DroneConfig config;
    [SerializeField] private Light irSpotlight;

    public bool isNightVisionActive = false;

    private ColorAdjustments colorAdjustments;
    private bool applyPending;

    private void Awake()
    {
        if (irSpotlight == null)
        {
            GameObject lightObject = new GameObject("IR_Spotlight");
            lightObject.transform.SetParent(transform, false);
            irSpotlight = lightObject.AddComponent<Light>();
            irSpotlight.type = LightType.Spot;
        }

        if (irSpotlight != null && config != null)
        {
            irSpotlight.range = config.irRange;
            irSpotlight.spotAngle = config.irSpotAngle;
            irSpotlight.intensity = config.irIntensity;
        }

        PostFxRouter.Ensure().Bind(this);
        SetNightVisionState(false);
    }

    public void SetColorAdjustments(ColorAdjustments adjustments)
    {
        colorAdjustments = adjustments;
        applyPending = true;
    }

    private void Update()
    {
        if (config != null && !config.hasNightVision)
        {
            if (isNightVisionActive) SetNightVisionState(false);
            return;
        }

        KeyCode toggleKey = (config != null) ? config.nvToggleKey : KeyCode.N;

        if (Input.GetKeyDown(toggleKey)) SetNightVisionState(!isNightVisionActive);

        if (isNightVisionActive || applyPending) ApplyLook();
    }

    private void SetNightVisionState(bool active)
    {
        isNightVisionActive = active;

        if (irSpotlight != null) irSpotlight.enabled = active;

        ApplyLook();
    }

    private void ApplyLook()
    {
        applyPending = false;

        if (colorAdjustments == null) return;

        colorAdjustments.active = isNightVisionActive;
        colorAdjustments.colorFilter.overrideState = isNightVisionActive;
        colorAdjustments.colorFilter.value = ResolveColor();
        colorAdjustments.postExposure.overrideState = isNightVisionActive;
        colorAdjustments.postExposure.value = ResolveExposure();
    }

    private Color ResolveColor()
    {
        SettingsService service = SettingsService.Instance;
        if (service != null && service.Data != null && service.Data.graphics != null)
        {
            switch (service.Data.graphics.nightVisionColor)
            {
                case 1:
                    return new Color(0.92f, 0.95f, 1f);
                case 2:
                    return new Color(1f, 0.72f, 0.32f);
                case 0:
                    return new Color(0.1f, 1f, 0.2f);
            }
        }

        return config != null ? config.nvColor : new Color(0.1f, 1f, 0.2f);
    }

    private float ResolveExposure()
    {
        float baseExposure = config != null ? config.nvPostExposure : 1.8f;

        SettingsService service = SettingsService.Instance;
        if (service == null || service.Data == null || service.Data.graphics == null) return baseExposure;

        return baseExposure * Mathf.Clamp(service.Data.graphics.nightVisionGain, 0.1f, 5f);
    }

    private void OnEnable()
    {
        SetNightVisionState(false);
    }

    private void OnDisable()
    {
        if (isNightVisionActive) SetNightVisionState(false);
        else ApplyLook();
    }
}
