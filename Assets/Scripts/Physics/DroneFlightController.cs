using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(DroneMotors))]
public class DroneFlightController : MonoBehaviour
{
    private Rigidbody rb;
    private DroneMotors motors;

    [Header("Режим Полета")]
    public bool angleMode = true;
    public bool hoverMode = false;
    public float maxAngle = 35f;

    [Header("PID Настройки")]
    public PIDController pitchPID = new PIDController(1.5f, 0.0f, 0.3f);
    public PIDController rollPID = new PIDController(1.5f, 0.0f, 0.3f);
    public PIDController yawPID = new PIDController(1.0f, 0.0f, 0.2f);

    [Header("Чувствительность Управления")]
    [SerializeField] private float maxPitchRate = 180f;
    [SerializeField] private float maxRollRate = 180f;
    [SerializeField] private float maxYawRate = 120f;

    private float targetPitchInput;
    private float targetRollInput;
    private float targetYawInput;
    private float throttleInput = 0.5f;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        motors = GetComponent<DroneMotors>();
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
        }

        if (hoverMode)
        {
            throttleInput = 0.5f;
            targetPitchInput = 0f;
            targetRollInput = 0f;
            targetYawInput = 0f;

            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.S) ||
                Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.DownArrow) ||
                Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.RightArrow) ||
                Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.D))
            {
                hoverMode = false;
            }
            return;
        }

        // Pitch (Вперед / Назад)
        targetPitchInput = 0f;
        if (Input.GetKey(KeyCode.UpArrow)) targetPitchInput = 1f;
        if (Input.GetKey(KeyCode.DownArrow)) targetPitchInput = -1f;

        // Roll (Крен Влево / Вправо)
        targetRollInput = 0f;
        if (Input.GetKey(KeyCode.RightArrow)) targetRollInput = 1f;
        if (Input.GetKey(KeyCode.LeftArrow)) targetRollInput = -1f;

        // Yaw (Поворот носа A / D)
        targetYawInput = 0f;
        if (Input.GetKey(KeyCode.D)) targetYawInput = 1f;
        if (Input.GetKey(KeyCode.A)) targetYawInput = -1f;

        // Throttle (Газ W / S)
        if (Input.GetKey(KeyCode.W))
            throttleInput = Mathf.MoveTowards(throttleInput, 1.0f, Time.deltaTime * 1.2f);
        else if (Input.GetKey(KeyCode.S))
            throttleInput = Mathf.MoveTowards(throttleInput, 0.0f, Time.deltaTime * 1.2f);
        else
            throttleInput = Mathf.MoveTowards(throttleInput, 0.5f, Time.deltaTime * 1.5f);
    }

    private void CalculateFlightPhysics()
    {
        float totalGravityForce = rb.mass * Mathf.Abs(Physics.gravity.y);
        float maxTotalThrust = 40f;
        float hoverBase = totalGravityForce / maxTotalThrust;

        float effectiveThrottle;

        if (hoverMode)
        {
            // Гашение вертикальной скорости (чтобы дрон не падал)
            float verticalDamping = -rb.linearVelocity.y * 0.15f;
            effectiveThrottle = Mathf.Clamp(hoverBase + verticalDamping, 0.0f, 0.85f);

            // Активное гашение горизонтальной инерции
            Vector3 horizontalVel = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
            rb.AddForce(-horizontalVel * 4.0f, ForceMode.Acceleration);
        }
        else
        {
            if (throttleInput >= 0.5f)
                effectiveThrottle = Mathf.Lerp(hoverBase, 1.0f, (throttleInput - 0.5f) * 2f);
            else
                effectiveThrottle = Mathf.Lerp(0.0f, hoverBase, throttleInput * 2f);
        }

        // Угловая скорость в ЛОКАЛЬНОЙ системе координат
        Vector3 localAngularVel = transform.InverseTransformDirection(rb.angularVelocity) * Mathf.Rad2Deg;

        float targetPitchRate = 0f;
        float targetRollRate = 0f;
        float targetYawRate = targetYawInput * maxYawRate;

        if (angleMode || hoverMode)
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

        float pitchError = targetPitchRate - localAngularVel.x;
        float rollError = targetRollRate - localAngularVel.z;
        float yawError = targetYawRate - localAngularVel.y;

        float pitchCorr = Mathf.Clamp(pitchPID.Update(pitchError, Time.fixedDeltaTime), -0.8f, 0.8f);
        float rollCorr = Mathf.Clamp(rollPID.Update(rollError, Time.fixedDeltaTime), -0.8f, 0.8f);
        float yawCorr = Mathf.Clamp(yawPID.Update(yawError, Time.fixedDeltaTime), -0.5f, 0.5f);

        // Поворот по оси Y (A / D) через крутящий момент
        rb.AddRelativeTorque(Vector3.up * yawCorr * 8.0f, ForceMode.Force);

        // Распределение тяги моторов (только Pitch и Roll)
        float motorFL = effectiveThrottle - pitchCorr + rollCorr;
        float motorFR = effectiveThrottle - pitchCorr - rollCorr;
        float motorBL = effectiveThrottle + pitchCorr + rollCorr;
        float motorBR = effectiveThrottle + pitchCorr - rollCorr;

        motors.ApplyMotorsThrust(motorFL, motorFR, motorBL, motorBR);
    }
}