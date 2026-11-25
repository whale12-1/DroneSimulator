using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

// Сборка viewmodel-руки от первого лица в Bunker(main): предплечье в рукаве,
// кисть/перчатка из примитивов, мягкая подсветка и WalkieTalkieHandAnchor
// внутри ладони, а также привязка поз к BunkerWalkieTalkieEvent и починка
// плашки InteractionCanvas/StorySubtitles.
// Меню идемпотентно: повторный запуск обновляет геометрию, материал и ссылки,
// дубликаты не создаются. Рука лежит на слое Default — камера исключает только
// слой PlayerBody (cullingMask = -65), поэтому viewmodel всегда в кадре.
public static class BunkerFirstPersonArmBuilder
{
    private const string ScenePath = "Assets/Scenes/Bunker(main).unity";
    private const string CameraName = "Main Camera";
    private const string ArmName = "FirstPersonRightArm";
    private const string AnchorName = "WalkieTalkieHandAnchor";
    private const string SubtitlesName = "StorySubtitles";
    private const string MaterialPath = "Assets/Materials/BunkerHand_Tactical.mat";

    private const string FirstRunText = "Я в бункере, что мне делать?";
    private const string BriefingText = "Ну что, Слава, готов к службе? Пройди сначала инструктаж.";

    // Плашка субтитров: pivot внизу, 64 px от нижнего края — над SittingHint (12…48 px).
    private const float SubtitleBottomOffset = 64f;

    private static readonly Color HandColor = new Color(0.24f, 0.26f, 0.19f, 1f);

    private static readonly Vector3 ArmRestPosition = new Vector3(0.30f, -0.55f, 0.06f);
    private static readonly Vector3 ArmRestEuler = new Vector3(-60f, 0f, 0f);
    private static readonly Vector3 ArmReachPosition = new Vector3(0.10f, -0.18f, 0.05f);
    private static readonly Vector3 ArmReachEuler = new Vector3(-10f, -8f, 0f);
    private static readonly Vector3 ArmHoldPosition = new Vector3(0.24f, -0.19f, 0.02f);
    private static readonly Vector3 ArmHoldEuler = new Vector3(-14f, -16f, 6f);

    // Рация в ладони: перед ладонью, чуть вверх и вправо, чтобы кисть читалась.
    private static readonly Vector3 AnchorLocalPosition = new Vector3(0.015f, 0.030f, 0.395f);
    private static readonly Vector3 GripLocalOffset = Vector3.zero;

    [MenuItem("Tools/DroneSimulator/Bunker/Build First Person Right Arm")]
    public static void Build()
    {
        Scene scene = EnsureSceneOpen();
        if (!scene.IsValid()) return;

        Transform cameraTransform = FindByName(scene, CameraName);
        if (cameraTransform == null)
        {
            Debug.LogError($"[BunkerFirstPersonArmBuilder] В сцене не найден {CameraName}.");
            return;
        }

        Material material = EnsureMaterial();
        Transform arm = BuildArm(cameraTransform, material);

        BunkerWalkieTalkieEvent walkie = Object.FindFirstObjectByType<BunkerWalkieTalkieEvent>(FindObjectsInactive.Include);
        if (walkie == null)
        {
            Debug.LogError("[BunkerFirstPersonArmBuilder] Не найден BunkerWalkieTalkieEvent (Prop_WalkieTalkie).");
            return;
        }

        BindWalkieEvent(walkie, arm);

        Transform subtitles = FindByName(scene, SubtitlesName);
        if (subtitles != null) FixSubtitlesRect(subtitles);
        else Debug.LogWarning($"[BunkerFirstPersonArmBuilder] Не найден InteractionCanvas/{SubtitlesName}.");

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log("[BunkerFirstPersonArmBuilder] Рука от первого лица собрана: предплечье, кисть, подсветка, " +
                  "WalkieTalkieHandAnchor в ладони; реплики и плашка субтитров обновлены.");
    }

    private static Scene EnsureSceneOpen()
    {
        Scene active = EditorSceneManager.GetActiveScene();
        if (string.Equals(active.path, ScenePath)) return active;

        if (active.isDirty)
        {
            Debug.LogError("[BunkerFirstPersonArmBuilder] Открыта другая сцена с несохранёнными изменениями. Сохраните её и повторите.");
            return default;
        }

        return EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
    }

