using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// Ассеты «рации в руке» для ОСНОВНОГО персонажа (Player/Visual/SWAT):
//  • Button Pushing.fbx -> клип WalkieReach (кадры 0..34, без зацикливания).
//    Кадр 34 — максимальный вынос кисти вперёд, всё после него (возврат руки) отрезано.
//  • Texting.fbx -> клип WalkieHold (кадры 29..30, loopTime = true).
//    Кадр 29 — рука уже поднята в позу удержания перед собой, «печатание»
//    пальцами ещё не началось; двухкадровый луп = застывшая поза.
//  • RightArmOnly.mask — humanoid-маска: разрешена только правая рука
//    (RightArm + RightFingers), ноги, туловище, голова и левая рука остаются
//    под управлением Base Layer.
//  • PlayerAnimator.controller: слой WalkieArmLayer (Override, defaultWeight = 0)
//    с состояниями Empty / WalkieReach / WalkieHold. Параметр WalkieSpeed
//    масштабирует скорость клипов слоя, не трогая глобальный Animator.speed
//    (иначе вместе с рукой замедлялась бы и ходьба персонажа).
// Меню идемпотентно: повторный запуск пересобирает клипы, маску и слой,
// GUID ассетов сохраняются. Меш PlayerRightArm.asset и viewmodel-контроллер
// больше не собираются — рука у персонажа родная.
public static class PlayerWalkieAssetBuilder
{
    public const string SwatPath = "Assets/Models/Player/Swat.fbx";
    public const string ReachFbxPath = "Assets/Models/Player/Animations/Button Pushing.fbx";
    public const string HoldFbxPath = "Assets/Models/Player/Animations/Texting.fbx";
    public const string AnimationsFolder = "Assets/Animations/Player";
    public const string MaskPath = AnimationsFolder + "/RightArmOnly.mask";
    public const string PlayerAnimatorPath = "Assets/Models/Player/PlayerAnimator.controller";

    public const string LayerName = "WalkieArmLayer";
    public const string EmptyStateName = "Empty";
    public const string ReachClipName = "WalkieReach";
    public const string HoldClipName = "WalkieHold";
    public const string SpeedParameterName = "WalkieSpeed";

    public const int ReachFirstFrame = 0;
    public const int ReachLastFrame = 34;
    public const int HoldFirstFrame = 29;
    public const int HoldLastFrame = 30;

    // Старое (viewmodel): больше не нужно, ассеты удаляются.
    public const string LegacyArmMeshPath = "Assets/Models/Player/PlayerRightArm.asset";
    public const string LegacyControllerPath = AnimationsFolder + "/PlayerArmViewmodel.controller";

    [MenuItem("Tools/DroneSimulator/Bunker/Build Walkie Arm Assets")]
    public static void Build()
    {
        EnsureFolder("Assets/Animations");
        EnsureFolder(AnimationsFolder);

        Avatar avatar = FindSwatAvatar();
        if (avatar == null)
        {
            Debug.LogError("[PlayerWalkieAssetBuilder] Не найден Avatar в " + SwatPath);
            return;
        }

        ConfigureClip(ReachFbxPath, avatar, ReachClipName, ReachFirstFrame, ReachLastFrame, false);
        ConfigureClip(HoldFbxPath, avatar, HoldClipName, HoldFirstFrame, HoldLastFrame, true);

        AssetDatabase.Refresh();

        AvatarMask mask = BuildMask();
        BuildControllerLayer(mask);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[PlayerWalkieAssetBuilder] Готово: клипы " + ReachClipName + " (0.." + ReachLastFrame +
                  ") и " + HoldClipName + " (" + HoldFirstFrame + ".." + HoldLastFrame + "), маска " + MaskPath +
                  " (RightArm + RightFingers), слой " + LayerName + " в " + PlayerAnimatorPath +
                  " (Override, weight 0; Empty / " + ReachClipName + " / " + HoldClipName + ", параметр скорости " +
                  SpeedParameterName + ").");
    }

    // Импорт клипа: humanoid-ретаргет на SwatAvatar + обрезка по кадрам.
    public static void ConfigureClip(string fbxPath, Avatar avatar, string clipName,
        int firstFrame, int lastFrame, bool loop)
    {
        ModelImporter importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
        if (importer == null)
        {
            Debug.LogError("[PlayerWalkieAssetBuilder] Нет ModelImporter у " + fbxPath);
            return;
        }

        bool needsRetarget = importer.animationType != ModelImporterAnimationType.Human
                             || importer.avatarSetup != ModelImporterAvatarSetup.CopyFromOther
                             || importer.sourceAvatar != avatar
                             || !importer.importAnimation;

        if (needsRetarget)
        {
            importer.importAnimation = true;
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
            importer.sourceAvatar = avatar;
            importer.SaveAndReimport();

            importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
            if (importer == null)
            {
                Debug.LogError("[PlayerWalkieAssetBuilder] Импортёр пропал после реимпорта " + fbxPath);
                return;
            }
        }

        ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
        if (clips == null || clips.Length == 0)
        {
            Debug.LogError("[PlayerWalkieAssetBuilder] В " + fbxPath + " не найдено ни одного клипа.");
            return;
        }

        ModelImporterClipAnimation clip = clips[0];
        clip.name = clipName;
        clip.firstFrame = firstFrame;
        clip.lastFrame = lastFrame;
        clip.loopTime = loop;
        clip.loopPose = false;
        clip.cycleOffset = 0f;

        importer.clipAnimations = new[] { clip };
        importer.SaveAndReimport();

        Debug.Log("[PlayerWalkieAssetBuilder] " + fbxPath + " -> клип '" + clipName + "' кадры " +
                  firstFrame + ".." + lastFrame + ", loop = " + loop + ".");
    }

