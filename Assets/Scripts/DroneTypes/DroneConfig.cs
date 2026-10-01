using UnityEngine;

public enum WarheadType
{
    HEAT,         // Кумулятивная
    HE_Frag,      // Осколочно-фугасная
    Thermobaric   // Термобарическая
}

[CreateAssetMenu(fileName = "NewDroneConfig", menuName = "FPV/Drone Config")]
public class DroneConfig : ScriptableObject
{
    [Header("1. Идентификация и Боевая Часть")]
    public string droneName = "FPV-7 PG-7VL";
    public WarheadType warheadType = WarheadType.HEAT;
    public float directDamage = 250f;
    public float splashRadius = 1.5f;
    public float splashDamage = 40f;
    public GameObject explosionPrefab;
    public AudioClip explosionSound;

    [Header("2. Условия Детонации и Взрывателя")]
    public bool allowManualDetonation = true;
    public KeyCode detonateKey = KeyCode.Space;
    public bool explodeOnAnyCollision = false;
    public float minImpactVelocity = 3.0f;
    public string[] instantExplodeTags = new string[] { "Hazard" };

    [Header("3. Объективный Контроль")]
    public GameObject reconDronePrefab;
    public Vector3 reconOffset = new Vector3(0f, 35f, -20f);

    [Header("4. Механизм Сброса (для Bomb/Drop Дронов)")]
    public GameObject bombPrefab;
    public int maxAmmo = 4;
    public float dropCooldown = 1.0f;

    [Header("5. Сенсоры и Автозахват")]
    public bool allowTargetLock = true;              // Разрешен ли автозахват на этой модели
    public KeyCode targetLockKey = KeyCode.Space;    // Клавиша захвата/сброса цели
    public float maxLockScreenRadius = 300f;         // Макс. отклонение от центра экрана в пикселях (0 = без ограничений)
    public float detectionRadius = 300f;
    public float viewAngle = 70f;
    public float autoLockDistance = 1500f;

    [Header("6. Оптика и Ночное Видение (ПНВ)")]
    public bool hasNightVision = true;
    public KeyCode nvToggleKey = KeyCode.N;
    public float irRange = 300f;
    public float irSpotAngle = 60f;
    public float irIntensity = 5f;
    public Color nvColor = new Color(0.1f, 1.0f, 0.2f);
    public float nvPostExposure = 1.8f;

    [Header("7. Тряска FPV Камеры")]
    public bool enableCameraShake = true;            // Включение / выключение тряски
    public float shakeFrequency = 35f;               // Частота вибрации моторов
    public float shakePosAmplitude = 0.003f;         // Амплитуда смещения позиции (метры)
    public float shakeRotAmplitude = 0.2f;           // Амплитуда поворота (градусы)
    public bool shakeUseThrottleEffect = true;       // Увеличивать ли тряску при увеличении газа
    public float shakeMinThrottleFactor = 0.3f;      // Тряска на холостых оборотах

    [Header("8. Визуал HUD / OSD (Ударный FPV)")]
    public bool showStrikeHUD = true;
    public Color hudColor = new Color(0.1f, 1.0f, 0.2f, 0.85f); // Зеленый армейский цвет
    public float crosshairSize = 14f;
    public Vector2 targetBoxSize = new Vector2(160f, 110f);


    [Header("9. Настройки РЭБ и Помех Видеосигнала")]
    [Tooltip("Устойчивость к РЭБ от 0.0 (гражданский дрон) до 1.0 (полная защита/защищенная частота)")]
    [Range(0f, 1f)] public float ewResistance = 0f;

    [Tooltip("Разрешение процедурной текстуры белого шума ('снега')")]
    public int noiseTextureResolution = 128;

    [Tooltip("Максимальная прозрачность аналогового шума при 100% РЭБ")]
    public float staticNoiseOpacity = 0.85f;

    [Header("Интенсивность пост-эффектов при 100% помех")]
    public float maxChromaticAberration = 1.0f;
    public float maxVignette = 0.8f;
    public float maxFilmGrain = 1.0f;

    [Header("Пороги срабатывания искажений кадра")]
    [Tooltip("Минимальный уровень РЭБ для появления 'снега'")]
    public float ewNoiseThreshold = 0.05f;

    [Tooltip("Минимальный уровень РЭБ для срыва развертки и сбоев FOV")]
    public float ewJitterThreshold = 0.15f;

    [Tooltip("Минимальный уровень РЭБ для появления горизонтальных полос разрыва")]
    public float ewLineTearingThreshold = 0.3f;


    [Header("10. Настройки Камеры и Подвеса")]
    public CameraType cameraType = CameraType.StrikeFixed;
    public float fixedPitchAngle = 25f;

    [Header("Параметры Подвеса (для ReconGimbal)")]
    public float defaultGimbalPitch = 20f;
    public float minPitch = -10f;
    public float maxPitch = 85f;
    public float minYaw = -120f;
    public float maxYaw = 120f;
    public float tiltSpeed = 45f;

    [Header("11. Визуал Recon HUD и Дальномер")]
    public Color reconColor = new Color(1f, 0.85f, 0.2f, 0.9f);
    public float hudLineWidth = 2f;
    public float minBoxSize = 20f;
    public LayerMask lrfMask; // Земля, препятствия и техника для LRF


    [Header("12. Оптический Зум (Zoom)")]
    public bool allowZoom = true;
    public float[] zoomLevels = new float[] { 100f, 50f, 25f, 10f }; // FOV в градусах (1x, 2x, 4x, 10x)
    public float zoomSpeed = 10f;
    public KeyCode zoomInKey = KeyCode.Z;
    public KeyCode zoomOutKey = KeyCode.X;
}