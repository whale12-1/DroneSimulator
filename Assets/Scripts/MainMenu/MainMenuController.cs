using UnityEngine;

public class MainMenuController : MonoBehaviour
{
    [Header("Данные")]
    [SerializeField] private MissionRegistry registry;

    [Header("Панели (наследники UIScreen)")]
    [SerializeField] private UIScreen mainPanel;
    [SerializeField] private MissionSelectScreen missionSelect;
    [SerializeField] private SettingsScreen settings;

    [Header("Отладка")]
    [SerializeField] private bool verboseLogging = true;

    private void Start()
    {
        Log("Start()");
        DumpFields();

        if (GameStateMachine.Current != GameState.MainMenu)
        {
            Log($"Текущее состояние {GameStateMachine.Current} → переход в MainMenu");
            GameStateMachine.TransitionTo(GameState.MainMenu);
        }
        else
        {
            Log("Уже в MainMenu, переход не нужен");
        }

        ShowMainPanel();
    }

    // ------------------ Навигация по панелям ------------------

    private void ShowMainPanel()
    {
        Log("ShowMainPanel()");
        mainPanel?.Show();
        missionSelect?.Hide();
        settings?.Hide();
    }

    // Вызывается с кнопки PlayButton
    public void OnOpenMissions()
    {
        Log("OnOpenMissions() — кнопка нажата");

        if (registry == null)
        {
            Debug.LogError("[MainMenu] registry == NULL. Перетащи MissionRegistry в поле 'Registry'.");
            return;
        }

        if (registry.allMissions == null || registry.allMissions.Length == 0)
        {
            Debug.LogError("[MainMenu] registry.allMissions пустой. Открой MissionRegistry.asset и добавь миссии.");
            return;
        }

        mainPanel?.Hide();

        if (missionSelect != null)
        {
            Log($"Populate: {registry.allMissions.Length} миссий");
            try
            {
                missionSelect.Populate(registry, StartMission);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[MainMenu] Ошибка в Populate: {e}");
                return;
            }

            Log("missionSelect.Show()");
            missionSelect.Show();
        }
        else
        {
            Debug.LogError("[MainMenu] missionSelect == NULL. Перетащи MissionSelectPanel в поле 'Mission Select'.");
            return;
        }

        Log($"CanTransitionTo(MissionSelect) = {GameStateMachine.CanTransitionTo(GameState.MissionSelect)}, current = {GameStateMachine.Current}");
        if (GameStateMachine.CanTransitionTo(GameState.MissionSelect))
            GameStateMachine.TransitionTo(GameState.MissionSelect);
    }

    // Вызывается с кнопки SettingsButton
    public void OnOpenSettings()
    {
        Log("OnOpenSettings() — кнопка нажата");

        if (settings == null)
        {
            Debug.LogError("[MainMenu] settings == NULL. Перетащи SettingsPanel в поле 'Settings'.");
            return;
        }

        mainPanel?.Hide();
        settings.Show();
    }

    public void OnBackFromMissions()
    {
        Log("OnBackFromMissions()");
        missionSelect?.Hide();
        ShowMainPanel();

        if (GameStateMachine.CanTransitionTo(GameState.MainMenu))
            GameStateMachine.TransitionTo(GameState.MainMenu);
    }

    public void OnBackFromSettings()
    {
        Log("OnBackFromSettings()");
        settings?.Hide();
        ShowMainPanel();
    }

    public void OnExit()
    {
        Log("OnExit()");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ------------------ Запуск миссии ------------------

    private void StartMission(MissionDefinition mission)
    {
        Log($"StartMission('{(mission != null ? mission.displayName : "NULL")}')");

        if (mission == null) { Debug.LogWarning("[MainMenu] mission == null"); return; }

        if (string.IsNullOrEmpty(mission.sceneName))
        {
            Debug.LogError($"[MainMenu] У миссии '{mission.displayName}' не задан sceneName.");
            return;
        }

        MissionContext.Begin(mission, mission.defaultDrone);

        if (GameStateMachine.CanTransitionTo(GameState.Loading))
            GameStateMachine.TransitionTo(GameState.Loading);
        else
            Debug.LogWarning($"[MainMenu] Переход в Loading запрещён из {GameStateMachine.Current}");

        if (SceneLoader.Instance != null)
            SceneLoader.Instance.LoadAsync(mission.sceneName);
        else
            Debug.LogError("[MainMenu] SceneLoader.Instance == null.");
    }

    // ------------------ Реакция на смену состояния ------------------

    private void OnEnable()
    {
        GameStateMachine.StateChanged += OnStateChanged;
    }

    private void OnDisable()
    {
        GameStateMachine.StateChanged -= OnStateChanged;
    }

    private void OnStateChanged(GameState prev, GameState next)
    {
        Log($"StateChanged: {prev} → {next}");
        if (next == GameState.MainMenu)
            ShowMainPanel();
    }

    // ------------------ Диагностика ------------------

    private void DumpFields()
    {
        Log($"registry      = {(registry == null ? "NULL ❌" : registry.name + $" ({registry.allMissions?.Length ?? 0} миссий)")}");
        Log($"mainPanel     = {(mainPanel == null ? "NULL ❌" : mainPanel.name)}");
        Log($"missionSelect = {(missionSelect == null ? "NULL ❌" : missionSelect.name)}");
        Log($"settings      = {(settings == null ? "NULL ❌" : settings.name)}");
        Log($"GameStateMachine.Current = {GameStateMachine.Current}");
    }

    private void Log(string msg)
    {
        if (verboseLogging) Debug.Log($"[MainMenu] {msg}", this);
    }

    private void OnValidate()
    {
        // Проверка прямо в редакторе при изменении инспектора
        if (registry == null) Debug.LogWarning("[MainMenu] Поле Registry не назначено.", this);
        if (mainPanel == null) Debug.LogWarning("[MainMenu] Поле Main Panel не назначено.", this);
        if (missionSelect == null) Debug.LogWarning("[MainMenu] Поле Mission Select не назначено.", this);
        if (settings == null) Debug.LogWarning("[MainMenu] Поле Settings не назначено.", this);

        // Проверка CanvasGroup у панелей — часто забывают
        WarnIfNoCanvasGroup(mainPanel);
        WarnIfNoCanvasGroup(missionSelect);
        WarnIfNoCanvasGroup(settings);
    }

    private void WarnIfNoCanvasGroup(MonoBehaviour screen)
    {
        if (screen == null) return;
        if (screen.GetComponent<CanvasGroup>() == null)
            Debug.LogWarning($"[MainMenu] У '{screen.name}' нет компонента CanvasGroup — Show/Hide не сработает.", screen);
    }
}