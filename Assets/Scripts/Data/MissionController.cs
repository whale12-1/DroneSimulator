// Gameplay/MissionController.cs
using UnityEngine;

public class MissionController : MonoBehaviour
{
    [SerializeField] private DroneManager droneManager;

    private MissionDefinition mission;
    private int destroyedTargets;
    private float startTime;

    private void Awake()
    {
        mission = MissionContext.Current;
        if (mission == null)
        {
            Debug.LogWarning("[Mission] Запуск без миссии — fallback.");
            return;
        }

        if (mission.defaultDrone != null)
            droneManager.SelectByConfig(mission.defaultDrone);
    }

    private void OnEnable()
    {
        GameEvents.VehicleDestroyed += OnVehicleDestroyed;
    }

    private void OnDisable()
    {
        GameEvents.VehicleDestroyed -= OnVehicleDestroyed;
    }

    private void Update()
    {
        if (mission == null) return;

        if (mission.timeLimit > 0 && Time.time - startTime > mission.timeLimit)
            Fail();
    }

    private void OnVehicleDestroyed(VehicleHealth v)
    {
        destroyedTargets++;
        if (mission.objectiveType == MissionObjectiveType.DestroyAllTargets &&
            destroyedTargets >= mission.targetCount)
            Complete();
    }

    private void Complete()
    {
        ProgressService.CompleteMission(mission, mission.rewardPoints);
        GameStateMachine.TransitionTo(GameState.Debrief);
        // показать Debrief-панель
    }

    private void Fail()
    {
        GameStateMachine.TransitionTo(GameState.Debrief);
        // показать Debrief-панель с провалом
    }
}