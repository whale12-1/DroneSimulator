using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class ShahedFlight : MonoBehaviour
{
    [Header("Цель и Скорость")]
    [SerializeField] private Transform target;     // Ссылка на БТР или точку атаки
    [SerializeField] private float speed = 35f;     // Скорость полета (м/с)
    [SerializeField] private float turnSpeed = 2f;  // Плавность разворота

    private Rigidbody rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }

    private void FixedUpdate()
    {
        if (target == null) return;

        // Вектор направления к цели
        Vector3 directionToTarget = (target.position - transform.position).normalized;

        // Плавный разворот дрона в сторону цели
        Quaternion targetRotation = Quaternion.LookRotation(directionToTarget);
        rb.MoveRotation(Quaternion.Slerp(rb.rotation, targetRotation, turnSpeed * Time.fixedDeltaTime));

        // Задаем физическую скорость (нужно для расчетов упреждения БТР!)
        rb.linearVelocity = transform.forward * speed;
    }
}