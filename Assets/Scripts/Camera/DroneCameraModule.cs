using UnityEngine;

public enum CameraType
{
    ReconGimbal, // Разведчик: 2-осевой подвес (Pitch/Yaw) + гиростабилизация
    StrikeFixed  // Ударный: фиксированный угол
}

public class DroneCameraModule : MonoBehaviour
{
    [Header("Тип Камеры")]
    public CameraType cameraType = CameraType.StrikeFixed;

    [Header("Настройки FPV (Strike)")]
    [SerializeField] private float fixedPitchAngle = 25f;

    [Header("Настройки Подвеса (Recon)")]
    [SerializeField] private float defaultGimbalPitch = 20f;
    [SerializeField] private float minPitch = -10f;
    [SerializeField] private float maxPitch = 85f;
    [SerializeField] private float minYaw = -120f;  // Ограничение поворота влево
    [SerializeField] private float maxYaw = 120f;   // Ограничение поворота вправо
    [SerializeField] private float tiltSpeed = 45f;


    private float currentGimbalPitch;
    private float currentGimbalYaw;

    private void Awake()
    {
        currentGimbalPitch = defaultGimbalPitch;
        currentGimbalYaw = 0f;
    }

    private void Update()
    {
        if (cameraType == CameraType.ReconGimbal)
        {
            HandleGimbalControl();
        }
    }

    private void LateUpdate()
    {
        ApplyCameraRotation();
    }

    private void HandleGimbalControl()
    {
        // R / F — Наклон Вверх / Вниз (Pitch)
        if (Input.GetKey(KeyCode.R)) currentGimbalPitch -= tiltSpeed * Time.deltaTime;
        if (Input.GetKey(KeyCode.F)) currentGimbalPitch += tiltSpeed * Time.deltaTime;

        // Q / E — Поворот Влево / Вправо (Yaw)
        if (Input.GetKey(KeyCode.Q)) currentGimbalYaw -= tiltSpeed * Time.deltaTime;
        if (Input.GetKey(KeyCode.E)) currentGimbalYaw += tiltSpeed * Time.deltaTime;

        // Сброс камеры в центр по нажатию 'C'
        if (Input.GetKeyDown(KeyCode.C))
        {
            currentGimbalPitch = defaultGimbalPitch;
            currentGimbalYaw = 0f;
        }

        currentGimbalPitch = Mathf.Clamp(currentGimbalPitch, minPitch, maxPitch);
        currentGimbalYaw = Mathf.Clamp(currentGimbalYaw, minYaw, maxYaw);
    }

    private void ApplyCameraRotation()
    {
        if (cameraType == CameraType.StrikeFixed)
        {
            transform.localRotation = Quaternion.Euler(fixedPitchAngle, 0f, 0f);
        }
        else if (cameraType == CameraType.ReconGimbal)
        {
            // Гиростабилизация: учитываем мировой поворот дрона по Y и добавляем смещение Yaw/Pitch подвеса
            Vector3 parentEuler = transform.parent != null ? transform.parent.eulerAngles : Vector3.zero;

            transform.rotation = Quaternion.Euler(currentGimbalPitch, parentEuler.y + currentGimbalYaw, 0f);
        }
    }
}