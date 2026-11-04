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

    private bool isReturning = false;

    private void Start()
    {
        GameStateMachine.StateChanged += OnStateChanged;

        if (returnToMenuButton != null)
        {
            returnToMenuButton.onClick.RemoveAllListeners();
            returnToMenuButton.onClick.AddListener(ReturnToMenu);
        }

        panel.SetActive(false);
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
        panel.SetActive(true);

        if (debriefCamera != null)
        {
            debriefCamera.gameObject.SetActive(true);
        }

        var result = MissionContext.LastResult;
        var mission = MissionContext.Current;

        if (result == MissionResult.Success)
        {
            titleText.text = "МИССИЯ ВЫПОЛНЕНА";
            titleText.color = Color.green;
        }
        else
        {
            titleText.text = "МИССИЯ ПРОВАЛЕНА";
            titleText.color = Color.red;
        }

        statsText.text = $"Время: {MissionContext.LastTimeElapsed:F1} сек\n" +
                         $"Целей уничтожено: {MissionContext.LastTargetsDestroyed} / {mission.targetCount}\n" +
                         $"Награда: {(result == MissionResult.Success ? mission.rewardPoints : 0)} очков";

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
