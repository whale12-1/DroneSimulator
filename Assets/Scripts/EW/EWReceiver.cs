using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class EWReceiver : MonoBehaviour
{
    [Header("Ссылки")]
    [SerializeField] private Volume globalVolume;

    [Header("Текущий уровень помех (0.0 - 1.0)")]
    [Range(0f, 1f)] public float jamIntensity = 0f;

    [Header("Настройки Аналогового Снега")]
    [SerializeField] private int noiseTextureResolution = 128;
    [SerializeField] private float staticNoiseOpacity = 0.85f;

    private Camera cam;
    private ChromaticAberration chromaticAberration;
    private Vignette vignette;
    private FilmGrain filmGrain;

    private Texture2D noiseTexture;
    private Color32[] noisePixels;
    private float baseChromatic = 0f;
    private float baseVignette = 0f;
    private float baseGrain = 0f;
    private float baseFOV;

    private void Awake()
    {
        cam = GetComponent<Camera>();
        if (cam != null) baseFOV = cam.fieldOfView;

        if (globalVolume == null) globalVolume = FindFirstObjectByType<Volume>();

        if (globalVolume != null && globalVolume.profile != null)
        {
            if (globalVolume.profile.TryGet(out chromaticAberration)) baseChromatic = chromaticAberration.intensity.value;
            if (globalVolume.profile.TryGet(out vignette)) baseVignette = vignette.intensity.value;
            if (globalVolume.profile.TryGet(out filmGrain)) baseGrain = filmGrain.intensity.value;
        }

        // Генерация текстуры для белого шума ("снега")
        noiseTexture = new Texture2D(noiseTextureResolution, noiseTextureResolution, TextureFormat.RGBA32, false);
        noisePixels = new Color32[noiseTextureResolution * noiseTextureResolution];
    }

    private void Update()
    {
        CalculateJammingLevel();
        ApplyPostProcessing();
        ApplyFrameJitter();
    }

    private void CalculateJammingLevel()
    {
        EWEmitter[] emitters = FindObjectsByType<EWEmitter>(FindObjectsSortMode.None);
        float maxJam = 0f;

        foreach (var emitter in emitters)
        {
            if (emitter == null || !emitter.isActive) continue;

            float distance = Vector3.Distance(transform.position, emitter.transform.position);
            if (distance < emitter.jamRadius)
            {
                float factor = 1f - (distance / emitter.jamRadius);
                if (factor > maxJam) maxJam = factor;
            }
        }

        jamIntensity = maxJam;
    }

    private void ApplyPostProcessing()
    {
        if (chromaticAberration != null)
        {
            chromaticAberration.active = true;
            // Дикий разрыв RGB-каналов при сильном РЭБ
            chromaticAberration.intensity.value = Mathf.Lerp(baseChromatic, 1.0f, jamIntensity);
        }

        if (vignette != null)
        {
            vignette.active = true;
            vignette.intensity.value = Mathf.Lerp(baseVignette, 0.8f, jamIntensity);
        }

        if (filmGrain != null)
        {
            filmGrain.active = true;
            filmGrain.intensity.value = Mathf.Lerp(baseGrain, 1.0f, jamIntensity);
        }
    }

    private void ApplyFrameJitter()
    {
        if (cam == null || jamIntensity < 0.15f) return;

        // 1. Дёрганье кадра по вертикали (срыв H-Sync)
        float verticalRoll = (Random.value - 0.5f) * 0.08f * jamIntensity;
        cam.rect = new Rect(0f, verticalRoll, 1f, 1f);

        // 2. Скачки FOV (имитация мгновенной потери синхронизации развертки)
        if (Random.value < jamIntensity * 0.3f)
        {
            cam.fieldOfView = baseFOV + Random.Range(-5f, 5f) * jamIntensity;
        }
        else
        {
            cam.fieldOfView = baseFOV;
        }
    }

    private void OnGUI()
    {
        if (jamIntensity < 0.05f) return;

        // Генерация быстрого монохромного шума ("снег")
        GenerateNoiseTexture();

        // Отрисовка снега поверх экрана
        GUI.color = new Color(1f, 1f, 1f, jamIntensity * staticNoiseOpacity);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), noiseTexture);

        // Отрисовка черных и белых горизонтальных полос разрыва (Line Tearing)
        if (jamIntensity > 0.3f)
        {
            int linesCount = Random.Range(1, Mathf.RoundToInt(jamIntensity * 8));
            for (int i = 0; i < linesCount; i++)
            {
                float lineY = Random.Range(0, Screen.height);
                float lineHeight = Random.Range(2f, 18f) * jamIntensity;
                GUI.color = (Random.value > 0.5f) ? Color.black : Color.white;
                GUI.DrawTexture(new Rect(0, lineY, Screen.width, lineHeight), Texture2D.whiteTexture);
            }
        }
    }

    private void GenerateNoiseTexture()
    {
        for (int i = 0; i < noisePixels.Length; i++)
        {
            byte val = (byte)Random.Range(0, 256);
            noisePixels[i] = new Color32(val, val, val, val);
        }
        noiseTexture.SetPixels32(noisePixels);
        noiseTexture.Apply();
    }
}