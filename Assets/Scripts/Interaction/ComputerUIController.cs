using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class ComputerUIController : MonoBehaviour
{
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private RectTransform panelTransform;

    private Coroutine openRoutine;
    private float lastOpenTime;
    private float lastCloseTime = -1f;

    public bool IsOpen { get; private set; }
    public bool IsOpenAnimationComplete { get; private set; }

    private void Awake()
    {
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
        if (panelTransform == null) panelTransform = GetComponent<RectTransform>();
    }

    private void Update()
    {
        if (!IsOpen) return;
        if (Time.unscaledTime - lastOpenTime < 0.2f) return;

        MissionMenuController missionMenu = FindAnyObjectByType<MissionMenuController>(FindObjectsInactive.Include);
        if (missionMenu != null && (missionMenu.IsOpen || Time.unscaledTime - missionMenu.LastClosedTime < 0.2f)) return;

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && (keyboard.eKey.wasPressedThisFrame || keyboard.escapeKey.wasPressedThisFrame))
        {
            CloseDesktop();
        }
    }

    public void OpenDesktop()
    {
        if (Time.unscaledTime - lastCloseTime < 0.2f) return;

        gameObject.SetActive(true);

        if (canvasGroup != null) canvasGroup.blocksRaycasts = true;

        IsOpen = true;
        IsOpenAnimationComplete = false;
        lastOpenTime = Time.unscaledTime;

        if (openRoutine != null)
        {
            StopCoroutine(openRoutine);
            openRoutine = null;
        }

        openRoutine = StartCoroutine(AnimateOpen());
    }

    public void CloseDesktop()
    {
        lastCloseTime = Time.unscaledTime;
        IsOpen = false;
        IsOpenAnimationComplete = false;

        if (openRoutine != null)
        {
            StopCoroutine(openRoutine);
            openRoutine = null;
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        Time.timeScale = 1f;
    }

    public void ToggleDesktop()
    {
        MissionMenuController missionMenu = FindAnyObjectByType<MissionMenuController>(FindObjectsInactive.Include);
        if (missionMenu != null && missionMenu.IsOpen)
            return;

        if (IsOpen) CloseDesktop();
        else OpenDesktop();
    }

    private IEnumerator AnimateOpen()
    {
        Time.timeScale = 0f;

        if (canvasGroup != null) canvasGroup.alpha = 0f;
        if (panelTransform != null) panelTransform.localScale = Vector3.one * 0.1f;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        float duration = 0.3f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            if (canvasGroup != null) canvasGroup.alpha = Mathf.Lerp(0f, 1f, t);
            if (panelTransform != null) panelTransform.localScale = Vector3.Lerp(Vector3.one * 0.1f, Vector3.one, t);

            yield return null;
        }

        if (canvasGroup != null) canvasGroup.alpha = 1f;
        if (panelTransform != null) panelTransform.localScale = Vector3.one;

        openRoutine = null;
        IsOpenAnimationComplete = true;
    }
}
