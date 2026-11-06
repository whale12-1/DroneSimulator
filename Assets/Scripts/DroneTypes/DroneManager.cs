using System.Collections.Generic;
using UnityEngine;

public class DroneManager : MonoBehaviour
{
    [System.Serializable]
    public class DroneSetup
    {
        public GameObject droneObject;
        public DroneConfig config;
    }

    [Header("Все дроны в сцене (пул)")]
    [Tooltip("Перетащи сюда все префабы/объекты дронов, которые могут быть в этой сцене")]
    [SerializeField] private DroneSetup[] allDronesInScene;

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

    private float currentBoundaryJam = 0f;

    private void OnEnable()
    {
        GameEvents.DroneExploded += OnDroneDestroyed;
    }

    private void OnDisable()
    {
        GameEvents.DroneExploded -= OnDroneDestroyed;
    }

    private void Start()
    {
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
                var match = System.Array.Find(allDronesInScene, d => d.config == allowedConfig);

                if (match != null && !activeSlots.Contains(match))
                {
                    activeSlots.Add(match);
                }
                else
                {
                    Debug.LogWarning($"[DroneManager] В сцене нет дрона с конфигом '{allowedConfig.name}'");
                }
            }
        }
        else
        {
            // Если миссия не задана (тестовый запуск), берем всех
            activeSlots.AddRange(allDronesInScene);
        }

        // 3. Если есть хотя бы один дрон - активируем первого
        if (activeSlots.Count > 0)
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
            return;
        }

        // Считаем расстояние от активного дрона до центра карты
        float distance = Vector3.Distance(activeDrone.transform.position, mapCenter.position);
        float targetJam = 0f;

        if (distance > safeRadius)
        {
            // Вычисляем процент (0 - на границе, 1 - на максимуме)
            targetJam = Mathf.Clamp01((distance - safeRadius) / (maxRadius - safeRadius));
        }

        // Плавно применяем
        if (targetJam > EWService.BoundaryJamIntensity)
            EWService.BoundaryJamIntensity = Mathf.MoveTowards(EWService.BoundaryJamIntensity, targetJam, Time.deltaTime * jamRiseSpeed);
        else
            EWService.BoundaryJamIntensity = Mathf.MoveTowards(EWService.BoundaryJamIntensity, targetJam, Time.deltaTime * jamFallSpeed);
    }

    private void Update()
    {
        // Динамическое переключение по клавишам 1-9
        for (int i = 0; i < activeSlots.Count; i++)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1 + i))
            {
                SelectDroneByIndex(i);
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
}