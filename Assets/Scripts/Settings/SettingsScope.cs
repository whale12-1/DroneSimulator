using UnityEngine;

public static class SettingsScope
{
    public static bool IsMission { get; private set; }

    public static void Refresh()
    {
        IsMission = Object.FindAnyObjectByType<DroneManager>() != null;
    }
}
