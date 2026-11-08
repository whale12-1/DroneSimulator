using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class MissionHudBinder
{
    private static readonly string[] ScenePaths =
    {
        "Assets/Scenes/PreparingDemo.unity",
        "Assets/Scenes/FirstMission(Forest).unity"
    };

    [MenuItem("Tools/DroneSimulator/UI/2. Add Mission HUD", false, 21)]
    public static void AddMissionHud()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        string originalScenePath = EditorSceneManager.GetActiveScene().path;

        for (int i = 0; i < ScenePaths.Length; i++)
        {
            BindScene(ScenePaths[i]);
        }

        if (!string.IsNullOrEmpty(originalScenePath) && AssetDatabase.LoadAssetAtPath<SceneAsset>(originalScenePath) != null)
        {
            EditorSceneManager.OpenScene(originalScenePath, OpenSceneMode.Single);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static void BindScene(string scenePath)
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null)
        {
            Debug.LogError($"[MissionHud] Сцена не найдена: {scenePath}");
            return;
        }

        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

        DroneManager droneManager = Object.FindAnyObjectByType<DroneManager>();
        MissionController missionController = Object.FindAnyObjectByType<MissionController>();
        MissionUIManager uiManager = Object.FindAnyObjectByType<MissionUIManager>();
        Canvas canvas = Object.FindAnyObjectByType<Canvas>();

        if (droneManager == null || missionController == null)
        {
            Debug.LogWarning($"[MissionHud] В сцене {scenePath} нет DroneManager или MissionController, HUD не добавлен.");
            return;
        }

        Transform hudParent = null;
        if (canvas != null) hudParent = canvas.transform;
        else if (uiManager != null) hudParent = uiManager.transform;

        MissionHud hud = Object.FindAnyObjectByType<MissionHud>(FindObjectsInactive.Include);

        if (hud == null)
        {
            GameObject hudObject = new GameObject("MissionHud");

            if (hudParent != null)
            {
                hudObject.transform.SetParent(hudParent, false);
            }

            hud = hudObject.AddComponent<MissionHud>();
        }
        else if (hudParent != null && hud.transform.parent != hudParent)
        {
            hud.transform.SetParent(hudParent, false);
        }

        SerializedObject serializedHud = new SerializedObject(hud);
        serializedHud.FindProperty("droneManager").objectReferenceValue = droneManager;
        serializedHud.FindProperty("missionController").objectReferenceValue = missionController;
        serializedHud.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log($"<color=lime>[MissionHud]</color> {scenePath}: HUD привязан к DroneManager и MissionController (родитель: {(hudParent != null ? hudParent.name : "корень сцены")}).");
    }
}
