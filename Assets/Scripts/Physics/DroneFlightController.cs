using UnityEngine;

public enum DroneType
{
    StrikeFPV,  // Ударный FPV: чистый Acro, без авто-выравнивания и зависания
    ReconDrop   // Разведчик/сбросник: Angle/Hover, плавное управление
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

    [Header("Режим Полета (активен только у ReconDrop)")]
    public bool angleMode = false;
    public bool hoverMode = false;
    public float maxAngle = 35f;

    [Header("PID Настройки")]
    public PIDController pitchPID = new PIDController(2.5f, 0.05f, 0.4f);
    public PIDController rollPID = new PIDController(2.5f, 0.05f, 0.4f);
    public PIDController yawPID = new PIDController(2.0f, 0.0f, 0.3f);

    [Header("Скорость Вращения (Deg/Sec)")]
    [SerializeField] private float maxPitchRate = 200f;
    [SerializeField] private float maxRollRate = 200f;
    [SerializeField] private float maxYawRate = 140f;

    [Header("Аэродинамика")]
    [SerializeField] private float forwardDrag = 0.35f;
    [SerializeField] private float lateralDrag = 1.5f;
    [SerializeField] private float verticalDrag = 0.5f;
    [SerializeField] private float dragMultiplier = 1f;
    [SerializeField] private float maxSpeed = 55f;

    [Header("Газ")]
    [SerializeField] private float throttleSpeed = 1.8f;

    private float stickSpeed = 4f;
    private float centeringSpeed = 8f;
    private float throttleRate = 1.8f;
    private int throttleMode = 1;
    private bool invertPitch;

    [Header("Влияние РЭБ")]
    [SerializeField] private bool enableEWImpact = true;

    [Header("Состояние")]
    public bool isInputLocked = false;

    private float targetPitchInput;
    private float targetRollInput;
    private float targetYawInput;
    private float throttleInput;

    [Header("Арминг (Заведение мотора)")]
    public bool isMotorStarted = false;
    public float invulnerabilityTime = 4f; // 4 секунды неуязвимости
    [HideInInspector] public float currentInvulnTimer = 0f;
    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        motors = GetComponent<DroneMotors>();

        ewReceiver = GetComponentInChildren<EWReceiver>();
        if (ewReceiver == null) ewReceiver = GetComponent<EWReceiver>();

