using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;

public static class AudioMixerBuilder
{
    private const string MixerFolder = "Assets/Resources/Audio";
    private const string MixerPath = "Assets/Resources/Audio/MainMixer.mixer";

    private static readonly string[] RootGroups = { "Music", "SFX", "UI", "Voice" };
    private static readonly string[] SfxGroups = { "DroneMotors", "EW" };

    private static readonly string[][] ExposedParameters =
    {
        new[] { "Master", AudioBus.MasterParameter },
        new[] { "Music", AudioBus.MusicParameter },
        new[] { "SFX", AudioBus.SfxParameter },
        new[] { "DroneMotors", AudioBus.MotorsParameter },
        new[] { "EW", AudioBus.EwParameter },
        new[] { "UI", AudioBus.UiParameter },
        new[] { "Voice", AudioBus.VoiceParameter }
    };

    [MenuItem("Tools/DroneSimulator/Impact/3. Build Audio Mixer", false, 202)]
    public static void BuildMainMixer()
    {
        Type controllerType = FindType("UnityEditor.Audio.AudioMixerController");
        Type groupType = FindType("UnityEditor.Audio.AudioMixerGroupController");
        Type exposedType = FindType("UnityEditor.Audio.ExposedAudioParameter");

        if (controllerType == null || groupType == null || exposedType == null)
        {
            Debug.LogError($"[AudioMixerBuilder] Внутренние типы Unity не найдены: controller={controllerType}, group={groupType}, exposed={exposedType}");
            return;
        }

        EnsureFolder(MixerFolder);

        object controller = LoadController(controllerType);

        if (controller == null)
        {
            controller = CreateController(controllerType);
            if (controller == null)
            {
                Debug.LogError("[AudioMixerBuilder] Не удалось создать микшер через внутренний API Unity.");
                return;
            }
        }

        object masterGroup = GetProperty(controllerType, "masterGroup").GetValue(controller) as UnityEngine.Object;

        if (masterGroup == null)
        {
            Debug.LogError("[AudioMixerBuilder] У микшера нет мастер-группы.");
            return;
        }

        object sfxGroup = EnsureGroup(controllerType, groupType, controller, masterGroup, "SFX");
        EnsureGroup(controllerType, groupType, controller, masterGroup, "Music");
        EnsureGroup(controllerType, groupType, controller, masterGroup, "UI");
        EnsureGroup(controllerType, groupType, controller, masterGroup, "Voice");

        foreach (string groupName in SfxGroups)
            EnsureGroup(controllerType, groupType, controller, sfxGroup, groupName);

        SetGroupVolumes(controllerType, groupType, controller);

        int exposed = ExposeVolumes(controllerType, groupType, exposedType, controller, masterGroup);

        EditorUtility.SetDirty((UnityEngine.Object)controller);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(MixerPath, ImportAssetOptions.ForceUpdate);

        BuildGroupViews(controllerType, groupType, controller);

        VerifyMixer();

        Debug.Log($"<color=lime>[AudioMixerBuilder]</color> Микшер готов: {MixerPath}. Exposed-параметров: {exposed}.");
    }

    [MenuItem("Tools/DroneSimulator/Impact/4. Verify Audio Mixer", false, 203)]
    public static void VerifyMixer()
    {
        AudioMixer mixer = Resources.Load<AudioMixer>("Audio/MainMixer");

        if (mixer == null)
        {
            Debug.LogError("[AudioMixerBuilder] Проверка не пройдена: Resources.Load('Audio/MainMixer') вернул null.");
            return;
        }

        string[] expectedGroups = { "Master", "Music", "SFX", "DroneMotors", "EW", "UI", "Voice" };

        foreach (string groupName in expectedGroups)
        {
            AudioMixerGroup[] groups = mixer.FindMatchingGroups(groupName);
            Debug.Log($"[AudioMixerBuilder] Группа '{groupName}': найдено {groups.Length}.");
        }

        string[] expectedParameters =
        {
            AudioBus.MasterParameter, AudioBus.MusicParameter, AudioBus.SfxParameter,
            AudioBus.MotorsParameter, AudioBus.EwParameter, AudioBus.UiParameter, AudioBus.VoiceParameter
        };

        foreach (string parameter in expectedParameters)
        {
            bool ok = mixer.SetFloat(parameter, -6f);
            Debug.Log($"[AudioMixerBuilder] Проверка mix.SetFloat('{parameter}'): {(ok ? "OK" : "требует Play Mode или инстанцирования микшера")}");
            if (ok) mixer.SetFloat(parameter, 0f);
        }

        SerializedObject serialized = new SerializedObject(mixer);
        SerializedProperty exposedProperty = serialized.FindProperty("m_ExposedParameters");
        SerializedProperty viewsProperty = serialized.FindProperty("m_AudioMixerGroupViews");

        if (exposedProperty != null)
        {
            Debug.Log($"[AudioMixerBuilder] m_ExposedParameters в ассете: {exposedProperty.arraySize}.");

            for (int i = 0; i < exposedProperty.arraySize; i++)
            {
                SerializedProperty entry = exposedProperty.GetArrayElementAtIndex(i);
                SerializedProperty nameProperty = entry.FindPropertyRelative("name");
                SerializedProperty guidProperty = entry.FindPropertyRelative("guid");
                Debug.Log($"[AudioMixerBuilder]   {i}: {DescribeProperty(nameProperty)} = {DescribeProperty(guidProperty)}");
            }
        }

        if (viewsProperty != null)
            Debug.Log($"[AudioMixerBuilder] m_AudioMixerGroupViews в ассете: {viewsProperty.arraySize}.");

        LogExposedParametersFromFile();

        Debug.Log($"<color=lime>[AudioMixerBuilder]</color> Проверка микшера завершена: {mixer.name}.");
    }

