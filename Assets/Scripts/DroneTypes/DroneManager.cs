using System.Collections.Generic;
using UnityEngine;

public class DroneManager : MonoBehaviour
{
    [System.Serializable]
    public class DroneSetup
    {
        public GameObject droneObject; // Объект в сцене
        public GameObject prefab;      // <--- ДОБАВЛЕНО: Префаб для возрождения
        public DroneConfig config;
    }

    [Header("Все дроны в сцене (пул)")]
    [Tooltip("Перетащи сюда все префабы/объекты дронов, которые могут быть в этой сцене")]
    [SerializeField] private DroneSetup[] allDronesInScene;

    [Header("Точка возрождения (для обучения)")]
    [Tooltip("Где будет появляться дрон после уничтожения (в режиме бесконечных попыток)")]
    [SerializeField] private Transform respawnPoint;

    [Header("Границы полигона")]
    [Tooltip("Сколько секунд дрон может быть за maxRadius, прежде чем его вернут в зону")]
    [SerializeField] private float outOfBoundsGrace = 5f;
    [Tooltip("Высота возврата над точкой респавна")]
    [SerializeField] private float outOfBoundsReturnHeight = 25f;
    [Tooltip("Зазор над землёй при спавне (мотор выключен — дрон не падает)")]
    [SerializeField] private float spawnGroundOffset = 0.8f;

    /// <summary>Дрон вышел за пределы полигона (растёт полоса помех).</summary>
    public bool IsOutOfBounds { get; private set; }

    /// <summary>Текущая точка возрождения (TrainingManager переключает её по главам).</summary>
    public Transform RespawnPoint
    {
        get => respawnPoint;
        set => respawnPoint = value;
    }

    public event System.Action OutOfBoundsStarted;
    public event System.Action OutOfBoundsEnded;

    private float outOfBoundsTimer;

    // Активные слоты для текущей миссии (формируются на старте)
    private List<DroneSetup> activeSlots = new List<DroneSetup>();
    private int currentActiveIndex = -1;

    [Header("Границы Карты (Out of Bounds)")]
    [Tooltip("Центр карты (обычно главный Terrain)")]
    [SerializeField] private Transform mapCenter;
    [Tooltip("Безопасный радиус. До этого значения помех нет.")]
    [SerializeField] private float safeRadius = 1000f;
    [Tooltip("Максимальный радиус. После этого значения помехи 100%.")]
    [SerializeField] private float maxRadius = 1500f;
    [Tooltip("Как быстро нарастают помехи при удалении.")]
    [SerializeField] private float jamRiseSpeed = 0.5f;
    [Tooltip("Как быстро спадают помехи при возвращении.")]
    [SerializeField] private float jamFallSpeed = 1f;

    private void OnEnable()
    {
        GameEvents.DroneExploded += OnDroneDestroyed;
    }

    private void OnDisable()
    {
        GameEvents.DroneExploded -= OnDroneDestroyed;
    }

    private void Awake()
    {
        // Строим пул активных дронов как можно раньше (до Start у MissionController),
        // чтобы MissionController мог выбрать дефолтный дрон через SelectByConfig.
        InitializeDrones();
    }

    private void InitializeDrones()
    {
        // 1. Выключаем всех дронов на сцене
        foreach (var setup in allDronesInScene)
        {
            if (setup.droneObject != null) setup.droneObject.SetActive(false);
        }

        activeSlots.Clear();

        // 2. Получаем список разрешенных конфигов из миссии
        var mission = MissionContext.Current;
        if (mission != null && mission.availableDrones != null && mission.availableDrones.Length > 0)
        {
            // Проходим по всем доступным дронам в миссии
            foreach (var allowedConfig in mission.availableDrones)
            {
                if (allowedConfig == null) continue;

                // Ищем в пуле сцены подходящий по конфигу
                var match = System.Array.Find(allDronesInScene, d => d.config == allowedConfig && !activeSlots.Exists(slot => slot.droneObject == d.droneObject));

                if (match != null)
                {
                    activeSlots.Add(match);
                }
                else
                {
                    Debug.LogWarning($"[DroneManager] В сцене нет свободного дрона с конфигом '{allowedConfig.name}'");
                }
            }
        }
        else
        {
            // Если миссия не задана (тестовый запуск), берем всех
            activeSlots.AddRange(allDronesInScene);
        }

        // 3. НЕ включаем дрона автоматически, если миссия задана:
        //    выбор делает MissionController через SelectByConfig(mission.defaultDrone).
        //    Для тестового запуска без миссии включаем первый дрон из пула.
        if ((mission == null || mission.defaultDrone == null) && activeSlots.Count > 0)
        {
            SelectDroneByIndex(0);
        }
    }

