using System.Collections;
using System.IO;
using DroneSimulator.Settings;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SettingsService : MonoBehaviour
{
    public static SettingsService Instance { get; private set; }

    private const int SchemaVersion = 3;
    private const float SaveDebounceSeconds = 0.5f;

    public SettingsData Data { get; private set; }

    private string savePath;
    private Coroutine saveRoutine;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        savePath = Path.Combine(Application.persistentDataPath, "settings.json");
        Data = Load();
        AudioBus.Reset();
        SettingsApplier.ResetScreenCache();
        SettingsScope.Refresh();
        ApplyAll();

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        if (Instance != this) return;

        SceneManager.sceneLoaded -= OnSceneLoaded;
        FlushSave();
        Instance = null;
    }

    private void OnApplicationQuit()
    {
        FlushSave();
    }

    public static void EnsureInstance()
    {
        if (Instance == null)
        {
            GameObject go = new GameObject("SettingsService");
            go.AddComponent<SettingsService>();
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        SettingsScope.Refresh();
        ApplyAll();
    }

    private SettingsData Load()
    {
        if (!File.Exists(savePath))
            return new SettingsData();

        try
        {
            string json = File.ReadAllText(savePath);
            SettingsData loaded = JsonUtility.FromJson<SettingsData>(json);
            if (loaded == null)
                return new SettingsData();

            if (loaded.version < SchemaVersion)
            {
                // Старый файл настроек: поля, добавленные в новой схеме, остаются дефолтными.
                Debug.Log($"[SettingsService] Миграция settings.json: версия {loaded.version} → {SchemaVersion}.");

                if (loaded.version < 3 && loaded.graphics != null)
                    loaded.graphics.windowMode = loaded.graphics.fullscreen ? 0 : 2;

                loaded.version = SchemaVersion;
            }
            else if (loaded.version > SchemaVersion)
            {
                Debug.LogWarning($"[SettingsService] Версия settings.json ({loaded.version}) новее ожидаемой ({SchemaVersion}). Используются значения по умолчанию.");
                return new SettingsData();
            }

            return Normalize(loaded);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[SettingsService] Не удалось прочитать settings.json: {e.Message}. Используются значения по умолчанию.");
            return new SettingsData();
        }
    }

    private static SettingsData Normalize(SettingsData data)
    {
        if (data.graphics == null) data.graphics = new GraphicsSettings();
        if (data.audio == null) data.audio = new DroneSimulator.Settings.AudioSettings();
        if (data.controls == null) data.controls = new ControlSettings();
        if (data.gameplay == null) data.gameplay = new GameplaySettings();
        return data;
    }

    public void MarkDirty()
    {
        if (saveRoutine != null) StopCoroutine(saveRoutine);
        saveRoutine = StartCoroutine(DebouncedSave());
    }

    public void SaveNow()
    {
        FlushSave();
    }

    public void ApplyAll()
    {
        SettingsApplier.Apply(Data);
    }

    public void ApplyInstant()
    {
        SettingsApplier.ApplyInstant(Data);
    }

    public void ApplyHeavy()
    {
        SettingsApplier.ApplyHeavy(Data);
    }

    public void ResetAll()
    {
        Data = new SettingsData();
        ApplyAll();
        MarkDirty();
    }

    public void SetMasterVolume(float v)
    {
        v = Mathf.Clamp01(v);
        Data.masterVolume = v;
        AudioApplier.Apply(Data.audio);
        MarkDirty();
    }

    public void SetSfxVolume(float v)
    {
        v = Mathf.Clamp01(v);
        Data.sfxVolume = v;
        AudioApplier.Apply(Data.audio);
        MarkDirty();
    }

    public void SetQuality(int level)
    {
        int max = Mathf.Max(0, QualitySettings.names.Length - 1);
        level = Mathf.Clamp(level, 0, max);
        Data.qualityLevel = level;
        QualitySettings.SetQualityLevel(level);
        MarkDirty();
    }

    public void SetFullscreen(bool fs)
    {
        Data.fullscreen = fs;
        Screen.fullScreen = fs;
        MarkDirty();
    }

    private IEnumerator DebouncedSave()
    {
        yield return new WaitForSecondsRealtime(SaveDebounceSeconds);
        SaveToDisk();
        saveRoutine = null;
    }

    private void FlushSave()
    {
        if (saveRoutine != null)
        {
            StopCoroutine(saveRoutine);
            saveRoutine = null;
        }

        SaveToDisk();
    }

    private void SaveToDisk()
    {
        try
        {
            if (Data != null) Data.version = SchemaVersion;

            string json = JsonUtility.ToJson(Data, true);
            string tmp = savePath + ".tmp";
            File.WriteAllText(tmp, json);

            if (File.Exists(savePath))
                File.Replace(tmp, savePath, savePath + ".bak");
            else
                File.Move(tmp, savePath);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[SettingsService] Не удалось сохранить settings.json: {e.Message}");
        }
    }
}
