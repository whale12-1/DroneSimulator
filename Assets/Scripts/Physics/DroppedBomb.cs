using UnityEngine;

public class DroppedBomb : MonoBehaviour
{
    [Header("Параметры взрыва")]
    [SerializeField] private float explosionRadius = 6f;
    [SerializeField] private float damage = 150f;
    [SerializeField] private float explosionForce = 700f;
    [SerializeField] private GameObject explosionVFX;

    private bool isExploded = false;

    private void OnCollisionEnter(Collision collision)
    {
        if (isExploded) return;
        Explode();
    }

    private void Explode()
    {
        isExploded = true;

        // 1. Визуальный эффект взрыва
        if (explosionVFX != null)
        {
            Instantiate(explosionVFX, transform.position, Quaternion.identity);
        }

        // 2. Поиск всех объектов в радиусе поражения
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, explosionRadius);
        foreach (var hit in hitColliders)
        {
            // Наносим урон технике (БТР)
            VehicleHealth vehicle = hit.GetComponentInParent<VehicleHealth>();
            if (vehicle != null)
            {
                vehicle.OnModuleHit(ModuleType.MainHull, damage, false, transform.position);
            }

            // Наносим урон разрушаемым стенам
            DestructibleWallZone wall = hit.GetComponent<DestructibleWallZone>();
            if (wall != null)
            {
                wall.TakeDamage(damage);
            }

            // Разбрасываем обломки и физические объекты
            Rigidbody rb = hit.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.AddExplosionForce(explosionForce, transform.position, explosionRadius);
            }
        }

        // Уничтожаем объект снаряда
        Destroy(gameObject);
    }

    private void OnDrawGizmosSelected()
    {
        // Отрисовка радиуса поражения в редакторе для удобства настройки
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, explosionRadius);
    }
}