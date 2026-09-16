using UnityEngine;

public class VehicleHealth : MonoBehaviour
{
    [Header("Прочность и Визуал")]
    public float totalHealth = 300f;
    public GameObject explosionVFX;
    public Transform turretTransform;
    [SerializeField] private Material overallBurntMaterial;

    [Header("Физика и Движение")]
    [SerializeField] private WheelCollider[] wheelColliders;
    [SerializeField] private MonoBehaviour vehicleDriveScript;

    private bool isDestroyed = false;
    private Vector3 lastHitPoint = Vector3.zero;

    // Добавили параметр hitPoint для сохранения точки последнего удара
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

        DisableVehicleMovement();

        // 1. Спавн крупного взрыва в ТОЧКЕ ПОПАДАНИЯ, а не в центре земли (0,0,0)
        if (explosionVFX != null)
        {
            Vector3 spawnPos = (lastHitPoint != Vector3.zero) ? lastHitPoint : (transform.position + Vector3.up * 1.2f);
            Instantiate(explosionVFX, spawnPos, Quaternion.identity);
        }

        // 2. Корректная замена ВСЕХ слотов материалов (Element 0, Element 1 и т.д.)
        if (overallBurntMaterial != null)
        {
            Renderer[] allRenderers = GetComponentsInChildren<Renderer>();
            foreach (Renderer rend in allRenderers)
            {
                if (rend.GetComponent<ParticleSystem>() == null)
                {
                    Material[] burntMaterials = new Material[rend.sharedMaterials.Length];
                    for (int i = 0; i < burntMaterials.Length; i++)
                    {
                        burntMaterials[i] = overallBurntMaterial;
                    }
                    rend.materials = burntMaterials; // Заменяем весь массив материалов
                }
            }
        }

        // 3. Отрыв башни/кузова
        if (catastrophicAmmoExplosion && turretTransform != null)
        {
            turretTransform.SetParent(null);
            Rigidbody turretRb = turretTransform.gameObject.GetComponent<Rigidbody>();
            if (turretRb == null) turretRb = turretTransform.gameObject.AddComponent<Rigidbody>();

            turretRb.mass = 1200f;
            turretRb.AddForce(Vector3.up * 14000f + Random.insideUnitSphere * 4000f, ForceMode.Impulse);
            turretRb.AddTorque(Random.insideUnitSphere * 6000f, ForceMode.Impulse);
        }

        Debug.Log("<color=red>ТЕХНИКА ПОЛНОСТЬЮ УНИЧТОЖЕНА!</color>");
    }
}