    private static void SetGroupVolumes(Type controllerType, Type groupType, object controller)
    {
        MethodInfo getAll = GetInstanceMethod(controllerType, "GetAllAudioGroupsSlow", 0);
        PropertyInfo targetSnapshot = GetProperty(controllerType, "TargetSnapshot");
        MethodInfo setVolume = GetInstanceMethod(groupType, "SetValueForVolume", 3);

        if (getAll == null || targetSnapshot == null || setVolume == null)
        {
            Debug.LogWarning("[AudioMixerBuilder] Нет доступа к TargetSnapshot / SetValueForVolume.");
            return;
        }

        System.Collections.IList groups = getAll.Invoke(controller, null) as System.Collections.IList;
        object snapshot = targetSnapshot.GetValue(controller);

        if (groups == null || snapshot == null) return;

        foreach (object group in groups)
            setVolume.Invoke(group, new[] { controller, snapshot, (object)1f });

        Debug.Log($"[AudioMixerBuilder] Громкость групп выставлена в 0 дБ: {groups.Count}.");
    }

    private static void BuildGroupViews(Type controllerType, Type groupType, object controller)
    {
        MethodInfo getAll = GetInstanceMethod(controllerType, "GetAllAudioGroupsSlow", 0);
        PropertyInfo groupIdProperty = GetProperty(groupType, "groupID");

        if (getAll == null || groupIdProperty == null)
        {
            Debug.LogWarning("[AudioMixerBuilder] Нет доступа к GetAllAudioGroupsSlow / groupID.");
            return;
        }

        System.Collections.IList groups = getAll.Invoke(controller, null) as System.Collections.IList;
        if (groups == null || groups.Count == 0) return;

        List<string> ids = new List<string>();

        foreach (object group in groups)
        {
            object id = groupIdProperty.GetValue(group);
            if (id == null) continue;

            string hex = id.ToString().Replace("-", string.Empty);
            if (string.IsNullOrEmpty(hex)) continue;
            ids.Add(hex);
        }

        if (ids.Count == 0) return;

        string fullPath = System.IO.Path.Combine(Directory.GetCurrentDirectory(), MixerPath);
        if (!System.IO.File.Exists(fullPath))
        {
            Debug.LogWarning($"[AudioMixerBuilder] Файл микшера не найден: {fullPath}");
            return;
        }

        string[] lines = System.IO.File.ReadAllLines(fullPath);
        List<string> result = new List<string>();
        bool replaced = false;
        int index = 0;

        while (index < lines.Length)
        {
            string line = lines[index];

            if (!replaced && line.TrimStart().StartsWith("m_AudioMixerGroupViews:"))
            {
                result.Add("  m_AudioMixerGroupViews:");
                result.Add("  - guids:");

                foreach (string id in ids)
                    result.Add("    - " + id);

                result.Add("    name: View");
                replaced = true;
                index++;

                while (index < lines.Length)
                {
                    string inner = lines[index];
                    if (inner.StartsWith("  m_") || inner.StartsWith("---")) break;
                    index++;
                }

                continue;
            }

            result.Add(line);
            index++;
        }

        if (!replaced) return;

        System.IO.File.WriteAllLines(fullPath, result.ToArray());
        AssetDatabase.ImportAsset(MixerPath, ImportAssetOptions.ForceUpdate);

        Debug.Log($"[AudioMixerBuilder] В вид микшера добавлено групп: {ids.Count}.");
    }

