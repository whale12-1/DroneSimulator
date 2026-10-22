using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

public class InteractionUIController : MonoBehaviour
{
    [SerializeField] private CanvasGroup hintGroup;
    [SerializeField] private CanvasGroup shopGroup;
    [SerializeField] private CanvasGroup missionBoardGroup;
    [SerializeField] private TMP_Text hintText;

    private float lastOpenTime;
    private OpenWindow openWindow;

    public bool IsWindowOpen => openWindow != OpenWindow.None;

    private enum OpenWindow
    {
        None,
        Shop,
        MissionBoard
    }

    private void Awake()
    {
        SetGroupVisible(shopGroup, false);
        SetGroupVisible(missionBoardGroup, false);
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
            CloseOpenWindow();
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

    public void OpenShop()
    {
        OpenWindowWithGroup(OpenWindow.Shop, shopGroup);
    }

    public void OpenMissionBoard()
    {
        lastOpenTime = Time.unscaledTime;
        OpenWindowWithGroup(OpenWindow.MissionBoard, missionBoardGroup);
    }

    public void CloseShop()
    {
        if (openWindow == OpenWindow.Shop)
            CloseOpenWindow();
    }

    public void CloseMissionBoard()
    {
        if (openWindow == OpenWindow.MissionBoard)
            CloseOpenWindow();
    }

    private void OpenWindowWithGroup(OpenWindow window, CanvasGroup group)
    {
        lastOpenTime = Time.unscaledTime;
        if (IsWindowOpen)
            return;

        openWindow = window;
        SetGroupVisible(group, true);
        HideHint();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Time.timeScale = 0f;
    }

    private void CloseOpenWindow()
    {
        if (!IsWindowOpen)
            return;

        CloseShopVisuals();
        openWindow = OpenWindow.None;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        Time.timeScale = 1f;
    }

    private void CloseShopVisuals()
    {
        SetGroupVisible(shopGroup, false);
        SetGroupVisible(missionBoardGroup, false);
    }

    private static void SetGroupVisible(CanvasGroup group, bool visible)
    {
        if (group == null)
            return;
        group.alpha = visible ? 1f : 0f;
        group.interactable = visible;
        group.blocksRaycasts = visible;
    }

    private void OnDisable()
    {
        if (IsWindowOpen)
        {
            CloseShopVisuals();
            openWindow = OpenWindow.None;
            Time.timeScale = 1f;
        }
    }
}
