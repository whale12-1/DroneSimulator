using UnityEngine;

public enum DroneType
{
    StrikeFPV, // Ударный FPV: резкий, ручное управление (Acro Mode по умолчанию)
    ReconDrop  // Разведчик/Сбросник: плавная стабилизация, автогоризонт (Angle/Hover)
}

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(DroneMotors))]
public class DroneFlightController : MonoBehaviour
{
    private Rigidbody rb;
    private DroneMotors motors;
    private EWReceiver ewReceiver;

    [Header("Профиль Дрона")]
    public DroneType droneType = DroneType.StrikeFPV;

    [Header("Режим Полета")]
    public bool angleMode = false;
    public bool hoverMode = false;
    public float maxAngle = 35f;

    [Header("PID Настройки")]
    public PIDController pitchPID = new PIDController(2.5f, 0.05f, 0.4f);
    public PIDController rollPID = new PIDController(2.5f, 0.05f, 0.4f);
    public PIDController yawPID = new PIDController(2.0f, 0.0f, 0.3f);

    [Header("Чувствительность Управления (Rates в Deg/Sec)")]
    [SerializeField] private float maxPitchRate = 450f;
    [SerializeField] private float maxRollRate = 450f;
    [SerializeField] private float maxYawRate = 250f;

    [Header("Влияние РЭБ")]
    [SerializeField] private bool enableEWImpact = true;

    private float targetPitchInput;
    private float targetRollInput;
    private float targetYawInput;
    private float throttleInput = 0.5f;

    private float rawPitch, rawRoll, rawYaw, rawThrottle;
    private float packetTimer;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        motors = GetComponent<DroneMotors>();

        ewReceiver = GetComponentInChildren<EWReceiver>();
        if (ewReceiver == null) ewReceiver = GetComponent<EWReceiver>();

