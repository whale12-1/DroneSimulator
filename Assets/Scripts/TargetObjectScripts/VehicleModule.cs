using UnityEngine;

public enum ModuleType { Engine, Tracks, AmmoRack, Turret, Optics, Armor }

public class VehicleModule : MonoBehaviour
{
    [Header("Основные Настройки")]
    public ModuleType moduleType;
    public float maxHealth = 100f;
    public float currentHealth;
    [Tooltip("Толщина брони в мм для расчета пробития")]
    public float armorThickness = 30f;

    [Header("Визуализация Повреждений")]
    [Tooltip("Эффект огня/дыма, который включается при выходе модуля из строя")]
    [SerializeField] private GameObject destroyedVFX;

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

        // 1. Расчет угла встречи кумулятивной струи с броней
        float angleFactor = Mathf.Abs(Vector3.Dot(flightDirection, hitNormal)); // 1.0 = под 90 градусов
        float effectiveArmor = armorThickness / Mathf.Max(0.2f, angleFactor);

        // 2. Проверка на рикошет при остром угле
        if (angleFactor < 0.25f)
        {
            Debug.Log("Рикошет кумулятивной струи!");
            return;
        }

        // 3. Нанесение урона с учетом эквивалента брони
        float damageAfterArmor = Mathf.Max(10f, rawDamage - effectiveArmor);
        currentHealth -= damageAfterArmor;

        if (currentHealth <= 0)
        {
            currentHealth = 0;
            isModuleDestroyed = true;
            OnModuleDestroyed(hitPoint); // Передаем точку попадания
        }

        if (mainVehicle != null)
        {
            mainVehicle.OnModuleHit(moduleType, damageAfterArmor, isModuleDestroyed);
        }
    }

    private void OnModuleDestroyed(Vector3 hitPoint)
    {
        // 1. Включаем привязанный эффект дыма/огня
        if (destroyedVFX != null)
        {
            if (destroyedVFX.scene.rootCount == 0)
            {
                // Если в поле закинут префаб из папки — спавним его в точке попадания
                Instantiate(destroyedVFX, hitPoint, Quaternion.identity, transform);
            }
            else
            {
                // Если в поле закинут объект со сцены — просто включаем его
                destroyedVFX.SetActive(true);
            }
        }

        // 2. Меняем текстуру детали на горелую
        if (burntMaterial != null)
        {
            Renderer rend = GetComponent<Renderer>();
            if (rend != null) rend.material = burntMaterial;
        }

        // 3. Отрываем деталь от иерархии машины с импульсом
        if (detachableMesh != null)
        {
            detachableMesh.transform.SetParent(null);

            Rigidbody partRb = detachableMesh.GetComponent<Rigidbody>();
            if (partRb == null) partRb = detachableMesh.AddComponent<Rigidbody>();

            partRb.mass = 15f;
            partRb.AddForce(Vector3.up * 6f + Random.insideUnitSphere * 3f, ForceMode.Impulse);
            partRb.AddTorque(Random.insideUnitSphere * 15f, ForceMode.Impulse);

            Destroy(detachableMesh, 15f);
        }

        switch (moduleType)
        {
            case ModuleType.Engine:
                Debug.Log("Двигатель уничтожен: МТО горит, техника остановлена.");
                break;
            case ModuleType.Tracks:
                Debug.Log("Гусеница перебита: Техника потеряла ход.");
                break;
            case ModuleType.AmmoRack:
                Debug.Log("Детонация боекомплекта!");
                break;
        }
    }
}