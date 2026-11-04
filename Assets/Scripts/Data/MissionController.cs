using UnityEngine;

public class MissionController : MonoBehaviour
{
    [Header("Ссылки")]
    [SerializeField] private DroneManager droneManager;
    [SerializeField] private Transform reachPointTarget; // Для миссии ReachPoint
    [SerializeField] private float reachRadius = 10f;

    private MissionDefinition mission;
    private int destroyedTargets;
    private float startTime;
    private bool isEnded;

    private void Awake()
    {
        if (GameStateMachine.Current == GameState.Loading)
        {
            GameStateMachine.TransitionTo(GameState.Gameplay);
        }

        mission = MissionContext.Current;
        if (mission == null) return;

        if (mission.defaultDrone != null && droneManager != null)
            droneManager.SelectByConfig(mission.defaultDrone);

        startTime = Time.time;
        isEnded = false;
    }

    private void OnEnable() => GameEvents.VehicleDestroyed += OnVehicleDestroyed;
    private void OnDisable() => GameEvents.VehicleDestroyed -= OnVehicleDestroyed;

    private void Update()
    {
        if (mission == null || isEnded) return;

        if (mission.timeLimit > 0 && mission.objectiveType != MissionObjectiveType.SurviveTime)
        {
            if (Time.time - startTime > mission.timeLimit)
            {
                EndMission(MissionResult.Failed);
                return;
            }
        }

        CheckWinCondition();
    }

    private void CheckWinCondition()
    {
        switch (mission.objectiveType)
        {
            case MissionObjectiveType.DestroyAllTargets:
                if (destroyedTargets >= mission.targetCount)
                    EndMission(MissionResult.Success);
                break;

            case MissionObjectiveType.SurviveTime:
                if (Time.time - startTime >= mission.timeLimit)
                    EndMission(MissionResult.Success);
                break;

            case MissionObjectiveType.ReachPoint:
                if (reachPointTarget != null && droneManager != null)
                {
                    float dist = Vector3.Distance(droneManager.transform.position, reachPointTarget.position);
                    if (dist <= reachRadius) EndMission(MissionResult.Success);
                }
                break;
        }
    }

    private void OnVehicleDestroyed(VehicleHealth v)
    {
        if (mission == null || mission.objectiveType != MissionObjectiveType.DestroyAllTargets) return;

        TargetObject target = v.GetComponent<TargetObject>();

        if (target != null && target.isHostile)
        {
            destroyedTargets++;
            Debug.Log($"[Mission] Цель уничтожена! Прогресс: {destroyedTargets} / {mission.targetCount}");
        }
        else
        {
            Debug.Log("[Mission] Уничтожен не-целевой объект. В прогресс не идёт.");
        }
    }

    private void LateUpdate()
    {
        if (isEnded || mission == null || droneManager == null) return;

        if (droneManager.GetAliveDronesCount() <= 0)
        {
            Debug.Log("[MissionController] Все дроны потеряны. Провал.");
            EndMission(MissionResult.Failed);
        }
    }

    private void EndMission(MissionResult result)
    {
        if (isEnded) return;
        isEnded = true;

        float timeElapsed = Time.time - startTime;
        MissionContext.MarkResult(result, destroyedTargets, timeElapsed);

        if (result == MissionResult.Success)
            ProgressService.CompleteMission(mission, mission.rewardPoints);

        GameStateMachine.TransitionTo(GameState.Debrief);
    }
}
