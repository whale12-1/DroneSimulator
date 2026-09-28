using UnityEngine;

public class DroneDropMechanism : MonoBehaviour
{
    [Header("Настройки боекомплекта")]
    [SerializeField] private GameObject bombPrefab;        // Префаб боеприпаса
    [SerializeField] private Transform dropPoint;            // Точка под днищем дрона
    [SerializeField] private KeyCode dropKey = KeyCode.Space; // Клавиша сброса
    [SerializeField] private int ammoCount = 4;              // Кол-во снарядов
    [SerializeField] private float dropCooldown = 1f;        // Задержка между сбросами

    private Rigidbody droneRigidbody;
    private float lastDropTime = 0f;

    private void Awake()
    {
        droneRigidbody = GetComponentInParent<Rigidbody>();
    }

    private void Update()
    {
        if (Input.GetKeyDown(dropKey) && Time.time >= lastDropTime + dropCooldown && ammoCount > 0)
        {
            DropBomb();
        }
    }

    private void DropBomb()
    {
        lastDropTime = Time.time;
        ammoCount--;

        // Спавним снаряд под дроном
        GameObject bombInstance = Instantiate(bombPrefab, dropPoint.position, dropPoint.rotation);
        Rigidbody bombRb = bombInstance.GetComponent<Rigidbody>();

        if (bombRb != null && droneRigidbody != null)
        {
            // Передаем снаряду вектор и скорость полета дрона (инерция)
            bombRb.linearVelocity = droneRigidbody.linearVelocity;
        }

        Debug.Log($"[BOMB DROP] Снаряд сброшен! Осталось: {ammoCount}");
    }

    public int GetAmmoCount()
    {
        return ammoCount;
    }
}