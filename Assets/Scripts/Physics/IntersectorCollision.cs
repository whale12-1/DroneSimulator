using System.Linq;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class InterceptorCollision : MonoBehaviour
{
    [Header("Конфигурация Дрона")]
    [SerializeField] private DroneConfig droneConfig;

    [Header("Резервные Настройки")]
    public WarheadType fallbackWarheadType = WarheadType.HEAT;
    public float fallbackDirectDamage = 150f;
    public float fallbackSplashRadius = 8f;
    public float fallbackSplashDamage = 100f;

    [Header("Радиовзрыватель")]
    [SerializeField] private bool enableProximityFuse = true;
    [SerializeField] private LayerMask targetLayer;
    [SerializeField] private float triggerDistance = 5.0f;

    [Header("Условия Взрыва по Столкновению")]
    public bool explodeOnAnyCollision = true;
    [SerializeField] private float minImpactVelocity = 2.0f;

    private bool isExploded = false;
    private DroneFlightController flightController;
    private DroneMotors motors;

    public DroneConfig Config => droneConfig;
    public float SplashRadius => droneConfig != null ? droneConfig.splashRadius : fallbackSplashRadius;
    public float SplashDamage => droneConfig != null ? droneConfig.splashDamage : fallbackSplashDamage;

    private void Awake()
    {
        flightController = GetComponent<DroneFlightController>();
        motors = GetComponent<DroneMotors>();
    }

    private void Update()
    {
        if (isExploded) return;

        if (enableProximityFuse)
        {
            CheckProximityFuse();
        }
    }

    private void CheckProximityFuse()
    {
        Collider[] targets = Physics.OverlapSphere(transform.position, triggerDistance, targetLayer);
        foreach (var col in targets)
        {
            if (col.transform.root == transform.root) continue;
            Explode(transform.position);
            break;
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (isExploded) return;

        float impactVelocity = collision.relativeVelocity.magnitude;
        if (explodeOnAnyCollision || impactVelocity >= minImpactVelocity)
        {
            Vector3 contactPoint = collision.contacts.Length > 0 ? collision.contacts[0].point : transform.position;
            Explode(contactPoint, collision.collider);
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
            if (col == ignoredCollider || col.transform.root == transform.root) continue;

            VehicleModule module = col.GetComponent<VehicleModule>();
            if (module != null)
            {
                float distance = Vector3.Distance(center, col.transform.position);
                float finalDamage = baseSplashDamage * Mathf.Clamp01(1.0f - (distance / radius));

                if (finalDamage > 1f)
                {
                    Vector3 dir = (col.transform.position - center).normalized;
                    module.TakeDamage(finalDamage, col.transform.position, -dir, dir);
                }
                continue;
            }

            VehicleHealth vehicle = col.GetComponentInParent<VehicleHealth>();
            if (vehicle != null)
            {
                float distance = Vector3.Distance(center, col.transform.position);
                vehicle.OnModuleHit(ModuleType.Armor, baseSplashDamage * Mathf.Clamp01(1.0f - (distance / radius)), false, col.transform.position);
            }
        }
    }

    public void Explode(Vector3 contactPoint, Collider directHitCollider = null)
    {
        if (isExploded) return;
        isExploded = true;

        // 1. Отключение узлов дрона
        foreach (var col in GetComponentsInChildren<Collider>()) col.enabled = false;
        foreach (var rend in GetComponentsInChildren<Renderer>()) rend.enabled = false;
        if (flightController != null) flightController.enabled = false;
        if (motors != null) motors.enabled = false;

        // 2. Расчет урона
        ApplySplashDamage(contactPoint, directHitCollider);

        // 3. ОТПРАВКА СОБЫТИЯ (Вся внешняя логика реагирует сама)
        GameEvents.OnDroneExploded?.Invoke(contactPoint, this);

        Destroy(gameObject, 0.1f);
    }
}