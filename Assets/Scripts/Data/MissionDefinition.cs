// Data/MissionDefinition.cs
using UnityEngine;

[CreateAssetMenu(menuName = "FPV/Mission")]
public class MissionDefinition : ScriptableObject
{
    [Header("Метаданные")]
    public string id;                      // "mission_01_village"
    public string displayName = "Патруль";
    [TextArea] public string description;
    public Sprite preview;

    [Header("Сцена")]
    public string sceneName;               // "Gameplay_Village"

    [Header("Условия")]
    public MissionObjectiveType objectiveType = MissionObjectiveType.DestroyAllTargets;
    public int targetCount = 3;
    public float timeLimit = 0f;           // 0 = без лимита

    [Header("Доступный арсенал")]
    public DroneConfig defaultDrone;
    public DroneConfig[] availableDrones;

    [Header("Прогрессия")]
    public MissionDefinition[] prerequisites;   // открывается после этих
    public int rewardPoints = 100;
}

public enum MissionObjectiveType
{
    DestroyAllTargets,
    SurviveTime,
    ReachPoint
}


[CreateAssetMenu(menuName = "FPV/Mission Registry")]
public class MissionRegistry : ScriptableObject
{
    public MissionDefinition[] allMissions;
}