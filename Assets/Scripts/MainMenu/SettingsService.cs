using UnityEngine;
using System.IO;

[System.Serializable]
public class SettingsData
{
    public float masterVolume = 1f;
    public float sfxVolume = 1f;
    public int qualityLevel = 2;
    public bool fullscreen = true;
}

public class SettingsService : MonoBehaviour
{
    public static SettingsService Instance { get; private set; }

    public SettingsData Data { get; private set; }

    private string Path => System.IO.Path.Combine(Application.persistentDataPath, "settings.json");

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        Data = File.Exists(Path)
            ? JsonUtility.FromJson<SettingsData>(File.ReadAllText(Path)) ?? new SettingsData()
            : new SettingsData();

        ApplyAll();
    }

    public void SetMasterVolume(float v) { Data.masterVolume = v; AudioListener.volume = v; Save(); }
    public void SetSfxVolume(float v) { Data.sfxVolume = v; /* хранить в AudioMixer, если есть */ Save(); }
    public void SetQuality(int level) { Data.qualityLevel = level; QualitySettings.SetQualityLevel(level); Save(); }
    public void SetFullscreen(bool fs) { Data.fullscreen = fs; Screen.fullScreen = fs; Save(); }

    private void ApplyAll()
    {
        AudioListener.volume = Data.masterVolume;
        QualitySettings.SetQualityLevel(Data.qualityLevel);
        Screen.fullScreen = Data.fullscreen;
    }

    private void Save() => File.WriteAllText(Path, JsonUtility.ToJson(Data, true));
}