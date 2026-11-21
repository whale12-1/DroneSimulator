using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class MissionGateValidator
{
    private const string Mission1Path = "Assets/Resources/Missions/Mission1.asset";
    private const string Mission2Path = "Assets/Resources/Missions/Mission2.asset";

    [MenuItem("Tools/DroneSimulator/Missions/1. Validate Mission Gating", false, 21)]
    public static void ValidateGating()
    {
        MissionDefinition mission1 = AssetDatabase.LoadAssetAtPath<MissionDefinition>(Mission1Path);
        MissionDefinition mission2 = AssetDatabase.LoadAssetAtPath<MissionDefinition>(Mission2Path);

        if (mission1 == null || mission2 == null)
        {
            Debug.LogError($"[MissionGate] Не найдены ассеты миссий: {Mission1Path} / {Mission2Path}");
            return;
        }

        bool hasPrerequisite = false;
        string prerequisiteNames = "пусто";

        if (mission2.prerequisites != null && mission2.prerequisites.Length > 0)
        {
            List<string> names = new List<string>();

            for (int i = 0; i < mission2.prerequisites.Length; i++)
            {
                MissionDefinition prerequisite = mission2.prerequisites[i];
                if (prerequisite == null)
                {
                    names.Add("<null>");
                    continue;
                }

                names.Add(prerequisite.name);
                if (prerequisite == mission1) hasPrerequisite = true;
            }

            prerequisiteNames = string.Join(", ", names);
        }

        Debug.Log($"[MissionGate] Mission2 id='{mission2.id}' prerequisites: {prerequisiteNames}");
        Debug.Log($"[MissionGate] Mission1 id='{mission1.id}' scene='{mission1.sceneName}', Mission2 scene='{mission2.sceneName}'");

        if (!hasPrerequisite)
        {
            Debug.LogError("[MissionGate] ОШИБКА: в prerequisites у Mission2 нет ссылки на Mission1.");
            return;
        }

        SaveData data = ProgressService.Data;
        List<string> completed = data.completedMissionIds;
        List<string> unlocked = data.unlockedMissionIds;

        bool currentCompleted = ProgressService.IsCompleted(mission1);
        bool currentUnlocked = ProgressService.IsUnlocked(mission2);

        List<string> completedBackup = new List<string>(completed);
        List<string> unlockedBackup = new List<string>(unlocked);

        completed.Remove(mission1.id);
        completed.Remove(mission2.id);
        unlocked.Remove(mission1.id);
        unlocked.Remove(mission2.id);
        bool unlockedWithoutPrerequisite = ProgressService.IsUnlocked(mission2);

        completed.Add(mission1.id);
        bool unlockedAfterPrerequisite = ProgressService.IsUnlocked(mission2);

        completed.Clear();
        completed.AddRange(completedBackup);
        unlocked.Clear();
        unlocked.AddRange(unlockedBackup);

        Debug.Log($"[MissionGate] Сохранение: Mission1 пройдена = {currentCompleted}, IsUnlocked(Mission2) = {currentUnlocked}");
        Debug.Log($"[MissionGate] Симуляция без Mission1: IsUnlocked(Mission2) = {unlockedWithoutPrerequisite} (ожидается False)");
        Debug.Log($"[MissionGate] Симуляция с пройденной Mission1: IsUnlocked(Mission2) = {unlockedAfterPrerequisite} (ожидается True)");

        if (!unlockedWithoutPrerequisite && unlockedAfterPrerequisite)
        {
            Debug.Log("<color=lime>[MissionGate]</color> Гейтинг миссий работает: Mission2 заблокирована до прохождения Mission1.");
            return;
        }

        Debug.LogError("[MissionGate] Гейтинг миссий НЕ работает: проверьте prerequisites и ProgressService.IsUnlocked.");
    }
}
