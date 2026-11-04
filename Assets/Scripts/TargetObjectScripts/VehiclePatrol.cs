using UnityEngine;

public class VehiclePatrol : MonoBehaviour
{
    [Header("Маршрут")]
    [SerializeField] private Transform[] waypoints;
    [SerializeField] private float speed = 12f;

    [Header("Плавность движения (Новое)")]
    [Tooltip("Время сглаживания при разгоне/торможении. Больше = тяжелее машина.")]
    [SerializeField] private float smoothTime = 0.6f;
    [Tooltip("Максимальная скорость поворота в градусах в секунду.")]
    [SerializeField] private float maxRotationSpeed = 45f;
    [Tooltip("За сколько метров до точки машина начнет поворачивать к следующей.")]
    [SerializeField] private float lookAheadDistance = 15f;

    [Header("Привязка к Земле и Подвеска")]
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private float heightOffset = 0.2f;
    [SerializeField] private float raycastDistance = 15f;
    [SerializeField] private float frontRearOffset = 2.5f;
    [SerializeField] private float suspensionSmoothness = 6f;

    [Header("Конец маршрута")]
    [SerializeField] private bool stopAtLastWaypoint = true;
    public bool IsFinished { get; private set; } = false;

    private int currentWaypointIndex = 0;
    private Rigidbody rb;
    
    // Внутренняя переменная для SmoothDamp
    private Vector3 currentVelocity = Vector3.zero;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    private void OnEnable()
    {
        if (rb != null) rb.isKinematic = true;
        currentVelocity = Vector3.zero; // Сбрасываем скорость при включении
    }

    private void OnDisable()
    {
        if (rb != null)
        {
            if (!rb.isKinematic)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
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

        // --- ЛОГИКА LOOK-AHEAD (Взгляд вперед) ---
        Vector3 nextTargetPos = waypoints[(currentWaypointIndex + 1) % waypoints.Length].position;
        Vector3 dirToTarget = (targetPos - transform.position).normalized;
        Vector3 dirToNext = (nextTargetPos - target.position).normalized;
        
        float distanceToTarget = Vector3.Distance(transform.position, targetPos);
        
        // Если мы близко к текущей точке, начинаем плавно смотреть на следующую
        Vector3 finalLookDirection = dirToTarget;
        if (distanceToTarget < lookAheadDistance)
        {
            float blend = 1f - (distanceToTarget / lookAheadDistance);
            finalLookDirection = Vector3.Slerp(dirToTarget, dirToNext, blend).normalized;
        }

        // --- ПЛАВНЫЙ ПОВОРОТ (Yaw) ---
        if (finalLookDirection != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(finalLookDirection, transform.up);
            // RotateTowards ограничивает скорость поворота
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation, 
                targetRotation, 
                maxRotationSpeed * Time.fixedDeltaTime
            );
        }

        // --- ПЛАВНОЕ ДВИЖЕНИЕ (SmoothDamp) ---
        // Вместо резкого MoveTowards используем SmoothDamp
        transform.position = Vector3.SmoothDamp(
            transform.position, 
            targetPos, 
            ref currentVelocity, 
            smoothTime, 
            speed * 1.5f, // Максимальная скорость (чуть больше базовой для запаса)
            Time.fixedDeltaTime
        );

        // --- ПЕРЕКЛЮЧЕНИЕ ВЕЙПОИНТОВ ---
        if (distanceToTarget < 2.5f)
        {
            if (currentWaypointIndex == waypoints.Length - 1 && stopAtLastWaypoint)
            {
                IsFinished = true;
                this.enabled = false;
                Debug.Log($"[{gameObject.name}] Прибыл в конечную точку и остановился.");
            }
            else
            {
                currentWaypointIndex = (currentWaypointIndex + 1) % waypoints.Length;
                
                // ВАЖНО: Сбрасываем накопленную скорость, чтобы на резком повороте машину не занесло
                currentVelocity = Vector3.zero; 
            }
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
            float smoothY = Mathf.Lerp(transform.position.y, targetY, Time.fixedDeltaTime * suspensionSmoothness);

            Vector3 newPos = transform.position;
            newPos.y = smoothY;
            transform.position = newPos;

            // 2. Наклон кузова по рельефу (БЕЗ сброса поворота руля)
            Vector3 surfaceNormal = (frontHit.normal + rearHit.normal).normalized;
            
            // Проектируем текущее направление "вперед" машины на плоскость земли
            Vector3 forwardOnTerrain = Vector3.ProjectOnPlane(transform.forward, surfaceNormal).normalized;

            if (forwardOnTerrain != Vector3.zero)
            {
                // Применяем наклон, сохраняя текущий поворот (Yaw)
                Quaternion targetRotation = Quaternion.LookRotation(forwardOnTerrain, surfaceNormal);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.fixedDeltaTime * suspensionSmoothness);
            }
        }
        else if (hitFront || hitRear)
        {
            RaycastHit activeHit = hitFront ? frontHit : rearHit;
            float targetY = activeHit.point.y + heightOffset;
            Vector3 newPos = transform.position;
            newPos.y = Mathf.Lerp(transform.position.y, targetY, Time.fixedDeltaTime * suspensionSmoothness);
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

        Gizmos.color = Color.cyan;
        Vector3 frontOrigin = transform.position + transform.forward * frontRearOffset + Vector3.up * 3f;
        Vector3 rearOrigin = transform.position - transform.forward * frontRearOffset + Vector3.up * 3f;
        Gizmos.DrawRay(frontOrigin, Vector3.down * raycastDistance);
        Gizmos.DrawRay(rearOrigin, Vector3.down * raycastDistance);
    }

    public void StartRetreat(float retreatSpeed)
    {
        if (IsFinished)
        {
            System.Array.Reverse(waypoints);
            speed = retreatSpeed;
            currentWaypointIndex = 1;
            stopAtLastWaypoint = false;
            IsFinished = false;
            
            // Сбрасываем скорость при развороте
            currentVelocity = Vector3.zero; 
            
            this.enabled = true;
            Debug.Log($"[{gameObject.name}] НАЧИНАЕТ ОТСТУПЛЕНИЕ!");
        }
    }
}