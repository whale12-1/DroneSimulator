using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

// Привязка рации к РОДНОЙ правой руке основного персонажа в Bunker(main).
// Рука от первого лица на камере (Main Camera/FirstPersonPlayerArm) больше не нужна:
// анимация удержания рации идёт отдельным слоем WalkieArmLayer в PlayerAnimator
// (AvatarMask RightArmOnly.mask) на скелете Player/Visual/SWAT.
// Меню идемпотентно: удаляет старую viewmodel-руку вместе с мешем и контроллером,
// переносит FillLight на Main Camera, создаёт WalkieTalkieHandAnchor в кости
// mixamorig:RightHand персонажа и перепривязывает BunkerWalkieTalkieEvent.
public static class BunkerWalkieHandBuilder
{
    private const string ScenePath = "Assets/Scenes/Bunker(main).unity";
    private const string CameraName = "Main Camera";
    private const string PlayerName = "Player";
    private const string VisualName = "Visual";
    private const string SwatName = "SWAT";
    private const string ArmName = "FirstPersonPlayerArm";
    private const string AnchorName = "WalkieTalkieHandAnchor";
    private const string FillLightName = "FillLight";

    private const string HandBoneName = "mixamorig:RightHand";
    private const string ShoulderBoneName = "mixamorig:RightShoulder";
    private const string UpperArmBoneName = "mixamorig:RightArm";
    private const string ForeArmBoneName = "mixamorig:RightForeArm";

    // Точка хвата в пространстве кости RightHand: карман между поверхностью
    // ладони (z ~ 0.023) и согнутыми пальцами (z ~ 0.045), ближе к основанию
    // пальцев. Сюда садится середина корпуса рации, а не центр полного AABB.
    private static readonly Vector3 AnchorLocalPosition = new Vector3(0.004f, 0.085f, 0.038f);

    // Мягкая подсветка кисти с рацией. Раньше жила на корне viewmodel-руки
    // (Main Camera localPos 0.14, -1.30, 0.24 + FillLight -0.14, 1.46, 0.02),
    // т.е. фактически в ~0.16 м над камерой и 0.26 м перед ней — значения ниже
    // это то же самое положение, но уже относительно Main Camera.
    private static readonly Vector3 FillLightLocalPosition = new Vector3(0f, 0.16f, 0.26f);
    private const float FillLightIntensity = 0.3f;
    private const float FillLightRange = 1f;

    // Смещение точки хвата в ладони: в ладонь садится середина корпуса рации,
    // а не центр полного AABB (он на 5.78 см выше, у основания антенны).
    private static readonly Vector3 GripLocalOffset = new Vector3(-0.026599f, 0.009152f, 0.046776f);

    // Рация в руке: 0.0264 x 0.66 = 0.0174 мирового масштаба — корпус (5.1 юнита
    // высотой) помещается в ладонь и не выпирает за согнутые пальцы.
    private const float HandScaleMultiplier = 0.66f;

    // Коррекция позы родной руки поверх слоя. «Перекрученные» углы
    // (плечо 40/0/20, верхняя рука -50/-40/45, предплечье -30/-40/0) убраны
    // полностью: плечо/предплечье/кисть = (0, 0, 0), ключица вообще не
    // трогается, чтобы не ломать куртку.
    // Осталась ОДНА аккуратная правка — лёгкий подъём верхней части руки на 20°
    // по Z. Замерено в Play Mode 26.11.2026 (поза WalkieHold, pitch 15°,
    // Camera.WorldToViewportPoint анкера кисти): без правки кисть уходит под
    // кадр (vp.y = -0.286), с X ±20 и Y вправо — только хуже/вбок, а Z+20
    // поднимает её до -0.023 и центрирует по X (0.490) без выворота локтя.
    private static readonly Vector3 ShoulderLocalEulerOffset = Vector3.zero;
    private static readonly Vector3 UpperArmLocalEulerOffset = new Vector3(0f, 0f, 20f);
    private static readonly Vector3 ForeArmLocalEulerOffset = Vector3.zero;
    private static readonly Vector3 HandLocalEulerOffset = Vector3.zero;

    // Скорости/тайминги: комфортное потягивание и возврат, 5 секунд удержания
    // рации в кадре (ровно столько идёт реплика командира).
    private const float ReachSpeed = 0.85f;
    private const float LowerSpeed = 0.85f;
    private const float HoldBlendDuration = 0.15f;
    private const float HoldDuration = 5f;
    private const float LayerWeightInDuration = 0.2f;
    private const float LayerWeightOutDuration = 0.25f;

