using UnityEngine;
using UnityEngine.InputSystem;

public class MainMenuController : MonoBehaviour
{
    [Header("Данные")]
    [SerializeField] private MissionRegistry registry;

    [Header("Панели (наследники UIScreen)")]
    [SerializeField] private UIScreen mainPanel;
    [SerializeField] private MissionSelectScreen missionSelect;
    [SerializeField] private SettingsPanel settings;

    [Header("Отладка")]
    [SerializeField] private bool verboseLogging = true;

    private void Start()
    {
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

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

    private void Update()
    {
        if (settings == null || !settings.IsOpen) return;

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            OnBackFromSettings();
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

        MissionContext.ReturnedFromMission = false;

        if (GameStateMachine.CanTransitionTo(GameState.Loading))
            GameStateMachine.TransitionTo(GameState.Loading);
        else
            Debug.LogWarning($"[MainMenu] Переход в Loading запрещён из {GameStateMachine.Current}");

        if (SceneLoader.Instance != null)
            SceneLoader.Instance.LoadAsync("Bunker(main)");
        else
            Debug.LogError("[MainMenu] SceneLoader.Instance == null.");
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
        if (registry == null) Debug.LogWarning("[MainMenu] Поле Registry не назначено.", this);
        if (mainPanel == null) Debug.LogWarning("[MainMenu] Поле Main Panel не назначено.", this);
        if (missionSelect == null) Debug.LogWarning("[MainMenu] Поле Mission Select не назначено.", this);
        if (settings == null) Debug.LogWarning("[MainMenu] Поле Settings не назначено.", this);

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
