// Gameplay/MissionContext.cs
public static class MissionContext
{
    public static MissionDefinition Current { get; private set; }
    public static DroneConfig SelectedDrone { get; private set; }

    public static void Begin(MissionDefinition mission, DroneConfig drone)
    {
        Current = mission;
        SelectedDrone = drone;
    }

    public static void Clear()
    {
        Current = null;
        SelectedDrone = null;
    }
}