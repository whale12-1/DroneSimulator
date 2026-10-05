using System.Linq;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class DroneCollision : MonoBehaviour
{
    [Header("Профиль Дрона")]
    [SerializeField] private DroneConfig config;

    private bool isExploded;
    private DroneFlightController flightController;
    private DroneMotors motors;

    public WarheadType CurrentWarhead => config != null ? config.warheadType : WarheadType.HEAT;
    public float DirectDamage => config != null ? config.directDamage : 250f;
    public float SplashRadius => config != null ? config.splashRadius : 1.5f;
    public float SplashDamage => config != null ? config.splashDamage : 40f;

    private void Awake()
    {
        flightController = GetComponent<DroneFlightController>();
        motors = GetComponent<DroneMotors>();
    }

    private void Update()
    {
        if (isExploded) return;

        bool allowDetonate = config == null || config.allowManualDetonation;
        KeyCode detonateKey = config != null ? config.detonateKey : KeyCode.Space;

        if (allowDetonate && Input.GetKeyDown(detonateKey))
        {
            Explode(transform.position);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (isExploded) return;

        float impactVelocity = collision.relativeVelocity.magnitude;
        bool explodeAny = config != null && config.explodeOnAnyCollision;
        float minVelocity = config != null ? config.minImpactVelocity : 3.0f;
        string[] hazardTags = (config != null && config.instantExplodeTags != null)
            ? config.instantExplodeTags
            : new[] { "Hazard" };

        bool hitHazardTag = hazardTags.Contains(collision.gameObject.tag);

        if (!explodeAny && !hitHazardTag && impactVelocity < minVelocity) return;

        ResolveContact(collision, out Vector3 contactPoint, out Vector3 hitNormal);
        ApplyDirectDamage(collision.collider, contactPoint, hitNormal, transform.forward);
        Explode(contactPoint, collision.collider);
    }

    private void ResolveContact(Collision collision, out Vector3 point, out Vector3 normal)
    {
        if (collision.contacts.Length > 0)
        {
            point = collision.contacts[0].point;
            normal = collision.contacts[0].normal;
            return;
        }

        point = transform.position;
        normal = -transform.forward;

        Ray ray = new Ray(transform.position - transform.forward, transform.forward);
        if (collision.collider.Raycast(ray, out RaycastHit hit, 5.0f))
        {
            point = hit.point;
            normal = hit.normal;
        }
    }

    private void ApplyDirectDamage(Collider hitCollider, Vector3 point, Vector3 normal, Vector3 flightDir)
    {
        // Термобарический заряд игнорирует острые углы брони
        Vector3 effectiveNormal = CurrentWarhead == WarheadType.Thermobaric ? -flightDir : normal;

        IDamageable target = hitCollider.GetComponent<IDamageable>()
                          ?? hitCollider.GetComponentInParent<IDamageable>();
        if (target == null) return;

        target.ApplyDamage(new DamageData(
            DirectDamage, point, effectiveNormal, flightDir, CurrentWarhead, ModuleType.Armor, gameObject));
    }

    public void Explode(Vector3 contactPoint, Collider directHitCollider = null)
    {
        if (isExploded) return;
        isExploded = true;

        DisableDrone();

        ExplosionService.ApplySplash(contactPoint, SplashRadius, SplashDamage,
                                     CurrentWarhead, directHitCollider, gameObject);

        SpawnReconDrone(contactPoint);

        EffectService.Spawn(config?.explosionPrefab, contactPoint);
        EffectService.PlaySound(config?.explosionSound, contactPoint);

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

    private void SpawnReconDrone(Vector3 contactPoint)
    {
        GameObject reconPrefab = config != null ? config.reconDronePrefab : null;
        if (reconPrefab == null) return;

        Vector3 offset = config != null ? config.reconOffset : new Vector3(0f, 35f, -20f);
        Vector3 spawnPos = contactPoint + offset;
        Quaternion spawnRot = Quaternion.LookRotation(contactPoint - spawnPos);
        GameObject recon = Instantiate(reconPrefab, spawnPos, spawnRot);

        var cameraModule = recon.GetComponentInChildren<DroneCameraModule>();
        if (cameraModule != null) cameraModule.transform.LookAt(contactPoint);
    }
}