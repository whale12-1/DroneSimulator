using UnityEngine;

public enum MissionResult
{
    None,
    Success,
    Failed
}

public static class MissionContext
{
    public static MissionDefinition Current { get; private set; }
    public static DroneConfig SelectedDrone { get; private set; }

    // Результаты последней сессии
    public static MissionResult LastResult { get; private set; } = MissionResult.None;
    public static float LastTimeElapsed { get; private set; }
    public static int LastTargetsDestroyed { get; private set; }

    public static void Begin(MissionDefinition mission, DroneConfig drone)
    {
        Current = mission;
        SelectedDrone = drone;
        LastResult = MissionResult.None;
        LastTimeElapsed = 0;
        LastTargetsDestroyed = 0;
    }

    public static void MarkResult(MissionResult result, int targetsDestroyed, float timeElapsed)
    {
        LastResult = result;
        LastTargetsDestroyed = targetsDestroyed;
        LastTimeElapsed = timeElapsed;
    }

    public static void Clear()
    {
        Current = null;
        SelectedDrone = null;
        LastResult = MissionResult.None;
    }
}