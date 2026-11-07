using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

[System.Serializable]
public class MissionRecord
{
    public string id;
    public bool completed;
    public float bestTime;
    public float lastTime;
    public int bestScore;
    public int lastScore;
    public int completions;
    public int attempts;
    public int bestTargets;
}

[System.Serializable]
public class SaveData
{
    public List<string> completedMissionIds = new();
    public List<string> unlockedMissionIds = new();
    public int totalScore;
    public List<MissionRecord> records = new();
}

public static class ProgressService
{
    private const int TargetScoreBonus = 25;
    private const int MaxTimeScoreBonus = 50;

    private static SaveData data;

    private static string SavePath => Path.Combine(Application.persistentDataPath, "progress.json");

    public static SaveData Data => data ??= Load();

    public static bool IsUnlocked(MissionDefinition m)
    {
        if (m == null) return false;
        if (m.prerequisites == null || m.prerequisites.Length == 0) return true;

        var d = Data;
        if (d.unlockedMissionIds.Contains(m.id)) return true;

        return m.prerequisites.All(p => p != null && d.completedMissionIds.Contains(p.id));
    }

    public static bool IsCompleted(MissionDefinition m)
    {
        if (m == null) return false;
        return Data.completedMissionIds.Contains(m.id);
    }

    public static MissionRecord GetRecord(MissionDefinition m)
    {
        if (m == null || string.IsNullOrEmpty(m.id)) return null;
        return GetRecord(Data, m.id);
    }

    public static MissionRecord GetRecord(string missionId)
    {
        if (string.IsNullOrEmpty(missionId)) return null;
        return GetRecord(Data, missionId);
    }

    public static int ComputeScore(MissionDefinition m, MissionResult result, float timeElapsed, int targetsDestroyed)
    {
        if (m == null) return 0;
        if (result != MissionResult.Success) return 0;

        int score = m.rewardPoints + Mathf.Max(0, targetsDestroyed) * TargetScoreBonus;

        if (m.timeLimit > 0f && timeElapsed > 0f)
        {
            float bonus = Mathf.Clamp01(1f - timeElapsed / m.timeLimit);
            score += Mathf.RoundToInt(bonus * MaxTimeScoreBonus);
        }

        return score;
    }

    public static void RecordResult(MissionDefinition m, MissionResult result, float timeElapsed, int targetsDestroyed)
    {
        if (m == null || string.IsNullOrEmpty(m.id)) return;

        SaveData d = Data;
        MissionRecord record = EnsureRecord(d, m.id);
        bool success = result == MissionResult.Success;

        record.attempts++;
        record.lastTime = timeElapsed;
        record.lastScore = ComputeScore(m, result, timeElapsed, targetsDestroyed);

        if (!success)
        {
            Save();
            return;
        }

        record.completed = true;
        record.completions++;

        if (record.bestTime <= 0f || timeElapsed < record.bestTime)
            record.bestTime = timeElapsed;

        if (record.lastScore > record.bestScore)
            record.bestScore = record.lastScore;

        if (targetsDestroyed > record.bestTargets)
            record.bestTargets = targetsDestroyed;

        if (!d.completedMissionIds.Contains(m.id))
            d.completedMissionIds.Add(m.id);

        d.totalScore += record.lastScore;
        Save();
    }

    public static void CompleteMission(MissionDefinition m, int score)
    {
        if (m == null) return;

        SaveData d = Data;
        if (!d.completedMissionIds.Contains(m.id))
            d.completedMissionIds.Add(m.id);

        MissionRecord record = EnsureRecord(d, m.id);
        record.completed = true;

        d.totalScore += score;
        Save();
    }

    private static MissionRecord GetRecord(SaveData d, string missionId)
    {
        if (d == null) return null;
        if (d.records == null) return null;

        foreach (MissionRecord record in d.records)
        {
            if (record != null && record.id == missionId)
                return record;
        }

        return null;
    }

    private static MissionRecord EnsureRecord(SaveData d, string missionId)
    {
        if (d.records == null)
            d.records = new List<MissionRecord>();

        MissionRecord existing = GetRecord(d, missionId);
        if (existing != null)
            return existing;

        MissionRecord created = new MissionRecord { id = missionId };
        d.records.Add(created);
        return created;
    }

    private static SaveData Load()
    {
        SaveData loaded = null;

        if (File.Exists(SavePath))
        {
            try
            {
                string json = File.ReadAllText(SavePath);
                loaded = JsonUtility.FromJson<SaveData>(json);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[ProgressService] Не удалось прочитать сейв: {e.Message}");
            }
        }

        return Sanitize(loaded ?? new SaveData());
    }

    private static SaveData Sanitize(SaveData target)
    {
        target.completedMissionIds ??= new List<string>();
        target.unlockedMissionIds ??= new List<string>();
        target.records ??= new List<MissionRecord>();
        return target;
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

    public static void ResetProgress()
    {
        data = new SaveData();
        Save();
    }
}