    [MenuItem("Tools/DroneSimulator/Bunker/Attach Walkie To Player Hand")]
    public static void Build()
    {
        Scene scene = EnsureSceneOpen();
        if (!scene.IsValid()) return;

        EnsureAssets();

        Transform cameraTransform = FindByName(scene, CameraName);
        if (cameraTransform == null)
        {
            Debug.LogError($"[BunkerWalkieHandBuilder] В сцене не найден {CameraName}.");
            return;
        }

        Transform player = FindByName(scene, PlayerName);
        Transform swat = FindSwat(player);
        if (swat == null)
        {
            Debug.LogError($"[BunkerWalkieHandBuilder] Не найден {PlayerName}/{VisualName}/{SwatName}.");
            return;
        }

        Animator animator = swat.GetComponent<Animator>();
        if (animator == null)
        {
            Debug.LogError("[BunkerWalkieHandBuilder] У SWAT нет Animator.");
            return;
        }

        Transform handBone = FindInHierarchy(swat, HandBoneName);
        if (handBone == null)
        {
            Debug.LogError($"[BunkerWalkieHandBuilder] В скелете SWAT не найдена кость {HandBoneName}.");
            return;
        }

        Light fillLight = TakeFillLight(cameraTransform);

        RemoveViewmodelArm(cameraTransform);
        DeleteLegacyAssets();

        Transform anchor = EnsureAnchor(handBone);

        BunkerWalkieTalkieEvent walkie =
            Object.FindAnyObjectByType<BunkerWalkieTalkieEvent>(FindObjectsInactive.Include);
        if (walkie == null)
        {
            Debug.LogError("[BunkerWalkieHandBuilder] Не найден BunkerWalkieTalkieEvent (Prop_WalkieTalkie).");
            return;
        }

        BindWalkieEvent(walkie, animator, anchor, swat);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log("[BunkerWalkieHandBuilder] Рация привязана к родной руке SWAT: слой " +
                  PlayerWalkieAssetBuilder.LayerName + " в PlayerAnimator (" +
                  PlayerWalkieAssetBuilder.MaskPath + "), анкер в кости " + HandBoneName +
                  ", FillLight " + (fillLight != null ? "на Main Camera" : "не найден") +
                  "; viewmodel-рука удалена.");
    }

    private static void EnsureAssets()
    {
        bool missing = AssetDatabase.LoadAssetAtPath<AvatarMask>(PlayerWalkieAssetBuilder.MaskPath) == null
                       || PlayerWalkieAssetBuilder.FindClip(PlayerWalkieAssetBuilder.ReachFbxPath,
                           PlayerWalkieAssetBuilder.ReachClipName) == null
                       || PlayerWalkieAssetBuilder.FindClip(PlayerWalkieAssetBuilder.HoldFbxPath,
                           PlayerWalkieAssetBuilder.HoldClipName) == null;

        if (missing) PlayerWalkieAssetBuilder.Build();
    }

    private static Scene EnsureSceneOpen()
    {
        Scene active = EditorSceneManager.GetActiveScene();
        if (string.Equals(active.path, ScenePath)) return active;

        if (active.isDirty)
        {
            Debug.LogError("[BunkerWalkieHandBuilder] Открыта другая сцена с несохранёнными изменениями. " +
                           "Сохраните её и повторите.");
            return default;
        }

        return EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
    }

    // Мягкий FillLight переезжает с удаляемой руки на Main Camera.
    private static Light TakeFillLight(Transform cameraTransform)
    {
        Light fill = null;

        Transform arm = cameraTransform.Find(ArmName);
        if (arm != null)
        {
            Transform fillTransform = arm.Find(FillLightName);
            if (fillTransform != null)
            {
                fill = fillTransform.GetComponent<Light>();
                fillTransform.SetParent(cameraTransform, false);
            }
        }

        if (fill == null)
        {
            Transform existing = cameraTransform.Find(FillLightName);
            if (existing != null) fill = existing.GetComponent<Light>();
        }

        if (fill == null) fill = CreateFillLight(cameraTransform);
        else fill.transform.SetParent(cameraTransform, false);

        fill.transform.localPosition = FillLightLocalPosition;
        fill.transform.localRotation = Quaternion.identity;
        fill.transform.localScale = Vector3.one;
        fill.intensity = FillLightIntensity;
        fill.range = FillLightRange;
        fill.shadows = LightShadows.None;

        return fill;
    }

    private static Light CreateFillLight(Transform parent)
    {
        GameObject host = new GameObject(FillLightName);
        host.transform.SetParent(parent, false);

        Light light = host.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1f, 0.95f, 0.86f);
        light.intensity = FillLightIntensity;
        light.range = FillLightRange;
        light.shadows = LightShadows.None;
        light.lightmapBakeType = LightmapBakeType.Realtime;

        if (host.GetComponent<UniversalAdditionalLightData>() == null)
            host.AddComponent<UniversalAdditionalLightData>();

