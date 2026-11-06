using UnityEngine;
using UnityEngine.Rendering.Universal;

public class EWReceiver : MonoBehaviour
{
    [SerializeField] private DroneConfig config;

    [Range(0f, 1f)] public float jamIntensity = 0f;

    private Camera cam;
    private FPVZoom zoom;
    private ChromaticAberration chromaticAberration;
    private Vignette vignette;
    private FilmGrain filmGrain;

    private Texture2D noiseTexture;
    private Color32[] noisePixels;
    private int noiseResolution;

    private float baseFov;
    private float settingsVisualScale = 1f;
    private float externalVisualScale = 1f;
    private bool postFxAllowed = true;
    private bool dangerousEffectsReduced;

    public float VisualScale => Mathf.Clamp01(settingsVisualScale * externalVisualScale);

    private void Awake()
    {
        cam = GetComponent<Camera>();
        zoom = GetComponent<FPVZoom>();
        if (cam != null) baseFov = cam.fieldOfView;

        PostFxRouter.Ensure().Bind(this);

        RefreshVisualProfile();
        EnsureNoiseTexture();
    }

    public void SetEffects(ChromaticAberration chromatic, Vignette vignetteEffect, FilmGrain grain)
    {
        chromaticAberration = chromatic;
        vignette = vignetteEffect;
        filmGrain = grain;
    }

    public void SetVisualScale(float scale)
    {
        externalVisualScale = Mathf.Clamp01(scale);
    }

    private void Update()
    {
        RefreshVisualProfile();
        CalculateJammingLevel();
        ApplyPostProcessing();
        ApplyFrameJitter();
    }

    private void RefreshVisualProfile()
    {
        SettingsService service = SettingsService.Instance;
        if (service == null || service.Data == null) return;

        DroneSimulator.Settings.GraphicsSettings graphics = service.Data.graphics;
        DroneSimulator.Settings.GameplaySettings gameplay = service.Data.gameplay;

        if (graphics != null)
        {
            settingsVisualScale = Mathf.Clamp01(graphics.ewVisualDistortion);
            postFxAllowed = graphics.ewPostFxQuality > 0;

            int resolution = Mathf.Clamp(graphics.ewNoiseResolution, 32, 512);
            if (resolution != noiseResolution)
            {
                noiseResolution = resolution;
                RebuildNoiseTexture();
            }
        }

        if (gameplay != null) dangerousEffectsReduced = gameplay.reduceDangerousEffects;
    }

    private void EnsureNoiseTexture()
    {
        if (noiseTexture != null) return;

        if (noiseResolution <= 0)
            noiseResolution = config != null ? Mathf.Clamp(config.noiseTextureResolution, 32, 512) : 128;

        RebuildNoiseTexture();
    }

    private void RebuildNoiseTexture()
    {
        if (noiseTexture != null)
        {
            Destroy(noiseTexture);
            noiseTexture = null;
        }

        if (noiseResolution <= 0) return;

        noiseTexture = new Texture2D(noiseResolution, noiseResolution, TextureFormat.RGBA32, false);
        noisePixels = new Color32[noiseResolution * noiseResolution];
    }

    private void CalculateJammingLevel()
    {
        float rawJam = EWService.SampleJamAt(transform.position);
        float combinedJam = Mathf.Max(rawJam, EWService.BoundaryJamIntensity);
        float resistance = config != null ? Mathf.Clamp01(config.ewResistance) : 0f;
        jamIntensity = Mathf.Clamp01(combinedJam * (1f - resistance));
    }

    private void ApplyPostProcessing()
    {
        if (chromaticAberration == null && vignette == null && filmGrain == null) return;

        float scale = postFxAllowed ? VisualScale : 0f;
        bool enabled = scale > 0.001f && jamIntensity > 0.001f;

        float maxChromatic = config != null ? config.maxChromaticAberration : 1f;
        float maxVignette = config != null ? config.maxVignette : 0.8f;
        float maxGrain = config != null ? config.maxFilmGrain : 1f;

        if (chromaticAberration != null)
        {
            chromaticAberration.active = enabled;
            chromaticAberration.intensity.overrideState = enabled;
            chromaticAberration.intensity.value = maxChromatic * scale * jamIntensity;
        }

        if (vignette != null)
        {
            vignette.active = enabled;
            vignette.intensity.overrideState = enabled;
            vignette.intensity.value = maxVignette * scale * jamIntensity;
        }

        if (filmGrain != null)
        {
            filmGrain.active = enabled;
            filmGrain.intensity.overrideState = enabled;
            filmGrain.intensity.value = maxGrain * scale * jamIntensity;
        }
    }

    private void ApplyFrameJitter()
    {
        float threshold = config != null ? config.ewJitterThreshold : 0.15f;
        bool active = cam != null && VisualScale > 0.001f && jamIntensity >= threshold;

        if (!active)
        {
            ResetFrameJitter();
            return;
        }

        float roll = (Random.value - 0.5f) * 0.08f * jamIntensity;
        cam.rect = new Rect(0f, roll, 1f, 1f);

        float offset = Random.value < jamIntensity * 0.3f
            ? Random.Range(-5f, 5f) * jamIntensity
            : 0f;

        if (zoom != null) zoom.SetFovOffset(offset);
        else cam.fieldOfView = baseFov + offset;
    }

    private void ResetFrameJitter()
    {
        if (cam != null && cam.rect.y != 0f) cam.rect = new Rect(0f, 0f, 1f, 1f);
        if (zoom != null) zoom.SetFovOffset(0f);
    }

    private void OnGUI()
    {
        if (SceneLoader.Instance != null && SceneLoader.Instance.IsLoading) return;
        if (MissionUIManager.IsHudHidden) return;
        if (dangerousEffectsReduced) return;

        float scale = VisualScale;
        if (scale <= 0.001f) return;

        float noiseThreshold = config != null ? config.ewNoiseThreshold : 0.05f;
        if (jamIntensity < noiseThreshold) return;

        EnsureNoiseTexture();
        GenerateNoiseTexture();

        float noiseOpacity = config != null ? config.staticNoiseOpacity : 0.85f;
        GUI.color = new Color(1f, 1f, 1f, jamIntensity * noiseOpacity * scale);
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), noiseTexture);

        float lineTearingThreshold = config != null ? config.ewLineTearingThreshold : 0.3f;
        if (jamIntensity <= lineTearingThreshold) return;

        int linesCount = Random.Range(1, Mathf.RoundToInt(jamIntensity * 8f));

        for (int i = 0; i < linesCount; i++)
        {
            float lineY = Random.Range(0f, Screen.height);
            float lineHeight = Random.Range(2f, 18f) * jamIntensity;
            GUI.color = Random.value > 0.5f ? Color.black : Color.white;
            GUI.DrawTexture(new Rect(0f, lineY, Screen.width, lineHeight), Texture2D.whiteTexture);
        }
    }

    private void GenerateNoiseTexture()
    {
        if (noiseTexture == null || noisePixels == null) return;

        for (int i = 0; i < noisePixels.Length; i++)
        {
            byte value = (byte)Random.Range(0, 256);
            noisePixels[i] = new Color32(value, value, value, value);
        }

        noiseTexture.SetPixels32(noisePixels);
        noiseTexture.Apply();
    }
}
