using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.UI;

public class KeyBindingRow : MonoBehaviour
{
    [SerializeField] private Text valueLabel;
    [SerializeField] private string actionName;
    [SerializeField] private int bindingIndex;

    private Button button;
    private System.Action onChanged;
    private bool isCapturing;

    private void Awake()
    {
        button = GetComponent<Button>();
    }

    public void Setup(string action, int index, Text label, System.Action changed)
    {
        actionName = action;
        bindingIndex = index;
        valueLabel = label;
        onChanged = changed;

        if (button == null) button = GetComponent<Button>();
        if (button != null)
        {
            button.onClick.RemoveListener(BeginCapture);
            button.onClick.AddListener(BeginCapture);
        }

        isCapturing = false;
        Refresh();
    }

    public void BeginCapture()
    {
        isCapturing = true;
        if (valueLabel != null) valueLabel.text = "Нажмите клавишу";
    }

    private void Update()
    {
        if (!isCapturing) return;

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;

        if (keyboard.escapeKey.wasPressedThisFrame)
        {
            isCapturing = false;
            Refresh();
            return;
        }

        if (keyboard.backspaceKey.wasPressedThisFrame || keyboard.deleteKey.wasPressedThisFrame)
        {
            isCapturing = false;
            RebindService.ResetBinding(actionName, bindingIndex);
            Refresh();
            Notify();
            return;
        }

        foreach (KeyControl control in keyboard.allKeys)
        {
            if (control == null || !control.wasPressedThisFrame) continue;

            isCapturing = false;
            ApplyKey(control.keyCode);
            return;
        }
    }

    private void ApplyKey(Key key)
    {
        string error;
        if (!RebindService.TryAssignKey(actionName, bindingIndex, key, out error))
        {
            Debug.LogWarning($"[KeyBindingRow] {error}");
            Refresh();
            return;
        }

        Refresh();
        Notify();
    }

    private void Refresh()
    {
        if (valueLabel == null) return;
        valueLabel.text = RebindService.GetDisplayName(actionName, bindingIndex);
    }

    private void Notify()
    {
        if (onChanged != null) onChanged();
    }
}
