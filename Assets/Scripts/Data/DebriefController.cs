using System.Collections;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class DebriefController : MonoBehaviour
{
    [Header("UI Элементы")]
    [SerializeField] private GameObject panel;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text statsText;
    [SerializeField] private Button returnToMenuButton;
    [SerializeField] private Camera debriefCamera;
    [SerializeField] private Button retryButton;
    [SerializeField] private Button mainMenuButton;

    private bool isReturning = false;

    private void Start()
    {
        GameStateMachine.StateChanged += OnStateChanged;

        WireButton(returnToMenuButton, ReturnToMenu);
        WireButton(retryButton, RetryMission);
        WireButton(mainMenuButton, ReturnToMainMenu);

        if (panel != null) UISoundService.Attach(panel);
        if (panel != null) panel.SetActive(false);
    }

    private static void WireButton(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null) return;

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(action);
    }

    // Esc в дебрифе = «Вернуться в бункер» (как обещает футер панели).
    private void Update()
    {
        if (panel == null || !panel.activeSelf) return;

        UnityEngine.InputSystem.Keyboard keyboard = UnityEngine.InputSystem.Keyboard.current;
        if (keyboard == null) return;
        if (!keyboard.escapeKey.wasPressedThisFrame) return;

        ReturnToMenu();
    }

    private void OnDestroy()
    {
        GameStateMachine.StateChanged -= OnStateChanged;
    }

    private void OnStateChanged(GameState prev, GameState next)
    {
        if (next == GameState.Debrief)
        {
            ShowDebrief();
        }
    }

    private void ShowDebrief()
    {
        if (panel != null) panel.SetActive(true);

        if (debriefCamera != null)
        {
            debriefCamera.gameObject.SetActive(true);
        }

        MissionResult result = MissionContext.LastResult;
        MissionDefinition mission = MissionContext.Current;

        if (titleText != null)
        {
            if (result == MissionResult.Success)
            {
                titleText.text = "МИССИЯ ВЫПОЛНЕНА";
                titleText.color = TacticalUITheme.StatusCompleted;
            }
            else
            {
                titleText.text = "МИССИЯ ПРОВАЛЕНА";
                titleText.color = TacticalUITheme.StatusLocked;
            }
        }

        // Прямой запуск сцены (без выбора миссии) оставляет MissionContext пустым:
        // дебриф обязан работать и в этом случае, а не падать на mission.targetCount.
        int targetCount = mission != null ? mission.targetCount : MissionContext.LastTargetsDestroyed;
        int score = mission != null
            ? ProgressService.ComputeScore(mission, result, MissionContext.LastTimeElapsed, MissionContext.LastTargetsDestroyed)
            : 0;

        if (statsText != null)
        {
            statsText.text = $"Время: {MissionContext.LastTimeElapsed:F1} сек\n" +
                             $"Целей уничтожено: {MissionContext.LastTargetsDestroyed} / {targetCount}\n" +
                             $"Награда: {score} очков";
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void ReturnToMenu()
    {
        if (isReturning) return;
        isReturning = true;

        panel.SetActive(false);

        if (debriefCamera != null)
        {
            debriefCamera.gameObject.SetActive(false);
        }

        MissionContext.ReturnedFromMission = true;

        if (GameStateMachine.CanTransitionTo(GameState.Loading))
        {
            GameStateMachine.TransitionTo(GameState.Loading);
        }

        SceneLoader.EnsureInstance();
        if (SceneLoader.Instance != null)
            SceneLoader.Instance.LoadAsync("Bunker(main)");

        StartCoroutine(ResetReturnFlagAfterTimeout(5f));
    }

    /// <summary>Повторить текущую миссию (из дебрифа, через Loading).</summary>
    public void RetryMission()
    {
        if (isReturning) return;
        isReturning = true;

        if (panel != null) panel.SetActive(false);
        if (debriefCamera != null) debriefCamera.gameObject.SetActive(false);

        Time.timeScale = 1f;
        AudioListener.pause = false;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        string sceneName = MissionContext.Current != null && !string.IsNullOrEmpty(MissionContext.Current.sceneName)
            ? MissionContext.Current.sceneName
            : "PreparingDemo";

        if (GameStateMachine.CanTransitionTo(GameState.Loading))
            GameStateMachine.TransitionTo(GameState.Loading);

        SceneLoader.EnsureInstance();
        if (SceneLoader.Instance != null)
            SceneLoader.Instance.LoadAsync(sceneName);
    }

    /// <summary>Выход в главное меню (из дебрифа).</summary>
    public void ReturnToMainMenu()
    {
        if (isReturning) return;
        isReturning = true;

        if (panel != null) panel.SetActive(false);
        if (debriefCamera != null) debriefCamera.gameObject.SetActive(false);

        Time.timeScale = 1f;
        AudioListener.pause = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (GameStateMachine.CanTransitionTo(GameState.MainMenu))
            GameStateMachine.TransitionTo(GameState.MainMenu);

        SceneLoader.EnsureInstance();
        if (SceneLoader.Instance != null)
            SceneLoader.Instance.LoadAsync("MainMenu");
    }

    private IEnumerator ResetReturnFlagAfterTimeout(float seconds)
    {
        yield return new WaitForSecondsRealtime(seconds);

        if (isReturning)
        {
            isReturning = false;
            Debug.LogWarning("[DebriefController] Возврат на базу не завершился за отведённое время. Флаг isReturning сброшен.");
        }
    }
}