    private void CalculateBoundaryJam()
    {
        if (mapCenter == null) return;

        // Находим текущий активный дрон
        GameObject activeDrone = null;
        if (currentActiveIndex >= 0 && currentActiveIndex < activeSlots.Count)
            activeDrone = activeSlots[currentActiveIndex].droneObject;

        // Если активного дрона нет (или он уничтожен), сбрасываем помехи
        if (activeDrone == null)
        {
            EWService.BoundaryJamIntensity = Mathf.MoveTowards(EWService.BoundaryJamIntensity, 0f, Time.deltaTime * jamFallSpeed);
            ResetOutOfBounds();
            return;
        }

        float distance = Vector3.Distance(activeDrone.transform.position, mapCenter.position);
        float targetJam = 0f;

        if (distance > safeRadius)
        {
            targetJam = Mathf.Clamp01((distance - safeRadius) / Mathf.Max(1f, maxRadius - safeRadius));
        }

        if (targetJam > EWService.BoundaryJamIntensity)
            EWService.BoundaryJamIntensity = Mathf.MoveTowards(EWService.BoundaryJamIntensity, targetJam, Time.deltaTime * jamRiseSpeed);
        else
            EWService.BoundaryJamIntensity = Mathf.MoveTowards(EWService.BoundaryJamIntensity, targetJam, Time.deltaTime * jamFallSpeed);

        UpdateOutOfBounds(activeDrone, distance);
    }

    // --- Контроль границ полигона ---

    private void UpdateOutOfBounds(GameObject activeDrone, float distance)
    {
        if (distance <= maxRadius)
        {
            ResetOutOfBounds();
            return;
        }

        SetOutOfBounds(true);

        // Time.deltaTime == 0 на паузе: таймер не тикает, пока игра стоит.
        outOfBoundsTimer += Time.deltaTime;
        if (outOfBoundsGrace > 0f && outOfBoundsTimer < outOfBoundsGrace) return;

        ReturnToZone(activeDrone);
    }

    private void ResetOutOfBounds()
    {
        outOfBoundsTimer = 0f;
        SetOutOfBounds(false);
    }

    private void SetOutOfBounds(bool value)
    {
        if (IsOutOfBounds == value) return;

        IsOutOfBounds = value;
        if (value) OutOfBoundsStarted?.Invoke();
        else OutOfBoundsEnded?.Invoke();
    }

    private void ReturnToZone(GameObject drone)
    {
        if (drone == null) return;

        outOfBoundsTimer = 0f;
        EWService.BoundaryJamIntensity = 0f;

        Transform anchor = respawnPoint != null ? respawnPoint : mapCenter;
        Vector3 spawn = ResolveSpawnPosition(anchor.position + Vector3.up * outOfBoundsReturnHeight);
        Quaternion rotation = anchor.rotation;

        Rigidbody body = drone.GetComponent<Rigidbody>();
        if (body != null)
        {
            body.rotation = rotation;
            body.position = spawn;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }
        else
        {
            drone.transform.SetPositionAndRotation(spawn, rotation);
        }

        SetOutOfBounds(false);
    }

    // Опускает точку спавна на опору под ней: дрон с выключенным мотором
    // не должен падать с 10 метров.
    private Vector3 ResolveSpawnPosition(Vector3 desired)
    {
        Vector3 origin = desired + Vector3.up * 5f;
        RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, 400f, ~0, QueryTriggerInteraction.Ignore);

        float best = float.MaxValue;
        for (int i = 0; i < hits.Length; i++)
        {
            Collider collider = hits[i].collider;
            if (collider == null) continue;
            if (collider.GetComponentInParent<DroneFlightController>() != null) continue;
            if (hits[i].distance < best) best = hits[i].distance;
        }

        if (best == float.MaxValue) return desired;