    private static Transform BuildArm(Transform cameraTransform, Material material)
    {
        Transform arm = EnsureChild(cameraTransform, ArmName);
        arm.gameObject.layer = 0;
        arm.localPosition = ArmRestPosition;
        arm.localRotation = Quaternion.Euler(ArmRestEuler);
        arm.localScale = Vector3.one;

        // Рукав: манжета у запястья + предплечье вдоль оси +Z камеры.
        EnsurePrimitive(arm, "Cuff", PrimitiveType.Capsule,
            new Vector3(0f, 0f, 0.045f), new Vector3(90f, 0f, 0f), new Vector3(0.086f, 0.07f, 0.086f), material);

        EnsurePrimitive(arm, "Forearm", PrimitiveType.Capsule,
            new Vector3(0f, 0f, 0.17f), new Vector3(90f, 0f, 0f), new Vector3(0.075f, 0.17f, 0.075f), material);

        // Кисть-перчатка: ладонь и согнутые пальцы вокруг рации.
        EnsurePrimitive(arm, "Palm", PrimitiveType.Cube,
            new Vector3(0f, 0.004f, 0.335f), Vector3.zero, new Vector3(0.075f, 0.05f, 0.10f), material);

        EnsurePrimitive(arm, "Finger_Index", PrimitiveType.Cube,
            new Vector3(-0.026f, 0.016f, 0.385f), new Vector3(16f, 0f, 0f), new Vector3(0.016f, 0.022f, 0.07f), material);

        EnsurePrimitive(arm, "Finger_Middle", PrimitiveType.Cube,
            new Vector3(0f, 0.018f, 0.39f), new Vector3(16f, 0f, 0f), new Vector3(0.016f, 0.022f, 0.072f), material);

        EnsurePrimitive(arm, "Finger_Ring", PrimitiveType.Cube,
            new Vector3(0.026f, 0.016f, 0.385f), new Vector3(16f, 0f, 0f), new Vector3(0.016f, 0.022f, 0.07f), material);

        EnsurePrimitive(arm, "Thumb", PrimitiveType.Capsule,
            new Vector3(-0.048f, 0.012f, 0.355f), new Vector3(0f, 0f, 62f), new Vector3(0.02f, 0.045f, 0.02f), material);

        // Ладонь находится за рацией, поэтому подсвечиваем кисть и корпус рации
        // мягким точечным светом: в бункере нет лайтпроб, ambient слишком тусклый.
        Transform fill = EnsureChild(arm, "FillLight");
        Light fillLight = fill.GetComponent<Light>();
        if (fillLight == null) fillLight = fill.gameObject.AddComponent<Light>();

        fillLight.type = LightType.Point;
        fillLight.color = new Color(1f, 0.95f, 0.86f);
        fillLight.intensity = 0.6f;
        fillLight.range = 0.9f;
        fillLight.shadows = LightShadows.None;
        fillLight.lightmapBakeType = LightmapBakeType.Realtime;
        fill.localPosition = new Vector3(0f, 0.12f, 0.12f);

        if (fill.GetComponent<UniversalAdditionalLightData>() == null)
            fill.gameObject.AddComponent<UniversalAdditionalLightData>();

        // Анкер рации переезжает внутрь кисти: рация лежит в ладони, а не висит
        // перед камерой отдельным объектом. Ищем его сначала в руке (после
        // первого запуска он уже там), потом среди детей камеры.
        Transform anchor = arm.Find(AnchorName);
        if (anchor == null) anchor = cameraTransform.Find(AnchorName);

        if (anchor == null)
        {
            anchor = new GameObject(AnchorName).transform;
            anchor.SetParent(arm, false);
        }

        // Повторный запуск не должен плодить анкеры: лишние копии удаляются.
        RemoveDuplicateAnchors(arm, anchor);

        anchor.SetParent(arm, false);
        anchor.localPosition = AnchorLocalPosition;
        anchor.localRotation = Quaternion.identity;
        anchor.localScale = Vector3.one;

        // В покое рука опущена за нижний край экрана и выключена.
        arm.gameObject.SetActive(false);
        return arm;
    }

