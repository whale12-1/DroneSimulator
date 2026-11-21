using UnityEngine;

public class DroneDropMechanism : MonoBehaviour, IPrimaryActionHandler
{
    [Header("Настройки боекомплекта")]
    [SerializeField] private GameObject bombPrefab;
    [SerializeField] private Transform dropPoint;
    [SerializeField] private int ammoCount = 4;
    [SerializeField] private float dropCooldown = 1f;

    private Rigidbody droneRigidbody;
    private float lastDropTime = 0f;

    public int PrimaryPriority => 20;

    public int AmmoCount => ammoCount;

    public bool HasPayload => bombPrefab != null && dropPoint != null && ammoCount > 0;

    public bool CanDrop => HasPayload && Time.time >= lastDropTime + dropCooldown;

    private void Awake()
    {
        droneRigidbody = GetComponentInParent<Rigidbody>();
    }

    private void OnEnable()
    {
        GameInput.RegisterPrimaryHandler(this);
    }

    private void OnDisable()
    {
        GameInput.UnregisterPrimaryHandler(this);
    }

    public bool WantsPrimaryAction()
    {
        return CanDrop;
    }

    public void OnPrimaryAction()
    {
        if (!CanDrop) return;
        DropBomb();
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
