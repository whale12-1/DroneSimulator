using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class MissionMenuController : MonoBehaviour
{
    [SerializeField] private MissionRegistry registry;
    [SerializeField] private MissionSelectScreen missionSelect;
    [SerializeField] private Button backButton;
    [SerializeField] private CanvasGroup panelGroup;

    private float lastOpenTime;

    public bool IsOpen { get; private set; }
    public float LastClosedTime { get; private set; } = -10f;

    private void Awake()
    {
        if (registry == null) registry = Resources.Load<MissionRegistry>("MissionRegistry");
        if (missionSelect == null) missionSelect = GetComponent<MissionSelectScreen>();
        if (panelGroup == null) panelGroup = GetComponent<CanvasGroup>();
        if (backButton == null) backButton = GetComponentInChildren<Button>(true);

        if (backButton != null)
        {
            backButton.onClick.RemoveAllListeners();
            backButton.onClick.AddListener(CloseMissionMenu);
        }

        if (GameStateMachine.Current == GameState.Boot)
            GameStateMachine.TransitionTo(GameState.MainMenu);

        HidePanel();
    }

    private void Update()
    {
        if (!IsOpen) return;
        if (Time.unscaledTime - lastOpenTime < 0.2f) return;

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            CloseMissionMenu();
    }

    public void OpenMissionMenu()
    {
        if (missionSelect == null)
        {
            Debug.LogError("[MissionMenuController] Поле missionSelect не назначено.");
            return;
        }

        if (registry == null)
        {
            Debug.LogError("[MissionMenuController] Поле registry не назначено.");
            return;
        }

        missionSelect.Populate(registry, StartMission);

        if (GameStateMachine.CanTransitionTo(GameState.MissionSelect))
            GameStateMachine.TransitionTo(GameState.MissionSelect);

        IsOpen = true;
        lastOpenTime = Time.unscaledTime;
        ShowPanel();

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Time.timeScale = 0f;
    }

    public void CloseMissionMenu()
    {
        IsOpen = false;
        LastClosedTime = Time.unscaledTime;
        HidePanel();

        if (GameStateMachine.CanTransitionTo(GameState.Gameplay))
            GameStateMachine.TransitionTo(GameState.Gameplay);
    }

    private void StartMission(MissionDefinition mission)
    {
        if (mission == null || string.IsNullOrEmpty(mission.sceneName))
        {
            Debug.LogWarning("У миссии не задан sceneName.");
            return;
        }

        MissionContext.Begin(mission, mission.defaultDrone);

        if (GameStateMachine.CanTransitionTo(GameState.Loading))
            GameStateMachine.TransitionTo(GameState.Loading);

        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        SceneLoader.EnsureInstance();
        SceneLoader.Instance.LoadAsync(mission.sceneName);
    }

    private void ShowPanel()
    {
        if (panelGroup != null)
        {
            panelGroup.alpha = 1f;
            panelGroup.interactable = true;
            panelGroup.blocksRaycasts = true;
        }
    }

    private void HidePanel()
    {
        if (panelGroup != null)
        {
            panelGroup.alpha = 0f;
            panelGroup.interactable = false;
            panelGroup.blocksRaycasts = false;
        }
    }
}
