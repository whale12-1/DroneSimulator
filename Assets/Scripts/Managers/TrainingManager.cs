using UnityEngine;
using TMPro;

public class TrainingManager : MonoBehaviour
{
    public enum TrainingStep
    {
        ReconTakeoff,
        ReconHover,
        ReconZoom,
        ReconGimbal,
        ReconNightVision,
        ReconTargetLock,
        SwitchToStrike,
        StrikeFlyGates,
        StrikeDestroyTarget,
        SwitchBackToRecon,       // НОВЫЙ ЭТАП
        ReconObjectiveControl,   // НОВЫЙ ЭТАП
        Completed
    }

    [Header("Ссылки")]
    [SerializeField] private Transform activeDroneTransform;
    [SerializeField] private TMP_Text instructionText;

    [Header("Настройки полета")]
    [SerializeField] private float targetHeight = 5f;
    [SerializeField] private float heightTolerance = 1.5f;

    [Header("Ворота")]
    [SerializeField] private int totalGates = 3;

    private TrainingStep currentStep = TrainingStep.ReconTakeoff;
    private int gatesPassed = 0;
    private float hoverTimer = 0f;
    private float startY;

    private DroneFlightController currentFlightController;
    private TargetLockManager currentLockManager;

    private void Start()
    {
        if (activeDroneTransform != null)
        {
            startY = activeDroneTransform.position.y;
            UpdateDroneReferences(activeDroneTransform.gameObject);
        }
        UpdateUI();
    }

    private void OnEnable()
    {
        TrainingGate.OnGatePassed += HandleGatePassed;
        GameEvents.VehicleDestroyed += HandleVehicleDestroyed;
        GameEvents.DroneSwitched += HandleDroneSwitched;
    }

    private void OnDisable()
    {
        TrainingGate.OnGatePassed -= HandleGatePassed;
        GameEvents.VehicleDestroyed -= HandleVehicleDestroyed;
        GameEvents.DroneSwitched -= HandleDroneSwitched;
    }

    private void Update()
    {
        switch (currentStep)
        {
            case TrainingStep.ReconTakeoff:
                if (CheckHeight(targetHeight)) NextStep();
                break;

            case TrainingStep.ReconHover:
                if (Input.GetKeyDown(KeyCode.H)) NextStep();
                break;

            case TrainingStep.ReconZoom:
                if (Input.GetKeyDown(KeyCode.Z) || Input.GetKeyDown(KeyCode.X)) NextStep();
                break;

            case TrainingStep.ReconGimbal:
                if (Input.GetKey(KeyCode.R) || Input.GetKey(KeyCode.F) || Input.GetKey(KeyCode.Q) || Input.GetKey(KeyCode.E))
                {
                    hoverTimer += Time.deltaTime;
                    if (hoverTimer > 1f) NextStep();
                }
                break;

            case TrainingStep.ReconNightVision:
                if (Input.GetKeyDown(KeyCode.N)) NextStep();
                break;

            case TrainingStep.ReconTargetLock:
                if (currentLockManager != null && currentLockManager.currentLockedTarget != null) NextStep();
                break;

            case TrainingStep.StrikeFlyGates:
                // Ждем события от ворот
                break;

            case TrainingStep.StrikeDestroyTarget:
                // Ждем события уничтожения
                break;

            case TrainingStep.ReconObjectiveControl:
                // Игрок должен нажать Пробел, чтобы "сделать снимок" уничтоженной цели
                if (Input.GetKeyDown(KeyCode.Space))
                {
                    NextStep();
                }
                break;
        }
    }

    private bool CheckHeight(float targetH)
    {
        if (activeDroneTransform == null) return false;
        float currentHeight = activeDroneTransform.position.y;
        return (currentHeight - startY) >= targetH - heightTolerance;
    }

    // --- ОБРАБОТЧИКИ СОБЫТИЙ ---

    private void HandleGatePassed()
    {
        if (currentStep != TrainingStep.StrikeFlyGates) return;
        gatesPassed++;
        UpdateUI();
        if (gatesPassed >= totalGates) NextStep();
    }

    private void HandleVehicleDestroyed(VehicleHealth vehicle)
    {
        if (currentStep == TrainingStep.StrikeDestroyTarget && vehicle.GetComponent<TargetObject>() != null)
        {
            NextStep(); // Переходим в SwitchBackToRecon
        }
    }

    private void HandleDroneSwitched(GameObject newDrone)
    {
        activeDroneTransform = newDrone.transform;
        UpdateDroneReferences(newDrone);

        var flight = newDrone.GetComponent<DroneFlightController>();

        // Переключились на ударный дрон
        if (currentStep == TrainingStep.SwitchToStrike && flight != null && flight.droneType == DroneType.StrikeFPV)
        {
            NextStep();
        }
        // Переключились обратно на разведчика
        else if (currentStep == TrainingStep.SwitchBackToRecon && flight != null && flight.droneType == DroneType.ReconDrop)
        {
            NextStep();
        }
    }

