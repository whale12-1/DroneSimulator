using System.Linq;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class DroneCollision : MonoBehaviour
{
    [Header("Боевая Часть (Урон)")]
    [Tooltip("Прямой кумулятивный урон (ПГ-7В) при контактном попадании")]
    [SerializeField] private float directDamage = 250f;

    [Tooltip("Радиус фугасного/осколочного поражения при взрыве")]
    [SerializeField] private float splashRadius = 3.5f;

    [Tooltip("Фуговый урон по окружающим модулям")]
    [SerializeField] private float splashDamage = 60f;

    [Header("Эффекты Взрыва")]
    [SerializeField] private GameObject explosionPrefab;
    [SerializeField] private AudioClip explosionSound;

    [Header("Объективный Контроль (Дрон-Разведчик)")]
    [Tooltip("Префаб готового разведчика (с настроенным DroneCameraModule)")]
    [SerializeField] private GameObject reconDronePrefab;

    [Tooltip("Высота и смещение спавна разведчика относительно точки взрыва")]
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
            ContactPoint contact = collision.contacts.Length > 0 ? collision.contacts[0] : default;
            Vector3 contactPoint = collision.contacts.Length > 0 ? contact.point : transform.position;
            Vector3 hitNormal = collision.contacts.Length > 0 ? contact.normal : -transform.forward;
            Vector3 flightDir = transform.forward; // Направление вектора кумулятивной струи

            // 1. Расчет прямого урона по конкретному хитбоксу
            ApplyDirectDamage(collision.collider, contactPoint, hitNormal, flightDir);

            // 2. Запуск подрыва и передача коллайдера прямого попадания (чтобы не задевать его сплэшем повторно)
            Explode(contactPoint, collision.collider);
        }
    }

    private void ApplyDirectDamage(Collider hitCollider, Vector3 point, Vector3 normal, Vector3 flightDir)
    {
        // Ищем точечный модуль (кабина, мотор, кузов)
        VehicleModule module = hitCollider.GetComponent<VehicleModule>();

        if (module != null)
        {
            module.TakeDamage(directDamage, point, normal, flightDir);
        }
        else
        {
            // Если попали в общую модель без VehicleModule, передаем урон напрямую в VehicleHealth
            VehicleHealth vehicle = hitCollider.GetComponentInParent<VehicleHealth>();
            if (vehicle != null)
            {
                vehicle.OnModuleHit(ModuleType.Armor, directDamage, isModuleDestroyed: false);
            }
        }
    }

    private void ApplySplashDamage(Vector3 center, Collider ignoredCollider)
    {
        if (splashRadius <= 0f) return;

        Collider[] nearbyColliders = Physics.OverlapSphere(center, splashRadius);
        foreach (var col in nearbyColliders)
        {
            // Пропускаем объект прямого попадания
            if (col == ignoredCollider) continue;

            VehicleModule module = col.GetComponent<VehicleModule>();
            if (module != null)
            {
                Vector3 dirToModule = (col.transform.position - center).normalized;
                module.TakeDamage(splashDamage, col.transform.position, -dirToModule, dirToModule);
            }
        }
    }

    public void Explode(Vector3 contactPoint, Collider directHitCollider = null)
    {
        if (isExploded) return;
        isExploded = true;

        // 1. Мгновенно отключаем коллайдеры и скрипты дрона
        foreach (var col in GetComponentsInChildren<Collider>()) col.enabled = false;
        foreach (var rend in GetComponentsInChildren<Renderer>()) rend.enabled = false;
        if (flightController != null) flightController.enabled = false;
        if (motors != null) motors.enabled = false;

        // 2. Наносим урон
        ApplySplashDamage(contactPoint, directHitCollider);

        // 3. Спавним разведчик и VFX
        if (reconDronePrefab != null)
        {
            Vector3 spawnPos = contactPoint + reconOffset;
            Quaternion spawnRot = Quaternion.LookRotation(contactPoint - spawnPos);
            GameObject recon = Instantiate(reconDronePrefab, spawnPos, spawnRot);
            var cameraModule = recon.GetComponentInChildren<DroneCameraModule>();
            if (cameraModule != null) cameraModule.transform.LookAt(contactPoint);
        }

        if (explosionPrefab != null) Instantiate(explosionPrefab, contactPoint, Quaternion.identity);
        if (explosionSound != null) AudioSource.PlayClipAtPoint(explosionSound, contactPoint, 1.0f);

        Destroy(gameObject, 0.1f);
    }
}