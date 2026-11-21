using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

public static class InputMapValidator
{
    private const string AssetPath = "Assets/Input/InputSystem_Actions.inputactions";

    [MenuItem("Tools/DroneSimulator/Input/1. Validate Drone Input Map", false, 22)]
    public static void ValidateDroneMap()
    {
        InputActionAsset asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(AssetPath);
        if (asset == null)
        {
            Debug.LogError($"[InputMap] Не найден ассет {AssetPath}");
            return;
        }

        InputActionMap map = asset.FindActionMap(GameInput.DroneMapName, false);
        if (map == null)
        {
            Debug.LogError($"[InputMap] В ассете нет карты '{GameInput.DroneMapName}'.");
            return;
        }

        int errors = 0;
        errors += CheckAxis(map, GameInput.Throttle, GameInput.ThrottleNegativePath, GameInput.ThrottlePositivePath);
        errors += CheckAxis(map, GameInput.Yaw, GameInput.YawNegativePath, GameInput.YawPositivePath);
        errors += CheckAxis(map, GameInput.Pitch, GameInput.PitchNegativePath, GameInput.PitchPositivePath);
        errors += CheckAxis(map, GameInput.Roll, GameInput.RollNegativePath, GameInput.RollPositivePath);
        errors += CheckAxis(map, GameInput.Zoom, GameInput.ZoomNegativePath, GameInput.ZoomPositivePath);
        errors += CheckVector(map, GameInput.Gimbal, GameInput.GimbalUpPath, GameInput.GimbalDownPath, GameInput.GimbalLeftPath, GameInput.GimbalRightPath);
        errors += CheckButton(map, GameInput.PrimaryAction, GameInput.PrimaryActionPath);
        errors += CheckButton(map, GameInput.NightVision, GameInput.NightVisionPath);
        errors += CheckButton(map, GameInput.MotorStart, GameInput.MotorStartPath);
        errors += CheckButton(map, GameInput.HoverToggle, GameInput.HoverTogglePath);
        errors += CheckButton(map, GameInput.AngleToggle, GameInput.AngleTogglePath);
        errors += CheckButton(map, GameInput.GimbalReset, GameInput.GimbalResetPath);
        errors += CheckRuntimeMap();
        errors += CheckOverrideRoundTrip();
        errors += CheckKeyPathOverride();
        LogKeyPathProbe();

        if (errors == 0)
        {
            Debug.Log($"<color=lime>[InputMap]</color> Карта '{GameInput.DroneMapName}' корректна: {map.actions.Count} действий, {map.bindings.Count} привязок, совпадает с GameInput.");
            return;
        }

        Debug.LogError($"[InputMap] Найдено проблем: {errors}. Подробности выше.");
    }

    private static int CheckAxis(InputActionMap map, string actionName, string negativePath, string positivePath)
    {
        InputAction action = map.FindAction(actionName, false);
        if (action == null) return Report($"в карте нет действия '{actionName}'");

        if (!action.bindings[0].isComposite) return Report($"'{actionName}': первая привязка не является композитной (1DAxis).");
        if (action.bindings.Count < 3) return Report($"'{actionName}': ожидались 3 привязки, найдено {action.bindings.Count}.");

        int errors = 0;
        errors += Compare(actionName, "negative", action.bindings[1], negativePath);
        errors += Compare(actionName, "positive", action.bindings[2], positivePath);
        return errors;
    }

    private static int CheckVector(InputActionMap map, string actionName, string upPath, string downPath, string leftPath, string rightPath)
    {
        InputAction action = map.FindAction(actionName, false);
        if (action == null) return Report($"в карте нет действия '{actionName}'");

        if (!action.bindings[0].isComposite) return Report($"'{actionName}': первая привязка не является композитной (2DVector).");
        if (action.bindings.Count < 5) return Report($"'{actionName}': ожидались 5 привязок, найдено {action.bindings.Count}.");

        int errors = 0;
        errors += Compare(actionName, "up", action.bindings[1], upPath);
        errors += Compare(actionName, "down", action.bindings[2], downPath);
        errors += Compare(actionName, "left", action.bindings[3], leftPath);
        errors += Compare(actionName, "right", action.bindings[4], rightPath);
        return errors;
    }

    private static int CheckButton(InputActionMap map, string actionName, string expectedPath)
    {
        InputAction action = map.FindAction(actionName, false);
        if (action == null) return Report($"в карте нет действия '{actionName}'");

        if (action.bindings.Count < 1) return Report($"'{actionName}': нет привязок.");

        int errors = 0;
        errors += Compare(actionName, "button", action.bindings[0], expectedPath);
        return errors;
    }

    private static int Compare(string actionName, string partName, InputBinding binding, string expectedPath)
    {
        StringBuilder message = new StringBuilder();
        int errors = 0;

        if (binding.path != expectedPath)
        {
            message.Append($"'{actionName}.{partName}': ожидался путь {expectedPath}, а в ассете {binding.path}. ");
            errors++;
        }

        if (string.IsNullOrEmpty(binding.effectivePath))
        {
            message.Append($"'{actionName}.{partName}': привязка не разрешилась (effectivePath пуст). ");
            errors++;
        }

        if (errors > 0) Debug.LogError("[InputMap] " + message.ToString());
        return errors;
    }

