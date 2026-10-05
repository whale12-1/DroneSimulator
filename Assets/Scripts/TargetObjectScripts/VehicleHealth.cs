using UnityEngine;

public class VehicleHealth : MonoBehaviour, IDamageable
{
    [Header("Прочность и Визуал")]
    public float totalHealth = 300f;
    public GameObject explosionVFX;
    public Transform turretTransform;
    [SerializeField] private Material overallBurntMaterial;

    [Header("Физика и Движение")]
    [SerializeField] private WheelCollider[] wheelColliders;
    [SerializeField] private MonoBehaviour vehicleDriveScript;
    [SerializeField] private MonoBehaviour vehicleAIScript;

    private bool isDestroyed;
    private Vector3 lastHitPoint = Vector3.zero;

    // Единый контракт урона — приходит от ExplosionService или любого IDamageable-канала.
    public void ApplyDamage(in DamageData data)
    {
        OnModuleHit(data.Module, data.Amount, isModuleDestroyed: false, data.HitPoint);
    }

    public void OnModuleHit(ModuleType module, float damage, bool isModuleDestroyed, Vector3 hitPoint = default)
    {
        if (isDestroyed) return;

        totalHealth -= damage;
        if (hitPoint != Vector3.zero) lastHitPoint = hitPoint;

        if (module == ModuleType.AmmoRack && isModuleDestroyed)
        {
            DestroyVehicle(catastrophicAmmoExplosion: true);
            return;
        }

        if (totalHealth <= 0)
        {
            DestroyVehicle(catastrophicAmmoExplosion: false);
        }
    }

    public void OnModuleFunctionalityLost(ModuleType module)
    {
        switch (module)
        {
            case ModuleType.Engine:
            case ModuleType.Tracks:
                DisableVehicleMovement();
                Debug.Log("<color=yellow>ТЕХНИКА ПОТЕРЯЛА ХОД!</color>");
                break;
            case ModuleType.AmmoRack:
                Debug.Log("<color=red>ДЕТОНАЦИЯ БОЕКОМПЛЕКТА!</color>");
                break;
        }
    }

    private void DisableVehicleMovement()
    {
        if (vehicleDriveScript != null) vehicleDriveScript.enabled = false;
        if (wheelColliders == null) return;

        foreach (var wheel in wheelColliders)
        {
            if (wheel == null) continue;
            wheel.motorTorque = 0f;
            wheel.brakeTorque = 10000f;
        }
    }

    private void DestroyVehicle(bool catastrophicAmmoExplosion)
    {
        if (isDestroyed) return;
        isDestroyed = true;

        if (vehicleAIScript != null) vehicleAIScript.enabled = false;
        DisableVehicleMovement();

        if (explosionVFX != null)
        {
            Vector3 spawnPos = lastHitPoint != Vector3.zero ? lastHitPoint : transform.position + Vector3.up * 1.2f;
            Instantiate(explosionVFX, spawnPos, Quaternion.identity);
        }

        if (overallBurntMaterial != null)
        {
            foreach (Renderer rend in GetComponentsInChildren<Renderer>())
            {
                if (rend.GetComponent<ParticleSystem>() != null) continue;

                var burnt = new Material[rend.sharedMaterials.Length];
                for (int i = 0; i < burnt.Length; i++) burnt[i] = overallBurntMaterial;
                rend.materials = burnt;
            }
        }

        if (catastrophicAmmoExplosion && turretTransform != null)
        {
            turretTransform.SetParent(null);

            Rigidbody turretRb = turretTransform.GetComponent<Rigidbody>();
            if (turretRb == null) turretRb = turretTransform.gameObject.AddComponent<Rigidbody>();

            turretRb.mass = 1200f;
            turretRb.AddForce(Vector3.up * 14000f + Random.insideUnitSphere * 4000f, ForceMode.Impulse);
            turretRb.AddTorque(Random.insideUnitSphere * 6000f, ForceMode.Impulse);
        }

        GameEvents.RaiseVehicleDestroyed(this);
        Debug.Log("<color=red>ТЕХНИКА ПОЛНОСТЬЮ УНИЧТОЖЕНА!</color>");
    }
}