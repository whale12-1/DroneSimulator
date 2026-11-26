using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// Витринная анимация дрона в магазине бункера: мягкое парение над столом
/// (±0.015 м), микростабилизация корпуса и вращение на 360° мышью.
/// Всё считается на unscaled-времени: магазин открывается из ComputerOS,
/// где Time.timeScale = 0 (см. BunkerModalStack).
/// Иерархия ставится контроллером: stage -> yawPitch -> hover -> модель.
/// </summary>
public class DroneShopShowcase : MonoBehaviour
{
    [Header("Парение")]
    [Tooltip("Амплитуда вертикального покачивания, метры")]
    [SerializeField] private float hoverAmplitude = 0.015f;

    [Tooltip("Частота покачивания, Гц")]
    [SerializeField] private float hoverFrequency = 0.32f;

    [Tooltip("Амплитуда микростабилизации корпуса, градусы")]
    [SerializeField] private float tiltAmplitude = 1.1f;

    [Header("Вращение мышью")]
    [Tooltip("Градусов поворота на пиксель движения мыши")]
    [SerializeField] private float dragSensitivity = 0.28f;

    [Tooltip("Скорость догоняния целевого угла (1/с)")]
    [SerializeField] private float rotationSmoothing = 16f;

    [Tooltip("Скорость возврата ракурса при сбросе (1/с)")]
    [SerializeField] private float resetSmoothing = 7f;

    private Transform yawPivot;
    private Transform hoverRoot;

    private float yaw;
    private float pitch;
    private float targetYaw;
    private float targetPitch;
    private float phase;
    private bool dragging;

    public bool IsDragging => dragging;

    /// <summary>Подключение к иерархии, собранной DroneShopController.</summary>
    public void Initialize(Transform yawPivot, Transform hoverRoot)
    {
        this.yawPivot = yawPivot;
        this.hoverRoot = hoverRoot;

        yaw = targetYaw = 0f;
        pitch = targetPitch = 0f;
        phase = Random.value * 10f;
        dragging = false;

        ApplyRotation();
        ApplyHover(0f);
    }

    /// <summary>Плавный возврат в исходную ориентацию лицом к камере (клавиша R).</summary>
    public void ResetView()
    {
        targetYaw = 0f;
        targetPitch = 0f;
    }

    private void Update()
    {
        float deltaTime = Time.unscaledDeltaTime;

        HandleDrag();

        float blend = 1f - Mathf.Exp(-rotationSmoothing * deltaTime);
        yaw = Mathf.LerpAngle(yaw, targetYaw, blend);
        pitch = Mathf.LerpAngle(pitch, targetPitch, blend);
        ApplyRotation();

        phase += deltaTime * hoverFrequency * Mathf.PI * 2f;
        ApplyHover(deltaTime);
    }

    private void HandleDrag()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null)
        {
            dragging = false;
            return;
        }

        if (mouse.leftButton.wasPressedThisFrame)
            dragging = !IsPointerOverUI();

        if (mouse.leftButton.wasReleasedThisFrame)
            dragging = false;

        if (!dragging) return;

        Vector2 delta = mouse.delta.ReadValue();
        if (delta.sqrMagnitude <= 0f) return;

        targetYaw += delta.x * dragSensitivity;
        targetPitch -= delta.y * dragSensitivity;
    }

    private void ApplyRotation()
    {
        if (yawPivot == null) return;
        yawPivot.localRotation = Quaternion.Euler(pitch, yaw, 0f);
    }

    private void ApplyHover(float deltaTime)
    {
        if (hoverRoot == null) return;

        Vector3 localPosition = hoverRoot.localPosition;
        localPosition.y = Mathf.Sin(phase) * hoverAmplitude;
        hoverRoot.localPosition = localPosition;

        float tiltX = Mathf.Sin(phase * 0.61f) * tiltAmplitude;
        float tiltZ = Mathf.Cos(phase * 0.43f) * tiltAmplitude * 0.7f;
        hoverRoot.localRotation = Quaternion.Euler(tiltX, 0f, tiltZ);
    }

    private static bool IsPointerOverUI()
    {
        EventSystem current = EventSystem.current;
        return current != null && current.IsPointerOverGameObject();
    }
}
