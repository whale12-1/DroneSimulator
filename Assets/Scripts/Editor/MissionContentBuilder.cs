using UnityEditor;
using UnityEngine;

public static class MissionContentBuilder
{
    private const string MissionOnePath = "Assets/Resources/Missions/Mission1.asset";
    private const string MissionTwoPath = "Assets/Resources/Missions/Mission2.asset";
    private const string ReconDronePath = "Assets/Settings/DroneConfigs/MavicReconConfig.asset";
    private const string StrikeDronePath = "Assets/Settings/DroneConfigs/SmallFpvHeatConfig.asset";

    private const string MissionOneDescription =
        "Учебный вылет в закрытой зоне. Отработайте взлёт, поиск цели и сброс боеприпаса на одиночную мишень. Потеря борта не завершает миссию: можно взять новый дрон и повторить заход.";

    private const string MissionTwoDescription =
        "Группа прикрытия противника выдвигается в лесополосу. Уничтожьте БТР охранения, затем догоните и уничтожьте отходящую РСЗО, пока она не покинула район. Техника ведёт огонь по дронам и ставит дымовые завесы.";

    private const string MissionTwoHint = "РСЗО отступает — не дайте ей выйти из района";

    [MenuItem("Tools/DroneSimulator/Mission UI/Apply Mission Content")]
    public static void Apply()
    {
        ApplyMission(MissionOnePath, "Тренировка", MissionOneDescription, string.Empty);
        ApplyMission(MissionTwoPath, "БТР и РСЗО", MissionTwoDescription, MissionTwoHint);
        ApplyDroneName(ReconDronePath, "Mavic Recon");
        ApplyDroneName(StrikeDronePath, "FPV-7 HEAT");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[MissionContentBuilder] Контент миссий и дронов обновлён.");
    }

    private static void ApplyMission(string path, string displayName, string description, string tacticalHint)
    {
        MissionDefinition definition = AssetDatabase.LoadAssetAtPath<MissionDefinition>(path);
        if (definition == null)
        {
            Debug.LogError($"[MissionContentBuilder] Не найден ассет миссии: {path}");
            return;
        }

        SerializedObject serialized = new SerializedObject(definition);
        SetString(serialized, "displayName", displayName);
        SetString(serialized, "description", description);
        SetString(serialized, "tacticalHint", tacticalHint);
        serialized.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(definition);
        Debug.Log($"[MissionContentBuilder] Миссия обновлена: {path}");
    }

    private static void ApplyDroneName(string path, string droneName)
    {
        DroneConfig config = AssetDatabase.LoadAssetAtPath<DroneConfig>(path);
        if (config == null)
        {
            Debug.LogError($"[MissionContentBuilder] Не найден конфиг дрона: {path}");
            return;
        }

        SerializedObject serialized = new SerializedObject(config);
        SetString(serialized, "droneName", droneName);
        serialized.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(config);
        Debug.Log($"[MissionContentBuilder] Дрон обновлён: {path}");
    }

    private static void SetString(SerializedObject serialized, string field, string value)
    {
        SerializedProperty property = serialized.FindProperty(field);
        if (property == null)
        {
            Debug.LogError($"[MissionContentBuilder] Не найдено поле '{field}' в {serialized.targetObject.GetType().Name}");
            return;
        }

        property.stringValue = value;
    }
}
