using UnityEngine;
using TMPro;
using System.Collections;

public class TrainingManager : MonoBehaviour
{
    public enum TrainingChapter { Chapter1_Basics, Chapter2_IndoorTarget, Chapter3_Mangal, Completed }

    public enum TrainingStep
    {
        // --- ГЛАВА 1 ---
        StartMotor,   // <--- НОВЫЙ ШАГ: Запуск мотора (W)
        DisableHover, // <--- Отключение автозависания (H)
        ReconTakeoff, // <--- Взлет (W)
        ReconHover,   // <--- Включение зависания (H) - опционально
        ReconZoom,
        ReconGimbal,
        ReconNightVision,
        ReconTargetLock,
        SwitchToStrike,
        StrikeFlyGates,
        StrikeDestroyTarget,
        SwitchBackToRecon,
        ReconObjectiveControl,

        // --- ГЛАВА 2 ---
        Chapter2_Start,
        Chapter2_DestroyIndoorTarget,

        // --- ГЛАВА 3 ---
        Chapter3_Start,
        Chapter3_DestroyModules,

        // --- ФИНАЛ ---
        Completed
    }

    [Header("Ссылки")]
    [SerializeField] private Transform activeDroneTransform;
    [SerializeField] private TMP_Text instructionText;
    [SerializeField] private ScreenFader screenFader;
    [SerializeField] private DroneManager droneManager;

    [Header("Окружение Глав")]
    [SerializeField] private GameObject envChapter1;
    [SerializeField] private GameObject envChapter2;
    [SerializeField] private GameObject envChapter3;

    [Header("Точки спавна для Глав")]
    [SerializeField] private Transform spawnPosChapter2;
    [SerializeField] private Transform spawnPosChapter3;

    [Header("Глава 3: Мангал")]
    [SerializeField] private int totalModulesToDestroy = 3;
    private int destroyedModulesCount = 0;

    [Header("Настройки")]
    [SerializeField] private float targetHeight = 5f;
    [SerializeField] private float heightTolerance = 1.5f;
    [SerializeField] private int totalGates = 3;

    private TrainingChapter currentChapter = TrainingChapter.Chapter1_Basics;
    private TrainingStep currentStep = TrainingStep.StartMotor; // <--- Начинаем с запуска мотора