    private static int CheckRuntimeMap()
    {
        InputActionMap runtimeMap = GameInput.DroneMap;
        InputActionMap assetMap = AssetDatabase.LoadAssetAtPath<InputActionAsset>(AssetPath).FindActionMap(GameInput.DroneMapName, false);

        if (runtimeMap == null)
        {
            Debug.LogError("[InputMap] GameInput не смог построить карту Drone.");
            return 1;
        }

        if (runtimeMap.actions.Count != assetMap.actions.Count)
        {
            Debug.LogError($"[InputMap] Несовпадение количества действий: код {runtimeMap.actions.Count}, ассет {assetMap.actions.Count}.");
            return 1;
        }

        int errors = 0;

        for (int i = 0; i < runtimeMap.actions.Count; i++)
        {
            InputAction runtimeAction = runtimeMap.actions[i];
            InputAction assetAction = assetMap.FindAction(runtimeAction.name, false);

            if (assetAction == null)
            {
                Debug.LogError($"[InputMap] Действие '{runtimeAction.name}' есть в коде, но отсутствует в ассете.");
                errors++;
                continue;
            }

            if (runtimeAction.bindings.Count != assetAction.bindings.Count)
            {
                Debug.LogError($"[InputMap] '{runtimeAction.name}': код {runtimeAction.bindings.Count} привязок, ассет {assetAction.bindings.Count}.");
                errors++;
                continue;
            }

            for (int b = 0; b < runtimeAction.bindings.Count; b++)
            {
                if (runtimeAction.bindings[b].path != assetAction.bindings[b].path)
                {
                    Debug.LogError($"[InputMap] '{runtimeAction.name}'[{b}]: код '{runtimeAction.bindings[b].path}', ассет '{assetAction.bindings[b].path}'.");
                    errors++;
                }
            }
        }

        return errors;
    }

    private static int Report(string message)
    {
        Debug.LogError("[InputMap] " + message);
        return 1;
    }

    private static int CheckOverrideRoundTrip()
    {
        InputAction throttle = GameInput.GetAction(GameInput.Throttle);
        if (throttle == null) return Report("нет действия Throttle: проверить оверрайды привязок нельзя.");

        int index = RebindService.AxisBindingPositiveIndex;
        string original = throttle.bindings[index].path;

        throttle.ApplyBindingOverride(index, "<Keyboard>/k");
        string effective = throttle.bindings[index].effectivePath;
        string display = throttle.GetBindingDisplayString(index);

        throttle.RemoveBindingOverride(index);
        string restored = throttle.bindings[index].path;

        if (effective != "<Keyboard>/k" || restored != original)
        {
            Debug.LogError($"[InputMap] Оверрайд привязки не работает: effective='{effective}', restored='{restored}', original='{original}'.");
            return 1;
        }

        Debug.Log($"[InputMap] Оверрайд привязки работает: {GameInput.Throttle}[{index}] -> '{effective}', отображение '{display}', откат в '{restored}'.");
        return 0;
    }

    private static int CheckKeyPathOverride()
    {
        InputAction throttle = GameInput.GetAction(GameInput.Throttle);
        if (throttle == null) return Report("нет действия Throttle: проверить путь клавиши нельзя.");

        string path = RebindService.ToControlPath(Key.K);
        if (string.IsNullOrEmpty(path)) return 0;

        InputControl control = InputSystem.FindControl(path);
        if (control == null)
        {
            Debug.LogError($"[InputMap] Путь клавиши '{path}' не разрешается в контрол (переназначение не сработает).");
            return 1;
        }

        int index = RebindService.AxisBindingPositiveIndex;
        string original = throttle.bindings[index].path;

        throttle.ApplyBindingOverride(index, path);
        string effective = throttle.bindings[index].effectivePath;
        string display = throttle.GetBindingDisplayString(index);
        throttle.RemoveBindingOverride(index);

        if (effective != path)
        {
            Debug.LogError($"[InputMap] Оверрайд по пути клавиши '{path}' не применился: effective='{effective}'.");
            return 1;
        }

        bool restored = throttle.bindings[index].path == original;
        if (!restored)
        {
            Debug.LogError($"[InputMap] Откат оверрайда по пути клавиши не удался: path='{throttle.bindings[index].path}'.");
            return 1;
        }

        Debug.Log($"[InputMap] Путь клавиши работает: '{path}' -> контрол '{control.name}', отображение '{display}'.");
        return 0;
    }

    private static void LogKeyPathProbe()
    {
        string spacePath = RebindService.ToControlPath(Key.Space);
        string wPath = RebindService.ToControlPath(Key.W);

        if (string.IsNullOrEmpty(spacePath) || string.IsNullOrEmpty(wPath))
        {
            Debug.LogWarning("[InputMap] Keyboard.current недоступна (Edit Mode): проверка путей клавиш пропущена, в Play Mode это выполнит KeyBindingRow.");
            return;
        }

        Debug.Log($"[InputMap] Пути клавиш для переназначения: Space -> '{spacePath}', W -> '{wPath}'.");
    }
}
