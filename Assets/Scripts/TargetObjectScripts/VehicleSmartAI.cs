using UnityEngine;

public class VehicleSmartAI : MonoBehaviour
{
    [Header("РЎРµРЅСЃРѕСЂС‹ Рё РЎР»РѕРё")]
    [SerializeField] private LayerMask droneLayerMask;
    [SerializeField] private float detectionRadius = 150f;

    [Header("РљРѕРјРїРѕРЅРµРЅС‚С‹ Р‘Р°С€РЅРё")]
    [SerializeField] private Transform turretGimbal; // РЈРєР°Р·С‹РІР°РµРј TurretPivot РёР»Рё GunObj
    [SerializeField] private float turretRotationSpeed = 60f;

    [Header("РћРіСЂР°РЅРёС‡РµРЅРёСЏ РќР°РєР»РѕРЅР° РЎС‚РІРѕР»Р°")]
    [SerializeField] private float minPitch = -5f;   // РћРїСѓСЃРєР°РЅРёРµ СЃС‚РІРѕР»Р° РІРЅРёР·
    [SerializeField] private float maxPitch = 70f;  // РџРѕРґСЉРµРј СЃС‚РІРѕР»Р° РІРІРµСЂС… (Сѓ Р‘РўР -80/82 РґРѕ 70В°)

    [Header("РЎС‚СЂРµР»СЊР±Р° (Particle System)")]
    [SerializeField] private ParticleSystem cannonParticleSystem; // РЎРёСЃС‚РµРјР° С‡Р°СЃС‚РёС† С‚СЂР°СЃСЃРµСЂРѕРІ/РїСѓР»СЊ
    [SerializeField] private AudioSource shootAudioSource;
    [SerializeField] private float fireRate = 0.1f;               // РџР°СѓР·Р° РјРµР¶РґСѓ РІС‹СЃС‚СЂРµР»Р°РјРё (0.1 = 600 РІС‹СЃС‚СЂРµР»РѕРІ/РјРёРЅ)
    [SerializeField] private float maxFireAngle = 10f;              // Р”РѕРїСѓСЃС‚РёРјС‹Р№ СѓРіРѕР» РїРѕРіСЂРµС€РЅРѕСЃС‚Рё РїСЂРёС†РµР»Р° РґР»СЏ РЅР°С‡Р°Р»Р° РѕРіРЅСЏ
    [SerializeField] private float bulletSpeed = 300f;            // РЎРєРѕСЂРѕСЃС‚СЊ РїСѓР»Рё (РґРѕР»Р¶РЅР° СЃРѕРІРїР°РґР°С‚СЊ СЃ Start Speed РІ ParticleSystem)

    [Header("РЎСЂРµРґСЃС‚РІР° Р—Р°С‰РёС‚С‹ (EW / Smoke)")]
    [SerializeField] private Transform smokeLaunchPoint;
    [SerializeField] private GameObject smokeGrenadePrefab;
    [SerializeField] private float dangerThresholdForSmoke = 75f;

    private Transform currentTarget;
    private bool smokeDeployed = false;
    private float nextFireTime = 0f;

