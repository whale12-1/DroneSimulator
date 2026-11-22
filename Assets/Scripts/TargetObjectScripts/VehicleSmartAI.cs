using UnityEngine;

public class VehicleSmartAI : MonoBehaviour
{
    [Header("Сенсоры и Слои")]
    [SerializeField] private LayerMask droneLayerMask;
    [SerializeField] private float detectionRadius = 150f;

    [Header("Компоненты Башни")]
    [SerializeField] private Transform turretGimbal; // Указываем TurretPivot или GunObj
    [SerializeField] private float turretRotationSpeed = 60f;

    [Header("Ограничения Наклона Ствола")]
    [SerializeField] private float minPitch = -5f;   // Опускание ствола вниз
    [SerializeField] private float maxPitch = 70f;  // Подъем ствола вверх (у БТР-80/82 до 70°)

    [Header("Стрельба (Particle System)")]
    [SerializeField] private ParticleSystem cannonParticleSystem; // Система частиц трассеров/пуль
    [SerializeField] private AudioSource shootAudioSource;
    [SerializeField] private float fireRate = 0.1f;               // Пауза между выстрелами (0.1 = 600 выстрелов/мин)
    [SerializeField] private float maxFireAngle = 10f;              // Допустимый угол погрешности прицела для начала огня
    [SerializeField] private float bulletSpeed = 300f;            // Скорость пули (должна совпадать с Start Speed в ParticleSystem)

    [Header("Средства Защиты (EW / Smoke)")]
    [SerializeField] private Transform smokeLaunchPoint;
    [SerializeField] private GameObject smokeGrenadePrefab;
    [SerializeField] private float dangerThresholdForSmoke = 75f;

    private Transform currentTarget;
    private bool smokeDeployed = false;
    private float nextFireTime = 0f;

    private void Update()
    {
        FindAndPrioritizeTarget();

        // 1. Если цель НЕ найдена
        if (currentTarget == null)
        {
            if (Time.frameCount % 60 == 0) // Лог раз в секунду
            {
                //Debug.LogWarning("[AI Debug] Цель НЕ найдена! Проверьте LayerMask и Collider на дроне.");
            }
            StopShooting();
            return;
        }

        // 2. Если цель НАЙДЕНА — выводим инфо о цели
        if (Time.frameCount % 30 == 0) // Лог раз в 0.5 секунды
        {
            float dist = Vector3.Distance(transform.position, currentTarget.position);
            //Debug.Log($"<color=green>[AI Target]</color> Цель захвачена: {currentTarget.name} | Дистанция: {dist:F1}м");
        }

        Vector3 predictedTargetPosition = CalculateLeadPosition(currentTarget);
        RotateTurretTowardsTarget(predictedTargetPosition);
        TryShootAtTarget(predictedTargetPosition);
        EvaluateTacticalDefense();
    }

    private void FindAndPrioritizeTarget()
    {
        Collider[] detectedDrones = Physics.OverlapSphere(transform.position, detectionRadius, droneLayerMask);

        float highestDangerScore = -1f;
        Transform mostDangerousDrone = null;

        foreach (Collider droneCol in detectedDrones)
        {
            Transform drone = droneCol.transform;

            float distance = Vector3.Distance(transform.position, drone.position);

            // Безопасный поиск Rigidbody на объекте, родителе или child
            Rigidbody droneRb = drone.GetComponent<Rigidbody>();
            if (droneRb == null) droneRb = drone.GetComponentInParent<Rigidbody>();
            if (droneRb == null) droneRb = drone.GetComponentInChildren<Rigidbody>();

            float approachSpeed = 0f;
            if (droneRb != null)
            {
                Vector3 directionToVehicle = (transform.position - drone.position).normalized;
                approachSpeed = Vector3.Dot(droneRb.linearVelocity, directionToVehicle);
            }

            float dangerScore = (100f / Mathf.Max(distance, 1f)) + (approachSpeed * 2f);

            if (dangerScore > highestDangerScore)
            {
                highestDangerScore = dangerScore;
                mostDangerousDrone = drone;
            }
        }

        currentTarget = mostDangerousDrone;
    }

    private Vector3 CalculateLeadPosition(Transform target)
    {
        if (target == null) return transform.position;

        Vector3 targetPos = target.position;
        Vector3 targetVelocity = Vector3.zero;

        // Ищем Rigidbody на самом объекте, у родителей или в дочерних элементах
        Rigidbody rb = target.GetComponent<Rigidbody>();
        if (rb == null) rb = target.GetComponentInParent<Rigidbody>();
        if (rb == null) rb = target.GetComponentInChildren<Rigidbody>();

        if (rb != null)
        {
            targetVelocity = rb.linearVelocity;
        }

        float distance = Vector3.Distance(turretGimbal != null ? turretGimbal.position : transform.position, targetPos);
        float timeToHit = distance / Mathf.Max(bulletSpeed, 1f);

        return targetPos + (targetVelocity * timeToHit);
    }

