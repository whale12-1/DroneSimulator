using UnityEngine;
using UnityEngine.InputSystem;

public class MissionUIManager : MonoBehaviour
{
    public static bool IsHudHidden { get; private set; }

    [SerializeField] private GameObject instructionText;
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private GameObject crashPanel;

    private void OnEnable()
    {
        IsHudHidden = false;
        GameStateMachine.StateChanged += OnStateChanged;
    }

    private void OnDisable() => GameStateMachine.StateChanged -= OnStateChanged;

    private void OnStateChanged(GameState prev, GameState next)
    {
        if (next == GameState.Debrief)
        {
            Time.timeScale = 1f;
            SetHudVisible(false);
            if (pausePanel != null) pausePanel.SetActive(false);
            if (crashPanel != null) crashPanel.SetActive(false);
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

        if (crashPanel != null && crashPanel.activeSelf)
            return;

        TogglePause();
    }

    public void ShowCrashPanel()
    {
        if (GameStateMachine.Current == GameState.Debrief)
            return;

        SetHudVisible(false);

        if (crashPanel != null)
            crashPanel.SetActive(true);

        if (pausePanel != null)
            pausePanel.SetActive(false);

        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void ResumeGame()
    {
        SetHudVisible(true);

        if (pausePanel != null)
            pausePanel.SetActive(false);

        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public void RestartMission()
    {
        Time.timeScale = 1f;
        SceneLoader.EnsureInstance();
        SceneLoader.Instance.LoadAsync("PreparingDemo");
    }

    public void ReturnToBunker()
    {
        Time.timeScale = 1f;
        MissionContext.ReturnedFromMission = true;
        if (GameStateMachine.CanTransitionTo(GameState.Loading))
        {
            GameStateMachine.TransitionTo(GameState.Loading);
        }
        SceneLoader.EnsureInstance();
        SceneLoader.Instance.LoadAsync("Bunker(main)");
    }

    private void TogglePause()
    {
        if (pausePanel == null)
            return;

        bool open = !pausePanel.activeSelf;
        pausePanel.SetActive(open);

        if (open)
        {
            SetHudVisible(false);
            Time.timeScale = 0f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            SetHudVisible(true);
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    private void SetHudVisible(bool visible)
    {
        IsHudHidden = !visible;
        if (instructionText != null) instructionText.SetActive(visible);
    }
}