    private void Update()
    {
        FindAndPrioritizeTarget();

        // 1. Р•СЃР»Рё С†РµР»СЊ РќР• РЅР°Р№РґРµРЅР°
        if (currentTarget == null)
        {
            if (Time.frameCount % 60 == 0) // Р›РѕРі СЂР°Р· РІ СЃРµРєСѓРЅРґСѓ
            {
                //Debug.LogWarning("[AI Debug] Р¦РµР»СЊ РќР• РЅР°Р№РґРµРЅР°! РџСЂРѕРІРµСЂСЊС‚Рµ LayerMask Рё Collider РЅР° РґСЂРѕРЅРµ.");
            }
            StopShooting();
            return;
        }

        // 2. Р•СЃР»Рё С†РµР»СЊ РќРђР™Р”Р•РќРђ вЂ” РІС‹РІРѕРґРёРј РёРЅС„Рѕ Рѕ С†РµР»Рё
        if (Time.frameCount % 30 == 0) // Р›РѕРі СЂР°Р· РІ 0.5 СЃРµРєСѓРЅРґС‹
        {
            float dist = Vector3.Distance(transform.position, currentTarget.position);
            //Debug.Log($"<color=green>[AI Target]</color> Р¦РµР»СЊ Р·Р°С…РІР°С‡РµРЅР°: {currentTarget.name} | Р”РёСЃС‚Р°РЅС†РёСЏ: {dist:F1}Рј");
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

            // Р‘РµР·РѕРїР°СЃРЅС‹Р№ РїРѕРёСЃРє Rigidbody РЅР° РѕР±СЉРµРєС‚Рµ, СЂРѕРґРёС‚РµР»Рµ РёР»Рё child
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

        // РС‰РµРј Rigidbody РЅР° СЃР°РјРѕРј РѕР±СЉРµРєС‚Рµ, Сѓ СЂРѕРґРёС‚РµР»РµР№ РёР»Рё РІ РґРѕС‡РµСЂРЅРёС… СЌР»РµРјРµРЅС‚Р°С…
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

        // 1. Р“РѕСЂРёР·РѕРЅС‚Р°Р»СЊРЅРѕРµ РЅР°РїСЂР°РІР»РµРЅРёРµ (Yaw) Р±РµР· Y-РєРѕРјРїРѕРЅРµРЅС‚С‹
        Vector3 horizontalDir = dirToTarget;
        horizontalDir.y = 0f;

        if (horizontalDir.sqrMagnitude < 0.001f) return;

        // РџРѕРІРѕСЂРѕС‚ Р±Р°С€РЅРё РїРѕ РіРѕСЂРёР·РѕРЅС‚Сѓ
        Quaternion targetYawRotation = Quaternion.LookRotation(horizontalDir, Vector3.up);

        // РљРѕРјРїРµРЅСЃР°С†РёСЏ СЃРґРІРёРіР° РѕСЃРµР№ 3D-РјРѕРґРµР»Рё (-90 РіСЂР°РґСѓСЃРѕРІ РїРѕ Y)
        targetYawRotation *= Quaternion.Euler(0f, -90f, 0f);

        // 2. Р Р°СЃС‡РµС‚ СѓРіР»Р° РЅР°РєР»РѕРЅР° СЃС‚РІРѕР»Р° РїРѕ РІС‹СЃРѕС‚Рµ (Pitch)
        float distanceHorizontal = horizontalDir.magnitude;
        // РџРѕР»РѕР¶РёС‚РµР»СЊРЅС‹Р№ СѓРіРѕР» РїРѕРґРЅРёРјР°РµС‚ СЃС‚РІРѕР» Рє С†РµР»Рё
        float targetPitchAngle = Mathf.Atan2(dirToTarget.y, distanceHorizontal) * Mathf.Rad2Deg;
        targetPitchAngle = Mathf.Clamp(targetPitchAngle, minPitch, maxPitch);

        // 3. РќР°РєР»Р°РґС‹РІР°РµРј РЅР°РєР»РѕРЅ РІРѕРєСЂСѓРі РѕСЃРё Vector3.forward (С‚Р°Рє РєР°Рє РїРѕСЃР»Рµ СЂР°Р·РІРѕСЂРѕС‚Р° -90В° РёРјРµРЅРЅРѕ РѕРЅР° РѕС‚РІРµС‡Р°РµС‚ Р·Р° Pitch)
        Quaternion targetFullRotation = targetYawRotation * Quaternion.AngleAxis(targetPitchAngle, Vector3.forward);

        // 4. РџР»Р°РІРЅРѕ РїРѕРІРѕСЂР°С‡РёРІР°РµРј Р±Р°С€РЅСЋ Рє С†РµР»Рё
        turretGimbal.rotation = Quaternion.RotateTowards(
            turretGimbal.rotation,
            targetFullRotation,
            turretRotationSpeed * Time.deltaTime
        );
    }

    private void TryShootAtTarget(Vector3 targetPosition)
    {
        if (cannonParticleSystem == null || turretGimbal == null) return;

        // 1. Р’РµРєС‚РѕСЂ РЅР°РїСЂР°РІР»РµРЅРёСЏ СЃС‚РІРѕР»Р° Р‘РўР  РІ РјРёСЂРѕРІС‹С… РєРѕРѕСЂРґРёРЅР°С‚Р°С…
        Vector3 fireDirection = (turretGimbal.rotation * Vector3.right).normalized;
        Vector3 fireOrigin = cannonParticleSystem.transform.position;
        Vector3 dirToTarget = (targetPosition - fireOrigin).normalized;

        // РћС‚СЂРёСЃРѕРІРєР° РѕС‚Р»Р°РґРѕС‡РЅС‹С… Р»СѓС‡РµР№ РІ РѕРєРЅРµ Scene
        Debug.DrawRay(fireOrigin, fireDirection * 100f, Color.red);   // РљСЂР°СЃРЅС‹Р№: СЃС‚РІРѕР»
        Debug.DrawRay(fireOrigin, dirToTarget * 100f, Color.green);  // Р—РµР»РµРЅС‹Р№: С†РµР»СЊ

        float angleToTarget = Vector3.Angle(fireDirection, dirToTarget);

        if (Time.frameCount % 15 == 0)
        {
            //Debug.Log($"[AIM ANGLE] РЈРіРѕР»: {angleToTarget:F1}В° | РџРѕСЂРѕРі: {maxFireAngle}В°");
        }

        if (angleToTarget <= maxFireAngle)
        {
            if (Time.time >= nextFireTime)
            {
                //Debug.Log("<color=red>[EMIT BULLET!]</color>");

                // 2. РџСЂРёРЅСѓРґРёС‚РµР»СЊРЅРѕ Р·Р°РґР°РµРј РїРѕР·РёС†РёСЋ Рё РІРµРєС‚РѕСЂ СЃРєРѕСЂРѕСЃС‚Рё С‡Р°СЃС‚РёС†С‹ РІ РјРёСЂРѕРІС‹С… РєРѕРѕСЂРґРёРЅР°С‚Р°С…
                ParticleSystem.EmitParams emitParams = new ParticleSystem.EmitParams
                {
                    position = fireOrigin,
                    velocity = fireDirection * bulletSpeed // РџСѓР»СЏ РїРѕР»РµС‚РёС‚ РЎРўР РћР“Рћ РІРґРѕР»СЊ РєСЂР°СЃРЅРѕРіРѕ Р»СѓС‡Р°
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
        //Debug.Log("[AI] РћРїР°СЃРЅРѕСЃС‚СЊ РїРѕРґСЂС‹РІР°! РћС‚СЃС‚СЂРµР» Р°СЌСЂРѕР·РѕР»СЊРЅРѕР№ РґС‹РјРѕРІРѕР№ Р·Р°РІРµСЃС‹.");

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