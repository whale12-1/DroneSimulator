using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public interface IPrimaryActionHandler
{
    int PrimaryPriority { get; }
    bool WantsPrimaryAction();
    void OnPrimaryAction();
}

public static class GameInput
{
    public const string DroneMapName = "Drone";

    public const string Throttle = "Throttle";
    public const string Yaw = "Yaw";
    public const string Pitch = "Pitch";
    public const string Roll = "Roll";
    public const string PrimaryAction = "PrimaryAction";
    public const string Gimbal = "Gimbal";
    public const string Zoom = "Zoom";
    public const string NightVision = "NightVision";
    public const string MotorStart = "MotorStart";
    public const string HoverToggle = "HoverToggle";
    public const string AngleToggle = "AngleToggle";
    public const string GimbalReset = "GimbalReset";

    public static readonly string[] ActionNames =
    {
        Throttle, Yaw, Pitch, Roll, PrimaryAction, Gimbal, Zoom, NightVision, MotorStart, HoverToggle, AngleToggle, GimbalReset
    };

    public const string ThrottlePositivePath = "<Keyboard>/w";
    public const string ThrottleNegativePath = "<Keyboard>/s";
    public const string YawPositivePath = "<Keyboard>/d";
    public const string YawNegativePath = "<Keyboard>/a";
    public const string PitchPositivePath = "<Keyboard>/upArrow";
    public const string PitchNegativePath = "<Keyboard>/downArrow";
    public const string RollPositivePath = "<Keyboard>/rightArrow";
    public const string RollNegativePath = "<Keyboard>/leftArrow";
    public const string GimbalUpPath = "<Keyboard>/r";
    public const string GimbalDownPath = "<Keyboard>/f";
    public const string GimbalLeftPath = "<Keyboard>/q";
    public const string GimbalRightPath = "<Keyboard>/e";
    public const string ZoomPositivePath = "<Keyboard>/z";
    public const string ZoomNegativePath = "<Keyboard>/x";
    public const string PrimaryActionPath = "<Keyboard>/space";
    public const string NightVisionPath = "<Keyboard>/n";
    public const string MotorStartPath = "<Keyboard>/w";
    public const string HoverTogglePath = "<Keyboard>/h";
    public const string AngleTogglePath = "<Keyboard>/m";
    public const string GimbalResetPath = "<Keyboard>/c";

    private const float DeadZone = 0.01f;

    private static InputActionAsset asset;
    private static InputActionMap droneMap;
    private static readonly List<IPrimaryActionHandler> primaryHandlers = new List<IPrimaryActionHandler>();
    private static bool primaryDispatching;
    private static bool initializing;

    public static InputActionAsset Asset
    {
        get
        {
            EnsureInitialized();
            return asset;
        }
    }

    public static InputActionMap DroneMap
    {
        get
        {
            EnsureInitialized();
            return droneMap;
        }
    }

    public static bool IsReady
    {
        get { return asset != null && droneMap != null && droneMap.enabled; }
    }

    public static float ThrottleAxis
    {
        get { return ReadAxis(Throttle); }
    }

    public static float YawAxis
    {
        get { return ReadAxis(Yaw); }
    }

    public static float PitchAxis
    {
        get { return ReadAxis(Pitch); }
    }

    public static float RollAxis
    {
        get { return ReadAxis(Roll); }
    }

    public static Vector2 GimbalAxis
    {
        get { return ReadVector2(Gimbal); }
    }

    public static bool HasGimbalInput
    {
        get
        {
            Vector2 axis = GimbalAxis;
            return Mathf.Abs(axis.x) > DeadZone || Mathf.Abs(axis.y) > DeadZone;
        }
    }

    public static int ZoomStep
    {
        get
        {
            InputAction action = GetAction(Zoom);
            if (action == null || !action.WasPressedThisFrame()) return 0;

            float value = action.ReadValue<float>();
            if (value > DeadZone) return 1;
            if (value < -DeadZone) return -1;
            return 0;
        }
    }

    public static bool PrimaryActionPressedThisFrame
    {
        get { return WasPressedThisFrame(PrimaryAction); }
    }

    public static bool MotorStartPressedThisFrame
    {
        get { return WasPressedThisFrame(MotorStart); }
    }

    public static bool NightVisionPressedThisFrame
    {
        get { return WasPressedThisFrame(NightVision); }
    }

    public static bool HoverTogglePressedThisFrame
    {
        get { return WasPressedThisFrame(HoverToggle); }
    }

    public static bool AngleTogglePressedThisFrame
    {
        get { return WasPressedThisFrame(AngleToggle); }
    }

    public static bool GimbalResetPressedThisFrame
    {
        get { return WasPressedThisFrame(GimbalReset); }
    }

    public static InputAction GetAction(string actionName)
    {
        EnsureInitialized();
        if (droneMap == null || string.IsNullOrEmpty(actionName)) return null;
        return droneMap.FindAction(actionName, false);
    }

    public static bool WasPressedThisFrame(string actionName)
    {
        InputAction action = GetAction(actionName);
        return action != null && action.WasPressedThisFrame();
    }

