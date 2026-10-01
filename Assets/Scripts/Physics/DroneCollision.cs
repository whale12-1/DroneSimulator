using System.Linq;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class DroneCollision : MonoBehaviour
{
    [Header("Профиль Дрона")]
    [SerializeField] private DroneConfig config;

    private bool isExploded = false;
    private DroneFlightController flightController;
    private DroneMotors motors;

    // Геттеры считывают значения из конфига (с безопасными значением по умолчанию)
    public WarheadType CurrentWarhead => config != null ? config.warheadType : WarheadType.HEAT;
    public float DirectDamage => config != null ? config.directDamage : 250f;
    public float SplashRadius => config != null ? config.splashRadius : 1.5f;
    public float SplashDamage => config != null ? config.splashDamage : 40f;
    public GameObject ExplosionPrefab => config != null ? config.explosionPrefab : null;
    public AudioClip ExplosionSound => config != null ? config.explosionSound : null;

    private void Awake()
    {
        flightController = GetComponent<DroneFlightController>();
        motors = GetComponent<DroneMotors>();
    }

    private void Update()
    {
        if (isExploded) return;

        bool allowDetonate = (config != null) ? config.allowManualDetonation : true;
        KeyCode detonateKey = (config != null) ? config.detonateKey : KeyCode.Space;

        if (allowDetonate && Input.GetKeyDown(detonateKey))
        {
            Explode(transform.position, directHitCollider: null);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (isExploded) return;

        float impactVelocity = collision.relativeVelocity.magnitude;

        bool explodeAny = (config != null) ? config.explodeOnAnyCollision : false;
        float minVelocity = (config != null) ? config.minImpactVelocity : 3.0f;
        string[] hazardTags = (config != null && config.instantExplodeTags != null)
            ? config.instantExplodeTags
            : new string[] { "Hazard" };

        bool hitHazardTag = hazardTags.Contains(collision.gameObject.tag);

        if (explodeAny || hitHazardTag || impactVelocity >= minVelocity)
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
                    module.TakeDamage(finalDamage, col.transform.position, -dirToModule, dirToModule);
                }
            }
        }
    }

    public void Explode(Vector3 contactPoint, Collider directHitCollider = null)
    {
        if (isExploded) return;
        isExploded = true;

        // 1. Отключение компонентов и коллайдеров дрона
        foreach (var col in GetComponentsInChildren<Collider>()) col.enabled = false;
        foreach (var rend in GetComponentsInChildren<Renderer>()) rend.enabled = false;
        if (flightController != null) flightController.enabled = false;
        if (motors != null) motors.enabled = false;

        // 2. Расчет урона по области
        ApplySplashDamage(contactPoint, directHitCollider);

        // 3. Объективный контроль (Спавн наблюдателя)
        GameObject reconPrefab = (config != null) ? config.reconDronePrefab : null;
        Vector3 reconOffset = (config != null) ? config.reconOffset : new Vector3(0f, 35f, -20f);

        if (reconPrefab != null)
        {
            Vector3 spawnPos = contactPoint + reconOffset;
            Quaternion spawnRot = Quaternion.LookRotation(contactPoint - spawnPos);
            GameObject recon = Instantiate(reconPrefab, spawnPos, spawnRot);

            var cameraModule = recon.GetComponentInChildren<DroneCameraModule>();
            if (cameraModule != null) cameraModule.transform.LookAt(contactPoint);
        }

        // 4. Эффекты детонации
        if (ExplosionPrefab != null) Instantiate(ExplosionPrefab, contactPoint, Quaternion.identity);
        if (ExplosionSound != null) AudioSource.PlayClipAtPoint(ExplosionSound, contactPoint, 1.0f);

        Destroy(gameObject, 0.1f);
    }
}