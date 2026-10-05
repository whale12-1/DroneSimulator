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

    private void Start() => SelectRecon();

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1)) SelectRecon();
        if (Input.GetKeyDown(KeyCode.Alpha2)) SelectStrike();
        if (Input.GetKeyDown(KeyCode.Alpha3)) SelectStrike1();
        if (Input.GetKeyDown(KeyCode.Alpha4)) SelectDropDrone();
        if (Input.GetKeyDown(KeyCode.Alpha5)) SelectIntersector();
    }

    /// <summary>Активирует дрон по конфигу. Если не найдено — остаётся текущий выбор.</summary>
    public void SelectByConfig(DroneConfig cfg)
    {
        if (cfg == null) return;

        if (cfg == reconConfig) { SelectRecon(); return; }
        if (cfg == strikeConfig) { SelectStrike(); return; }
        if (cfg == strike2Config) { SelectStrike1(); return; }
        if (cfg == dropConfig) { SelectDropDrone(); return; }
        if (cfg == intersectorConfig) { SelectIntersector(); return; }

        Debug.LogWarning($"[DroneManager] Конфиг '{cfg.name}' не привязан ни к одному слоту.");
    }

    public void SelectRecon() => SetActive(reconDrone);
    public void SelectStrike() => SetActive(strikeDrone);
    public void SelectStrike1() => SetActive(strikeDrone_2);
    public void SelectDropDrone() => SetActive(dropDrone);
    public void SelectIntersector() => SetActive(intersectorDrone);

    private void SetActive(GameObject target)
    {
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
}