        return light;
    }

    private static void RemoveViewmodelArm(Transform cameraTransform)
    {
        Transform arm = cameraTransform.Find(ArmName);
        if (arm != null) Object.DestroyImmediate(arm.gameObject);
    }

    private static void DeleteLegacyAssets()
    {
        DeleteAsset(PlayerWalkieAssetBuilder.LegacyArmMeshPath);
        DeleteAsset(PlayerWalkieAssetBuilder.LegacyControllerPath);
    }

    private static void DeleteAsset(string path)
    {
        if (AssetDatabase.LoadAssetAtPath<Object>(path) == null) return;
        if (!AssetDatabase.DeleteAsset(path))
        {
            Debug.LogWarning("[BunkerWalkieHandBuilder] Не удалось удалить неиспользуемый ассет " + path);
            return;
        }

        Debug.Log("[BunkerWalkieHandBuilder] Удалён неиспользуемый ассет " + path);
    }

    private static Transform EnsureAnchor(Transform handBone)
    {
        Transform anchor = handBone.Find(AnchorName);
        if (anchor == null) anchor = new GameObject(AnchorName).transform;

        anchor.SetParent(handBone, false);
        anchor.localPosition = AnchorLocalPosition;
        anchor.localRotation = Quaternion.identity;
        anchor.localScale = Vector3.one;
        anchor.gameObject.layer = handBone.gameObject.layer;

        return anchor;
    }

    // Привязка Animator персонажа, слоя и анкера к событию рации. В сцене значения
    // сериализованы, поэтому одних дефолтов в коде недостаточно.
    private static void BindWalkieEvent(BunkerWalkieTalkieEvent walkie, Animator animator, Transform anchor,
        Transform swat)
    {
        SerializedObject serialized = new SerializedObject(walkie);

        serialized.FindProperty("characterAnimator").objectReferenceValue = animator;
        serialized.FindProperty("layerName").stringValue = PlayerWalkieAssetBuilder.LayerName;
        serialized.FindProperty("emptyStateName").stringValue = PlayerWalkieAssetBuilder.EmptyStateName;
        serialized.FindProperty("reachStateName").stringValue = PlayerWalkieAssetBuilder.ReachClipName;
        serialized.FindProperty("holdStateName").stringValue = PlayerWalkieAssetBuilder.HoldClipName;
        serialized.FindProperty("speedParameterName").stringValue = PlayerWalkieAssetBuilder.SpeedParameterName;

        serialized.FindProperty("reachSpeed").floatValue = ReachSpeed;
        serialized.FindProperty("holdBlendDuration").floatValue = HoldBlendDuration;
        serialized.FindProperty("holdDuration").floatValue = HoldDuration;
        serialized.FindProperty("lowerSpeed").floatValue = LowerSpeed;
        serialized.FindProperty("layerWeightInDuration").floatValue = LayerWeightInDuration;
        serialized.FindProperty("layerWeightOutDuration").floatValue = LayerWeightOutDuration;

        serialized.FindProperty("handAnchor").objectReferenceValue = anchor;
        serialized.FindProperty("handLocalPosition").vector3Value = Vector3.zero;
        serialized.FindProperty("handLocalEuler").vector3Value = Vector3.zero;
        serialized.FindProperty("handScaleMultiplier").floatValue = HandScaleMultiplier;
        serialized.FindProperty("alignBoundsToGrip").boolValue = true;
        serialized.FindProperty("gripLocalOffset").vector3Value = GripLocalOffset;
        serialized.FindProperty("alignUprightToCamera").boolValue = true;

        serialized.FindProperty("shoulderBone").objectReferenceValue = FindInHierarchy(swat, ShoulderBoneName);
        serialized.FindProperty("shoulderLocalEulerOffset").vector3Value = ShoulderLocalEulerOffset;
        serialized.FindProperty("upperArmBone").objectReferenceValue = FindInHierarchy(swat, UpperArmBoneName);
        serialized.FindProperty("upperArmLocalEulerOffset").vector3Value = UpperArmLocalEulerOffset;
        serialized.FindProperty("foreArmBone").objectReferenceValue = FindInHierarchy(swat, ForeArmBoneName);
        serialized.FindProperty("foreArmLocalEulerOffset").vector3Value = ForeArmLocalEulerOffset;
        serialized.FindProperty("handBone").objectReferenceValue = FindInHierarchy(swat, HandBoneName);
        serialized.FindProperty("handLocalEulerOffset").vector3Value = HandLocalEulerOffset;

        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static Transform FindSwat(Transform player)
    {
        if (player == null) return null;

        Transform visual = player.Find(VisualName);
        if (visual != null)
        {
            Transform swat = visual.Find(SwatName);
            if (swat != null) return swat;
        }

        return FindInHierarchy(player, SwatName);
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
