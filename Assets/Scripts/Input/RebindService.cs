using System.Collections.Generic;
using DroneSimulator.Settings;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

public static class RebindService
{
    public const int ButtonBindingIndex = 0;
    public const int AxisBindingNegativeIndex = 1;
    public const int AxisBindingPositiveIndex = 2;
    public const int VectorBindingUpIndex = 1;
    public const int VectorBindingDownIndex = 2;
    public const int VectorBindingLeftIndex = 3;
    public const int VectorBindingRightIndex = 4;

    private const char BindingIdSeparator = ':';

    public static void ApplySavedBindings()
    {
        InputActionMap map = GameInput.DroneMap;
        if (map == null) return;

        for (int i = 0; i < map.actions.Count; i++)
        {
            map.actions[i].RemoveAllBindingOverrides();
        }

        ControlSettings controls = ResolveControls();
        if (controls == null || controls.keyBindings == null) return;

        for (int i = 0; i < controls.keyBindings.Count; i++)
        {
            KeyBinding saved = controls.keyBindings[i];
            if (saved == null) continue;
            if (!TryParseBindingId(saved.actionId, out string actionName, out int bindingIndex)) continue;
            ApplyOverride(actionName, bindingIndex, saved.bindingJson);
        }
    }

    public static string GetDisplayName(string actionName, int bindingIndex)
    {
        InputAction action = GameInput.GetAction(actionName);
        if (action == null || bindingIndex < 0 || bindingIndex >= action.bindings.Count) return "—";

        string display = action.GetBindingDisplayString(bindingIndex);
        return string.IsNullOrWhiteSpace(display) ? "—" : display;
    }

    public static string GetBindingId(string actionName, int bindingIndex)
    {
        return actionName + BindingIdSeparator + bindingIndex.ToString();
    }

    public static bool TryAssignKey(string actionName, int bindingIndex, Key key, out string error)
    {
        error = null;

        InputAction action = GameInput.GetAction(actionName);
        if (action == null)
        {
            error = "Действие не найдено: " + actionName;
            return false;
        }

        if (bindingIndex < 0 || bindingIndex >= action.bindings.Count)
        {
            error = "Недопустимый индекс привязки: " + bindingIndex;
            return false;
        }

        string path = ToControlPath(key);
        if (string.IsNullOrEmpty(path))
        {
            error = "Клавиша не поддерживается: " + key;
            return false;
        }

        action.ApplyBindingOverride(bindingIndex, path);
        SaveBinding(actionName, bindingIndex, path);

        string conflict = FindConflict(actionName, bindingIndex, path);
        if (!string.IsNullOrEmpty(conflict))
        {
            Debug.LogWarning($"[RebindService] Клавиша {GetDisplayName(actionName, bindingIndex)} у действия {actionName} совпадает с {conflict}.");
        }

        return true;
    }

    public static void ResetBinding(string actionName, int bindingIndex)
    {
        InputAction action = GameInput.GetAction(actionName);
        if (action == null) return;
        if (bindingIndex < 0 || bindingIndex >= action.bindings.Count) return;

        action.RemoveBindingOverride(bindingIndex);
        RemoveSavedBinding(actionName, bindingIndex);
    }

    public static void ResetAllBindings()
    {
        InputActionMap map = GameInput.DroneMap;
        if (map == null) return;

        for (int i = 0; i < map.actions.Count; i++)
        {
            map.actions[i].RemoveAllBindingOverrides();
        }

        ControlSettings controls = ResolveControls();
        if (controls == null) return;

        if (controls.keyBindings != null) controls.keyBindings.Clear();

        SettingsService service = SettingsService.Instance;
        if (service != null)
        {
            service.MarkDirty();
            service.ApplyInstant();
        }
    }

