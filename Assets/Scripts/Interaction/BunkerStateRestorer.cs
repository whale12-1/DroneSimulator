using System.Collections;
using UnityEngine;

public class BunkerStateRestorer : MonoBehaviour
{
    [SerializeField] private ChairInteractable chair;
    [SerializeField] private ComputerUIController computer;
    [SerializeField] private MissionMenuController missionMenu;

    private void Start()
    {
        if (GameStateMachine.Current == GameState.Loading && GameStateMachine.CanTransitionTo(GameState.Gameplay))
            GameStateMachine.TransitionTo(GameState.Gameplay);

        if (!MissionContext.ReturnedFromMission)
            return;

        StartCoroutine(RestoreStateRoutine());
    }

    private IEnumerator RestoreStateRoutine()
    {
        float loadDeadline = Time.unscaledTime + 2f;
        while (SceneLoader.Instance != null && SceneLoader.Instance.IsLoading && Time.unscaledTime < loadDeadline)
            yield return null;

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
    }
}
