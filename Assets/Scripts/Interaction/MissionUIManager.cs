using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class MissionUIManager : MonoBehaviour
{
    public static bool IsHudHidden { get; private set; }

    [SerializeField] private GameObject instructionText;
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private GameObject crashPanel;
    [SerializeField] private GameObject fallbackCamera;
    [SerializeField] private SettingsPanel settingsPanel;
    [SerializeField] private Button settingsButton;

    private void OnEnable()
    {
        IsHudHidden = false;
        GameStateMachine.StateChanged += OnStateChanged;
    }

    private void OnDisable() => GameStateMachine.StateChanged -= OnStateChanged;

    private void Start()
    {
        // Все кнопки миссии подписываются кодом (persistent-вызовы не дублируются).
        // Поиск рекурсивный: кнопки лежат внутри Plate модального окна.
        WireNamedButton(pausePanel, "continue", ResumeGame);
        WireNamedButton(pausePanel, "settings", OpenSettings);
        WireNamedButton(pausePanel, "exit", ReturnToBunker);

        WireNamedButton(crashPanel, "return", RestartMission);
        WireNamedButton(crashPanel, "exit", ReturnToBunker);

        if (settingsButton == null) settingsButton = FindButtonDeep(pausePanel, "settings");

        if (pausePanel != null) UISoundService.Attach(pausePanel);
        if (crashPanel != null) UISoundService.Attach(crashPanel);
        if (settingsPanel != null) settingsPanel.Closed += OnSettingsClosed;
    }

    private static void WireNamedButton(GameObject panel, string buttonName, UnityEngine.Events.UnityAction action)
    {
        Button button = FindButtonDeep(panel, buttonName);
        if (button == null) return;

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(action);
    }

    private static Button FindButtonDeep(GameObject panel, string buttonName)
    {
        if (panel == null) return null;

        foreach (Button button in panel.GetComponentsInChildren<Button>(true))
            if (button != null && button.name == buttonName) return button;

        return null;
    }

    private void OnDestroy()
    {
        if (settingsPanel != null) settingsPanel.Closed -= OnSettingsClosed;
    }

    private void OnSettingsClosed()
    {
        // Настройки закрылись (X) — возвращаемся в паузу, время остаётся остановленным.
        if (pausePanel != null && pausePanel.activeSelf) ApplyPausedState();
    }


    private void OnStateChanged(GameState prev, GameState next)
    {
        if (next == GameState.Debrief)
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
            SetHudVisible(false);
            if (pausePanel != null) pausePanel.SetActive(false);
            if (crashPanel != null) crashPanel.SetActive(false);
            if (settingsPanel != null && settingsPanel.IsOpen) settingsPanel.Hide();
        }
    }

    private void Update()
    {
        if (GameStateMachine.Current == GameState.Debrief)
            return;

        if (Keyboard.current == null)
            return;

        if (!Keyboard.current.escapeKey.wasPressedThisFrame)
            return;

        HandleEscapePressed();
    }

    /// <summary>
    /// Единая обработка Esc в миссии. Приоритет: настройки → пауза → игра.
    /// Вынесено из Update отдельным методом, чтобы поведение можно было
    /// проверить без эмуляции устройства ввода.
    /// </summary>
    public void HandleEscapePressed()
    {
        if (crashPanel != null && crashPanel.activeSelf)
            return;

        if (settingsPanel != null && settingsPanel.IsOpen)
        {
            settingsPanel.Hide();
            return;
        }

        if (pausePanel != null && pausePanel.activeSelf)
        {
            ResumeGame();
            return;
        }

        TogglePause();
    }

    /// <summary>
    /// Модальное окно «все борта потеряны». Возвращает true только если панель
    /// реально показана: без назначенного crashPanel метод ничего не морозит,
    /// иначе игра замирала на статичном кадре без единой кнопки.
    /// </summary>
    public bool ShowCrashPanel()
    {
        if (GameStateMachine.Current == GameState.Debrief)
            return false;

        if (crashPanel == null)
        {
            Debug.LogWarning("[MissionUIManager] crashPanel не назначен — панель краха пропущена. Завершение миссии выполнит MissionController (Debrief).", this);
            return false;
        }

        SetHudVisible(false);

        crashPanel.SetActive(true);

        if (pausePanel != null)
            pausePanel.SetActive(false);

        if (fallbackCamera != null) fallbackCamera.SetActive(true);

        Time.timeScale = 0f;
        AudioListener.pause = true;
        // Confined, а не None: DroneFlightController.Update() по ЛКМ захватывает
        // курсор только при lockState == None (см. LateUpdate ниже).
        Cursor.lockState = CursorLockMode.Confined;
        Cursor.visible = true;
        return true;
    }

    public void ResumeGame()
    {
        if (settingsPanel != null && settingsPanel.IsOpen) settingsPanel.Hide();

        SetHudVisible(true);

        if (pausePanel != null)
            pausePanel.SetActive(false);

        if (fallbackCamera != null)
            fallbackCamera.SetActive(false);

        Time.timeScale = 1f;
        AudioListener.pause = false;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (GameStateMachine.CanTransitionTo(GameState.Gameplay))
            GameStateMachine.TransitionTo(GameState.Gameplay);
    }

    public void RestartMission()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // Рестарт должен перезапускать текущую миссию, а не всегда обучение.
        string sceneName = MissionContext.Current != null && !string.IsNullOrEmpty(MissionContext.Current.sceneName)
            ? MissionContext.Current.sceneName
            : "PreparingDemo";

        if (GameStateMachine.CanTransitionTo(GameState.Loading))
            GameStateMachine.TransitionTo(GameState.Loading);

        SceneLoader.EnsureInstance();
        if (SceneLoader.Instance != null)
            SceneLoader.Instance.LoadAsync(sceneName);
    }

    public void ReturnToMainMenu()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
        if (settingsPanel != null && settingsPanel.IsOpen) settingsPanel.Hide();

        if (GameStateMachine.CanTransitionTo(GameState.MainMenu))
            GameStateMachine.TransitionTo(GameState.MainMenu);

        SceneLoader.EnsureInstance();
        if (SceneLoader.Instance != null)
            SceneLoader.Instance.LoadAsync("MainMenu");
    }

    public void ReturnToBunker()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
        if (settingsPanel != null && settingsPanel.IsOpen) settingsPanel.Hide();

        MissionContext.ReturnedFromMission = true;
        if (GameStateMachine.CanTransitionTo(GameState.Loading))
        {
            GameStateMachine.TransitionTo(GameState.Loading);
        }
        SceneLoader.EnsureInstance();
        SceneLoader.Instance.LoadAsync("Bunker(main)");
    }

    // Единая точка входа «пауза открыта»: держит timeScale, курсор, звук и FSM
    // в согласованном состоянии. Настройки живут поверх паузы.
    private void TogglePause()
    {
        if (pausePanel == null)
            return;

        if (pausePanel.activeSelf) ResumeGame();
        else OpenPause();
    }

    public void OpenPause()
    {
        if (pausePanel == null) return;

        pausePanel.SetActive(true);
        SetHudVisible(false);
        ApplyPausedState();
    }

    public void OpenSettings()
    {
        if (settingsPanel == null)
        {
            Debug.LogWarning("[MissionUIManager] SettingsPanel не назначен — настройки в миссии недоступны.", this);
            return;
        }

        if (pausePanel != null && !pausePanel.activeSelf) OpenPause();

        settingsPanel.Show();
        ApplyPausedState();
    }

    // Игра «стоит»: timeScale 0, курсор свободен, звук мира на паузе (клики UI
    // исключены через AudioSource.ignoreListenerPause), FSM в состоянии Paused.
    private void ApplyPausedState()
    {
        Time.timeScale = 0f;
        // Confined, а не None: DroneFlightController.Update() по ЛКМ захватывает
        // курсор только при lockState == None (см. LateUpdate ниже).
        Cursor.lockState = CursorLockMode.Confined;
        Cursor.visible = true;
        AudioListener.pause = true;

        if (GameStateMachine.CanTransitionTo(GameState.Paused))
            GameStateMachine.TransitionTo(GameState.Paused);
    }

    /// <summary>
    /// Открыта любая модалка миссии: пауза, настройки, crash-панель или дебриф.
    /// </summary>
    public bool IsModalOpen =>
        (pausePanel != null && pausePanel.activeSelf) ||
        (crashPanel != null && crashPanel.activeSelf) ||
        (settingsPanel != null && settingsPanel.IsOpen) ||
        GameStateMachine.Current == GameState.Debrief;

    // DroneFlightController.Update() (строки 113-119) выполняется и при timeScale 0
    // и на нажатие ЛКМ захватывает курсор, если Cursor.lockState == None.
    // При Locked InputSystemUIInputModule уводит UI-указатель в (-1,-1)
    // (InputSystemUIInputModule.cs:428) — клики по панели до кнопок не доходят.
    // Confined не является ни None, ни Locked: дрон курсор не трогает, а указатель
    // UI остаётся на экране. LateUpdate идёт после всех Update и удерживает
    // состояние, даже если дрон успел выставить None в этом же кадре.
    private void LateUpdate()
    {
        if (!IsModalOpen)
            return;

        if (Cursor.lockState != CursorLockMode.Confined)
            Cursor.lockState = CursorLockMode.Confined;

        if (!Cursor.visible)
            Cursor.visible = true;
    }

    private void SetHudVisible(bool visible)
    {
        IsHudHidden = !visible;
        if (instructionText != null) instructionText.SetActive(visible);
    }
}
