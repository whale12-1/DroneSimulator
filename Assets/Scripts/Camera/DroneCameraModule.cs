using UnityEngine;

public enum CameraType
{
    ReconGimbal, // Разведчик: 2-осевой подвес (Pitch/Yaw) + гиростабилизация
    StrikeFixed  // Ударный: фиксированный угол
}

public class DroneCameraModule : MonoBehaviour
{
    [Header("Профиль Дрона")]
    [SerializeField] private DroneConfig config;

    private float currentGimbalPitch;
    private float currentGimbalYaw;

    public CameraType CurrentCameraType => (config != null) ? config.cameraType : CameraType.StrikeFixed;

    private void Awake()
    {
        ResetGimbalToDefault();
    }

    private void Update()
    {
        if (CurrentCameraType == CameraType.ReconGimbal)
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
        float speed = (config != null) ? config.tiltSpeed : 45f;

        // R / F — Наклон Вверх / Вниз (Pitch)
        if (Input.GetKey(KeyCode.R)) currentGimbalPitch -= speed * Time.deltaTime;
        if (Input.GetKey(KeyCode.F)) currentGimbalPitch += speed * Time.deltaTime;

        // Q / E — Поворот Влево / Вправо (Yaw)
        if (Input.GetKey(KeyCode.Q)) currentGimbalYaw -= speed * Time.deltaTime;
        if (Input.GetKey(KeyCode.E)) currentGimbalYaw += speed * Time.deltaTime;

        // Сброс камеры в центр по нажатию 'C'
        if (Input.GetKeyDown(KeyCode.C))
        {
            ResetGimbalToDefault();
        }

        float minPitch = (config != null) ? config.minPitch : -10f;
        float maxPitch = (config != null) ? config.maxPitch : 85f;
        float minYaw = (config != null) ? config.minYaw : -120f;
        float maxYaw = (config != null) ? config.maxYaw : 120f;

        currentGimbalPitch = Mathf.Clamp(currentGimbalPitch, minPitch, maxPitch);
        currentGimbalYaw = Mathf.Clamp(currentGimbalYaw, minYaw, maxYaw);
    }

    private void ApplyCameraRotation()
    {
        if (CurrentCameraType == CameraType.StrikeFixed)
        {
            float pitch = (config != null) ? config.fixedPitchAngle : 25f;
            transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }
        else if (CurrentCameraType == CameraType.ReconGimbal)
        {
            // Гиростабилизация: учитываем мировой поворот дрона по Y и добавляем смещение Yaw/Pitch подвеса
            Vector3 parentEuler = transform.parent != null ? transform.parent.eulerAngles : Vector3.zero;
            transform.rotation = Quaternion.Euler(currentGimbalPitch, parentEuler.y + currentGimbalYaw, 0f);
        }
    }

    public void ResetGimbalToDefault()
    {
        currentGimbalPitch = (config != null) ? config.defaultGimbalPitch : 20f;
        currentGimbalYaw = 0f;
    }
}