using UnityEngine;

public enum ModuleType { Engine, Tracks, AmmoRack, Turret, Optics, Armor, MainHull }

public class VehicleModule : MonoBehaviour, IDamageable
{
    [Header("Основные Настройки")]
    public ModuleType moduleType;
    public float maxHealth = 100f;
    public float currentHealth;
    [Tooltip("Толщина брони в мм для расчета пробития")]
    public float armorThickness = 30f;

    [Header("Визуализация Повреждений")]
    [SerializeField] private GameObject destroyedVFX;
    [SerializeField] private GameObject smokeVFX;
    [SerializeField] private GameObject detachableMesh;
    [SerializeField] private Material burntMaterial;

    private VehicleHealth mainVehicle;
    private bool isModuleDestroyed;

    private void Awake()
    {
        currentHealth = maxHealth;
        mainVehicle = GetComponentInParent<VehicleHealth>();
    }

    // Единый контракт урона
    public void ApplyDamage(in DamageData data)
    {
        TakeDamage(data.Amount, data.HitPoint, data.HitNormal, data.FlightDirection);
    }

    // Сохранён для совместимости со старыми вызовами
    public void TakeDamage(float rawDamage, Vector3 hitPoint, Vector3 hitNormal, Vector3 flightDirection)
    {
        if (isModuleDestroyed) return;

        float angleFactor = Mathf.Abs(Vector3.Dot(flightDirection, hitNormal));
        if (angleFactor < 0.25f)
        {
            Debug.Log("Рикошет кумулятивной струи!");
            return;
        }

        float effectiveArmor = armorThickness / Mathf.Max(0.2f, angleFactor);
        float damageAfterArmor = Mathf.Max(10f, rawDamage - effectiveArmor);

        currentHealth -= damageAfterArmor;
        if (currentHealth <= 0)
        {
            currentHealth = 0;
            isModuleDestroyed = true;
            OnModuleDestroyed(hitPoint);
        }

        mainVehicle?.OnModuleHit(moduleType, damageAfterArmor, isModuleDestroyed, hitPoint);
    }

    private void OnModuleDestroyed(Vector3 hitPoint)
    {
        Vector3 spawnPos = hitPoint != Vector3.zero ? hitPoint : transform.position;

        if (destroyedVFX != null)
        {
            if (destroyedVFX.scene.rootCount == 0)
                Instantiate(destroyedVFX, spawnPos, Quaternion.identity);
            else
            {
                destroyedVFX.transform.position = spawnPos;
                destroyedVFX.SetActive(true);
            }
        }

        if (smokeVFX != null) Instantiate(smokeVFX, spawnPos, Quaternion.identity);

        if (burntMaterial != null)
        {
            Renderer rend = GetComponent<Renderer>();
            if (rend != null)
            {
                var burntArray = new Material[rend.sharedMaterials.Length];
                for (int i = 0; i < burntArray.Length; i++) burntArray[i] = burntMaterial;
                rend.materials = burntArray;
            }
        }

        if (detachableMesh != null)
        {
            detachableMesh.transform.SetParent(null);

            // ВАЖНО: НЕ использовать ?? для UnityEngine.Object — fake null.
            Rigidbody partRb = detachableMesh.GetComponent<Rigidbody>();
            if (partRb == null) partRb = detachableMesh.AddComponent<Rigidbody>();

            partRb.mass = 15f;
            partRb.AddForce(Vector3.up * 5f + Random.insideUnitSphere * 3f, ForceMode.Impulse);
            partRb.AddTorque(Random.insideUnitSphere * 15f, ForceMode.Impulse);
            Destroy(detachableMesh, 15f);
        }

        GameEvents.RaiseModuleDestroyed(this);
        mainVehicle?.OnModuleFunctionalityLost(moduleType);
    }
}