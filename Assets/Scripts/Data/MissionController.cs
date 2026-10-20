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
        mission = MissionContext.Current;
        if (mission == null) return;

        if (mission.defaultDrone != null)
            droneManager.SelectByConfig(mission.defaultDrone);

        startTime = Time.time;
        isEnded = false;

        // ВОТ ЭТА СТРОКА: Сообщаем FSM, что сцена загрузилась и игра началась
        if (GameStateMachine.Current == GameState.Loading)
        {
            GameStateMachine.TransitionTo(GameState.Gameplay);
        }
    }

    private void OnEnable() => GameEvents.VehicleDestroyed += OnVehicleDestroyed;
    private void OnDisable() => GameEvents.VehicleDestroyed -= OnVehicleDestroyed;

    private void Update()
    {
        if (mission == null || isEnded) return;

        // Проверка таймера (для миссий с лимитом, но не на выживание)
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
                if (reachPointTarget != null)
                {
                    // Проверяем, долетел ли хоть один дрон
                    float dist = Vector3.Distance(droneManager.transform.position, reachPointTarget.position);
                    if (dist <= reachRadius) EndMission(MissionResult.Success);
                }
                break;
        }
    }

    private void OnVehicleDestroyed(VehicleHealth v)
    {
        if (mission == null || mission.objectiveType != MissionObjectiveType.DestroyAllTargets) return;

        // Проверяем, является ли уничтоженный объект целью миссии
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

    // Вызывается в Update, чтобы отслеживать потерю всех дронов
    private void LateUpdate()
    {
        if (isEnded || mission == null) return;

        // Если у игрока не осталось дронов, а цели не выполнены — провал
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