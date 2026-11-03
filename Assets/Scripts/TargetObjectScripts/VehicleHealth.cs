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

    public float currentHealth => totalHealth;

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
        // 1. Отключаем скрипты, переданные вручную в инспекторе
        if (vehicleDriveScript != null) vehicleDriveScript.enabled = false;
        if (vehicleAIScript != null) vehicleAIScript.enabled = false;

        // 2. ПРИНУДИТЕЛЬНО отключаем наши скрипты движения и ИИ (самый надежный способ)
        var patrol = GetComponent<VehiclePatrol>();
        if (patrol != null) patrol.enabled = false;

        var ai = GetComponent<VehicleSmartAI>();
        if (ai != null) ai.enabled = false;

        // 3. Обнуляем скорость Rigidbody, чтобы машина не катилась по инерции
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            // СНАЧАЛА делаем кинематическим, чтобы физика перестала работать
            rb.isKinematic = true;

            // Теперь обнуляем скорости (это безопасно, так как машина уже заморожена)
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        // 4. Останавливаем колеса (если они есть)
        if (wheelColliders != null)
        {
            foreach (var wheel in wheelColliders)
            {
                if (wheel != null)
                {
                    wheel.motorTorque = 0f;
                    wheel.brakeTorque = 10000f;
                }
            }
        }
    }

    private void DestroyVehicle(bool catastrophicAmmoExplosion)
    {
        if (isDestroyed) return;
        isDestroyed = true;

        // Сначала полностью останавливаем машину
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