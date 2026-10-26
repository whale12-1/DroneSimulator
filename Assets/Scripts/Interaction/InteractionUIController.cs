using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

public class InteractionUIController : MonoBehaviour
{
    [Header("Center Hint")]
    [SerializeField] private CanvasGroup hintGroup;
    [SerializeField] private TMP_Text hintText;

    [Header("Bottom Sitting Hint")]
    [SerializeField] private CanvasGroup sittingHintGroup;
    [SerializeField] private TMP_Text sittingHintText;

    [Header("Windows")]
    [SerializeField] private CanvasGroup shopGroup;
    [SerializeField] private CanvasGroup missionBoardGroup;

    private float lastOpenTime;
    private int currentOpenWindow = 0;

    private void Awake()
    {
        if (hintGroup != null) hintGroup.alpha = 0f;
        if (sittingHintGroup != null) sittingHintGroup.alpha = 0f;
        if (shopGroup != null) shopGroup.alpha = 0f;
        if (missionBoardGroup != null) missionBoardGroup.alpha = 0f;
    }

    private void Update()
    {
        if (currentOpenWindow == 0) return;
        if (Time.unscaledTime - lastOpenTime < 0.2f) return;

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && (keyboard.escapeKey.wasPressedThisFrame || keyboard.eKey.wasPressedThisFrame))
        {
            if (currentOpenWindow == 1) CloseShop();
            else if (currentOpenWindow == 2) CloseMissionBoard();
        }
    }

    public void ShowHint(string text)
    {
        if (hintText != null) hintText.text = text;
        if (hintGroup != null) hintGroup.alpha = 1f;
    }

    public void HideHint()
    {
        if (hintGroup != null) hintGroup.alpha = 0f;
    }

    public void ShowSittingHint(string text)
    {
        if (sittingHintText != null) sittingHintText.text = text;
        if (sittingHintGroup != null) sittingHintGroup.alpha = 1f;
    }

    public void HideSittingHint()
    {
        if (sittingHintGroup != null) sittingHintGroup.alpha = 0f;
    }

    public void OpenShop()
    {
        lastOpenTime = Time.unscaledTime;
        currentOpenWindow = 1;
        if (shopGroup != null) shopGroup.alpha = 1f;
        HideHint();
        HideSittingHint();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Time.timeScale = 0f;
    }

    public void CloseShop()
    {
        currentOpenWindow = 0;
        if (shopGroup != null) shopGroup.alpha = 0f;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        Time.timeScale = 1f;
    }

    public void OpenMissionBoard()
    {
        lastOpenTime = Time.unscaledTime;
        currentOpenWindow = 2;
        if (missionBoardGroup != null) missionBoardGroup.alpha = 1f;
        HideHint();
        HideSittingHint();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Time.timeScale = 0f;
    }

    public void CloseMissionBoard()
    {
        currentOpenWindow = 0;
        if (missionBoardGroup != null) missionBoardGroup.alpha = 0f;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        Time.timeScale = 1f;
    }
}