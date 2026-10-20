using UnityEngine;

public class DroneManager : MonoBehaviour
{
    [Header("Объекты Дронов")]
    [SerializeField] private GameObject reconDrone;
    [SerializeField] private GameObject strikeDrone;
    [SerializeField] private GameObject strikeDrone_2;
    [SerializeField] private GameObject dropDrone;
    [SerializeField] private GameObject intersectorDrone;

    [Header("Профили (должны совпадать по порядку с объектами выше)")]
    [SerializeField] private DroneConfig reconConfig;
    [SerializeField] private DroneConfig strikeConfig;
    [SerializeField] private DroneConfig strike2Config;
    [SerializeField] private DroneConfig dropConfig;
    [SerializeField] private DroneConfig intersectorConfig;

    private void OnEnable()
    {
        // Подписываемся на событие уничтожения дрона
        GameEvents.DroneExploded += OnDroneDestroyed;
    }

    private void OnDisable()
    {
        GameEvents.DroneExploded -= OnDroneDestroyed;
    }

    private void Start()
    {
        SelectRecon();
    }

    private void Update()
    {
        // Проверяем, что дрон не null (то есть он ещё жив), прежде чем переключаться
        if (Input.GetKeyDown(KeyCode.Alpha1) && reconDrone != null) SelectRecon();
        if (Input.GetKeyDown(KeyCode.Alpha2) && strikeDrone != null) SelectStrike();
        if (Input.GetKeyDown(KeyCode.Alpha3) && strikeDrone_2 != null) SelectStrike1();
        if (Input.GetKeyDown(KeyCode.Alpha4) && dropDrone != null) SelectDropDrone();
        if (Input.GetKeyDown(KeyCode.Alpha5) && intersectorDrone != null) SelectIntersector();
    }

    /// <summary>Активирует дрон по конфигу. Если не найдено или уничтожен — игнорирует.</summary>
    public void SelectByConfig(DroneConfig cfg)
    {
        if (cfg == null) return;

        if (cfg == reconConfig && reconDrone != null) { SelectRecon(); return; }
        if (cfg == strikeConfig && strikeDrone != null) { SelectStrike(); return; }
        if (cfg == strike2Config && strikeDrone_2 != null) { SelectStrike1(); return; }
        if (cfg == dropConfig && dropDrone != null) { SelectDropDrone(); return; }
        if (cfg == intersectorConfig && intersectorDrone != null) { SelectIntersector(); return; }

        Debug.LogWarning($"[DroneManager] Конфиг '{cfg.name}' не привязан или дрон уже уничтожен.");
    }

    public void SelectRecon() => SetActive(reconDrone);
    public void SelectStrike() => SetActive(strikeDrone);
    public void SelectStrike1() => SetActive(strikeDrone_2);
    public void SelectDropDrone() => SetActive(dropDrone);
    public void SelectIntersector() => SetActive(intersectorDrone);

    private void SetActive(GameObject target)
    {
        // Если дрон уничтожен (null), ничего не делаем
        if (target == null) return;

        // Выключаем всех, кроме target
        Toggle(reconDrone, target);
        Toggle(strikeDrone, target);
        Toggle(strikeDrone_2, target);
        Toggle(dropDrone, target);
        Toggle(intersectorDrone, target);

        GameEvents.RaiseDroneSwitched(target);
    }

    private static void Toggle(GameObject go, GameObject active)
    {
        if (go != null) go.SetActive(go == active);
    }

    // --- Логика отслеживания смертей ---

    private void OnDroneDestroyed(Vector3 pos, MonoBehaviour destroyedDroneScript)
    {
        GameObject destroyedObj = destroyedDroneScript.gameObject;

        // Обнуляем ссылки на уничтоженный дрон
        if (reconDrone == destroyedObj) reconDrone = null;
        if (strikeDrone == destroyedObj) strikeDrone = null;
        if (strikeDrone_2 == destroyedObj) strikeDrone_2 = null;
        if (dropDrone == destroyedObj) dropDrone = null;
        if (intersectorDrone == destroyedObj) intersectorDrone = null;

        int alive = GetAliveDronesCount();
        if (alive > 0)
        {
            Debug.Log($"<color=orange>[DroneManager]</color> Дрон уничтожен! Осталось: {alive}. Нажмите 1-5, чтобы выбрать другого.");
        }
    }

    /// <summary>Возвращает количество живых дронов (для MissionController).</summary>
    public int GetAliveDronesCount()
    {
        int count = 0;
        if (reconDrone != null) count++;
        if (strikeDrone != null) count++;
        if (strikeDrone_2 != null) count++;
        if (dropDrone != null) count++;
        if (intersectorDrone != null) count++;
        return count;
    }
}