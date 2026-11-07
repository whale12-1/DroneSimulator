using UnityEngine;
using UnityEngine.InputSystem;

public class BunkerPauseController : MonoBehaviour
{
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private ComputerUIController computerUI;
    [SerializeField] private MissionMenuController missionMenu;
    [SerializeField] private BunkerStateRestorer restorer;
    [SerializeField] private PlayerInteractionDetector interactionDetector;
    [SerializeField] private ChairInteractable chair;
    [SerializeField] private InteractionUIController uiController;
    [SerializeField] private PlayerController playerController;
    [SerializeField] private GameObject settingsNotice;
    [SerializeField] private SettingsPanel settingsPanel;

    public static bool IsPaused { get; private set; }

    public bool IsOpen => pausePanel != null && pausePanel.activeSelf;

    private bool computerWasOpen;
    private float startTime;

    private void OnEnable()
    {
        IsPaused = false;
        startTime = Time.unscaledTime;
        GameStateMachine.StateChanged += OnStateChanged;
    }

    private void OnDisable()
    {
        GameStateMachine.StateChanged -= OnStateChanged;
        ReleaseInput();
    }

    private void LateUpdate()
    {
        computerWasOpen = computerUI != null && computerUI.IsOpen;
    }

    private void Update()
    {
        if (Keyboard.current == null)
            return;

        if (restorer != null && restorer.IsRestoring)
            return;

        if (GameStateMachine.Current != GameState.Gameplay && GameStateMachine.Current != GameState.Paused)
            return;

        if (!Keyboard.current.escapeKey.wasPressedThisFrame)
            return;

        if (IsOpen)
        {
            if (settingsPanel != null && settingsPanel.IsOpen)
            {
                settingsPanel.Hide();
                return;
            }

            Resume();
            return;
        }

        if (missionMenu != null && (missionMenu.IsOpen || Time.unscaledTime - missionMenu.LastClosedTime < 0.2f))
            return;

        if (computerUI != null && computerUI.IsOpen)
            return;

        if (computerWasOpen)
            return;

        if (playerController != null && playerController.isSitting)
            return;

        if (Time.unscaledTime - startTime < 0.25f)
            return;

        OpenPause();
    }

    public void Resume()
    {
        if (settingsPanel != null) settingsPanel.Hide();

        if (pausePanel != null)
            pausePanel.SetActive(false);

        if (settingsNotice != null)
            settingsNotice.SetActive(false);

        IsPaused = false;

        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (GameStateMachine.CanTransitionTo(GameState.Gameplay))
            GameStateMachine.TransitionTo(GameState.Gameplay);

        ReleaseInput();
    }

    public void OpenSettings()
    {
        if (settingsPanel != null)
        {
            settingsPanel.Show();
            return;
        }

        if (settingsNotice == null)
            return;

        settingsNotice.SetActive(!settingsNotice.activeSelf);
    }

    public void ExitToMainMenu()
    {
        if (settingsPanel != null) settingsPanel.Hide();

        if (pausePanel != null)
            pausePanel.SetActive(false);

        IsPaused = false;

        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (GameStateMachine.CanTransitionTo(GameState.MainMenu))
            GameStateMachine.TransitionTo(GameState.MainMenu);

        SceneLoader.EnsureInstance();
        SceneLoader.Instance.LoadAsync("MainMenu");
    }

    private void OpenPause()
    {
        if (settingsPanel != null) settingsPanel.Hide();

        if (pausePanel != null)
            pausePanel.SetActive(true);

        IsPaused = true;

        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (GameStateMachine.CanTransitionTo(GameState.Paused))
            GameStateMachine.TransitionTo(GameState.Paused);

        BlockInput();
    }

    private void OnStateChanged(GameState prev, GameState next)
    {
        if (!IsPaused && !IsOpen)
            return;

        if (next == GameState.Loading || next == GameState.MainMenu || next == GameState.MissionSelect || next == GameState.Debrief)
        {
            if (settingsPanel != null) settingsPanel.Hide();

            if (pausePanel != null)
                pausePanel.SetActive(false);

            IsPaused = false;
            Time.timeScale = 1f;
            ReleaseInput();
        }
    }

    private void BlockInput()
    {
        if (interactionDetector != null)
            interactionDetector.enabled = false;

        if (chair != null)
            chair.enabled = false;

        if (uiController != null)
        {
            uiController.HideHint();
            uiController.HideSittingHint();
        }
    }

    private void ReleaseInput()
    {
        if (interactionDetector != null)
            interactionDetector.enabled = true;

        if (chair != null)
            chair.enabled = true;

        if (uiController != null && playerController != null && playerController.isSitting)
            uiController.ShowSittingHint("Встать [Пробел]");
    }
}
