using System.Linq;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class DroneCollision : MonoBehaviour
{
    [Header("Конфигурация Дрона")]
    [Tooltip("Профиль БЧ из папки проекта. Если пуст, берутся значения по умолчанию ниже")]
    [SerializeField] private DroneConfig droneConfig;

    [Header("Резервные Настройки (Если нет DroneConfig)")]
    public WarheadType fallbackWarheadType = WarheadType.HEAT;
    public float fallbackDirectDamage = 250f;
    public float fallbackSplashRadius = 1.5f;
    public float fallbackSplashDamage = 40f;
    [SerializeField] private GameObject fallbackExplosionPrefab;
    [SerializeField] private AudioClip fallbackExplosionSound;

    [Header("Объективный Контроль (Дрон-Разведчик)")]
    [SerializeField] private GameObject reconDronePrefab;
    [SerializeField] private Vector3 reconOffset = new Vector3(0f, 35f, -20f);

    [Header("Ручная Детонация")]
    public bool allowManualDetonation = true;
    public KeyCode detonateKey = KeyCode.Space;

    [Header("Условия Взрыва по Столкновению")]
    public bool explodeOnAnyCollision = false;
    [SerializeField] private float minImpactVelocity = 3.0f;
    [SerializeField] private string[] instantExplodeTags = new string[] { "Hazard" };

    private bool isExploded = false;
    private DroneFlightController flightController;
    private DroneMotors motors;

    // Геттеры для гибридного использования (Config / Inspector)
    public WarheadType CurrentWarhead => droneConfig != null ? droneConfig.warheadType : fallbackWarheadType;
    public float DirectDamage => droneConfig != null ? droneConfig.directDamage : fallbackDirectDamage;
    public float SplashRadius => droneConfig != null ? droneConfig.splashRadius : fallbackSplashRadius;
    public float SplashDamage => droneConfig != null ? droneConfig.splashDamage : fallbackSplashDamage;
    public GameObject ExplosionPrefab => droneConfig != null ? droneConfig.explosionPrefab : fallbackExplosionPrefab;
    public AudioClip ExplosionSound => droneConfig != null ? droneConfig.explosionSound : fallbackExplosionSound;

    private void Awake()
    {
        flightController = GetComponent<DroneFlightController>();
        motors = GetComponent<DroneMotors>();
    }

    private void Update()
    {
        if (!isExploded && allowManualDetonation && Input.GetKeyDown(detonateKey))
        {
            Explode(transform.position, directHitCollider: null);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (isExploded) return;

        float impactVelocity = collision.relativeVelocity.magnitude;
        bool hitHazardTag = instantExplodeTags.Contains(collision.gameObject.tag);

        if (explodeOnAnyCollision || hitHazardTag || impactVelocity >= minImpactVelocity)
        {
            Vector3 contactPoint = transform.position;
            Vector3 hitNormal = -transform.forward;

            if (collision.contacts.Length > 0)
            {
                contactPoint = collision.contacts[0].point;
                hitNormal = collision.contacts[0].normal;
            }
            else
            {
                Ray ray = new Ray(transform.position - transform.forward * 1.0f, transform.forward);
                if (collision.collider.Raycast(ray, out RaycastHit hit, 5.0f))
                {
                    contactPoint = hit.point;
                    hitNormal = hit.normal;
                }
            }

            Vector3 flightDir = transform.forward;

            ApplyDirectDamage(collision.collider, contactPoint, hitNormal, flightDir);
            Explode(contactPoint, collision.collider);
        }
    }

    private void ApplyDirectDamage(Collider hitCollider, Vector3 point, Vector3 normal, Vector3 flightDir)
    {
        VehicleModule module = hitCollider.GetComponent<VehicleModule>();
        float damage = DirectDamage;
        Vector3 calculatedNormal = normal;

        // Специфика БЧ: Термобарический заряд игнорирует острые углы брони
        if (CurrentWarhead == WarheadType.Thermobaric)
        {
            calculatedNormal = -flightDir;
        }

        if (module != null)
        {
            module.TakeDamage(damage, point, calculatedNormal, flightDir);
        }
        else
        {
            VehicleHealth vehicle = hitCollider.GetComponentInParent<VehicleHealth>();
            if (vehicle != null)
            {
                vehicle.OnModuleHit(ModuleType.Armor, damage, isModuleDestroyed: false, point);
            }
        }
    }

    private void ApplySplashDamage(Vector3 center, Collider ignoredCollider)
    {
        float radius = SplashRadius;
        float baseSplashDamage = SplashDamage;

        if (radius <= 0f || baseSplashDamage <= 0f) return;

        Collider[] nearbyColliders = Physics.OverlapSphere(center, radius);
        foreach (var col in nearbyColliders)
        {
            if (col == ignoredCollider) continue;

            VehicleModule module = col.GetComponent<VehicleModule>();
            if (module != null)
            {
                float distance = Vector3.Distance(center, col.transform.position);
                // Затухание урона от эпицентра взрыва к краям
                float attenuation = Mathf.Clamp01(1.0f - (distance / radius));
                float finalDamage = baseSplashDamage * attenuation;

                if (finalDamage > 1f)
                {
                    Vector3 dirToModule = (col.transform.position - center).normalized;
                    Vector3 normal = (CurrentWarhead == WarheadType.Thermobaric) ? -dirToModule : -dirToModule;

                    module.TakeDamage(finalDamage, col.transform.position, normal, dirToModule);
                }
            }
        }
    }

    public void Explode(Vector3 contactPoint, Collider directHitCollider = null)
    {
        if (isExploded) return;
        isExploded = true;

        // 1. Отключение компонентов дрона
        foreach (var col in GetComponentsInChildren<Collider>()) col.enabled = false;
        foreach (var rend in GetComponentsInChildren<Renderer>()) rend.enabled = false;
        if (flightController != null) flightController.enabled = false;
        if (motors != null) motors.enabled = false;

        // 2. Расчет урона по области с учетом выбранной БЧ
        ApplySplashDamage(contactPoint, directHitCollider);

        // 3. Объективный контроль
        if (reconDronePrefab != null)
        {
            Vector3 spawnPos = contactPoint + reconOffset;
            Quaternion spawnRot = Quaternion.LookRotation(contactPoint - spawnPos);
            GameObject recon = Instantiate(reconDronePrefab, spawnPos, spawnRot);
            var cameraModule = recon.GetComponentInChildren<DroneCameraModule>();
            if (cameraModule != null) cameraModule.transform.LookAt(contactPoint);
        }

        // 4. Эффекты детонации
        if (ExplosionPrefab != null) Instantiate(ExplosionPrefab, contactPoint, Quaternion.identity);
        if (ExplosionSound != null) AudioSource.PlayClipAtPoint(ExplosionSound, contactPoint, 1.0f);

        Destroy(gameObject, 0.1f);
    }
}