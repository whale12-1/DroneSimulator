using UnityEditor;
using UnityEngine;

public static class DroneAudioBinder
{
    private const string DroneFolder = "Assets/Prefabs/Drones";

    [MenuItem("Tools/DroneSimulator/Impact/2. Attach Drone Audio Sources", false, 201)]
    public static void AttachDroneAudio()
    {
        if (!AssetDatabase.IsValidFolder(DroneFolder))
        {
            Debug.LogError($"[DroneAudioBinder] Папка не найдена: {DroneFolder}");
            return;
        }

        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { DroneFolder });
        int motors = 0;
        int ew = 0;
        int processed = 0;

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.EndsWith(".prefab")) continue;

            processed++;
            BindPrefab(path, ref motors, ref ew);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"<color=lime>[DroneAudioBinder]</color> Обработано префабов: {processed}. Добавлено DroneMotorAudio: {motors}, EWAudio: {ew}.");
    }

    private static void BindPrefab(string path, ref int motorsCount, ref int ewCount)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        if (root == null)
        {
            Debug.LogWarning($"[DroneAudioBinder] Не удалось загрузить префаб: {path}");
            return;
        }

        bool changed = false;

        if (root.GetComponent<DroneMotorAudio>() == null)
        {
            AudioSource source = root.GetComponent<AudioSource>();
            if (source == null) source = root.AddComponent<AudioSource>();
            ConfigureMotorSource(source);

            DroneMotorAudio motorAudio = root.AddComponent<DroneMotorAudio>();
            AssignField(motorAudio, "motors", root.GetComponent<DroneMotors>());
            AssignField(motorAudio, "flightController", root.GetComponent<DroneFlightController>());

            changed = true;
            motorsCount++;

            Debug.Log($"[DroneAudioBinder] {path}: добавлен DroneMotorAudio на корень.");
        }

        Camera[] cameras = root.GetComponentsInChildren<Camera>(true);

        if (cameras.Length == 0)
        {
            Debug.Log($"[DroneAudioBinder] {path}: камера не найдена (префаб-вариант или без камеры) — EWAudio не добавлен.");
        }
        else
        {
            Camera camera = cameras[0];
            EWReceiver receiver = camera.GetComponent<EWReceiver>();
            if (receiver == null) receiver = camera.GetComponentInParent<EWReceiver>();
            if (receiver == null) receiver = root.GetComponentInChildren<EWReceiver>(true);

            if (camera.GetComponent<EWAudio>() == null)
            {
                AudioSource source = camera.GetComponent<AudioSource>();
                if (source == null) source = camera.gameObject.AddComponent<AudioSource>();
                ConfigureEwSource(source);

                EWAudio ewAudio = camera.gameObject.AddComponent<EWAudio>();
                AssignField(ewAudio, "receiver", receiver);

                changed = true;
                ewCount++;

                Debug.Log($"[DroneAudioBinder] {path}: добавлен EWAudio на камеру '{camera.gameObject.name}' (receiver={(receiver != null)}).");
            }
            else if (receiver != null)
            {
                EWAudio ewAudio = camera.GetComponent<EWAudio>();
                if (AssignField(ewAudio, "receiver", receiver)) changed = true;
            }
        }

        if (changed)
            PrefabUtility.SaveAsPrefabAsset(root, path);

        PrefabUtility.UnloadPrefabContents(root);
    }

    private static void ConfigureMotorSource(AudioSource source)
    {
        source.playOnAwake = false;
        source.loop = true;
        source.spatialBlend = 1f;
        source.dopplerLevel = 0f;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = 3f;
        source.maxDistance = 150f;
        source.volume = 0f;
        source.outputAudioMixerGroup = AudioBus.Motors;
    }

    private static void ConfigureEwSource(AudioSource source)
    {
        source.playOnAwake = false;
        source.loop = true;
        source.spatialBlend = 0f;
        source.dopplerLevel = 0f;
        source.volume = 0f;
        source.outputAudioMixerGroup = AudioBus.EW;
    }

    private static bool AssignField(Object target, string field, Object value)
    {
        if (value == null) return false;

        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(field);
        if (property == null) return false;
        if (property.objectReferenceValue == value) return false;

        property.objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return true;
    }
}