    private static string DescribeProperty(SerializedProperty property)
    {
        if (property == null) return "?";

        switch (property.propertyType)
        {
            case SerializedPropertyType.String:
                return property.stringValue;

            case SerializedPropertyType.ObjectReference:
                return property.objectReferenceValue != null ? property.objectReferenceValue.name : "null";

            case SerializedPropertyType.Integer:
            case SerializedPropertyType.LayerMask:
            case SerializedPropertyType.Character:
                return property.intValue.ToString();

            case SerializedPropertyType.ArraySize:
                return property.arraySize.ToString();

            case SerializedPropertyType.Float:
                return property.floatValue.ToString("0.###");

            case SerializedPropertyType.Boolean:
                return property.boolValue.ToString();

            case SerializedPropertyType.Color:
                return property.colorValue.ToString();

            case SerializedPropertyType.Enum:
                return property.enumValueIndex.ToString();

            case SerializedPropertyType.Vector2:
                return property.vector2Value.ToString();

            case SerializedPropertyType.Vector3:
                return property.vector3Value.ToString();

            case SerializedPropertyType.Vector4:
                return property.vector4Value.ToString();

            case SerializedPropertyType.Rect:
                return property.rectValue.ToString();

            case SerializedPropertyType.Bounds:
                return property.boundsValue.ToString();

            case SerializedPropertyType.Generic:
                return "struct:" + property.type;

            default:
                return property.propertyType.ToString();
        }
    }

    private static void LogExposedParametersFromFile()
    {
        string fullPath = Path.Combine(Directory.GetCurrentDirectory(), MixerPath);

        if (!File.Exists(fullPath))
        {
            Debug.LogWarning($"[AudioMixerBuilder] Файл микшера не найден для чтения Expose-параметров: {fullPath}");
            return;
        }

        string[] lines = File.ReadAllLines(fullPath);
        bool inside = false;
        int logged = 0;

        foreach (string line in lines)
        {
            if (!inside && line.TrimStart().StartsWith("m_ExposedParameters:"))
            {
                inside = true;
                Debug.Log($"[AudioMixerBuilder] Файл: {line.Trim()}");
                continue;
            }

            if (!inside) continue;
            if (line.StartsWith("  m_") || line.StartsWith("---")) break;

            Debug.Log($"[AudioMixerBuilder] Файл: {line.Trim()}");
            logged++;
        }

        Debug.Log($"[AudioMixerBuilder] Expose-параметров прочитано из файла: {logged}.");
    }

    private static object LoadController(Type controllerType)
    {
        UnityEngine.Object existing = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(MixerPath);
        if (existing == null) return null;

        if (!controllerType.IsInstanceOfType(existing))
        {
            Debug.LogError($"[AudioMixerBuilder] Ассет {MixerPath} имеет неожиданный тип: {existing.GetType().Name}");
            return null;
        }

        Debug.Log($"[AudioMixerBuilder] Существующий микшер загружен: {MixerPath}");
        return existing;
    }

    private static object CreateController(Type controllerType)
    {
        MethodInfo factory = GetStaticMethod(controllerType, "CreateMixerControllerAtPath", 1);

        if (factory != null)
        {
            object created = factory.Invoke(null, new object[] { MixerPath });
            if (created != null)
            {
                Debug.Log($"[AudioMixerBuilder] Микшер создан через CreateMixerControllerAtPath: {MixerPath}");
                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(MixerPath, ImportAssetOptions.ForceUpdate);
                return AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(MixerPath);
            }
        }

        MethodInfo defaultAsset = GetInstanceMethod(controllerType, "CreateDefaultAsset", 1);

        if (defaultAsset == null)
        {
            Debug.LogError("[AudioMixerBuilder] CreateMixerControllerAtPath и CreateDefaultAsset недоступны.");
            return null;
        }

        object instance = ScriptableObject.CreateInstance(controllerType);
        defaultAsset.Invoke(instance, new object[] { MixerPath });
        AssetDatabase.CreateAsset((UnityEngine.Object)instance, MixerPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(MixerPath, ImportAssetOptions.ForceUpdate);

        Debug.Log($"[AudioMixerBuilder] Микшер создан через CreateDefaultAsset: {MixerPath}");
        return AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(MixerPath);
    }

    private static object EnsureGroup(Type controllerType, Type groupType, object controller, object parent, string groupName)
    {
        if (parent == null)
        {
            Debug.LogError($"[AudioMixerBuilder] Нет родительской группы для '{groupName}'.");
            return null;
        }

        Array children = GetProperty(groupType, "children").GetValue(parent) as Array;

        if (children != null)
        {
            foreach (object child in children)
            {
                UnityEngine.Object childObject = child as UnityEngine.Object;
                if (childObject != null && childObject.name == groupName)
                {
                    Debug.Log($"[AudioMixerBuilder] Группа '{groupName}' уже существует.");
                    return child;
                }
            }
        }

        MethodInfo createNewGroup = GetInstanceMethod(controllerType, "CreateNewGroup", 2);
        MethodInfo addChildToParent = GetInstanceMethod(controllerType, "AddChildToParent", 2);