    public static bool HasSavedBinding(string actionName, int bindingIndex)
    {
        ControlSettings controls = ResolveControls();
        if (controls == null || controls.keyBindings == null) return false;

        string id = GetBindingId(actionName, bindingIndex);
        for (int i = 0; i < controls.keyBindings.Count; i++)
        {
            KeyBinding entry = controls.keyBindings[i];
            if (entry == null) continue;
            if (entry.actionId == id) return true;
        }

        return false;
    }

    public static int CountSavedBindings()
    {
        ControlSettings controls = ResolveControls();
        if (controls == null || controls.keyBindings == null) return 0;

        int count = 0;
        for (int i = 0; i < controls.keyBindings.Count; i++)
        {
            KeyBinding entry = controls.keyBindings[i];
            if (entry == null || string.IsNullOrEmpty(entry.bindingJson)) continue;
            count++;
        }

        return count;
    }

    public static string ToControlPath(Key key)
    {
        if (key == Key.None) return null;

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return null;

        KeyControl control = keyboard[key];
        if (control == null || control.device == null) return null;

        return "<" + control.device.layout + ">/" + control.name;
    }

    private static void ApplyOverride(string actionName, int bindingIndex, string path)
    {
        if (string.IsNullOrEmpty(path)) return;

        InputAction action = GameInput.GetAction(actionName);
        if (action == null) return;
        if (bindingIndex < 0 || bindingIndex >= action.bindings.Count) return;

        action.ApplyBindingOverride(bindingIndex, path);
    }

    private static void SaveBinding(string actionName, int bindingIndex, string path)
    {
        ControlSettings controls = ResolveControls();
        if (controls == null) return;

        if (controls.keyBindings == null) controls.keyBindings = new List<KeyBinding>();

        string id = GetBindingId(actionName, bindingIndex);
        RemoveEntry(controls.keyBindings, id);
        controls.keyBindings.Add(new KeyBinding { actionId = id, bindingJson = path });

        SettingsService service = SettingsService.Instance;
        if (service != null) service.MarkDirty();
    }

    private static void RemoveSavedBinding(string actionName, int bindingIndex)
    {
        ControlSettings controls = ResolveControls();
        if (controls == null || controls.keyBindings == null) return;

        RemoveEntry(controls.keyBindings, GetBindingId(actionName, bindingIndex));

        SettingsService service = SettingsService.Instance;
        if (service != null) service.MarkDirty();
    }

    private static void RemoveEntry(List<KeyBinding> bindings, string id)
    {
        if (bindings == null || string.IsNullOrEmpty(id)) return;

        for (int i = bindings.Count - 1; i >= 0; i--)
        {
            KeyBinding entry = bindings[i];
            if (entry == null || entry.actionId != id) continue;
            bindings.RemoveAt(i);
        }
    }

    private static string FindConflict(string actionName, int bindingIndex, string path)
    {
        InputActionMap map = GameInput.DroneMap;
        if (map == null) return null;

        string ownId = GetBindingId(actionName, bindingIndex);

        for (int i = 0; i < map.actions.Count; i++)
        {
            InputAction action = map.actions[i];
            for (int b = 0; b < action.bindings.Count; b++)
            {
                InputBinding binding = action.bindings[b];
                if (binding.isComposite) continue;
                if (binding.effectivePath != path) continue;

                string currentId = GetBindingId(action.name, b);
                if (currentId == ownId) continue;
                return currentId;
            }
        }

        return null;
    }

    private static bool TryParseBindingId(string id, out string actionName, out int bindingIndex)
    {
        actionName = null;
        bindingIndex = -1;

        if (string.IsNullOrEmpty(id)) return false;

        int separator = id.LastIndexOf(BindingIdSeparator);
        if (separator <= 0 || separator == id.Length - 1) return false;

        actionName = id.Substring(0, separator);
        return int.TryParse(id.Substring(separator + 1), out bindingIndex);
    }

    private static ControlSettings ResolveControls()
    {
        SettingsService service = SettingsService.Instance;
        if (service == null || service.Data == null) return null;
        return service.Data.controls;
    }
}
