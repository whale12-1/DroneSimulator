using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class ProjectStabilizer
{
    private const string PreparingScene = "Assets/Scenes/PreparingDemo.unity";
    private const string ForestScene = "Assets/Scenes/FirstMission(Forest).unity";
    private const string BunkerScene = "Assets/Scenes/Bunker(main).unity";

    private const string MissionOnePath = "Assets/Resources/Missions/Mission1.asset";
    private const string MissionTwoPath = "Assets/Resources/Missions/Mission2.asset";
    private const string MissionThreePath = "Assets/Resources/Missions/Mission3.asset";

    private const string CrashTypo = "рызбились";
    private const string CrashFixed = "разбились";

    [MenuItem("Tools/DroneSimulator/Stabilization/1. Reset TMP Font Materials", false, 100)]
    public static void ResetFontMaterials()
    {
        string previousScene = EditorSceneManager.GetActiveScene().path;
        string[] scenes = { PreparingScene, ForestScene, BunkerScene };
        int total = 0;

        foreach (string path in scenes)
        {
            if (!File.Exists(path))
            {
                Debug.LogWarning($"[ProjectStabilizer] Сцена не найдена: {path}");
                continue;
            }

            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            int changed = ResetFontMaterialsInScene();
            total += changed;

            if (changed > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }

            Debug.Log($"[ProjectStabilizer] {path}: сброшено TMP-материалов — {changed}.");
        }

        RestoreScene(previousScene);
        AssetDatabase.SaveAssets();
        Debug.Log($"<color=lime>[ProjectStabilizer]</color> Сброс материалов шрифта завершён. Всего исправлено: {total}.");
    }

    [MenuItem("Tools/DroneSimulator/Stabilization/2. Fix Crash Panel Typo", false, 101)]
    public static void FixCrashTypo()
    {
        string previousScene = EditorSceneManager.GetActiveScene().path;
        string[] scenes = { PreparingScene, ForestScene };
        int total = 0;

        foreach (string path in scenes)
        {
            if (!File.Exists(path))
            {
                Debug.LogWarning($"[ProjectStabilizer] Сцена не найдена: {path}");
                continue;
            }

            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            int changed = FixCrashTypoInScene();
            total += changed;

            if (changed > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }

            Debug.Log($"[ProjectStabilizer] {path}: исправлено надписей — {changed}.");
        }

        RestoreScene(previousScene);
        AssetDatabase.SaveAssets();
        Debug.Log($"<color=lime>[ProjectStabilizer]</color> Опечатка «{CrashTypo}» устранена. Всего правок: {total}.");
    }

    [MenuItem("Tools/DroneSimulator/Stabilization/3. Validate Mission Drone Links", false, 102)]
    public static void ValidateMissionDroneLinks()
    {
        string[] missions = { MissionOnePath, MissionTwoPath, MissionThreePath };
        int total = 0;

        foreach (string path in missions)
            total += ValidateMissionDrones(path);

        AssetDatabase.SaveAssets();
        Debug.Log($"<color=lime>[ProjectStabilizer]</color> Проверка ссылок на дроны завершена. Исправлений: {total}.");
    }

    [MenuItem("Tools/DroneSimulator/Stabilization/4. Clean Build Settings", false, 103)]
    public static void CleanBuildSettings()
    {
        string[] excluded = { "SampleScene", "TerrainDemoScene" };
        List<EditorBuildSettingsScene> kept = new List<EditorBuildSettingsScene>();
        int removed = 0;

        foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
        {
            string sceneName = Path.GetFileNameWithoutExtension(scene.path);

            if (System.Array.IndexOf(excluded, sceneName) >= 0)
            {
                Debug.Log($"[ProjectStabilizer] Из Build Settings удалена сцена: {scene.path}");
                removed++;
                continue;
            }

            kept.Add(scene);
        }

        EditorBuildSettings.scenes = kept.ToArray();
        PersistBuildSettings();
        VerifyBuildSettings();

        foreach (EditorBuildSettingsScene scene in kept)
            Debug.Log($"[ProjectStabilizer] Build Settings: {scene.path}");

        Debug.Log($"<color=lime>[ProjectStabilizer]</color> Build Settings очищены. Удалено сцен: {removed}.");
    }

    private static void PersistBuildSettings()
    {
        string path = Path.Combine(Directory.GetCurrentDirectory(), "ProjectSettings/EditorBuildSettings.asset");
        if (!File.Exists(path))
        {
            Debug.LogWarning($"[ProjectStabilizer] Файл Build Settings не найден: {path}");
            return;
        }

        string[] lines = File.ReadAllLines(path);
        List<string> result = new List<string>();
        int index = 0;
        bool blockReplaced = false;

        while (index < lines.Length)
        {
            string line = lines[index];

            if (!blockReplaced && line.TrimStart().StartsWith("m_Scenes:"))
            {
                result.Add(line);
                result.AddRange(BuildSceneLines());
                blockReplaced = true;
                index++;

                while (index < lines.Length)
                {
                    string inner = lines[index];
                    if (inner.StartsWith("  m_") && !inner.TrimStart().StartsWith("m_Scenes:")) break;
                    index++;
                }

                continue;
            }

            result.Add(line);
            index++;
        }

        if (!blockReplaced)
        {
            Debug.LogWarning("[ProjectStabilizer] Блок m_Scenes не найден в EditorBuildSettings.asset.");
            return;
        }

        File.WriteAllLines(path, result.ToArray());
        Debug.Log($"[ProjectStabilizer] EditorBuildSettings.asset перезаписан напрямую ({BuildSceneLines().Count} строк сцен).");
    }

    private static List<string> BuildSceneLines()
    {
        List<string> lines = new List<string>();

        foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
        {
            lines.Add($"  - enabled: {(scene.enabled ? 1 : 0)}");
            lines.Add($"    path: {scene.path}");
            lines.Add($"    guid: {AssetDatabase.AssetPathToGUID(scene.path)}");
        }

        return lines;
    }

    private static void VerifyBuildSettings()
    {
        Debug.Log($"[ProjectStabilizer] Проверка Build Settings в памяти: сцен {EditorBuildSettings.scenes.Length}.");

        foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
            Debug.Log($"[ProjectStabilizer] Проверка Build Settings: {scene.path} (enabled={scene.enabled})");
    }

    private static int ResetFontMaterialsInScene()
    {
        TMP_Text[] texts = Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include);
        List<Material> suspects = new List<Material>();
        int changed = 0;

        foreach (TMP_Text text in texts)
        {
            if (text == null || text.font == null) continue;

            Material defaultMaterial = text.font.material;
            if (defaultMaterial == null) continue;

            Material shared = text.fontSharedMaterial;
            if (shared == defaultMaterial) continue;

            if (shared != null) suspects.Add(shared);

            ApplyDefaultFontMaterial(text, defaultMaterial);
            EditorUtility.SetDirty(text);
            changed++;

            string previousName = shared != null ? shared.name : "null";
            Debug.Log($"[ProjectStabilizer] Материал шрифта «{previousName}» заменён на дефолтный у объекта: {DescribePath(text.transform)}");
        }

        if (suspects.Count > 0)
            RemoveOrphanMaterials(suspects);

        return changed;
    }

    private static void ApplyDefaultFontMaterial(TMP_Text text, Material defaultMaterial)
    {
        SerializedObject serialized = new SerializedObject(text);

        SerializedProperty shared = serialized.FindProperty("m_sharedMaterial");
        if (shared != null) shared.objectReferenceValue = defaultMaterial;

        SerializedProperty sharedArray = serialized.FindProperty("m_fontSharedMaterials");
        if (sharedArray != null) sharedArray.arraySize = 0;

        SerializedProperty instanceMaterial = serialized.FindProperty("m_fontMaterial");
        if (instanceMaterial != null) instanceMaterial.objectReferenceValue = null;

        SerializedProperty instanceArray = serialized.FindProperty("m_fontMaterials");
        if (instanceArray != null) instanceArray.arraySize = 0;

        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void RemoveOrphanMaterials(List<Material> materials)
    {
        foreach (Material material in materials)
        {
            if (material == null) continue;
            if (AssetDatabase.Contains(material)) continue;
            if (IsMaterialReferenced(material)) continue;

            string label = material.name;
            Object.DestroyImmediate(material, true);
            Debug.Log($"[ProjectStabilizer] Удалён запечённый инстанс материала: {label}");
        }
    }

    private static bool IsMaterialReferenced(Material material)
    {
        Component[] components = Object.FindObjectsByType<Component>(FindObjectsInactive.Include);

        foreach (Component component in components)
        {
            if (component == null) continue;

            SerializedObject serialized = new SerializedObject(component);
            SerializedProperty property = serialized.GetIterator();

            while (property.NextVisible(true))
            {
                if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
                if (property.objectReferenceValue == material) return true;
            }
        }

        return false;
    }

    private static int FixCrashTypoInScene()
    {
        int changed = 0;
        TMP_Text[] texts = Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include);

        foreach (TMP_Text text in texts)
        {
            if (text == null || string.IsNullOrEmpty(text.text)) continue;
            if (!text.text.Contains(CrashTypo)) continue;

            text.text = text.text.Replace(CrashTypo, CrashFixed);
            EditorUtility.SetDirty(text);
            changed++;

            Debug.Log($"[ProjectStabilizer] Опечатка исправлена у объекта: {DescribePath(text.transform)} → «{text.text.Trim()}»");
        }

        Text[] legacyTexts = Object.FindObjectsByType<Text>(FindObjectsInactive.Include);

        foreach (Text text in legacyTexts)
        {
            if (text == null || string.IsNullOrEmpty(text.text)) continue;
            if (!text.text.Contains(CrashTypo)) continue;

            text.text = text.text.Replace(CrashTypo, CrashFixed);
            EditorUtility.SetDirty(text);
            changed++;
        }

        return changed;
    }

    private static int ValidateMissionDrones(string path)
    {
        MissionDefinition definition = AssetDatabase.LoadAssetAtPath<MissionDefinition>(path);
        if (definition == null)
        {
            Debug.LogWarning($"[ProjectStabilizer] Ассет миссии не найден: {path}");
            return 0;
        }

        SerializedObject serialized = new SerializedObject(definition);
        SerializedProperty drones = serialized.FindProperty("availableDrones");
        SerializedProperty defaultDrone = serialized.FindProperty("defaultDrone");
        int fixedCount = 0;

        if (drones != null && drones.isArray)
        {
            for (int i = drones.arraySize - 1; i >= 0; i--)
            {
                UnityEngine.Object reference = drones.GetArrayElementAtIndex(i).objectReferenceValue;

                if (reference == null)
                {
                    Debug.LogWarning($"[ProjectStabilizer] {path}: пустой слот availableDrones[{i}] — оставлен как есть.");
                    continue;
                }

                if (reference is DroneConfig) continue;

                Debug.Log($"[ProjectStabilizer] {path}: удалена некорректная ссылка availableDrones[{i}] → {reference.name} ({reference.GetType().Name}).");
                drones.DeleteArrayElementAtIndex(i);
                fixedCount++;
            }
        }

        if (defaultDrone != null && drones != null && drones.arraySize > 0)
        {
            if (!(defaultDrone.objectReferenceValue is DroneConfig))
            {
                UnityEngine.Object replacement = drones.GetArrayElementAtIndex(0).objectReferenceValue;
                string previous = defaultDrone.objectReferenceValue != null ? defaultDrone.objectReferenceValue.name : "null";
                defaultDrone.objectReferenceValue = replacement;
                fixedCount++;
                Debug.Log($"[ProjectStabilizer] {path}: defaultDrone «{previous}» заменён на {(replacement != null ? replacement.name : "null")}.");
            }
        }

        if (fixedCount > 0)
        {
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);
        }
        else
        {
            Debug.Log($"[ProjectStabilizer] {path}: ссылки на дроны корректны (только DroneConfig).");
        }

        return fixedCount;
    }

    private static string DescribePath(Transform transform)
    {
        string path = transform.name;
        Transform parent = transform.parent;

        while (parent != null)
        {
            path = parent.name + "/" + path;
            parent = parent.parent;
        }

        return path;
    }

    private static void RestoreScene(string path)
    {
        if (string.IsNullOrEmpty(path)) return;
        if (!File.Exists(path)) return;

        EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
    }
}
