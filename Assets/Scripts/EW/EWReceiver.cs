using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class EWReceiver : MonoBehaviour
{
    [SerializeField] private DroneConfig config;
    [SerializeField] private Volume globalVolume;

    [Range(0f, 1f)] public float jamIntensity = 0f;

    private Camera cam;
    private ChromaticAberration chromaticAberration;
    private Vignette vignette;
    private FilmGrain filmGrain;

    private Texture2D noiseTexture;
    private Color32[] noisePixels;
    private float baseChromatic, baseVignette, baseGrain, baseFOV;

    private void Awake()
    {
        cam = GetComponent<Camera>();
        if (cam != null) baseFOV = cam.fieldOfView;

        if (globalVolume == null) globalVolume = FindFirstObjectByType<Volume>();

        if (globalVolume != null && globalVolume.profile != null)
        {
            if (globalVolume.profile.TryGet(out chromaticAberration)) baseChromatic = chromaticAberration.intensity.value;
            if (globalVolume.profile.TryGet(out vignette)) baseVignette    = vignette.intensity.value;
            if (globalVolume.profile.TryGet(out filmGrain)) baseGrain       = filmGrain.intensity.value;
        }

        int res = config != null ? config.noiseTextureResolution : 128;
        noiseTexture = new Texture2D(res, res, TextureFormat.RGBA32, false);
        noisePixels = new Color32[res * res];
    }

    private void Update()
    {
        CalculateJammingLevel();
        ApplyPostProcessing();
        ApplyFrameJitter();
    }

    private void CalculateJammingLevel()
    {
        float rawJam = EWService.SampleJamAt(transform.position);
        float resistance = config != null ? Mathf.Clamp01(config.ewResistance) : 0f;
        jamIntensity = Mathf.Clamp01(rawJam * (1f - resistance));
    }

    private void ApplyPostProcessing()
    {
        float maxChromatic = config != null ? config.maxChromaticAberration : 1.0f;
        float maxVignette = config != null ? config.maxVignette : 0.8f;
        float maxGrain = config != null ? config.maxFilmGrain : 1.0f;

        if (chromaticAberration != null)
        {
            chromaticAberration.active = true;
            chromaticAberration.intensity.value = Mathf.Lerp(baseChromatic, maxChromatic, jamIntensity);
        }
        if (vignette != null)
        {
            vignette.active = true;
            vignette.intensity.value = Mathf.Lerp(baseVignette, maxVignette, jamIntensity);
        }
        if (filmGrain != null)
        {
            filmGrain.active = true;
            filmGrain.intensity.value = Mathf.Lerp(baseGrain, maxGrain, jamIntensity);
        }
    }

    private void ApplyFrameJitter()
    {
        float jitterThreshold = config != null ? config.ewJitterThreshold : 0.15f;
        if (cam == null || jamIntensity < jitterThreshold) return;

        float verticalRoll = (Random.value - 0.5f) * 0.08f * jamIntensity;
        cam.rect = new Rect(0f, verticalRoll, 1f, 1f);

        cam.fieldOfView = (Random.value < jamIntensity * 0.3f)
            ? baseFOV + Random.Range(-5f, 5f) * jamIntensity
            : baseFOV;
    }

    private void OnGUI()
    {
        float noiseThreshold = config != null ? config.ewNoiseThreshold : 0.05f;
        if (jamIntensity < noiseThreshold) return;

        GenerateNoiseTexture();

        float noiseOpacity = config != null ? config.staticNoiseOpacity : 0.85f;
        GUI.color = new Color(1f, 1f, 1f, jamIntensity * noiseOpacity);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), noiseTexture);

        float lineTearingThreshold = config != null ? config.ewLineTearingThreshold : 0.3f;
        if (jamIntensity > lineTearingThreshold)
        {
            int linesCount = Random.Range(1, Mathf.RoundToInt(jamIntensity * 8));
            for (int i = 0; i < linesCount; i++)
            {
                float lineY = Random.Range(0, Screen.height);
                float lineHeight = Random.Range(2f, 18f) * jamIntensity;
                GUI.color = Random.value > 0.5f ? Color.black : Color.white;
                GUI.DrawTexture(new Rect(0, lineY, Screen.width, lineHeight), Texture2D.whiteTexture);
            }
        }
    }

    private void GenerateNoiseTexture()
    {
        for (int i = 0; i < noisePixels.Length; i++)
        {
            byte v = (byte)Random.Range(0, 256);
            noisePixels[i] = new Color32(v, v, v, v);
        }
        noiseTexture.SetPixels32(noisePixels);
        noiseTexture.Apply();
    }
}