    private static Transform EnsurePrimitive(Transform parent, string name, PrimitiveType type,
        Vector3 localPosition, Vector3 localEuler, Vector3 localScale, Material material)
    {
        Transform existing = parent.Find(name);
        GameObject go;

        if (existing != null)
        {
            go = existing.gameObject;
        }
        else
        {
            go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
        }

        // У примитивов CreatePrimitive есть коллайдеры — viewmodel они не нужны
        // (иначе рука может мешать CharacterController/рейкастам).
        Collider[] colliders = go.GetComponents<Collider>();
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null) Object.DestroyImmediate(colliders[i]);
        }

        go.layer = 0;
        go.transform.localPosition = localPosition;
        go.transform.localRotation = Quaternion.Euler(localEuler);
        go.transform.localScale = localScale;

        MeshRenderer renderer = go.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            if (material != null) renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        return go.transform;
    }

    private static void RemoveDuplicateAnchors(Transform arm, Transform keep)
    {
        for (int i = arm.childCount - 1; i >= 0; i--)
        {
            Transform child = arm.GetChild(i);
            if (child == keep || child.name != AnchorName) continue;

            Debug.Log($"[BunkerFirstPersonArmBuilder] Удалён дубликат {AnchorName} в {arm.name}.");
            Object.DestroyImmediate(child.gameObject);
        }
    }

    private static Material EnsureMaterial()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);

        if (material == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                Debug.LogError("[BunkerFirstPersonArmBuilder] Не найден шейдер Universal Render Pipeline/Lit.");
                return null;
            }

            material = new Material(shader);
            material.name = "BunkerHand_Tactical";
            AssetDatabase.CreateAsset(material, MaterialPath);
        }

        SetColor(material, "_BaseColor", HandColor);
        SetColor(material, "_Color", HandColor);
        SetFloat(material, "_Metallic", 0.05f);
        SetFloat(material, "_Smoothness", 0.35f);

        EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssets();
        return material;
    }

    private static void SetColor(Material material, string property, Color value)
    {
        if (material.HasProperty(property)) material.SetColor(property, value);
    }

    private static void SetFloat(Material material, string property, float value)
    {
        if (material.HasProperty(property)) material.SetFloat(property, value);
    }

    // Привязка руки, поз и новых реплик к событию рации. В сцене значения
    // сериализованы, поэтому одних дефолтов в коде недостаточно.
    private static void BindWalkieEvent(BunkerWalkieTalkieEvent walkie, Transform arm)
    {
        Transform anchor = arm.Find(AnchorName);
        SerializedObject serialized = new SerializedObject(walkie);

        serialized.FindProperty("armRoot").objectReferenceValue = arm;
        serialized.FindProperty("handAnchor").objectReferenceValue = anchor;
        serialized.FindProperty("handLocalPosition").vector3Value = Vector3.zero;
        serialized.FindProperty("handLocalEuler").vector3Value = new Vector3(270f, 180f, 0f);
        serialized.FindProperty("handScaleMultiplier").floatValue = 0.7f;
        serialized.FindProperty("alignBoundsToGrip").boolValue = true;
        serialized.FindProperty("gripLocalOffset").vector3Value = GripLocalOffset;

        serialized.FindProperty("armRestPosition").vector3Value = ArmRestPosition;
        serialized.FindProperty("armRestEuler").vector3Value = ArmRestEuler;
        serialized.FindProperty("armReachPosition").vector3Value = ArmReachPosition;
        serialized.FindProperty("armReachEuler").vector3Value = ArmReachEuler;
        serialized.FindProperty("armHoldPosition").vector3Value = ArmHoldPosition;
        serialized.FindProperty("armHoldEuler").vector3Value = ArmHoldEuler;
        serialized.FindProperty("hideArmWhenIdle").boolValue = true;

        serialized.FindProperty("firstRunLine.speaker").stringValue = "Персонаж";
        serialized.FindProperty("firstRunLine.text").stringValue = FirstRunText;
        serialized.FindProperty("firstRunLine.duration").floatValue = 4f;

        serialized.FindProperty("briefingLine.speaker").stringValue = "Командир";
        serialized.FindProperty("briefingLine.text").stringValue = BriefingText;
        serialized.FindProperty("briefingLine.duration").floatValue = 5f;

        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    // Плашка субтитров: pivot внизу, 64 px от нижнего края — над SittingHint
    // (12…48 px) и не перекрывает центр экрана. Раньше pivot + y = -365 уводили
    // её целиком ниже экрана: alpha доходила до 1, а пикселей не было.
    private static void FixSubtitlesRect(Transform subtitles)
    {
        RectTransform rect = subtitles as RectTransform;
        if (rect == null) return;

        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(0f, SubtitleBottomOffset);
        rect.sizeDelta = new Vector2(1120f, 132f);

        CanvasGroup group = subtitles.GetComponent<CanvasGroup>();
        if (group != null)
        {
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
        }

        // Над подсказками (SittingHint — индекс 2), под модалками ComputerOS/паузой.
        subtitles.SetSiblingIndex(3);
    }

    private static Transform EnsureChild(Transform parent, string name)
    {
        Transform existing = parent.Find(name);
        if (existing != null) return existing;

        GameObject child = new GameObject(name);
        child.transform.SetParent(parent, false);
        return child.transform;
    }

    private static Transform FindByName(Scene scene, string objectName)
    {
        foreach (GameObject rootObject in scene.GetRootGameObjects())
        {
            Transform found = FindInHierarchy(rootObject.transform, objectName);
            if (found != null) return found;
        }

        return null;
    }

    private static Transform FindInHierarchy(Transform current, string objectName)
    {
        if (current.name == objectName) return current;

        for (int i = 0; i < current.childCount; i++)
        {
            Transform found = FindInHierarchy(current.GetChild(i), objectName);
            if (found != null) return found;
        }

        return null;
    }
}