    private int gatesPassed = 0;
    private float hoverTimer = 0f;
    private float startY;
    private bool isTransitioning = false;

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
        GameEvents.ModuleDestroyed += HandleModuleDestroyed;
        GameEvents.DroneSwitched += HandleDroneSwitched;
        GameEvents.DroneExploded += HandleDroneExploded;
    }

    private void OnDisable()
    {
        TrainingGate.OnGatePassed -= HandleGatePassed;
        GameEvents.VehicleDestroyed -= HandleVehicleDestroyed;
        GameEvents.ModuleDestroyed -= HandleModuleDestroyed;
        GameEvents.DroneSwitched -= HandleDroneSwitched;
        GameEvents.DroneExploded -= HandleDroneExploded;
    }

    private void Update()
    {
        if (isTransitioning) return;

        switch (currentStep)
        {
            case TrainingStep.StartMotor:
                // Ждем нажатия W для запуска мотора
                if (Input.GetKeyDown(KeyCode.W)) NextStep();
                break;

            case TrainingStep.DisableHover:
                // Ждем H для отключения автопилота
                if (Input.GetKeyDown(KeyCode.H)) NextStep();
                break;

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

            case TrainingStep.ReconObjectiveControl:
                if (Input.GetKeyDown(KeyCode.Space)) NextStep();
                break;
        }
    }

    private void NextStep()
    {
        if (isTransitioning) return;

        if (currentStep == TrainingStep.ReconObjectiveControl)
        {
            StartCoroutine(TransitionToChapter(TrainingChapter.Chapter2_IndoorTarget, "ГЛАВА 2\nЦель в помещении", TrainingStep.Chapter2_Start));
            return;
        }
        if (currentStep == TrainingStep.Chapter2_DestroyIndoorTarget)
        {
            StartCoroutine(TransitionToChapter(TrainingChapter.Chapter3_Mangal, "ГЛАВА 3\nБронированные цели", TrainingStep.Chapter3_Start));
            return;
        }
        if (currentStep == TrainingStep.Chapter3_DestroyModules && destroyedModulesCount >= totalModulesToDestroy)
        {
            StartCoroutine(TransitionToChapter(TrainingChapter.Completed, "ОБУЧЕНИЕ ЗАВЕРШЕНО!", TrainingStep.Chapter3_DestroyModules));
            return;
        }

        currentStep = (TrainingStep)((int)currentStep + 1);
        hoverTimer = 0f;
        if (activeDroneTransform != null) startY = activeDroneTransform.position.y;
        UpdateUI();
    }

    private IEnumerator TransitionToChapter(TrainingChapter nextChapter, string title, TrainingStep firstStepOfNextChapter)
    {
        isTransitioning = true;
        instructionText.text = "";

        if (screenFader != null)
            yield return StartCoroutine(screenFader.FadeOut(title));

        // Очистка VFX и звуков
        ParticleSystem[] allParticles = FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None);
        foreach (var ps in allParticles)
        {
            if (ps.GetComponentInParent<Canvas>() == null) Destroy(ps.gameObject);
        }

        AudioSource[] allAudio = FindObjectsByType<AudioSource>(FindObjectsSortMode.None);
        foreach (var audio in allAudio)
        {
            if (audio.playOnAwake && audio.loop == false) audio.Stop();
        }

        if (nextChapter == TrainingChapter.Completed)
        {
            UpdateUI();
            if (screenFader != null) yield return StartCoroutine(screenFader.FadeIn());

            MissionController mc = FindFirstObjectByType<MissionController>();
            if (mc != null) mc.CompleteMission(MissionResult.Success, totalModulesToDestroy);
            else
            {
                MissionContext.MarkResult(MissionResult.Success, totalModulesToDestroy, Time.time);
                GameStateMachine.TransitionTo(GameState.Debrief);
            }
            yield break;
        }

        if (envChapter1 != null) envChapter1.SetActive(false);
        if (envChapter2 != null) envChapter2.SetActive(false);
        if (envChapter3 != null) envChapter3.SetActive(false);

        Vector3 newSpawnPos = Vector3.zero;
        Quaternion newSpawnRot = Quaternion.identity;

        if (nextChapter == TrainingChapter.Chapter2_IndoorTarget)
        {
            if (envChapter2 != null) envChapter2.SetActive(true);
            if (spawnPosChapter2 != null)
            {
                newSpawnPos = spawnPosChapter2.position;
                newSpawnRot = spawnPosChapter2.rotation;
            }
        }
        else if (nextChapter == TrainingChapter.Chapter3_Mangal)
        {
            if (envChapter3 != null) envChapter3.SetActive(true);
            if (spawnPosChapter3 != null)
            {
                newSpawnPos = spawnPosChapter3.position;
                newSpawnRot = spawnPosChapter3.rotation;
            }
        }

        currentChapter = nextChapter;
        currentStep = firstStepOfNextChapter;

        if (nextChapter != TrainingChapter.Chapter1_Basics && droneManager != null)
        {
            droneManager.RespawnStrikeDroneAt(newSpawnPos, newSpawnRot);
            yield return null;
        }

        UpdateUI();
        if (screenFader != null)
            yield return StartCoroutine(screenFader.FadeIn());

        isTransitioning = false;
    }

    private void HandleVehicleDestroyed(VehicleHealth vehicle)
    {
        if (vehicle.GetComponent<TargetObject>() == null) return;

        if (currentStep == TrainingStep.StrikeDestroyTarget) NextStep();
        else if (currentChapter == TrainingChapter.Chapter2_IndoorTarget &&
                (currentStep == TrainingStep.Chapter2_Start || currentStep == TrainingStep.Chapter2_DestroyIndoorTarget))
        {
            StartCoroutine(TransitionToChapter(TrainingChapter.Chapter3_Mangal, "ГЛАВА 3\nБронированные цели", TrainingStep.Chapter3_Start));
        }
    }

    private void HandleModuleDestroyed(VehicleModule module)
    {
        if (currentChapter == TrainingChapter.Chapter3_Mangal)
        {
            currentStep = TrainingStep.Chapter3_DestroyModules;
            destroyedModulesCount++;
            UpdateUI();

            if (destroyedModulesCount >= totalModulesToDestroy) NextStep();
        }
    }

    private void HandleDroneExploded(Vector3 pos, MonoBehaviour drone)
    {
        if (isTransitioning) return;

        if (currentStep == TrainingStep.StrikeFlyGates ||
            currentStep == TrainingStep.StrikeDestroyTarget ||
            currentStep == TrainingStep.Chapter2_Start ||
            currentStep == TrainingStep.Chapter2_DestroyIndoorTarget ||
            currentStep == TrainingStep.Chapter3_Start ||
            currentStep == TrainingStep.Chapter3_DestroyModules)
        {
            Invoke(nameof(RespawnStrikeDrone), 1.5f);
        }
    }

    private void RespawnStrikeDrone()
    {
        if (droneManager != null) droneManager.RespawnStrikeDrone();
    }

    private void HandleDroneSwitched(GameObject newDrone)
    {
        activeDroneTransform = newDrone.transform;
        UpdateDroneReferences(newDrone);

        var flight = newDrone.GetComponent<DroneFlightController>();
        if (currentStep == TrainingStep.SwitchToStrike && flight != null && flight.droneType == DroneType.StrikeFPV) NextStep();
        else if (currentStep == TrainingStep.SwitchBackToRecon && flight != null && flight.droneType == DroneType.ReconDrop) NextStep();
    }

    private bool CheckHeight(float targetH)
    {
        if (activeDroneTransform == null) return false;
        return (activeDroneTransform.position.y - startY) >= targetH - heightTolerance;
    }

    private void HandleGatePassed()
    {
        if (currentStep != TrainingStep.StrikeFlyGates) return;
        gatesPassed++;
        UpdateUI();
        if (gatesPassed >= totalGates) NextStep();
    }

    private void UpdateDroneReferences(GameObject drone)
    {
        currentLockManager = drone.GetComponentInChildren<TargetLockManager>();
    }

    private void UpdateUI()
    {
        if (instructionText == null) return;
        string cW = "<color=#00FF00>[W]</color>";
        string cS = "<color=#00FF00>[S]</color>";

        switch (currentStep)
        {
            // --- ГЛАВА 1 ---
            case TrainingStep.StartMotor:
                instructionText.text = "ИНСТРУКТОР:\nДля начала работы заведите мотор. Нажмите <color=#00FF00>[W]</color>.";
                break;

            case TrainingStep.DisableHover:
                instructionText.text = "ИНСТРУКТОР:\nДрон сейчас в режиме автозависания. Нажмите <color=#00FF00>[H]</color>, чтобы перейти на ручное управление газом.";
                break;

            case TrainingStep.ReconTakeoff:
                instructionText.text = "ИНСТРУКТОР:\nОтлично! Теперь взлетите на высоту 5 метров. (" + cW + " / " + cS + ")";
                break;

            case TrainingStep.ReconHover:
                instructionText.text = "ИНСТРУКТОР:\nНажмите <color=#00FF00>[H]</color> снова, если захотите включить режим зависания.";
                break;

            case TrainingStep.ReconZoom: instructionText.text = "ИНСТРУКТОР:\nОптика. Нажмите <color=#00FF00>[Z]</color> или <color=#00FF00>[X]</color>."; break;
            case TrainingStep.ReconGimbal: instructionText.text = "ИНСТРУКТОР:\nПокрутите камеру: <color=#00FF00>[R/F]</color> и <color=#00FF00>[Q/E]</color>."; break;
            case TrainingStep.ReconNightVision: instructionText.text = "ИНСТРУКТОР:\nПНВ. Нажмите <color=#00FF00>[N]</color>."; break;
            case TrainingStep.ReconTargetLock: instructionText.text = "ИНСТРУКТОР:\nНаведитесь на макет и нажмите <color=#00FF00>[Пробел]</color>."; break;
            case TrainingStep.SwitchToStrike: instructionText.text = "ИНСТРУКТОР:\nНажмите <color=#00FF00>[2]</color> для перехода на ударный дрон."; break;
            case TrainingStep.StrikeFlyGates: instructionText.text = "<b>ПОЛОСА ПРЕПЯТСТВИЙ</b>\nПролетите через кольца.\nПрогресс: " + gatesPassed + "/" + totalGates; break;
            case TrainingStep.StrikeDestroyTarget: instructionText.text = "ИНСТРУКТОР:\nУничтожьте макет тараном."; break;
            case TrainingStep.SwitchBackToRecon: instructionText.text = "ИНСТРУКТОР:\nНажмите <color=#00FF00>[1]</color>, чтобы вернуться на разведчик."; break;
            case TrainingStep.ReconObjectiveControl: instructionText.text = "ИНСТРУКТОР:\nНажмите <color=#00FF00>[Пробел]</color> для снимка обломков."; break;

            // --- ГЛАВА 2 ---
            case TrainingStep.Chapter2_Start: instructionText.text = "<b>ГЛАВА 2: ТОЧНОСТЬ</b>\nВы на ударном дроне. Найдите дом впереди и залетите ВНУТРЬ через открытое окно или дверь."; break;
            case TrainingStep.Chapter2_DestroyIndoorTarget: instructionText.text = "<b>ЗАДАЧА:</b>\nНайдите склад боеприпасов внутри дома и уничтожьте его."; break;

            // --- ГЛАВА 3 ---
            case TrainingStep.Chapter3_Start: instructionText.text = "<b>ГЛАВА 3: БРОНЯ</b>\nПеред вами техника с защитной сеткой (мангал). Лоб и верх защищены. Цельтесь в двигатель или гусеницы. Дроны бесконечны."; break;
            case TrainingStep.Chapter3_DestroyModules: instructionText.text = $"<b>ЗАДАЧА:</b>\nУничтожьте уязвимые части техники!\nПрогресс: {destroyedModulesCount} / {totalModulesToDestroy}"; break;

            case TrainingStep.Completed: instructionText.text = "ОБУЧЕНИЕ ЗАВЕРШЕНО!\nВы готовы к боевым вылетам."; break;
        }
    }
}