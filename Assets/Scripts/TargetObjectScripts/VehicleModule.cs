using UnityEngine;

public enum ModuleType { Engine, Tracks, AmmoRack, Turret, Optics, Armor, MainHull }

public class VehicleModule : MonoBehaviour
{
    [Header("Основные Настройки")]
    public ModuleType moduleType;
    public float maxHealth = 100f;
    public float currentHealth;
    [Tooltip("Толщина брони в мм для расчета пробития")]
    public float armorThickness = 30f;

    [Header("Визуализация Повреждений")]
    [Tooltip("Эффект огня, который включается при уничтожении модуля")]
    [SerializeField] private GameObject destroyedVFX;

    [Tooltip("Эффект дыма, спавнящийся вместе с огнем")]
    [SerializeField] private GameObject smokeVFX;

    [Tooltip("Отделяемая 3D-деталь (дверь, капот, колесо)")]
    [SerializeField] private GameObject detachableMesh;

    [Tooltip("Материал сгоревшей/поврежденной детали")]
    [SerializeField] private Material burntMaterial;

    private VehicleHealth mainVehicle;
    private bool isModuleDestroyed = false;

    private void Awake()
    {
        currentHealth = maxHealth;
        mainVehicle = GetComponentInParent<VehicleHealth>();
    }

    public void TakeDamage(float rawDamage, Vector3 hitPoint, Vector3 hitNormal, Vector3 flightDirection)
    {
        if (isModuleDestroyed) return;

        float angleFactor = Mathf.Abs(Vector3.Dot(flightDirection, hitNormal));
        float effectiveArmor = armorThickness / Mathf.Max(0.2f, angleFactor);

        if (angleFactor < 0.25f)
        {
            Debug.Log("Рикошет кумулятивной струи!");
            return;
        }

        float damageAfterArmor = Mathf.Max(10f, rawDamage - effectiveArmor);
        currentHealth -= damageAfterArmor;

        if (currentHealth <= 0)
        {
            currentHealth = 0;
            isModuleDestroyed = true;
            OnModuleDestroyed(hitPoint);
        }

        // Передаем hitPoint в главный скрипт
        if (mainVehicle != null)
        {
            mainVehicle.OnModuleHit(moduleType, damageAfterArmor, isModuleDestroyed, hitPoint);
        }
    }

    private void OnModuleDestroyed(Vector3 hitPoint)
    {
        Vector3 spawnPos = (hitPoint != Vector3.zero) ? hitPoint : transform.position;

        // 1. Спавн VFX без привязки к родителю (чтобы scale 10x не искажал огонь)
        if (destroyedVFX != null)
        {
            if (destroyedVFX.scene.rootCount == 0)
            {
                Instantiate(destroyedVFX, spawnPos, Quaternion.identity);
            }
            else
            {
                destroyedVFX.transform.position = spawnPos;
                destroyedVFX.SetActive(true);
            }
        }

        if (smokeVFX != null)
        {
            Instantiate(smokeVFX, spawnPos, Quaternion.identity);
        }

        // 2. Смена всех слотов материала у отдельного модуля
        if (burntMaterial != null)
        {
            Renderer rend = GetComponent<Renderer>();
            if (rend != null)
            {
                Material[] burntArray = new Material[rend.sharedMaterials.Length];
                for (int i = 0; i < burntArray.Length; i++)
                {
                    burntArray[i] = burntMaterial;
                }
                rend.materials = burntArray;
            }
        }

        // 3. Физический отрыв детали
        if (detachableMesh != null)
        {
            detachableMesh.transform.SetParent(null);

            Rigidbody partRb = detachableMesh.GetComponent<Rigidbody>();
            if (partRb == null) partRb = detachableMesh.AddComponent<Rigidbody>();

            partRb.mass = 15f;
            partRb.AddForce(Vector3.up * 5f + Random.insideUnitSphere * 3f, ForceMode.Impulse);
            partRb.AddTorque(Random.insideUnitSphere * 15f, ForceMode.Impulse);

            Destroy(detachableMesh, 15f);
        }

        // 4. Уведомление системы здоровья
        if (mainVehicle != null)
        {
            mainVehicle.OnModuleFunctionalityLost(moduleType);
        }
    }
}