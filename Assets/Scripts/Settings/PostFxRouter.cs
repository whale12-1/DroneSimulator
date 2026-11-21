using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[DefaultExecutionOrder(-100)]
public class PostFxRouter : MonoBehaviour
{
    private const string ObjectName = "PostFxRouter";
    private const float EwPriority = 10f;
    private const float NvPriority = 20f;
    private const float SettingsPriority = 30f;

    // Плотность эффекта при включённой настройке (раньше значение жёстко
    // затиралось нулём, из-за чего Motion Blur не был виден никогда).
    private const float MotionBlurIntensity = 0.6f;

    public static PostFxRouter Instance { get; private set; }

    private ChromaticAberration ewChromaticAberration;
    private Vignette ewVignette;
    private FilmGrain ewFilmGrain;
    private ColorAdjustments nvColorAdjustments;
    private MotionBlur settingsMotionBlur;

    public static PostFxRouter Ensure()
    {
        if (Instance != null) return Instance;

        PostFxRouter existing = FindAnyObjectByType<PostFxRouter>(FindObjectsInactive.Include);
        if (existing != null) return existing;

        GameObject host = new GameObject(ObjectName);
        DontDestroyOnLoad(host);
        return host.AddComponent<PostFxRouter>();
    }

    public void Bind(EWReceiver receiver)
    {
        if (receiver == null) return;
        receiver.SetEffects(ewChromaticAberration, ewVignette, ewFilmGrain);
    }

    public void Bind(NightVisionController controller)
    {
        if (controller == null) return;
        //controller.SetColorAdjustments(nvColorAdjustments);
    }

    public void ApplyMotionBlur(bool enabled)
    {
        if (settingsMotionBlur == null) return;

        settingsMotionBlur.intensity.overrideState = true;
        settingsMotionBlur.intensity.value = enabled ? MotionBlurIntensity : 0f;
        settingsMotionBlur.active = enabled;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        BuildProfiles();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void BuildProfiles()
    {
        VolumeProfile ewProfile = CreateProfile("PostFX_EW_Runtime");
        ewChromaticAberration = ewProfile.Add<ChromaticAberration>(true);
        ewVignette = ewProfile.Add<Vignette>(true);
        ewFilmGrain = ewProfile.Add<FilmGrain>(true);
        CreateVolume("PostFX_EW", ewProfile, EwPriority);

        VolumeProfile nvProfile = CreateProfile("PostFX_NV_Runtime");
        nvColorAdjustments = nvProfile.Add<ColorAdjustments>(true);
        CreateVolume("PostFX_NV", nvProfile, NvPriority);

        VolumeProfile settingsProfile = CreateProfile("PostFX_Settings_Runtime");
        settingsMotionBlur = settingsProfile.Add<MotionBlur>(true);
        CreateVolume("PostFX_Settings", settingsProfile, SettingsPriority);
    }

    private static VolumeProfile CreateProfile(string profileName)
    {
        VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
        profile.name = profileName;
        return profile;
    }

    private void CreateVolume(string volumeName, VolumeProfile profile, float priority)
    {
        GameObject volumeObject = new GameObject(volumeName);
        volumeObject.transform.SetParent(transform, false);

        Volume volume = volumeObject.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = priority;
        volume.weight = 1f;
        volume.sharedProfile = profile;
    }
}
