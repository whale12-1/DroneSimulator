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
    public const string RadioParameter = "RadioVolume";

    private const string ResourcePath = "Audio/MainMixer";

    private static bool loadAttempted;
    private static AudioMixer mixer;
    private static AudioMixerGroup masterGroup;
    private static AudioMixerGroup musicGroup;
    private static AudioMixerGroup sfxGroup;
    private static AudioMixerGroup motorsGroup;
    private static AudioMixerGroup ewGroup;
    private static AudioMixerGroup uiGroup;
    private static AudioMixerGroup radioGroup;

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

    // Отдельная шина радиоприёмника бункера (RadioMusic в MainMixer).
    public static AudioMixerGroup Radio
    {
        get
        {
            Load();
            return radioGroup;
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
        radioGroup = null;
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
        radioGroup = FindGroup("RadioMusic");
    }

    private static AudioMixerGroup FindGroup(string groupName)
    {
        if (mixer == null) return null;

        AudioMixerGroup[] groups = mixer.FindMatchingGroups(groupName);
        if (groups == null || groups.Length == 0) return null;

        // FindMatchingGroups ищет по подстроке: "Music" теперь матчит и
        // "RadioMusic". Сначала ищем точное совпадение имени группы.
        for (int i = 0; i < groups.Length; i++)
        {
            if (groups[i] != null && groups[i].name == groupName) return groups[i];
        }

        return groups[0];
    }
}
