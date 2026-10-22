using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

public class InteractionUIController : MonoBehaviour
{
    [SerializeField] private CanvasGroup hintGroup;
    [SerializeField] private CanvasGroup missionMapGroup;
    [SerializeField] private TMP_Text hintText;

    private float lastOpenTime;

    public bool IsWindowOpen { get; private set; }

    private void Awake()
    {
        CloseMissionMapVisuals();
        HideHint();
    }

    private void Update()
    {
        if (!IsWindowOpen)
            return;

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return;

        if (Time.unscaledTime - lastOpenTime < 0.2f)
            return;

        if (keyboard.eKey.wasPressedThisFrame || keyboard.escapeKey.wasPressedThisFrame)
            CloseMissionMap();
    }

    public void ShowHint()
    {
        ShowHint("Нажмите E");
    }

    public void ShowHint(string prompt)
    {
        if (hintText != null)
            hintText.text = string.IsNullOrEmpty(prompt) ? "Нажмите E" : prompt;
        if (hintGroup == null)
            return;
        hintGroup.alpha = 1f;
        hintGroup.interactable = false;
        hintGroup.blocksRaycasts = false;
    }

    public void HideHint()
    {
        if (hintGroup == null)
            return;
        hintGroup.alpha = 0f;
        hintGroup.interactable = false;
        hintGroup.blocksRaycasts = false;
    }

    public void OpenMissionMap()
    {
        lastOpenTime = Time.unscaledTime;

        if (IsWindowOpen)
            return;

        IsWindowOpen = true;
        if (missionMapGroup != null)
        {
            missionMapGroup.alpha = 1f;
            missionMapGroup.interactable = true;
            missionMapGroup.blocksRaycasts = true;
        }
        HideHint();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Time.timeScale = 0f;
    }

    public void CloseMissionMap()
    {
        if (!IsWindowOpen)
            return;

        IsWindowOpen = false;
        CloseMissionMapVisuals();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        Time.timeScale = 1f;
    }

    private void CloseMissionMapVisuals()
    {
        if (missionMapGroup == null)
            return;
        missionMapGroup.alpha = 0f;
        missionMapGroup.interactable = false;
        missionMapGroup.blocksRaycasts = false;
    }

    private void OnDisable()
    {
        if (IsWindowOpen)
        {
            IsWindowOpen = false;
            Time.timeScale = 1f;
        }
    }
}
