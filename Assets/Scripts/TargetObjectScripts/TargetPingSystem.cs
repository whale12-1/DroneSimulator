using UnityEngine;

public class TargetPingSystem : MonoBehaviour
{
    [Header("Ссылки")]
    [SerializeField] private DroneManager droneManager;
    [SerializeField] private GameObject pingMarkerPrefab;
    [SerializeField] private Camera reconCamera;

    [Header("Настройки")]
    [SerializeField] private LayerMask groundMask;
    [SerializeField] private float maxDistanceForRay = 1000f;

    [Header("Близость (Когда маркер исчезает)")]
    [Tooltip("Расстояние, ближе которого маркер исчезает (в метрах)")]
    [SerializeField] private float disappearDistance = 200f;

    private GameObject activePing;
    private Transform activeDroneTransform;

    private void OnEnable()
    {
        GameEvents.DroneSwitched += OnDroneSwitched;
    }

    private void OnDisable()
    {
        GameEvents.DroneSwitched -= OnDroneSwitched;
    }

    private void Update()
    {
        // Если маркер активен и у нас есть активный дрон
        if (activePing != null && activeDroneTransform != null)
        {
            float distance = Vector3.Distance(activeDroneTransform.position, activePing.transform.position);

            // Если подлетели ближе 200 метров - прячем маркер
            if (distance < disappearDistance)
            {
                Destroy(activePing);
            }
        }
    }

    private void OnDroneSwitched(GameObject newDrone)
    {
        activeDroneTransform = newDrone.transform;
        var flight = newDrone.GetComponent<DroneFlightController>();

        // Если переключились на ударный дрон — ставим маркер
        if (flight != null && flight.droneType == DroneType.StrikeFPV)
        {
            CreatePingAtLastKnownPosition();
        }
        // Если вернулись на разведчика — убираем старый маркер
        else if (flight != null && flight.droneType == DroneType.ReconDrop)
        {
            if (activePing != null) Destroy(activePing);
        }
    }

    private void CreatePingAtLastKnownPosition()
    {
        Vector3 targetPos = Vector3.zero;
        bool foundTarget = false;

        // 1. Пытаемся взять позицию цели, которую захватил разведчик
        var lockManager = FindFirstObjectByType<TargetLockManager>();
        if (lockManager != null && lockManager.currentLockedTarget != null)
        {
            targetPos = lockManager.currentLockedTarget.transform.position;
            foundTarget = true;
        }
        // 2. Если цель не захвачена — стреляем лучом из центра камеры разведчика
        else if (reconCamera != null)
        {
            Ray ray = reconCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
            if (Physics.Raycast(ray, out RaycastHit hit, maxDistanceForRay, groundMask))
            {
                targetPos = hit.point;
                foundTarget = true;
            }
        }

        if (foundTarget)
        {
            if (activePing != null) Destroy(activePing);

            // Создаем маркер ровно в точке цели
            activePing = Instantiate(pingMarkerPrefab, targetPos, Quaternion.identity);
        }
    }
}