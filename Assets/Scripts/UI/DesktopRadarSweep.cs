using UnityEngine;

/// <summary>
/// Вращает «лепесток» радара на рабочем столе ComputerOS.
/// Время берётся из Time.unscaledDeltaTime, поэтому вращение не останавливается при Time.timeScale = 0
/// (ComputerUIController выставляет 0 при открытии рабочего стола).
/// Пока рабочий стол закрыт, кадры не тратятся: объект ComputerOS после CloseDesktop() остаётся
/// активным (alpha = 0), поэтому анимация останавливается по ComputerUIController.IsOpen.
/// </summary>
[DisallowMultipleComponent]
public class DesktopRadarSweep : MonoBehaviour
{
    [SerializeField] private RectTransform sweepTransform;
    [SerializeField] private ComputerUIController computerUI;
    [SerializeField] private float degreesPerSecond = 120f;
    [SerializeField] private bool clockwise = true;

    private float angle;

    private void Awake()
    {
        if (sweepTransform == null) sweepTransform = transform as RectTransform;
        if (computerUI == null) computerUI = GetComponentInParent<ComputerUIController>();
    }

    private void OnEnable()
    {
        angle = 0f;
        ApplyAngle();
    }

    private void Update()
    {
        if (sweepTransform == null) return;
        if (computerUI != null && !computerUI.IsOpen) return;

        angle += (clockwise ? -1f : 1f) * degreesPerSecond * Time.unscaledDeltaTime;
        ApplyAngle();
    }

    private void ApplyAngle()
    {
        if (sweepTransform != null) sweepTransform.localRotation = Quaternion.Euler(0f, 0f, angle);
    }
}