        // Авто-настройка параметров под выбранный тип дрона
        ApplyDroneProfile();
    }

    public void ApplyDroneProfile()
    {
        if (droneType == DroneType.ReconDrop)
        {
            angleMode = true;       // Разведчик/Сбросник всегда держит горизонт
            hoverMode = true;       // По умолчанию стартует в зависании
            maxAngle = 50f;         // Плавные наклоны для точного прицеливания

            maxPitchRate = 250f;    // Более плавные повороты
            maxRollRate = 250f;
            maxYawRate = 200f;
        }
    }

    private void Update()
    {
        ReadInput();
    }

    private void FixedUpdate()
    {
        CalculateFlightPhysics();
    }

    private void ReadInput()
    {
        if (Input.GetKeyDown(KeyCode.H))
        {
            hoverMode = !hoverMode;
            if (hoverMode) angleMode = true;
        }

        if (Input.GetKeyDown(KeyCode.M) && droneType == DroneType.StrikeFPV)
        {
            angleMode = !angleMode;
            if (!angleMode) hoverMode = false;
        }

        if (hoverMode)
        {
            targetPitchInput = 0f;
            targetRollInput = 0f;
            targetYawInput = 0f;

            // Если зажаты клавиши движения — временно снимаем с удержания позиции
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.S) ||
                Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.D) ||
                Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.DownArrow) ||
                Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.RightArrow))
            {
                hoverMode = false;
            }
        }

        float jam = (enableEWImpact && ewReceiver != null) ? ewReceiver.jamIntensity : 0f;

        float packetInterval = Mathf.Lerp(0f, 0.22f, jam);
        packetTimer += Time.deltaTime;

        if (packetTimer >= packetInterval)
        {
            packetTimer = 0f;

            rawPitch = 0f;
            if (Input.GetKey(KeyCode.UpArrow)) rawPitch = 1f;
            if (Input.GetKey(KeyCode.DownArrow)) rawPitch = -1f;

            rawRoll = 0f;
            if (Input.GetKey(KeyCode.RightArrow)) rawRoll = 1f;
            if (Input.GetKey(KeyCode.LeftArrow)) rawRoll = -1f;

            rawYaw = 0f;
            if (Input.GetKey(KeyCode.D)) rawYaw = 1f;
            if (Input.GetKey(KeyCode.A)) rawYaw = -1f;

            rawThrottle = throttleInput;
            if (Input.GetKey(KeyCode.W))
                rawThrottle = Mathf.MoveTowards(rawThrottle, 1.0f, Time.deltaTime * 1.5f);
            else if (Input.GetKey(KeyCode.S))
                rawThrottle = Mathf.MoveTowards(rawThrottle, 0.0f, Time.deltaTime * 1.5f);
        }

        float noiseTime = Time.time * 2.5f;
        float driftPitch = (Mathf.PerlinNoise(noiseTime, 0f) - 0.5f) * 0.5f * jam;
        float driftRoll = (Mathf.PerlinNoise(0f, noiseTime) - 0.5f) * 0.5f * jam;
        float driftYaw = (Mathf.PerlinNoise(noiseTime, noiseTime) - 0.5f) * 0.6f * jam;

        float responseSpeed = Mathf.Lerp(30f, 2.5f, jam);
        targetPitchInput = Mathf.Lerp(targetPitchInput, rawPitch + driftPitch, Time.deltaTime * responseSpeed);
        targetRollInput = Mathf.Lerp(targetRollInput, rawRoll + driftRoll, Time.deltaTime * responseSpeed);
        targetYawInput = Mathf.Lerp(targetYawInput, rawYaw + driftYaw, Time.deltaTime * responseSpeed);

        float throttleSag = (jam > 0.3f && Mathf.PerlinNoise(noiseTime * 4f, 50f) < jam * 0.4f) ? (1f - jam * 0.35f) : 1f;
        throttleInput = rawThrottle * throttleSag;
    }

    private void CalculateFlightPhysics()
    {
        float jam = (enableEWImpact && ewReceiver != null) ? ewReceiver.jamIntensity : 0f;

        float totalGravityForce = rb.mass * Mathf.Abs(Physics.gravity.y);
        float maxTotalThrust = 40f;
        float hoverBase = totalGravityForce / maxTotalThrust;

        float effectiveThrottle;

        if (hoverMode)
        {
            // Автоматическая подстройка тяги под удержание вертикальной скорости (Altitude Damp)
            float verticalDamping = -rb.linearVelocity.y * 0.25f;
            effectiveThrottle = Mathf.Clamp(hoverBase + verticalDamping, 0.0f, 0.85f);

            // Торможение горизонтального дрейфа
            Vector3 horizontalVel = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
            rb.AddForce(-horizontalVel * 5.0f, ForceMode.Acceleration);
        }
        else
        {
            if (throttleInput >= 0.5f)
                effectiveThrottle = Mathf.Lerp(hoverBase, 1.0f, (throttleInput - 0.5f) * 2f);
            else
                effectiveThrottle = Mathf.Lerp(0.0f, hoverBase, throttleInput * 2f);
        }

        Vector3 localAngularVel = transform.InverseTransformDirection(rb.angularVelocity) * Mathf.Rad2Deg;

        if (jam > 0.2f)
        {
            float gyroNoiseX = (Random.value - 0.5f) * 40f * jam;
            float gyroNoiseY = (Random.value - 0.5f) * 25f * jam;
            float gyroNoiseZ = (Random.value - 0.5f) * 40f * jam;
            localAngularVel += new Vector3(gyroNoiseX, gyroNoiseY, gyroNoiseZ);
        }

        float targetPitchRate;
        float targetRollRate;
        float targetYawRate = targetYawInput * maxYawRate;

        if (angleMode)
        {
            float currentPitch = transform.localEulerAngles.x;
            if (currentPitch > 180f) currentPitch -= 360f;

            float currentRoll = transform.localEulerAngles.z;
            if (currentRoll > 180f) currentRoll -= 360f;

            float desiredPitch = hoverMode ? 0f : targetPitchInput * maxAngle;
            float desiredRoll = hoverMode ? 0f : targetRollInput * maxAngle;

            float responseSpeed = hoverMode ? 6.0f : 4.0f;
            targetPitchRate = (desiredPitch - currentPitch) * responseSpeed;
            targetRollRate = (desiredRoll - currentRoll) * responseSpeed;
        }
        else
        {
            targetPitchRate = targetPitchInput * maxPitchRate;
            targetRollRate = targetRollInput * maxRollRate;
        }

        float pitchError = targetPitchRate - localAngularVel.x;
        float rollError = targetRollRate - localAngularVel.z;
        float yawError = targetYawRate - localAngularVel.y;

        float pitchCorr = Mathf.Clamp(pitchPID.Update(pitchError, Time.fixedDeltaTime), -0.8f, 0.8f);
        float rollCorr = Mathf.Clamp(rollPID.Update(rollError, Time.fixedDeltaTime), -0.8f, 0.8f);
        float yawCorr = Mathf.Clamp(yawPID.Update(yawError, Time.fixedDeltaTime), -0.5f, 0.5f);

        rb.AddRelativeTorque(Vector3.up * yawCorr * 8.0f, ForceMode.Force);

        float motorFL = effectiveThrottle - pitchCorr + rollCorr;
        float motorFR = effectiveThrottle - pitchCorr - rollCorr;
        float motorBL = effectiveThrottle + pitchCorr + rollCorr;
        float motorBR = effectiveThrottle + pitchCorr - rollCorr;

        motors.ApplyMotorsThrust(motorFL, motorFR, motorBL, motorBR);
    }
}