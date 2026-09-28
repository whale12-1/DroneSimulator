using UnityEngine;

public class DestructibleWallZone : MonoBehaviour
{
    [Header("Здоровье стены")]
    [SerializeField] private float wallHealth = 50f;

    [Header("Маска пролома (DepthMask)")]
    [Tooltip("Дочерний объект Mask с материалом DepthMask")]
    [SerializeField] private GameObject holeMaskObject;

    [Header("Эффекты и Обломки")]
    [SerializeField] private GameObject debrisPrefab; // Префаб вылетающих кусков бетона/дерева
    [SerializeField] private GameObject dustFx;       // Эффект пыли

    private Collider wallCollider;
    private bool isDestroyed = false;

    private void Awake()
    {
        wallCollider = GetComponent<Collider>();

        // Гарантируем, что маска выключена на старте игры
        if (holeMaskObject != null)
        {
            holeMaskObject.SetActive(false);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (isDestroyed) return;

        // Регистрируем столкновение с дроном или бомбой
        if (collision.gameObject.CompareTag("Drone") || collision.gameObject.CompareTag("Bomb"))
        {
            TakeDamage(100f);
        }
    }

    private void OnParticleCollision(GameObject other)
    {
        if (isDestroyed) return;

        TakeDamage(15f);
    }

    public void TakeDamage(float amount)
    {
        if (isDestroyed) return;

        wallHealth -= amount;

        if (wallHealth <= 0f)
        {
            BreakZone();
        }
    }

    private void BreakZone()
    {
        isDestroyed = true;

        // 1. Включаем маску глубины (DepthMask), создавая визуальную дыру
        if (holeMaskObject != null)
        {
            holeMaskObject.SetActive(true);
        }

        // 2. Отключаем коллайдер — дрон и объекты свободно пролетают сквозь стену
        if (wallCollider != null)
        {
            wallCollider.enabled = false;
        }

        // 3. Спавним пыль и обломки
        if (dustFx != null)
        {
            Instantiate(dustFx, transform.position, transform.rotation);
        }

        if (debrisPrefab != null)
        {
            Instantiate(debrisPrefab, transform.position, transform.rotation);
        }

        Debug.Log($"[BREACH] Стена {gameObject.name} пробита!");
    }
}