        ApplyDroneProfile();
    }

    public void ApplyDroneProfile()
    {
        if (droneType == DroneType.ReconDrop)
        {
            angleMode = true;
            hoverMode = true;
            maxAngle = 50f;
            maxPitchRate = 150f;
            maxRollRate  = 150f;
            maxYawRate   = 120f;
            throttleInput = 0.5f;
        }
        else // StrikeFPV
        {
            angleMode = false;
            hoverMode = false;
            maxPitchRate = 220f;
            maxRollRate  = 220f;
            maxYawRate   = 140f;
            throttleInput = 0f; // Стартуем с нуля, дрон падает
        }
    }

    private void Update()
    {
        ReadInput();
    }

    private void FixedUpdate()
    {
        ApplyAerodynamics();
        ApplyThrustAndRotation();
    }

    private void ReadInput()
    {
        RefreshInputSettings();

        if (!isMotorStarted)
        {
            // Заводим мотор по нажатию Пробела (или можешь поменять на W)
            if (GameInput.MotorStartPressedThisFrame)
            {
                isMotorStarted = true;
                currentInvulnTimer = invulnerabilityTime; // Запускаем таймер на 4 секунды
                Debug.Log("[ДРОН] Мотор заведен! Неуязвимость на 4 секунды.");
            }
            else
            {
                // Пока мотор не заведен, дрон не слушается стиков и висит на месте
                return;
            }
        }

        if (isInputLocked)
        {
            targetPitchInput = Mathf.MoveTowards(targetPitchInput, 0f, Time.deltaTime * 8f);
            targetRollInput = Mathf.MoveTowards(targetRollInput, 0f, Time.deltaTime * 8f);
            targetYawInput = Mathf.MoveTowards(targetYawInput, 0f, Time.deltaTime * 8f);

            if (droneType == DroneType.ReconDrop)
            {
                hoverMode = true;
                angleMode = true;
            }
            throttleInput = Mathf.MoveTowards(throttleInput, 0.5f, Time.deltaTime * throttleRate);
            return;
        }

        if (droneType == DroneType.ReconDrop && GameInput.HoverTogglePressedThisFrame)
        {
            hoverMode = !hoverMode;
            if (hoverMode) angleMode = true;
        }

        if (droneType == DroneType.ReconDrop && GameInput.AngleTogglePressedThisFrame)
        {
            angleMode = !angleMode;
            if (!angleMode) hoverMode = false;
        }

        float pitchAxis = GameInput.PitchAxis;
        if (invertPitch) pitchAxis = -pitchAxis;

        UpdateStickRate(ref targetPitchInput, pitchAxis);
        UpdateStickRate(ref targetRollInput, GameInput.RollAxis);
        UpdateStickRate(ref targetYawInput, GameInput.YawAxis);

        // Читаем газ ТОЛЬКО если мы не в режиме зависания
        if (!hoverMode)
        {
            float throttleAxis = GameInput.ThrottleAxis;

            if (throttleAxis > 0f)
                throttleInput = Mathf.MoveTowards(throttleInput, 1f, Time.deltaTime * throttleRate);
            else if (throttleAxis < 0f)
                throttleInput = Mathf.MoveTowards(throttleInput, 0f, Time.deltaTime * throttleRate);
            else if (throttleMode == 0)
                throttleInput = Mathf.MoveTowards(throttleInput, 0.5f, Time.deltaTime * throttleRate);

            // УБРАН БЛОК ELSE! Теперь газ остается там, где ты его оставил.
        }

        float jam = (enableEWImpact && ewReceiver != null) ? ewReceiver.jamIntensity : 0f;
        if (jam > 0f)
        {
            float noiseTime = Time.time * 2.5f;
            float driftPitch = (Mathf.PerlinNoise(noiseTime, 0f) - 0.5f) * 0.6f * jam;
            float driftRoll = (Mathf.PerlinNoise(0f, noiseTime) - 0.5f) * 0.6f * jam;
            float driftYaw = (Mathf.PerlinNoise(noiseTime, noiseTime) - 0.5f) * 0.8f * jam;

            targetPitchInput = Mathf.Clamp(targetPitchInput + driftPitch * Time.deltaTime, -1f, 1f);
            targetRollInput  = Mathf.Clamp(targetRollInput  + driftRoll  * Time.deltaTime, -1f, 1f);
            targetYawInput   = Mathf.Clamp(targetYawInput   + driftYaw   * Time.deltaTime, -1f, 1f);
        }
    }

    private void UpdateStickRate(ref float value, float axis)
    {
        if (Mathf.Abs(axis) > 0.01f)
            value = Mathf.MoveTowards(value, Mathf.Sign(axis), Time.deltaTime * stickSpeed);
        else
            value = Mathf.MoveTowards(value, 0f, Time.deltaTime * centeringSpeed);
    }

    private void RefreshInputSettings()
    {
        SettingsService service = SettingsService.Instance;
        if (service == null || service.Data == null || service.Data.controls == null)
        {
            stickSpeed = 4f;
            centeringSpeed = 8f;
            throttleRate = throttleSpeed;
            throttleMode = 1;
            invertPitch = false;
            return;
        }

        DroneSimulator.Settings.ControlSettings controls = service.Data.controls;

        stickSpeed = Mathf.Clamp(controls.droneStickSpeed, 0.5f, 20f);
        centeringSpeed = Mathf.Clamp(controls.droneCenteringSpeed, 0.5f, 30f);
        throttleRate = Mathf.Clamp(controls.throttleSpeed, 0.1f, 10f);
        throttleMode = controls.throttleMode;
        invertPitch = controls.droneInvertY;
    }

    private void ApplyAerodynamics()
    {
        Vector3 worldVel = rb.linearVelocity;
        Vector3 localVel = transform.InverseTransformDirection(worldVel);

        Vector3 localDrag = new Vector3(
            -localVel.x * lateralDrag,
            -localVel.y * verticalDrag,
            -localVel.z * forwardDrag
        ) * (rb.mass * dragMultiplier);

        rb.AddForce(transform.TransformDirection(localDrag), ForceMode.Force);

        if (maxSpeed > 0f && worldVel.sqrMagnitude > maxSpeed * maxSpeed)
            rb.linearVelocity = worldVel.normalized * maxSpeed;
    }

    private void ApplyThrustAndRotation()
    {
        if (currentInvulnTimer > 0f)
        {
            currentInvulnTimer -= Time.fixedDeltaTime;
        }

        float jam = (enableEWImpact && ewReceiver != null) ? ewReceiver.jamIntensity : 0f;

        float totalGravityForce = rb.mass * Mathf.Abs(Physics.gravity.y);
        float maxTotalThrust = motors.MaxTotalThrust;
        float hoverBase = maxTotalThrust > 0f ? totalGravityForce / maxTotalThrust : 0.5f;

        float effectiveThrottle;

        if (hoverMode)
        {
            float verticalDamping = -rb.linearVelocity.y * 0.25f;
            effectiveThrottle = Mathf.Clamp(hoverBase + verticalDamping, 0f, 0.9f);

            Vector3 horizontalVel = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
            rb.AddForce(-horizontalVel * 5f, ForceMode.Acceleration);
        }
        else
        {
            effectiveThrottle = ComputeThrottleCurve(throttleInput, hoverBase, jam);
        }

        Vector3 localAngularVel = transform.InverseTransformDirection(rb.angularVelocity) * Mathf.Rad2Deg;

        if (jam > 0.2f)
        {
            localAngularVel += new Vector3(
                (Random.value - 0.5f) * 40f * jam,
                (Random.value - 0.5f) * 25f * jam,
                (Random.value - 0.5f) * 40f * jam
            );
        }

        float targetPitchRate;
        float targetRollRate;
        float targetYawRate = targetYawInput * maxYawRate;

        if (angleMode)
        {
            float currentPitch = NormalizeAngle(transform.localEulerAngles.x);
            float currentRoll = NormalizeAngle(transform.localEulerAngles.z);

            float desiredPitch = hoverMode ? 0f : targetPitchInput * maxAngle;
            float desiredRoll = hoverMode ? 0f : targetRollInput  * maxAngle;

            float response = hoverMode ? 6f : 4f;
            targetPitchRate = (desiredPitch - currentPitch) * response;
            targetRollRate  = (desiredRoll  - currentRoll)  * response;
        }
        else
        {
            targetPitchRate = targetPitchInput * maxPitchRate;
            targetRollRate  = targetRollInput  * maxRollRate;
        }

        float pitchError = targetPitchRate - localAngularVel.x;
        float rollError = targetRollRate  - localAngularVel.z;
        float yawError = targetYawRate   - localAngularVel.y;

        float pitchCorr = Mathf.Clamp(pitchPID.Update(pitchError, Time.fixedDeltaTime), -0.9f, 0.9f);
        float rollCorr = Mathf.Clamp(rollPID.Update(rollError, Time.fixedDeltaTime), -0.9f, 0.9f);
        float yawCorr = Mathf.Clamp(yawPID.Update(yawError, Time.fixedDeltaTime), -0.6f, 0.6f);

        // --- НОВОЕ: Если газ на нуле, отключаем PID выравнивания ---
        // Это заставит моторы ПОЛНОСТЬЮ остановиться и дрон упадет.
        if (effectiveThrottle <= 0.01f)
        {
            pitchCorr = 0f;
            rollCorr = 0f;
        }

        rb.AddRelativeTorque(Vector3.up * yawCorr * 8f, ForceMode.Force);

        float motorFL = effectiveThrottle - pitchCorr + rollCorr;
        float motorFR = effectiveThrottle - pitchCorr - rollCorr;
        float motorBL = effectiveThrottle + pitchCorr + rollCorr;
        float motorBR = effectiveThrottle + pitchCorr - rollCorr;

        motors.ApplyMotorsThrust(motorFL, motorFR, motorBL, motorBR);
    }

    private float ComputeThrottleCurve(float throttle, float hoverBase, float jam)
    {
        if (jam > 0.3f && Mathf.PerlinNoise(Time.time * 8f, 50f) < jam * 0.4f)
            throttle *= 1f - jam * 0.35f;

        if (throttle >= 0.5f)
        {
            float t = (throttle - 0.5f) * 2f;
            float eased = 1f - (1f - t) * (1f - t);
            return Mathf.Lerp(hoverBase, 1f, eased);
        }
        else
        {
            float t = throttle * 2f;
            float eased = t * t;
            return Mathf.Lerp(0f, hoverBase, eased);
        }
    }

    private static float NormalizeAngle(float angle)
    {
        if (angle > 180f) angle -= 360f;
        return angle;
    }
}