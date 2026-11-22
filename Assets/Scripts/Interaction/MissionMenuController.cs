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
        if (backButton == null) backButton = FindBackButton();

        if (backButton != null)
        {
            backButton.onClick.RemoveAllListeners();
            backButton.onClick.AddListener(CloseMissionMenu);
        }

        // Состояние FSM доводит до Gameplay BunkerStateRestorer: бункер можно
        // запустить напрямую из редактора, и самовольный переход Boot → MainMenu
        // оставлял паузу недоступной.
        BunkerModalStack.Register(this);

        // Клики по «Назад»/«ЗАПУСТИТЬ МИССИЮ» (карточки подписываются после Populate).
        UISoundService.Attach(gameObject);

        HidePanel();
    }

    private void OnDestroy()
    {
        BunkerModalStack.Unregister(this);
    }

    private void Update()
    {
        if (!IsOpen) return;
        if (Time.unscaledTime - lastOpenTime < 0.2f) return;

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || !keyboard.escapeKey.wasPressedThisFrame) return;

        // Один Esc закрывает всю панель, как кнопка «НАЗАД». Раньше при открытых
        // деталях первый Esc закрывал только их, и это выглядело как «Esc не работает».
        CloseMissionMenu();
    }

    private Button FindBackButton()
    {
        Button[] buttons = GetComponentsInChildren<Button>(true);

        foreach (Button candidate in buttons)
        {
            if (candidate != null && candidate.name == "BackButton")
                return candidate;
        }

        return buttons.Length > 0 ? buttons[0] : null;
    }

    public void OpenMissionMenu()
    {
        // Повторный вызов (двойной клик по иконке, повторный вход из ресторера)
        // раньше пересобирал карточки: старые ещё жили до конца кадра, и на один
        // кадр в списке оказывалось по две копии каждой миссии.
        if (IsOpen) return;

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

        // Карточки создаются в Populate — подписываем их на звук клика.
        UISoundService.Attach(gameObject);

        if (GameStateMachine.CanTransitionTo(GameState.MissionSelect))
            GameStateMachine.TransitionTo(GameState.MissionSelect);

        IsOpen = true;
        lastOpenTime = Time.unscaledTime;
        ShowPanel();

        // Время и курсор выставляет общий координатор: он учитывает остальные
        // открытые окна (например, рабочий стол, поверх которого мы открылись).
        BunkerModalStack.Apply();
    }

    public void CloseMissionMenu()
    {
        IsOpen = false;
        LastClosedTime = Time.unscaledTime;

        if (missionSelect != null) missionSelect.CloseDetails();

        HidePanel();

        if (GameStateMachine.CanTransitionTo(GameState.Gameplay))
            GameStateMachine.TransitionTo(GameState.Gameplay);

        // Если за панелью остался открытый рабочий стол — timeScale останется 0,
        // если нет — управление вернётся игроку (раньше здесь оставался «замороженный» кадр).
        BunkerModalStack.Apply();
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
