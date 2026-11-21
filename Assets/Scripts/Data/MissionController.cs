using System.Collections;
using UnityEngine;

public class MissionController : MonoBehaviour
{
    [Header("Ссылки")]
    [SerializeField] private DroneManager droneManager;
    [SerializeField] private Transform reachPointTarget; // Для миссии ReachPoint
    [SerializeField] private float reachRadius = 10f;

    [Header("Управление завершением")]
    [Tooltip("Если true, MissionController сам завершает миссию по условиям. Внешний директор (например AmbushMissionDirector) выставляет false и завершает миссию через CompleteMission().")]
    public bool autoComplete = true;

    private MissionDefinition mission;
    private int destroyedTargets;
    private float startTime;
    private bool isEnded;

    public MissionDefinition Mission => mission;

    public int DestroyedTargets => destroyedTargets;

    public int TargetCount => mission != null ? mission.targetCount : 0;

    public bool IsEnded => isEnded;

    public float ElapsedTime => Mathf.Max(0f, Time.time - startTime);

    public float TimeLimit => mission != null ? mission.timeLimit : 0f;

    public float TimeRemaining
    {
        get
        {
            if (mission == null || mission.timeLimit <= 0f) return -1f;
            return Mathf.Max(0f, mission.timeLimit - ElapsedTime);
        }
    }

    private void Awake()
    {
        if (GameStateMachine.Current == GameState.Loading)
        {
            GameStateMachine.TransitionTo(GameState.Gameplay);
        }

        mission = MissionContext.Current;
        startTime = Time.time;
        isEnded = false;
    }

    private void Start()
    {
        // Выбор дрона выполняем в Start, когда DroneManager уже построил пул activeSlots в Awake.
        if (mission != null && mission.defaultDrone != null && droneManager != null)
            droneManager.SelectByConfig(mission.defaultDrone);
    }

    private void OnEnable()
    {
        GameEvents.VehicleDestroyed += OnVehicleDestroyed;
        GameEvents.DroneExploded += OnDroneDestroyed;
    }

    private void OnDisable()
    {
        GameEvents.VehicleDestroyed -= OnVehicleDestroyed;
        GameEvents.DroneExploded -= OnDroneDestroyed;
    }

    private void Update()
    {
        if (mission == null || isEnded) return;

        // Внешний директор (AmbushMissionDirector) ведёт миссию сам — не завершаем её здесь.
        if (!autoComplete) return;

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

    private void OnDroneDestroyed(Vector3 pos, MonoBehaviour drone)
    {
        if (isEnded) return; // Если миссия уже завершена, крушение игнорируем
        if (mission != null && mission.sceneName.Contains("PreparingDemo"))
        {
            StartCoroutine(DelayedCrashPanel());
        }
    }

    private IEnumerator DelayedCrashPanel()
    {
        yield return new WaitForSecondsRealtime(0.5f);

        if (isEnded) yield break;

        MissionUIManager ui = FindAnyObjectByType<MissionUIManager>();
        if (ui != null) ui.ShowCrashPanel();
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

    /// <summary>
    /// Публичный способ завершить миссию извне (например, из AmbushMissionDirector).
    /// Выполняет общий путь: фиксирует результат, пишет прогресс и переводит FSM в Debrief.
    /// </summary>
    public void CompleteMission(MissionResult result, int targetsDestroyed)
    {
        if (isEnded) return;
        EndMission(result, targetsDestroyed);
    }

    private void EndMission(MissionResult result)
    {
        EndMission(result, destroyedTargets);
    }

    private void EndMission(MissionResult result, int targetsDestroyed)
    {
        if (isEnded) return;
        isEnded = true;

        float timeElapsed = Time.time - startTime;
        MissionContext.MarkResult(result, targetsDestroyed, timeElapsed);

        if (mission != null)
            ProgressService.RecordResult(mission, result, timeElapsed, targetsDestroyed);

        GameStateMachine.TransitionTo(GameState.Debrief);
    }
}
