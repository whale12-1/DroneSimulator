using System.Collections;
using System.IO;
using DroneSimulator.Settings;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SettingsService : MonoBehaviour
{
    public static SettingsService Instance { get; private set; }

    private const int SchemaVersion = 4;
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
        GameStateMachine.StateChanged += OnGameStateChanged;
    }

    private void OnDestroy()
    {
        if (Instance != this) return;

        SceneManager.sceneLoaded -= OnSceneLoaded;
        GameStateMachine.StateChanged -= OnGameStateChanged;
        FlushSave();
        Instance = null;
    }

    // Громкость музыки зависит от "меню/геймплей" (настройка duckOnMenus),
    // поэтому пересчитываем аудио при каждой смене состояния FSM.
    private void OnGameStateChanged(GameState previous, GameState next)
    {
        if (Data == null) return;
        AudioApplier.Apply(Data.audio);
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

                // v3: windowMode появился как отдельное поле, до этого был только fullscreen.
                if (loaded.version < 3 && loaded.graphics != null)
                    loaded.graphics.windowMode = loaded.graphics.fullscreen ? 0 : 2;

                // v4: разрешение хранится в пикселях, а не индексом в Screen.resolutions.
                if (loaded.version < 4 && loaded.graphics != null)
                {
                    int legacyIndex = loaded.graphics.resolutionIndex;

                    if (loaded.graphics.resolutionWidth <= 0 || loaded.graphics.resolutionHeight <= 0)
                    {
                        Resolution[] unique = legacyIndex >= 0 ? SettingsApplier.GetUniqueResolutions() : null;
                        if (unique != null && legacyIndex < unique.Length)
                        {
                            loaded.graphics.resolutionWidth = unique[legacyIndex].width;
                            loaded.graphics.resolutionHeight = unique[legacyIndex].height;
                        }
                    }

                    loaded.graphics.resolutionIndex = -1;
                }

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

        GraphicsSettings g = data.graphics;

        g.windowMode = Mathf.Clamp(g.windowMode, 0, 2);
        // windowMode — единственный источник истины; fullscreen — производное зеркало.
        g.fullscreen = g.windowMode != 2;

        g.resolutionIndex = -1;
        g.resolutionWidth = Mathf.Max(0, g.resolutionWidth);
        g.resolutionHeight = Mathf.Max(0, g.resolutionHeight);

        int maxQuality = Mathf.Max(0, QualitySettings.names.Length - 1);
        g.qualityLevel = Mathf.Clamp(g.qualityLevel, 0, maxQuality);
        g.fpsLimit = Mathf.Clamp(g.fpsLimit, 0, 1000);
        g.renderScale = Mathf.Clamp(g.renderScale, 0.1f, 1f);

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
        Data.version = SchemaVersion;
        SettingsApplier.ResetScreenCache();
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

        // SetQualityLevel применяет customRenderPipeline уровня (у 0..4 он пуст),
        // поэтому сразу переустанавливаем runtime-клон URP-ассета.
        ApplyHeavy();
        MarkDirty();
    }

    public void SetFullscreen(bool fs)
    {
        // windowMode — единственный источник истины (0 = полный экран, 2 = окно).
        Data.graphics.windowMode = fs ? 0 : 2;
        Data.graphics.fullscreen = fs;
        ApplyHeavy();
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
