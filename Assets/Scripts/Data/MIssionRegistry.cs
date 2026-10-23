using UnityEngine;

[CreateAssetMenu(menuName = "FPV/Mission Registry")]
public class MissionRegistry : ScriptableObject
{
    public MissionDefinition[] allMissions;
}