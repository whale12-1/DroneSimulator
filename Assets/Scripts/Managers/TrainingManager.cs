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
    [SerializeField] private TMP_Text subtitleText;
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
    private readonly System.Collections.Generic.HashSet<TrainingGate> countedGates = new System.Collections.Generic.HashSet<TrainingGate>();
    private bool isRespawning;
    private Coroutine respawnRoutine;
    private Coroutine subtitleRoutine;
    private bool lastHintsEnabled = true;
    private float hoverTimer = 0f;
    private float startY;
    private bool isTransitioning = false;

    private TargetLockManager currentLockManager;

    private void Start()
    {
        lastHintsEnabled = HintsEnabled();
        if (subtitleText != null) subtitleText.gameObject.SetActive(false);

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

        if (droneManager != null)
        {
            droneManager.OutOfBoundsStarted += HandleOutOfBoundsStarted;
            droneManager.OutOfBoundsEnded += HandleOutOfBoundsEnded;
        }
    }

    private void OnDisable()
    {
        TrainingGate.OnGatePassed -= HandleGatePassed;
        GameEvents.VehicleDestroyed -= HandleVehicleDestroyed;
        GameEvents.ModuleDestroyed -= HandleModuleDestroyed;
        GameEvents.DroneSwitched -= HandleDroneSwitched;
        GameEvents.DroneExploded -= HandleDroneExploded;

        if (droneManager != null)
        {
            droneManager.OutOfBoundsStarted -= HandleOutOfBoundsStarted;
            droneManager.OutOfBoundsEnded -= HandleOutOfBoundsEnded;
        }
    }

    private void Update()
    {
        if (isTransitioning || isRespawning) return;

        // MonoBehavour.Update не зависит от Time.timeScale: без этого guard'а
        // клавиши (H/Z/X/N/Space) двигали шаги обучения при открытой паузе.
        UpdateHintVisibility();

        if (Time.timeScale <= 0f) return;

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

        // Вход в шаг полосы препятствий: кольца, пройденные раньше (например,
        // разведчиком на пути к макету), не должны «сгорать», иначе 3/3 не набрать.
        if (currentStep == TrainingStep.StrikeFlyGates)
            ResetGateProgress();

        UpdateUI();
    }

    private IEnumerator TransitionToChapter(TrainingChapter nextChapter, string title, TrainingStep firstStepOfNextChapter)
    {
        isTransitioning = true;
        instructionText.text = "";

        if (screenFader != null)
            yield return StartCoroutine(screenFader.FadeOut(title));

        // Точечная очистка: гасим эффекты и звуки только уходящих глав,
        // а не все ParticleSystem/AudioSource в сцене (раньше сносились
        // и клоны взрывов, созданные в текущей главе).
        CleanupChapter(envChapter1);
        CleanupChapter(envChapter2);
        CleanupChapter(envChapter3);

        // Бесхозные рантайм-клоны эффектов (Instantiate без родителя) не попадают
        // в CleanupChapter: они висят в корне сцены. Пример — WildFire от макета
        // 1-й главы, который «уезжал» в 3-ю главу и висел в воздухе справа от танка.
        DestroyOrphanRootVfx();

        if (nextChapter == TrainingChapter.Completed)
        {
            // Финальный шаг выставляем явно: раньше он оставался Chapter3_DestroyModules,
            // поэтому текст «ОБУЧЕНИЕ ЗАВЕРШЕНО» не показывался никогда.
            currentChapter = TrainingChapter.Completed;
            currentStep = TrainingStep.Completed;
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

            // Замена борта в главе 2 — у дома, а не в начале трассы.
            if (droneManager != null && spawnPosChapter2 != null)
                droneManager.RespawnPoint = spawnPosChapter2;
        }
        else if (nextChapter == TrainingChapter.Chapter3_Mangal)
        {
            if (envChapter3 != null) envChapter3.SetActive(true);
            if (spawnPosChapter3 != null)
            {
                newSpawnPos = spawnPosChapter3.position;
                newSpawnRot = spawnPosChapter3.rotation;
            }

            // Замена борта в главе 3 — рядом с танком (раньше использовалась
            // точка старта трассы — 315 м до цели).
            if (droneManager != null && spawnPosChapter3 != null)
                droneManager.RespawnPoint = spawnPosChapter3;
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
        if (isTransitioning || isRespawning) return;

        if (currentStep == TrainingStep.StrikeFlyGates ||
            currentStep == TrainingStep.StrikeDestroyTarget ||
            currentStep == TrainingStep.Chapter2_Start ||
            currentStep == TrainingStep.Chapter2_DestroyIndoorTarget ||
            currentStep == TrainingStep.Chapter3_Start ||
            currentStep == TrainingStep.Chapter3_DestroyModules)
        {
            respawnRoutine = StartCoroutine(RespawnStrikeDroneRoutine());
        }
    }

    // Замена борта идёт на unscaled времени и не открывает панель краха:
    // прогресс текущей главы сохраняется, игрок получает новый борт.
    private IEnumerator RespawnStrikeDroneRoutine()
    {
        isRespawning = true;

        ShowSubtitle("<b>БОРТ ПОТЕРЯН</b> · ЗАМЕНА ЧЕРЕЗ 1.5 с", 1.5f);

        yield return new WaitForSecondsRealtime(1.5f);

        if (droneManager != null) droneManager.RespawnStrikeDrone();
        if (activeDroneTransform != null) startY = activeDroneTransform.position.y;

        // Кольца, пройденные до потери борта, не должны блокировать шаг полосы.
        if (currentStep == TrainingStep.StrikeFlyGates)
            ResetGateProgress();

        isRespawning = false;
        respawnRoutine = null;
        UpdateUI();
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

    private void HandleGatePassed(TrainingGate gate)
    {
        if (currentStep != TrainingStep.StrikeFlyGates) return;
        if (gate == null) return;

        // Каждое кольцо засчитывается один раз за заход; при сбросе прогресса
        // кольца возвращаются в исходное состояние, поэтому softlock невозможен.
        if (!countedGates.Add(gate)) return;

        gatesPassed = countedGates.Count;
        UpdateUI();
        if (gatesPassed >= totalGates) NextStep();
    }

    private void ResetGateProgress()
    {
        countedGates.Clear();
        gatesPassed = 0;

        foreach (TrainingGate gate in FindObjectsByType<TrainingGate>(FindObjectsSortMode.None))
        {
            if (gate != null) gate.ResetGate();
        }
    }

    private void UpdateDroneReferences(GameObject drone)
    {
        currentLockManager = drone.GetComponentInChildren<TargetLockManager>();
    }

    private bool HintsEnabled()
    {
        SettingsService service = SettingsService.Instance;
        if (service == null || service.Data == null || service.Data.gameplay == null) return true;

        return service.Data.gameplay.trainingHints;
    }

    private void UpdateHintVisibility()
    {
        bool enabled = HintsEnabled();
        if (enabled == lastHintsEnabled) return;

        lastHintsEnabled = enabled;
        UpdateUI();
    }

    // Радиопереговоры/уведомления инструктора: отдельный канал субтитров,
    // чтобы не затирать текущую задачу в InstructionText.
    private void ShowSubtitle(string message, float seconds)
    {
        if (subtitleText == null) return;

        SettingsService service = SettingsService.Instance;
        if (service != null && service.Data != null && service.Data.gameplay != null)
        {
            if (!service.Data.gameplay.subtitlesEnabled) return;
            subtitleText.fontSize = Mathf.Clamp(service.Data.gameplay.subtitleFontSize, 10, 40);
        }

        if (subtitleRoutine != null) StopCoroutine(subtitleRoutine);
        subtitleRoutine = StartCoroutine(SubtitleRoutine(message, Mathf.Max(0.5f, seconds)));
    }

    private IEnumerator SubtitleRoutine(string message, float seconds)
    {
        subtitleText.text = message;
        subtitleText.gameObject.SetActive(true);

        yield return new WaitForSecondsRealtime(seconds);

        subtitleText.text = "";
        subtitleText.gameObject.SetActive(false);
        subtitleRoutine = null;
    }

    private void HandleOutOfBoundsStarted()
    {
        ShowSubtitle("<b>СИГНАЛ ТЕРЯЕТСЯ</b>\nВернитесь в зону полигона", 3f);
    }

    private void HandleOutOfBoundsEnded()
    {
        ShowSubtitle("Сигнал восстановлен", 1.5f);
    }

    // Удаляем только корневые объекты-клоны с ParticleSystem: эффекты дронов и
    // Canvas не затрагиваются, потому что они дочерние.
    private static void DestroyOrphanRootVfx()
    {
        GameObject[] roots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();

        foreach (GameObject root in roots)
        {
            if (root == null) continue;
            if (!root.name.EndsWith("(Clone)", System.StringComparison.Ordinal)) continue;
            if (root.GetComponent<ParticleSystem>() == null) continue;

            Destroy(root);
        }
    }

    // Гасим только эффекты/звуки указанной главы (точечная очистка).
    // ParticleSystem.Clear вместо Stop: у систем с sub-emitters Unity пишет
    // предупреждение «Sub-emitters may not use stop actions». Сама глава
    // выключается через SetActive(false) сразу после — эмиссия и так встанет.
    private void CleanupChapter(GameObject chapterRoot)
    {
        if (chapterRoot == null) return;

        foreach (ParticleSystem particles in chapterRoot.GetComponentsInChildren<ParticleSystem>(true))
        {
            particles.Clear(true);
        }

        foreach (AudioSource audio in chapterRoot.GetComponentsInChildren<AudioSource>(true))
        {
            audio.Stop();
        }
    }

    private void UpdateUI()
    {
        if (instructionText == null) return;

        // Настройка «Подсказки обучения» скрывает текст инструктора.
        if (!HintsEnabled())
        {
            instructionText.text = "";
            return;
        }
        string cW = "<color=#DB143D>[W]</color>";
        string cS = "<color=#DB143D>[S]</color>";

        switch (currentStep)
        {
            // --- ГЛАВА 1 ---
            case TrainingStep.StartMotor:
                instructionText.text = "ИНСТРУКТОР:\nДля начала работы заведите мотор. Нажмите <color=#DB143D>[W]</color>.";
                break;

            case TrainingStep.DisableHover:
                instructionText.text = "ИНСТРУКТОР:\nДрон сейчас в режиме автозависания. Нажмите <color=#DB143D>[H]</color>, чтобы перейти на ручное управление газом.";
                break;

            case TrainingStep.ReconTakeoff:
                instructionText.text = "ИНСТРУКТОР:\nОтлично! Теперь взлетите на высоту 5 метров. (" + cW + " / " + cS + ")";
                break;

            case TrainingStep.ReconHover:
                instructionText.text = "ИНСТРУКТОР:\nНажмите <color=#DB143D>[H]</color> снова, если захотите включить режим зависания.";
                break;

            case TrainingStep.ReconZoom: instructionText.text = "ИНСТРУКТОР:\nОптика. Нажмите <color=#DB143D>[Z]</color> или <color=#DB143D>[X]</color>."; break;
            case TrainingStep.ReconGimbal: instructionText.text = "ИНСТРУКТОР:\nПокрутите камеру: <color=#DB143D>[R/F]</color> и <color=#DB143D>[Q/E]</color>."; break;
            case TrainingStep.ReconNightVision: instructionText.text = "ИНСТРУКТОР:\nПНВ. Нажмите <color=#DB143D>[N]</color>."; break;
            case TrainingStep.ReconTargetLock: instructionText.text = "ИНСТРУКТОР:\nНаведитесь на макет и нажмите <color=#DB143D>[Пробел]</color>."; break;
            case TrainingStep.SwitchToStrike: instructionText.text = "ИНСТРУКТОР:\nНажмите <color=#DB143D>[2]</color> для перехода на ударный дрон."; break;
            case TrainingStep.StrikeFlyGates: instructionText.text = "<b>ПОЛОСА ПРЕПЯТСТВИЙ</b>\nПролетите через кольца.\nПрогресс: " + gatesPassed + "/" + totalGates; break;
            case TrainingStep.StrikeDestroyTarget: instructionText.text = "ИНСТРУКТОР:\nУничтожьте макет тараном."; break;
            case TrainingStep.SwitchBackToRecon: instructionText.text = "ИНСТРУКТОР:\nНажмите <color=#DB143D>[1]</color>, чтобы вернуться на разведчик."; break;
            case TrainingStep.ReconObjectiveControl: instructionText.text = "ИНСТРУКТОР:\nНажмите <color=#DB143D>[Пробел]</color> для снимка обломков."; break;

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