using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class MainMenuController : MonoBehaviour
{
    // Срок, в течение которого «Выйти?» на кнопке ждёт подтверждения.
    private const float ExitConfirmSeconds = 3f;

    [Header("Данные")]
    [SerializeField] private MissionRegistry registry;

    [Header("Панели (наследники UIScreen)")]
    [SerializeField] private UIScreen mainPanel;
    [SerializeField] private SettingsPanel settings;

    [Header("Отладка")]
    [SerializeField] private bool verboseLogging = true;

    private TMP_Text exitLabel;
    private string exitLabelDefault = "Выход";
    private float exitConfirmDeadline = -1f;

    private void Start()
    {
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        Log("Start()");
        DumpFields();

        // Клики по кнопкам главного меню (Play / Settings / Exit).
        if (mainPanel != null) UISoundService.Attach(mainPanel.gameObject);

        ResolveExitLabel();

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
        UpdateExitConfirmation();

        Keyboard keyboard = Keyboard.current;

        // Esc отменяет взведённое подтверждение выхода: раньше Esc в меню без
        // открытых настроек не делал ничего.
        if (exitConfirmDeadline > 0f && keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            CancelExitConfirmation();

        if (settings == null || !settings.IsOpen) return;

        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            OnBackFromSettings();
    }

    // ------------------ Навигация по панелям ------------------

    private void ShowMainPanel()
    {
        Log("ShowMainPanel()");
        mainPanel?.Show();
        settings?.Hide();

        // Настройки закрыты — сбрасываем недожатое подтверждение выхода,
        // чтобы кнопка не осталась с текстом «Выйти?».
        CancelExitConfirmation();
    }

    // Вызывается с кнопки PlayButton
    public void OnOpenMissions()
    {
        Log("OnOpenMissions() — кнопка нажата");

        CancelExitConfirmation();
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

    public void OnBackFromSettings()
    {
        Log("OnBackFromSettings()");
        CancelExitConfirmation();

        // Панель сама сообщит о закрытии событием Closed, и главная панель
        // вернётся и по X, и по Esc — без зависимости от persistent-вызова в сцене.
        if (settings != null) settings.Hide();
        else ShowMainPanel();
    }

    // Первый клик взводит подтверждение («Выйти?»), второй в течение 3 с — выход.
    // Одиночный клик раньше сразу завершал приложение (в редакторе — останавливал Play).
    public void OnExit()
    {
        Log("OnExit() — кнопка нажата");

        if (exitConfirmDeadline > 0f && Time.unscaledTime <= exitConfirmDeadline)
        {
            CancelExitConfirmation();
            Log("OnExit() — подтверждено");
            Quit();
            return;
        }

        exitConfirmDeadline = Time.unscaledTime + ExitConfirmSeconds;
        if (exitLabel != null) exitLabel.text = "Выйти?";
    }

    private void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ------------------ Подтверждение выхода ------------------

    private void ResolveExitLabel()
    {
        Button exitButton = FindExitButton();
        if (exitButton == null) return;

        exitLabel = exitButton.GetComponentInChildren<TMP_Text>(true);
        if (exitLabel != null) exitLabelDefault = exitLabel.text;
    }

    private void UpdateExitConfirmation()
    {
        if (exitConfirmDeadline < 0f) return;
        if (Time.unscaledTime < exitConfirmDeadline) return;

        CancelExitConfirmation();
    }

    private void CancelExitConfirmation()
    {
        exitConfirmDeadline = -1f;
        if (exitLabel != null) exitLabel.text = exitLabelDefault;
    }

    // Кнопка выхода не сериализована в контроллере (вызов висит в OnClick),
    // поэтому ищем её по имени среди кнопок главной панели — один раз в Start.
    private Button FindExitButton()
    {
        if (mainPanel == null) return null;

        Button[] buttons = mainPanel.GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i] != null && buttons[i].name == "ExitButton") return buttons[i];
        }

        return null;
    }

    // ------------------ Реакция на смену состояния ------------------

    private void OnEnable()
    {
        GameStateMachine.StateChanged += OnStateChanged;
        if (settings != null) settings.Closed += OnSettingsClosed;
    }

    private void OnDisable()
    {
        GameStateMachine.StateChanged -= OnStateChanged;
        if (settings != null) settings.Closed -= OnSettingsClosed;
    }

    private void OnStateChanged(GameState prev, GameState next)
    {
        Log($"StateChanged: {prev} → {next}");
        if (next == GameState.MainMenu)
            ShowMainPanel();
    }

    // Гарантированный возврат главной панели при закрытии настроек (X или Esc),
    // даже если persistent-вызов на кнопке когда-нибудь потеряется.
    private void OnSettingsClosed()
    {
        Log("Настройки закрыты → ShowMainPanel()");
        ShowMainPanel();
    }

    // ------------------ Диагностика ------------------

    private void DumpFields()
    {
        Log($"registry      = {(registry == null ? "NULL ❌" : registry.name + $" ({registry.allMissions?.Length ?? 0} миссий)")}");
        Log($"mainPanel     = {(mainPanel == null ? "NULL ❌" : mainPanel.name)}");
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
        if (settings == null) Debug.LogWarning("[MainMenu] Поле Settings не назначено.", this);

        WarnIfNoCanvasGroup(mainPanel);
        WarnIfNoCanvasGroup(settings);
    }

    private void WarnIfNoCanvasGroup(MonoBehaviour screen)
    {
        if (screen == null) return;
        if (screen.GetComponent<CanvasGroup>() == null)
            Debug.LogWarning($"[MainMenu] У '{screen.name}' нет компонента CanvasGroup — Show/Hide не сработает.", screen);
    }
}
