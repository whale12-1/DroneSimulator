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

    private void Start()
    {
        // Подписываемся на смену состояния
        GameStateMachine.StateChanged += OnStateChanged;

        if (returnToMenuButton != null)
            returnToMenuButton.onClick.AddListener(ReturnToMenu);

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

        // Отключаем управление игроком/камерой и включаем курсор
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void ReturnToMenu()
    {
        panel.SetActive(false);

        // Переводим стейт-машину обратно в MainMenu
        GameStateMachine.TransitionTo(GameState.MainMenu);

        // Загружаем сцену меню/хаба
        if (SceneLoader.Instance != null)
            SceneLoader.Instance.LoadAsync("MainMenu"); // Или "Hub", если используешь 3D базу
    }
}