    public static void RegisterPrimaryHandler(IPrimaryActionHandler handler)
    {
        if (handler == null) return;
        if (primaryHandlers.Contains(handler)) return;

        EnsureInitialized();
        primaryHandlers.Add(handler);
    }

    public static void UnregisterPrimaryHandler(IPrimaryActionHandler handler)
    {
        if (handler == null) return;
        primaryHandlers.Remove(handler);
    }

    public static void ReloadBindings()
    {
        EnsureInitialized();
        RebindService.ApplySavedBindings();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        if (droneMap != null) droneMap.Disable();
        if (asset != null) Object.Destroy(asset);

        asset = null;
        droneMap = null;
        primaryHandlers.Clear();
        primaryDispatching = false;
        initializing = false;
    }

    private static void EnsureInitialized()
    {
        if (asset != null || initializing) return;

        initializing = true;

        try
        {
            if (Application.isPlaying) SettingsService.EnsureInstance();

            BuildAsset();
            RebindService.ApplySavedBindings();
            droneMap.Enable();
        }
        finally
        {
            initializing = false;
        }
    }

    private static void BuildAsset()
    {
        asset = ScriptableObject.CreateInstance<InputActionAsset>();
        asset.name = "InputSystem_Actions";
        droneMap = asset.AddActionMap(DroneMapName);

        AddAxisAction(droneMap, Throttle, ThrottlePositivePath, ThrottleNegativePath);
        AddAxisAction(droneMap, Yaw, YawPositivePath, YawNegativePath);
        AddAxisAction(droneMap, Pitch, PitchPositivePath, PitchNegativePath);
        AddAxisAction(droneMap, Roll, RollPositivePath, RollNegativePath);
        AddAxisAction(droneMap, Zoom, ZoomPositivePath, ZoomNegativePath);
        AddVectorAction(droneMap, Gimbal, GimbalUpPath, GimbalDownPath, GimbalLeftPath, GimbalRightPath);

        InputAction primary = droneMap.AddAction(PrimaryAction, InputActionType.Button, PrimaryActionPath);
        primary.started += OnPrimaryStarted;

        droneMap.AddAction(NightVision, InputActionType.Button, NightVisionPath);
        droneMap.AddAction(MotorStart, InputActionType.Button, MotorStartPath);
        droneMap.AddAction(HoverToggle, InputActionType.Button, HoverTogglePath);
        droneMap.AddAction(AngleToggle, InputActionType.Button, AngleTogglePath);
        droneMap.AddAction(GimbalReset, InputActionType.Button, GimbalResetPath);
    }

    private static InputAction AddAxisAction(InputActionMap map, string actionName, string positivePath, string negativePath)
    {
        InputAction action = map.AddAction(actionName, InputActionType.Value, null, null, null, null, "Axis");
        action.AddCompositeBinding("1DAxis")
            .With("Negative", negativePath)
            .With("Positive", positivePath);
        return action;
    }

    private static InputAction AddVectorAction(InputActionMap map, string actionName, string upPath, string downPath, string leftPath, string rightPath)
    {
        InputAction action = map.AddAction(actionName, InputActionType.Value, null, null, null, null, "Vector2");
        action.AddCompositeBinding("2DVector")
            .With("Up", upPath)
            .With("Down", downPath)
            .With("Left", leftPath)
            .With("Right", rightPath);
        return action;
    }

    private static float ReadAxis(string actionName)
    {
        InputAction action = GetAction(actionName);
        if (action == null || !action.enabled) return 0f;

        float value = action.ReadValue<float>();
        if (Mathf.Abs(value) < DeadZone) return 0f;
        return Mathf.Clamp(value, -1f, 1f);
    }

    private static Vector2 ReadVector2(string actionName)
    {
        InputAction action = GetAction(actionName);
        if (action == null || !action.enabled) return Vector2.zero;

        Vector2 value = action.ReadValue<Vector2>();
        if (value.sqrMagnitude < DeadZone * DeadZone) return Vector2.zero;
        return Vector2.ClampMagnitude(value, 1f);
    }

    private static void OnPrimaryStarted(InputAction.CallbackContext context)
    {
        DispatchPrimaryAction();
    }

    private static void DispatchPrimaryAction()
    {
        if (primaryDispatching) return;
        primaryDispatching = true;

        try
        {
            IPrimaryActionHandler winner = null;
            int bestPriority = int.MinValue;

            for (int i = 0; i < primaryHandlers.Count; i++)
            {
                IPrimaryActionHandler handler = primaryHandlers[i];
                if (handler == null) continue;
                if (handler is Object unityObject && unityObject == null) continue;
                if (handler.PrimaryPriority < bestPriority) continue;
                if (!handler.WantsPrimaryAction()) continue;

                winner = handler;
                bestPriority = handler.PrimaryPriority;
            }

            if (winner != null) winner.OnPrimaryAction();
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception);
        }
        finally
        {
            primaryDispatching = false;
        }
    }
}
