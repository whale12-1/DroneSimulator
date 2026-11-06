using UnityEngine;
using UnityEngine.Audio;

public static class AudioBus
{
    public const string MasterParameter = "MasterVolume";
    public const string MusicParameter = "MusicVolume";
    public const string SfxParameter = "SfxVolume";
    public const string MotorsParameter = "MotorsVolume";
    public const string EwParameter = "EwVolume";
    public const string UiParameter = "UiVolume";
    public const string VoiceParameter = "VoiceVolume";

    private const string ResourcePath = "Audio/MainMixer";

    private static bool loadAttempted;
    private static AudioMixer mixer;
    private static AudioMixerGroup masterGroup;
    private static AudioMixerGroup musicGroup;
    private static AudioMixerGroup sfxGroup;
    private static AudioMixerGroup motorsGroup;
    private static AudioMixerGroup ewGroup;
    private static AudioMixerGroup uiGroup;

    public static AudioMixer Mixer
    {
        get
        {
            Load();
            return mixer;
        }
    }

    public static AudioMixerGroup Master
    {
        get
        {
            Load();
            return masterGroup;
        }
    }

    public static AudioMixerGroup Music
    {
        get
        {
            Load();
            return musicGroup;
        }
    }

    public static AudioMixerGroup Sfx
    {
        get
        {
            Load();
            return sfxGroup;
        }
    }

    public static AudioMixerGroup Motors
    {
        get
        {
            Load();
            return motorsGroup;
        }
    }

    public static AudioMixerGroup EW
    {
        get
        {
            Load();
            return ewGroup;
        }
    }

    public static AudioMixerGroup UI
    {
        get
        {
            Load();
            return uiGroup;
        }
    }

    public static bool Ready
    {
        get
        {
            Load();
            return mixer != null;
        }
    }

    public static bool SetVolume(string parameter, float linear)
    {
        Load();
        if (mixer == null || string.IsNullOrEmpty(parameter)) return false;

        float clamped = Mathf.Clamp01(linear);
        float decibels = clamped <= 0.0001f ? -80f : 20f * Mathf.Log10(clamped);
        return mixer.SetFloat(parameter, decibels);
    }

    public static void Reset()
    {
        loadAttempted = false;
        mixer = null;
        masterGroup = null;
        musicGroup = null;
        sfxGroup = null;
        motorsGroup = null;
        ewGroup = null;
        uiGroup = null;
    }

    private static void Load()
    {
        if (loadAttempted) return;
        loadAttempted = true;

        mixer = Resources.Load<AudioMixer>(ResourcePath);
        if (mixer == null)
        {
            Debug.LogWarning("[AudioBus] Не найден Assets/Resources/Audio/MainMixer.mixer. Ползунки категорий не действуют, общая громкость идёт через AudioListener.volume.");
            return;
        }

        masterGroup = FindGroup("Master");
        musicGroup = FindGroup("Music");
        sfxGroup = FindGroup("SFX");
        motorsGroup = FindGroup("DroneMotors");
        ewGroup = FindGroup("EW");
        uiGroup = FindGroup("UI");
    }

    private static AudioMixerGroup FindGroup(string groupName)
    {
        if (mixer == null) return null;

        AudioMixerGroup[] groups = mixer.FindMatchingGroups(groupName);
        return groups != null && groups.Length > 0 ? groups[0] : null;
    }
}