    // Маска только для правой руки. Humanoid-маска (без transform-путей) —
    // иначе Unity считает маску generic и мышечные каналы клипов не мапятся.
    // Плечо (mixamorig:RightShoulder) входит в группу RightArm, пальцы — RightFingers.
    public static AvatarMask BuildMask()
    {
        AvatarMask mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(MaskPath);
        bool created = mask == null;
        if (created) mask = new AvatarMask();

        int last = (int)AvatarMaskBodyPart.LastBodyPart;
        for (int i = 0; i < last; i++)
            mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i, false);

        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightArm, true);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightFingers, true);

        if (created) AssetDatabase.CreateAsset(mask, MaskPath);
        else EditorUtility.SetDirty(mask);

        return mask;
    }

    // Слой WalkieArmLayer в PlayerAnimator: Override, вес 0, состояния без переходов.
    public static void BuildControllerLayer(AvatarMask mask)
    {
        AnimationClip reach = FindClip(ReachFbxPath, ReachClipName);
        AnimationClip hold = FindClip(HoldFbxPath, HoldClipName);

        if (reach == null || hold == null)
        {
            Debug.LogError("[PlayerWalkieAssetBuilder] Не найдены клипы " + ReachClipName + " / " + HoldClipName +
                           " — слой не собран.");
            return;
        }

        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(PlayerAnimatorPath);
        if (controller == null)
        {
            Debug.LogError("[PlayerWalkieAssetBuilder] Не найден " + PlayerAnimatorPath);
            return;
        }

        EnsureFloatParameter(controller, SpeedParameterName);

        int index = FindLayer(controller, LayerName);
        if (index < 0)
        {
            controller.AddLayer(LayerName);
            index = controller.layers.Length - 1;
        }

        AnimatorControllerLayer[] layers = controller.layers;
        AnimatorControllerLayer layer = layers[index];
        AnimatorStateMachine machine = layer.stateMachine;

        layer.name = LayerName;
        layer.avatarMask = mask;
        layer.blendingMode = AnimatorLayerBlendingMode.Override;
        layer.defaultWeight = 0f;
        layer.iKPass = false;
        layer.syncedLayerIndex = -1;
        layer.syncedLayerAffectsTiming = false;
        layers[index] = layer;
        controller.layers = layers;

        if (machine == null)
        {
            Debug.LogError("[PlayerWalkieAssetBuilder] У слоя " + LayerName + " нет StateMachine.");
            return;
        }

        ChildAnimatorState[] states = machine.states;
        for (int i = states.Length - 1; i >= 0; i--)
        {
            if (states[i].state != null) machine.RemoveState(states[i].state);
        }

        // Пустое состояние: без клипа и без записи дефолтов — при весе 0 слой
        // не участвует, а при весе > 0 ничего не перетирает.
        AnimatorState empty = machine.AddState(EmptyStateName, new Vector3(320f, 0f, 0f));
        empty.motion = null;
        empty.writeDefaultValues = false;
        machine.defaultState = empty;

        AnimatorState reachState = machine.AddState(ReachClipName, new Vector3(560f, -60f, 0f));
        reachState.motion = reach;
        reachState.speed = 1f;
        reachState.speedParameterActive = true;
        reachState.speedParameter = SpeedParameterName;

        AnimatorState holdState = machine.AddState(HoldClipName, new Vector3(560f, 60f, 0f));
        holdState.motion = hold;
        holdState.speed = 1f;
        holdState.speedParameterActive = true;
        holdState.speedParameter = SpeedParameterName;

        EditorUtility.SetDirty(controller);
    }

    public static Avatar FindSwatAvatar()
    {
        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(SwatPath))
        {
            Avatar avatar = asset as Avatar;
            if (avatar != null && avatar.isHuman && avatar.isValid) return avatar;
        }

        return null;
    }

    public static AnimationClip FindClip(string fbxPath, string clipName)
    {
        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(fbxPath))
        {
            AnimationClip clip = asset as AnimationClip;
            if (clip != null && clip.name == clipName) return clip;
        }

        return null;
    }

    private static int FindLayer(AnimatorController controller, string layerName)
    {
        AnimatorControllerLayer[] layers = controller.layers;
        for (int i = 0; i < layers.Length; i++)
        {
            if (layers[i].name == layerName) return i;
        }

        return -1;
    }

    private static void EnsureFloatParameter(AnimatorController controller, string parameterName)
    {
        AnimatorControllerParameter[] parameters = controller.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].name == parameterName) return;
        }

        controller.AddParameter(new AnimatorControllerParameter
        {
            name = parameterName,
            type = AnimatorControllerParameterType.Float,
            defaultFloat = 1f
        });
    }

    private static void EnsureFolder(string path)
    {
        if (string.IsNullOrEmpty(path) || AssetDatabase.IsValidFolder(path)) return;

        int split = path.LastIndexOf('/');
        if (split <= 0) return;

        string parent = path.Substring(0, split);
        string name = path.Substring(split + 1);

        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, name);
    }
}