        if (createNewGroup == null || addChildToParent == null)
        {
            Debug.LogError("[AudioMixerBuilder] CreateNewGroup / AddChildToParent недоступны.");
            return null;
        }

        object group = createNewGroup.Invoke(controller, new object[] { groupName, false });
        if (group == null)
        {
            Debug.LogError($"[AudioMixerBuilder] Не удалось создать группу '{groupName}'.");
            return null;
        }

        UnityEngine.Object groupObject = group as UnityEngine.Object;
        if (groupObject != null) groupObject.name = groupName;

        addChildToParent.Invoke(controller, new object[] { group, parent });

        Debug.Log($"[AudioMixerBuilder] Создана группа '{groupName}' в '{(parent as UnityEngine.Object)?.name}'.");
        return group;
    }

    private static int ExposeVolumes(Type controllerType, Type groupType, Type exposedType, object controller, object masterGroup)
    {
        PropertyInfo exposedProperty = GetProperty(controllerType, "exposedParameters");
        FieldInfo guidField = exposedType.GetField("guid", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        FieldInfo nameField = exposedType.GetField("name", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        MethodInfo getVolumeGuid = GetInstanceMethod(groupType, "GetGUIDForVolume", 0);

        if (exposedProperty == null || guidField == null || nameField == null || getVolumeGuid == null)
        {
            Debug.LogError("[AudioMixerBuilder] Нет доступа к exposedParameters / GetGUIDForVolume.");
            return 0;
        }

        Array current = exposedProperty.GetValue(controller) as Array;
        List<object> entries = new List<object>();

        if (current != null)
        {
            foreach (object entry in current) entries.Add(entry);
        }

        HashSet<string> existingNames = new HashSet<string>();
        foreach (object entry in entries)
        {
            string existingName = nameField.GetValue(entry) as string;
            if (!string.IsNullOrEmpty(existingName)) existingNames.Add(existingName);
        }

        int added = 0;

        foreach (string[] pair in ExposedParameters)
        {
            string groupName = pair[0];
            string parameterName = pair[1];

            if (existingNames.Contains(parameterName)) continue;

            object group = FindGroup(controllerType, groupType, controller, masterGroup, groupName);
            if (group == null)
            {
                Debug.LogWarning($"[AudioMixerBuilder] Группа '{groupName}' не найдена — параметр '{parameterName}' не выставлен.");
                continue;
            }

            object volumeGuid = getVolumeGuid.Invoke(group, null);
            object entry = Activator.CreateInstance(exposedType);
            guidField.SetValue(entry, volumeGuid);
            nameField.SetValue(entry, parameterName);

            entries.Add(entry);
            existingNames.Add(parameterName);
            added++;

            Debug.Log($"[AudioMixerBuilder] Exposed: {groupName}.Volume → '{parameterName}' (guidType={guidField.FieldType.Name})");
        }

        Array result = Array.CreateInstance(exposedType, entries.Count);
        for (int i = 0; i < entries.Count; i++) result.SetValue(entries[i], i);

        exposedProperty.SetValue(controller, result);
        Debug.Log($"[AudioMixerBuilder] Exposed-параметров всего: {entries.Count} (добавлено {added}).");

        return entries.Count;
    }

    private static object FindGroup(Type controllerType, Type groupType, object controller, object masterGroup, string groupName)
    {
        MethodInfo getAll = GetInstanceMethod(controllerType, "GetAllAudioGroupsSlow", 0);
        if (getAll == null) return null;

        System.Collections.IList groups = getAll.Invoke(controller, null) as System.Collections.IList;
        if (groups == null) return null;

        foreach (object group in groups)
        {
            UnityEngine.Object groupObject = group as UnityEngine.Object;
            if (groupObject != null && groupObject.name == groupName) return group;
        }

        return null;
    }

    private static Type FindType(string fullName)
    {
        System.Reflection.Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();

        foreach (System.Reflection.Assembly assembly in assemblies)
        {
            Type type = assembly.GetType(fullName, false);
            if (type != null) return type;
        }

        return null;
    }

    private static PropertyInfo GetProperty(Type type, string name)
    {
        return type.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
    }

    private static MethodInfo GetStaticMethod(Type type, string name, int parameterCount)
    {
        foreach (MethodInfo method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
        {
            if (method.Name != name) continue;
            if (method.GetParameters().Length != parameterCount) continue;
            return method;
        }

        return null;
    }

    private static MethodInfo GetInstanceMethod(Type type, string name, int parameterCount)
    {
        foreach (MethodInfo method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
        {
            if (method.Name != name) continue;
            if (method.GetParameters().Length != parameterCount) continue;
            return method;
        }

        return null;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;

        string parent = System.IO.Path.GetDirectoryName(path).Replace("\\", "/");
        string leaf = System.IO.Path.GetFileName(path);

        if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);

        AssetDatabase.CreateFolder(parent, leaf);
    }
}
