using System.Collections;
using UnityEngine;

public class BunkerStateRestorer : MonoBehaviour
{
    [SerializeField] private ChairInteractable chair;
    [SerializeField] private ComputerUIController computer;
    [SerializeField] private MissionMenuController missionMenu;

    public bool IsRestoring { get; private set; }

    private void Awake()
    {
        if (MissionContext.ReturnedFromMission)
            IsRestoring = true;
    }

    private void Start()
    {
        EnsureGameplayState();

        if (!MissionContext.ReturnedFromMission)
            return;

        StartCoroutine(RestoreStateRoutine());
    }

    // Бункер можно запустить Play-кнопкой напрямую из редактора, минуя Boot и
    // MainMenu. Тогда FSM остаётся в Boot/MainMenu, пауза (BunkerPauseController
    // требует Gameplay/Paused) не открывается, а рестарт позы после миссии
    // не может перейти Loading → Gameplay. Доводим состояние до Gameplay
    // по допустимым переходам матрицы FSM.
    private static void EnsureGameplayState()
    {
        GameState state = GameStateMachine.Current;

        if (state == GameState.Boot && GameStateMachine.CanTransitionTo(GameState.MainMenu))
        {
            GameStateMachine.TransitionTo(GameState.MainMenu);
            state = GameStateMachine.Current;
        }

        if (state == GameState.MainMenu && GameStateMachine.CanTransitionTo(GameState.Loading))
        {
            GameStateMachine.TransitionTo(GameState.Loading);
            state = GameStateMachine.Current;
        }

        if (state == GameState.Loading && GameStateMachine.CanTransitionTo(GameState.Gameplay))
            GameStateMachine.TransitionTo(GameState.Gameplay);
    }

    private IEnumerator RestoreStateRoutine()
    {
        yield return null;

        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (chair != null)
            chair.Interact(gameObject, true);

        if (computer != null)
        {
            computer.OpenDesktop();

            float animDeadline = Time.unscaledTime + 2f;
            while (!computer.IsOpenAnimationComplete && Time.unscaledTime < animDeadline)
                yield return null;
        }

        if (GameStateMachine.CanTransitionTo(GameState.Gameplay))
            GameStateMachine.TransitionTo(GameState.Gameplay);

        if (missionMenu != null)
            missionMenu.OpenMissionMenu();

        MissionContext.ReturnedFromMission = false;
        IsRestoring = false;
    }
}