    private void UpdateDroneReferences(GameObject drone)
    {
        currentFlightController = drone.GetComponent<DroneFlightController>();
        currentLockManager = drone.GetComponentInChildren<TargetLockManager>();
    }

    // --- ЛОГИКА ШАГОВ И UI ---

    private void NextStep()
    {
        currentStep++;
        hoverTimer = 0f;

        if (activeDroneTransform != null) startY = activeDroneTransform.position.y;
        UpdateUI();
    }

    private void UpdateUI()
    {
        if (instructionText == null) return;

        // Добавляем цветовые теги для важных кнопок, чтобы они выделялись
        string cW = "<color=#00FF00>[W]</color>";
        string cS = "<color=#00FF00>[S]</color>";
        string cArrows = "<color=#00FF00>[Стрелки]</color>";
        string cAD = "<color=#00FF00>[A/D]</color>";

        switch (currentStep)
        {
            // --- ЭТАП 1: РАЗВЕДЧИК ---
            case TrainingStep.ReconTakeoff:
                instructionText.text = "ИНСТРУКТОР (Разведчик):\nВзлетите на высоту 5 метров.\n(Используйте " + cW + " / " + cS + ")";
                break;
            case TrainingStep.ReconHover:
                instructionText.text = "ИНСТРУКТОР (Разведчик):\nНажмите <color=#00FF00>[H]</color>, чтобы включить режим зависания.";
                break;
            case TrainingStep.ReconZoom:
                instructionText.text = "ИНСТРУКТОР (Разведчик):\nОптика готова. Нажмите <color=#00FF00>[Z]</color> или <color=#00FF00>[X]</color> для изменения зума.";
                break;
            case TrainingStep.ReconGimbal:
                instructionText.text = "ИНСТРУКТОР (Разведчик):\nПокрутите подвес камеры: <color=#00FF00>[R/F]</color> (вверх/вниз) и <color=#00FF00>[Q/E]</color> (влево/вправо).";
                break;
            case TrainingStep.ReconNightVision:
                instructionText.text = "ИНСТРУКТОР (Разведчик):\nНажмите <color=#00FF00>[N]</color>, чтобы протестировать ПНВ.";
                break;
            case TrainingStep.ReconTargetLock:
                instructionText.text = "ИНСТРУКТОР (Разведчик):\nНаведитесь на макет в конце ангара и нажмите <color=#00FF00>[Пробел]</color> для захвата.";
                break;
            case TrainingStep.SwitchToStrike:
                instructionText.text = "ИНСТРУКТОР:\nЦель отмечена! Нажмите <color=#00FF00>[2]</color>, чтобы переключиться на ударный FPV-дрон.";
                break;

            // --- ЭТАП 2: УДАРНЫЙ ДРОН (ПОЛОСА ПРЕПЯТСТВИЙ) ---
            case TrainingStep.StrikeFlyGates:
                if (gatesPassed == 0)
                {
                    instructionText.text = "<b>УРОК 1: ГАЗ И ТАНГАЖ</b>\n" +
                                           $"Держите {cW}, чтобы подняться. Чтобы лететь вперед, наклоните дрон стрелкой <color=#00FF00>[Вверх]</color>.";
                }
                else if (gatesPassed == 1)
                {
                    instructionText.text = "<b>УРОК 2: КРЕН И ПОВОРОТ</b>\n" +
                                           $"Используйте <color=#00FF00>[Влево/Вправо]</color> для наклона (крена).\n" +
                                           $"Используйте {cAD} для поворота корпуса (Yaw).\n" +
                                           $"<size=80%>Прогресс: {gatesPassed}/{totalGates}</size>";
                }
                else
                {
                    instructionText.text = "<b>УРОК 3: ТОРМОЖЕНИЕ</b>\n" +
                                           $"Чтобы сбросить скорость перед финальной целью, просто <color=#00FF00>отпустите стрелку [Вверх]</color> (или нажмите [Вниз], чтобы клюнуть носом).\n" +
                                           $"<size=80%>Прогресс: {gatesPassed}/{totalGates}</size>";
                }
                break;

            case TrainingStep.StrikeDestroyTarget:
                instructionText.text = "ИНСТРУКТОР (FPV):\nОтлично! Теперь разгонитесь и уничтожьте макет тараном.";
                break;
            case TrainingStep.SwitchBackToRecon:
                instructionText.text = "ИНСТРУКТОР:\nЦель поражена! Нажмите <color=#00FF00>[1]</color>, чтобы вернуться на зависший разведчик.";
                break;
            case TrainingStep.ReconObjectiveControl:
                instructionText.text = "ИНСТРУКТОР:\nОцените ущерб. Наведитесь на обломки и нажмите <color=#00FF00>[Пробел]</color>, чтобы сделать снимок.";
                break;
            case TrainingStep.Completed:
                instructionText.text = "ОБУЧЕНИЕ ЗАВЕРШЕНО!\nВы готовы к боевым вылетам.";
                break;
        }
    }
}