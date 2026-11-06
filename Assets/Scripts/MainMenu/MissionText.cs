using UnityEngine;

public static class MissionText
{
    public static string Targets(int count)
    {
        int mod10 = count % 10;
        int mod100 = count % 100;

        if (mod10 == 1 && mod100 != 11)
            return $"{count} цель";

        if (mod10 >= 2 && mod10 <= 4 && (mod100 < 12 || mod100 > 14))
            return $"{count} цели";

        return $"{count} целей";
    }

    public static string Objective(MissionDefinition mission)
    {
        if (mission == null) return "—";

        switch (mission.objectiveType)
        {
            case MissionObjectiveType.DestroyAllTargets:
                return $"Уничтожить {Targets(mission.targetCount)}";
            case MissionObjectiveType.SurviveTime:
                return $"Продержаться {Duration(mission.timeLimit)}";
            case MissionObjectiveType.ReachPoint:
                return "Выйти в точку назначения";
            default:
                return "—";
        }
    }

    public static string TimeLimit(float seconds)
    {
        return seconds > 0f ? Duration(seconds) : "без лимита";
    }

    public static string Duration(float seconds)
    {
        if (seconds <= 0f) return "—:—";

        int total = Mathf.RoundToInt(seconds);
        int minutes = total / 60;
        int rest = total % 60;

        if (minutes >= 60)
            return $"{minutes / 60}:{minutes % 60:00}:{rest:00}";

        return $"{minutes:00}:{rest:00}";
    }

    public static string DroneRole(DroneConfig config)
    {
        if (config == null) return "не назначена";
        return config.cameraType == CameraType.ReconGimbal ? "РАЗВЕДЧИК" : "УДАРНЫЙ FPV";
    }
}