        return origin + Vector3.down * best + Vector3.up * spawnGroundOffset;
    }

    // Роль задана явно (DroneConfig.droneRole); проверка по имени осталась
    // только фолбэком для старых конфигов без заполненной роли.
    private int FindStrikeSlotIndex()
    {
        int index = activeSlots.FindIndex(s => s.config != null && s.config.droneRole == DroneRole.Strike);
        if (index == -1)
            index = activeSlots.FindIndex(s => s.config != null && s.config.droneName.Contains("FPV"));
        return index;
    }

    private void Update()
    {
        // Переключение дронов 1..9 — только в игре: на паузе ввод не обрабатывается.
        if (Time.timeScale > 0f)
        {
            for (int i = 0; i < activeSlots.Count; i++)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1 + i))
                {
                    SelectDroneByIndex(i);
                }
            }
        }

        CalculateBoundaryJam();
    }

    private void OnDrawGizmosSelected()
    {
        if (mapCenter == null) return;

        // Зеленая сфера - безопасная зона
        Gizmos.color = new Color(0, 1, 0, 0.2f);
        Gizmos.DrawSphere(mapCenter.position, safeRadius);

        // Красная сфера - зона полного отключения
        Gizmos.color = new Color(1, 0, 0, 0.1f);
        Gizmos.DrawSphere(mapCenter.position, maxRadius);
    }

    public void SelectDroneByIndex(int index)
    {
        if (index < 0 || index >= activeSlots.Count) return;

        // Проверяем, жив ли дрон (объект может быть null, если уничтожен)
        if (activeSlots[index].droneObject == null) return;

        SetActive(activeSlots[index].droneObject);
        currentActiveIndex = index;
    }

    public void SelectStrike()
    {
        int index = FindStrikeSlotIndex();
        if (index != -1 && activeSlots[index].droneObject != null) SelectDroneByIndex(index);
    }

    /// <summary>Активирует дрон по конфигу. Используется в миссиях.</summary>
    public void SelectByConfig(DroneConfig cfg)
    {
        if (cfg == null) return;

        int index = activeSlots.FindIndex(s => s.config == cfg && s.droneObject != null);
        if (index != -1)
        {
            SelectDroneByIndex(index);
        }
        else
        {
            Debug.LogWarning($"[DroneManager] Конфиг '{cfg.name}' не найден среди активных дронов.");
        }
    }

    private void SetActive(GameObject target)
    {
        if (target == null) return;

        foreach (var slot in activeSlots)
        {
            if (slot.droneObject != null)
                slot.droneObject.SetActive(slot.droneObject == target);
        }

        GameEvents.RaiseDroneSwitched(target);
    }

    // --- Логика отслеживания смертей ---
    private void OnDroneDestroyed(Vector3 pos, MonoBehaviour destroyedDroneScript)
    {
        GameObject destroyedObj = destroyedDroneScript.gameObject;

        // Обнуляем ссылку в слоте
        var slot = activeSlots.Find(s => s.droneObject == destroyedObj);
        if (slot != null)
        {
            slot.droneObject = null; // Помечаем, что дрон уничтожен
        }

        int alive = GetAliveDronesCount();
        if (alive > 0)
        {
            Debug.Log($"<color=orange>[DroneManager]</color> Дрон уничтожен! Осталось: {alive}.");
        }
    }

    public int GetAliveDronesCount()
    {
        int count = 0;
        foreach (var slot in activeSlots)
        {
            if (slot.droneObject != null) count++;
        }
        return count;
    }

    /// <summary>
    /// Возрождает ударный дрон. Используется в обучении для бесконечных попыток.
    /// </summary>
    public void RespawnStrikeDrone()
    {
        int index = FindStrikeSlotIndex();

        if (index == -1)
        {
            Debug.LogError("[DroneManager] Не найден слот для ударного дрона (DroneRole.Strike)!", this);
            return;
        }

        var slot = activeSlots[index];

        if (slot.prefab == null)
        {
            Debug.LogError("[DroneManager] В инспекторе не назначен Prefab для ударного дрона! Возрождение невозможно.", this);
            return;
        }

        if (slot.droneObject != null) Destroy(slot.droneObject);

        Vector3 spawnPos = respawnPoint != null ? respawnPoint.position : Vector3.zero;
        Quaternion spawnRot = respawnPoint != null ? respawnPoint.rotation : Quaternion.identity;
        spawnPos = ResolveSpawnPosition(spawnPos);

        GameObject newDrone = Instantiate(slot.prefab, spawnPos, spawnRot);
        slot.droneObject = newDrone;

        SelectDroneByIndex(index);
        Debug.Log("<color=green>[DroneManager]</color> Ударный дрон успешно возрожден!");
    }

    /// <summary>
    /// Спавнит ударный дрон в конкретной точке (используется при переходах между главами).
    /// </summary>
    public void RespawnStrikeDroneAt(Vector3 position, Quaternion rotation)
    {
        int index = FindStrikeSlotIndex();

        if (index == -1)
        {
            Debug.LogError("[DroneManager] Не найден слот для ударного дрона (DroneRole.Strike)!", this);
            return;
        }

        var slot = activeSlots[index];

        if (slot.prefab == null)
        {
            Debug.LogError("[DroneManager] Не назначен Prefab для ударного дрона!", this);
            return;
        }

        if (slot.droneObject != null) Destroy(slot.droneObject);

        // Безопасная высота: ищем опору под точкой спавна (мотор ещё не запущен).
        Vector3 spawnPos = ResolveSpawnPosition(position);
        GameObject newDrone = Instantiate(slot.prefab, spawnPos, rotation);
        slot.droneObject = newDrone;

        SelectDroneByIndex(index);
        Debug.Log("<color=green>[DroneManager]</color> Ударный дрон заспавнен на новой точке!");
    }

}