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

    private bool isExploded;
    private DroneFlightController flightController;
    private DroneMotors motors;

    public DroneConfig Config => droneConfig;
    public WarheadType CurrentWarhead => droneConfig != null ? droneConfig.warheadType : fallbackWarheadType;
    public float SplashRadius => droneConfig != null ? droneConfig.splashRadius : fallbackSplashRadius;
    public float SplashDamage => droneConfig != null ? droneConfig.splashDamage : fallbackSplashDamage;

    private void Awake()
    {
        flightController = GetComponent<DroneFlightController>();
        motors = GetComponent<DroneMotors>();
    }

    private void Update()
    {
        if (isExploded || !enableProximityFuse) return;
        CheckProximityFuse();
    }

    private void CheckProximityFuse()
    {
        Collider[] targets = Physics.OverlapSphere(transform.position, triggerDistance, targetLayer);
        foreach (var col in targets)
        {
            if (col.transform.root == transform.root) continue;
            Explode(transform.position);
            return;
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (isExploded) return;

        float impactVelocity = collision.relativeVelocity.magnitude;
        if (!explodeOnAnyCollision && impactVelocity < minImpactVelocity) return;

        Vector3 contactPoint = collision.contacts.Length > 0 ? collision.contacts[0].point : transform.position;
        Explode(contactPoint, collision.collider);
    }

    public void Explode(Vector3 contactPoint, Collider directHitCollider = null)
    {
        if (isExploded) return;
        isExploded = true;

        DisableDrone();

        ExplosionService.ApplySplash(contactPoint, SplashRadius, SplashDamage,
                                     CurrentWarhead, directHitCollider, gameObject);

        // Вся внешняя логика (VFX, звук, счёт, рекон) реагирует через шину.
        GameEvents.RaiseExplosion(new ExplosionEventData(contactPoint, SplashRadius, SplashDamage, CurrentWarhead, gameObject));
        GameEvents.RaiseDroneExploded(contactPoint, this);

        Destroy(gameObject, 0.1f);
    }

    private void DisableDrone()
    {
        foreach (var col in GetComponentsInChildren<Collider>()) col.enabled = false;
        foreach (var rend in GetComponentsInChildren<Renderer>()) rend.enabled = false;
        if (flightController != null) flightController.enabled = false;
        if (motors != null) motors.enabled = false;
    }
}