    private void RotateTurretTowardsTarget(Vector3 targetPosition)
    {
        if (turretGimbal == null) return;

        Vector3 dirToTarget = targetPosition - turretGimbal.position;
        if (dirToTarget.sqrMagnitude < 0.001f) return;

        // 1. Горизонтальное направление (Yaw) без Y-компоненты
        Vector3 horizontalDir = dirToTarget;
        horizontalDir.y = 0f;

        if (horizontalDir.sqrMagnitude < 0.001f) return;

        // Поворот башни по горизонту
        Quaternion targetYawRotation = Quaternion.LookRotation(horizontalDir, Vector3.up);

        // Компенсация сдвига осей 3D-модели (-90 градусов по Y)
        targetYawRotation *= Quaternion.Euler(0f, -90f, 0f);

        // 2. Расчет угла наклона ствола по высоте (Pitch)
        float distanceHorizontal = horizontalDir.magnitude;
        // Положительный угол поднимает ствол к цели
        float targetPitchAngle = Mathf.Atan2(dirToTarget.y, distanceHorizontal) * Mathf.Rad2Deg;
        targetPitchAngle = Mathf.Clamp(targetPitchAngle, minPitch, maxPitch);

        // 3. Накладываем наклон вокруг оси Vector3.forward (так как после разворота -90° именно она отвечает за Pitch)
        Quaternion targetFullRotation = targetYawRotation * Quaternion.AngleAxis(targetPitchAngle, Vector3.forward);

        // 4. Плавно поворачиваем башню к цели
        turretGimbal.rotation = Quaternion.RotateTowards(
            turretGimbal.rotation,
            targetFullRotation,
            turretRotationSpeed * Time.deltaTime
        );
    }

    private void TryShootAtTarget(Vector3 targetPosition)
    {
        if (cannonParticleSystem == null || turretGimbal == null) return;

        // 1. Вектор направления ствола БТР в мировых координатах
        Vector3 fireDirection = (turretGimbal.rotation * Vector3.right).normalized;
        Vector3 fireOrigin = cannonParticleSystem.transform.position;
        Vector3 dirToTarget = (targetPosition - fireOrigin).normalized;

        // Отрисовка отладочных лучей в окне Scene
        Debug.DrawRay(fireOrigin, fireDirection * 100f, Color.red);   // Красный: ствол
        Debug.DrawRay(fireOrigin, dirToTarget * 100f, Color.green);  // Зеленый: цель

        float angleToTarget = Vector3.Angle(fireDirection, dirToTarget);

        if (Time.frameCount % 15 == 0)
        {
            //Debug.Log($"[AIM ANGLE] Угол: {angleToTarget:F1}° | Порог: {maxFireAngle}°");
        }

        if (angleToTarget <= maxFireAngle)
        {
            if (Time.time >= nextFireTime)
            {
                //Debug.Log("<color=red>[EMIT BULLET!]</color>");

                // 2. Принудительно задаем позицию и вектор скорости частицы в мировых координатах
                ParticleSystem.EmitParams emitParams = new ParticleSystem.EmitParams
                {
                    position = fireOrigin,
                    velocity = fireDirection * bulletSpeed // Пуля полетит СТРОГО вдоль красного луча
                };

                cannonParticleSystem.Emit(emitParams, 1);

                if (shootAudioSource != null && shootAudioSource.clip != null)
                {
                    shootAudioSource.PlayOneShot(shootAudioSource.clip);
                }

                nextFireTime = Time.time + fireRate;
            }
        }
    }

    private void StopShooting()
    {
        if (shootAudioSource != null && shootAudioSource.isPlaying && shootAudioSource.loop)
        {
            shootAudioSource.Stop();
        }
    }

    private void EvaluateTacticalDefense()
    {
        if (smokeDeployed || currentTarget == null) return;

        float distance = Vector3.Distance(transform.position, currentTarget.position);

        if (distance < 35f)
        {
            DeploySmokeScreen();
        }
    }

    private void DeploySmokeScreen()
    {
        smokeDeployed = true;
        //Debug.Log("[AI] Опасность подрыва! Отстрел аэрозольной дымовой завесы.");

        if (smokeGrenadePrefab != null && smokeLaunchPoint != null)
        {
            Instantiate(smokeGrenadePrefab, smokeLaunchPoint.position, smokeLaunchPoint.rotation);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
    }
}