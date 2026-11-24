using UnityEngine;

public class VehicleHealth : MonoBehaviour, IDamageable
{
    [Header("Прочность и Визуал")]
    public float totalHealth = 300f;
    public GameObject explosionVFX;
    [Tooltip("Через сколько секунд удалять клон explosionVFX. 0 = авто: для циклических эффектов время считается по ParticleSystem, одноразовые удаляют себя сами.")]
    [SerializeField] private float explosionVfxLifetime = 0f;
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

        // 3. Обнуляем скорость Rigidbody, чтобы машина не катилась по инерции.
        // ВАЖНО: скорости обнуляем ДО перевода в kinematic — у кинематического тела
        // Unity игнорирует запись linearVelocity/angularVelocity и пишет warning
        // "Setting linear velocity of a kinematic body is not supported".
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
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
            GameObject fx = Instantiate(explosionVFX, spawnPos, Quaternion.identity);

            // WildFire и подобные префабы — циклические (loop = true, StopAction = None):
            // без ограничения времени они горят до конца сцены и «переезжают» в следующие главы.
            float lifetime = explosionVfxLifetime;
            if (lifetime <= 0f) lifetime = ResolveLoopingVfxLifetime(explosionVFX);
            if (lifetime > 0f) Destroy(fx, lifetime);
        }

        EWEmitter emitter = GetComponent<EWEmitter>();
        if (emitter == null) emitter = GetComponentInChildren<EWEmitter>();

        if (emitter != null)
        {
            emitter.isActive = false;  // Отключаем логику излучения
            emitter.enabled = false;   // Полностью отключаем компонент (вызовет OnDisable)
            Debug.Log("<color=cyan>[РЭБ] Передатчик помех уничтожен вместе с техникой!</color>");
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

    // Возвращает безопасное время жизни для эффекта, внутри которого есть
    // циклические ParticleSystem (0 — эффект одноразовый и удаляет себя сам).
    private static float ResolveLoopingVfxLifetime(GameObject prefab)
    {
        if (prefab == null) return 0f;

        bool hasLooping = false;
        float longest = 0f;

        foreach (ParticleSystem particles in prefab.GetComponentsInChildren<ParticleSystem>(true))
        {
            if (particles == null) continue;

            ParticleSystem.MainModule main = particles.main;
            if (!main.loop) continue;

            hasLooping = true;
            longest = Mathf.Max(longest, main.duration + main.startLifetime.constantMax);
        }

        if (!hasLooping) return 0f;
        return Mathf.Clamp(longest, 5f, 60f);
    }
}