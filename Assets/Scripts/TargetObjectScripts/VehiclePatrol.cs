using UnityEngine;

public class VehiclePatrol : MonoBehaviour
{
    [Header("Маршрут")]
    [SerializeField] private Transform[] waypoints;
    [SerializeField] private float speed = 12f;
    [SerializeField] private float rotationSpeed = 3f;

    [Header("Привязка к Земле и Подвеска")]
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private float heightOffset = 0.2f;
    [SerializeField] private float raycastDistance = 15f;
    [SerializeField] private float frontRearOffset = 2.5f;     // Расстояние между передом и кормой (для ЗиЛ/БТР ~ 2.5)
    [SerializeField] private float suspensionSmoothness = 6f;  // Плавность сглаживания подвески

    private int currentWaypointIndex = 0;
    private Rigidbody rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    private void OnEnable()
    {
        if (rb != null)
        {
            rb.isKinematic = true; // Замораживаем физические толчки от кочек
        }
    }

    private void OnDisable()
    {
        if (rb != null)
        {
            // Проверяем: сбрасывать скорость можно ТОЛЬКО у не-кинематического тела
            if (!rb.isKinematic)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            // Если нужно заморозить объект при выключении скрипта:
            rb.isKinematic = true;
        }
    }

    private void FixedUpdate()
    {
        if (waypoints == null || waypoints.Length == 0) return;

        MoveToWaypoint();
        AlignToTerrainTwoPoints();
    }

    private void MoveToWaypoint()
    {
        Transform target = waypoints[currentWaypointIndex];
        Vector3 targetPos = new Vector3(target.position.x, transform.position.y, target.position.z);

        // Плавный поворот в сторону точки
        Vector3 direction = (targetPos - transform.position).normalized;
        if (direction != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotationSpeed);
        }

        // Плавное движение вперед
        transform.position = Vector3.MoveTowards(transform.position, targetPos, speed * Time.deltaTime);

        // Переключение вейпоинтов
        if (Vector3.Distance(new Vector3(transform.position.x, 0, transform.position.z),
                             new Vector3(target.position.x, 0, target.position.z)) < 2.5f)
        {
            currentWaypointIndex = (currentWaypointIndex + 1) % waypoints.Length;
        }
    }

    private void AlignToTerrainTwoPoints()
    {
        Vector3 frontOrigin = transform.position + transform.forward * frontRearOffset + Vector3.up * 3f;
        Vector3 rearOrigin = transform.position - transform.forward * frontRearOffset + Vector3.up * 3f;

        bool hitFront = Physics.Raycast(frontOrigin, Vector3.down, out RaycastHit frontHit, raycastDistance, groundLayer);
        bool hitRear = Physics.Raycast(rearOrigin, Vector3.down, out RaycastHit rearHit, raycastDistance, groundLayer);

        if (hitFront && hitRear)
        {
            // 1. Мягкое сглаживание высоты Y (подвеска)
            float targetY = (frontHit.point.y + rearHit.point.y) * 0.5f + heightOffset;
            float smoothY = Mathf.Lerp(transform.position.y, targetY, Time.deltaTime * suspensionSmoothness);

            Vector3 newPos = transform.position;
            newPos.y = smoothY;
            transform.position = newPos;

            // 2. Плавный наклон кузова по вектору между передней и задней точкой
            Vector3 pitchDirection = (frontHit.point - rearHit.point).normalized;
            Vector3 surfaceNormal = (frontHit.normal + rearHit.normal).normalized;

            if (pitchDirection != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(pitchDirection, surfaceNormal);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * suspensionSmoothness);
            }
        }
        else if (hitFront || hitRear)
        {
            RaycastHit activeHit = hitFront ? frontHit : rearHit;
            float targetY = activeHit.point.y + heightOffset;
            Vector3 newPos = transform.position;
            newPos.y = Mathf.Lerp(transform.position.y, targetY, Time.deltaTime * suspensionSmoothness);
            transform.position = newPos;
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (waypoints == null || waypoints.Length < 2) return;

        Gizmos.color = Color.yellow;
        for (int i = 0; i < waypoints.Length; i++)
        {
            if (waypoints[i] == null) continue;
            Vector3 current = waypoints[i].position;
            Vector3 next = waypoints[(i + 1) % waypoints.Length].position;
            Gizmos.DrawSphere(current, 0.8f);
            Gizmos.DrawLine(current, next);
        }

        // Отображение переднего и заднего лучей подвески
        Gizmos.color = Color.cyan;
        Vector3 frontOrigin = transform.position + transform.forward * frontRearOffset + Vector3.up * 3f;
        Vector3 rearOrigin = transform.position - transform.forward * frontRearOffset + Vector3.up * 3f;
        Gizmos.DrawRay(frontOrigin, Vector3.down * raycastDistance);
        Gizmos.DrawRay(rearOrigin, Vector3.down * raycastDistance);
    }
}