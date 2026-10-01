using UnityEngine;

public class EnemyDroneHealth : MonoBehaviour
{
    [Header("Здоровье и Эффекты")]
    [SerializeField] private float maxHealth = 20f;
    [SerializeField] private GameObject explosionEffectPrefab; // Эффект взрыва
    [SerializeField] private GameObject smokeTrailPrefab;     // Дым при подбитии

    private float currentHealth;
    private bool isDestroyed = false;
    private Rigidbody rb;
    private ShahedFlight flightScript;

    private void Awake()
    {
        currentHealth = maxHealth;
        rb = GetComponent<Rigidbody>();
        flightScript = GetComponent<ShahedFlight>();
    }

    // Вызывается из BulletParticleCollision при попадании пули БТР
    public void TakeDamage(float damage, Vector3 hitPoint)
    {
        if (isDestroyed) return;

        currentHealth -= damage;

        if (currentHealth <= 0f)
        {
            StartFalling();
        }
    }

    private void StartFalling()
    {
        isDestroyed = true;

        // 1. Отключаем скрипт полёта
        if (flightScript != null) flightScript.enabled = false;

        // 2. Включаем физику падения
        rb.useGravity = true;

        // Закручиваем дрон в воздухе
        rb.AddTorque(Random.insideUnitSphere * 15f, ForceMode.Impulse);

        // 3. Спавним шлейф дыма
        if (smokeTrailPrefab != null)
        {
            Instantiate(smokeTrailPrefab, transform.position, Quaternion.identity, transform);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        Vector3 hitPoint = collision.contacts.Length > 0 ? collision.contacts[0].point : transform.position;

        // 1. Если дрон УЖЕ сбит и падал — взрывается от любого касания (земля, здание и т.д.)
        if (isDestroyed)
        {
            Explode(hitPoint);
            return;
        }

        // 2. Столкновение с другим дроном в воздухе (например, таран FPV-дроном)
        Transform otherRoot = collision.transform.root;
        if (collision.gameObject.CompareTag("Drone") || otherRoot.CompareTag("Drone"))
        {
            // Проверяем, что врезался НЕ сам в себя
            if (otherRoot != transform.root)
            {
                Debug.Log($"<color=orange>[AIR COLLISION]</color> Таран дронов: {name} и {otherRoot.name}");

                // Если у второго дрона тоже есть скрипт здоровья — взрываем и его
                EnemyDroneHealth otherHealth = otherRoot.GetComponent<EnemyDroneHealth>();
                if (otherHealth != null)
                {
                    otherHealth.Explode(hitPoint);
                }
                else
                {
                    Destroy(otherRoot.gameObject);
                }

                Explode(hitPoint);
                return;
            }
        }

        // 3. Камикадзе-удар на лету (по БТР, технике, зданию или земле)
        if (collision.gameObject.CompareTag("Vehicle") ||
            collision.gameObject.CompareTag("Player") ||
            collision.gameObject.CompareTag("Building") ||
            collision.gameObject.layer == LayerMask.NameToLayer("Terrain"))
        {
            Debug.Log($"<color=red>[KAMIKAZE IMPACT]</color> Дрон врезался в: {collision.gameObject.name}");
            Explode(hitPoint);
        }
    }

    // Вспомогательный метод моментального взрыва
    public void Explode(Vector3 hitPoint)
    {
        if (explosionEffectPrefab != null)
        {
            Instantiate(explosionEffectPrefab, hitPoint, Quaternion.identity);
        }
        Destroy(gameObject);
    }
}