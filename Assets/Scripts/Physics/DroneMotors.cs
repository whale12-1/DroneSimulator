using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class DroneMotors : MonoBehaviour
{
    [Header("Точки Моторов")]
    [SerializeField] private Transform motorFL;
    [SerializeField] private Transform motorFR;
    [SerializeField] private Transform motorBL;
    [SerializeField] private Transform motorBR;

    [Header("Настройки Максимальной Тяги (Ньютоны)")]
    [SerializeField] private float maxThrust = 10f;

    private Rigidbody rb;
    public float MaxTotalThrust => maxThrust * 4f;
    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        // Принудительно центрируем центр масс, чтобы BoxCollider не сбивал баланс
        rb.centerOfMass = Vector3.zero;
    }

    /// <summary>
    /// Применение силы тяги к каждому мотору
    /// Сигнал от 0.0 до 1.0 для каждого мотора
    /// </summary>
    public void ApplyMotorsThrust(float thrustFL, float thrustFR, float thrustBL, float thrustBR)
    {
        ApplyForceToMotor(motorFL, thrustFL);
        ApplyForceToMotor(motorFR, thrustFR);
        ApplyForceToMotor(motorBL, thrustBL);
        ApplyForceToMotor(motorBR, thrustBR);
    }

    private void ApplyForceToMotor(Transform motorTransform, float thrustPercent)
    {
        if (motorTransform == null) return;

        thrustPercent = Mathf.Clamp01(thrustPercent);

        // Чистая физическая тяга без искусственного умножения/деления
        Vector3 force = motorTransform.up * (maxThrust * thrustPercent);

        rb.AddForceAtPosition(force, motorTransform.position, ForceMode.Force);
    }
}