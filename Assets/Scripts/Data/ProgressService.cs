using System.IO;        // <-- вот это забыл
using System.Linq;      // <-- нужно для .All(), если ещё не подключено
using UnityEngine;

[System.Serializable]
public class SaveData
{
    public System.Collections.Generic.List<string> completedMissionIds = new();
    public System.Collections.Generic.List<string> unlockedMissionIds = new();
    public int totalScore;
}

public static class ProgressService
{
    private static SaveData data;

    private static string SavePath => Path.Combine(Application.persistentDataPath, "progress.json");

    // Единая точка доступа — всегда через свойство, оно гарантирует загрузку
    public static SaveData Data => data ??= Load();

    public static bool IsUnlocked(MissionDefinition m)
    {
        if (m == null) return false;
        if (m.prerequisites == null || m.prerequisites.Length == 0) return true;

        var d = Data;   // <-- было raw-поле data
        if (d.unlockedMissionIds.Contains(m.id)) return true;

        return m.prerequisites.All(p => p != null && d.completedMissionIds.Contains(p.id));
    }

    public static void CompleteMission(MissionDefinition m, int score)
    {
        if (m == null) return;

        var d = Data;   // <-- было raw-поле data
        if (!d.completedMissionIds.Contains(m.id))
            d.completedMissionIds.Add(m.id);

        d.totalScore += score;
        Save();
    }

    private static SaveData Load()
    {
        if (!File.Exists(SavePath)) return new SaveData();

        try
        {
            var json = File.ReadAllText(SavePath);
            return JsonUtility.FromJson<SaveData>(json) ?? new SaveData();
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[ProgressService] Не удалось прочитать сейв: {e.Message}");
            return new SaveData();
        }
    }

    public static void Save()
    {
        try
        {
            File.WriteAllText(SavePath, JsonUtility.ToJson(Data, true));
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[ProgressService] Не удалось сохранить: {e.Message}");
        }
    }

    /// <summary>Сброс прогресса — удобно для тестов и кнопки "Reset progress".</summary>
    public static void ResetProgress()
    {
        data = new SaveData();
        Save();
    }
}