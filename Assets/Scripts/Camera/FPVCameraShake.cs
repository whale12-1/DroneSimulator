using UnityEngine;

public class FPVCameraShake : MonoBehaviour
{
    [Header("Профиль Дрона")]
    [SerializeField] private DroneConfig config;

    private Vector3 initialLocalPos;
    private float seed;

    private void Start()
    {
        // Запоминаем ИСХОДНУЮ позицию. Важно: она должна быть 0, 0.05, -0.05 в инспекторе!
        initialLocalPos = transform.localPosition;
        seed = Random.value * 100f;
    }

    private void LateUpdate()
    {
        // 1. Если тряска выключена в конфиге — просто ставим камеру в исходную позицию
        if (config != null && !config.enableCameraShake)
        {
            transform.localPosition = initialLocalPos;
            return;
        }

        // 2. Считываем настройки
        float frequency = (config != null) ? config.shakeFrequency : 35f;
        float posAmplitude = (config != null) ? config.shakePosAmplitude : 0.003f;
        float rotAmplitude = (config != null) ? config.shakeRotAmplitude : 0.2f;
        bool useThrottle = (config != null) ? config.shakeUseThrottleEffect : true;
        float minThrottleFactor = (config != null) ? config.shakeMinThrottleFactor : 0.3f;

        float intensity = 1f;
        SettingsService service = SettingsService.Instance;
        if (service != null && service.Data != null && service.Data.gameplay != null)
            intensity = Mathf.Max(0f, service.Data.gameplay.cameraShakeIntensity);

        if (intensity <= 0f)
        {
            transform.localPosition = initialLocalPos;
            return;
        }

        // 3. Считаем фактор газа
        float throttleFactor = 1f;
        if (useThrottle)
        {
            float throttleInput = Mathf.Clamp01(Mathf.Abs(Input.GetAxis("Vertical")));
            throttleFactor = Mathf.Lerp(minThrottleFactor, 1.2f, throttleInput);
        }

        // 4. Генерируем шум
        float time = (Time.time + seed) * frequency;
        float offsetX = (Mathf.PerlinNoise(time, 0f) - 0.5f) * 2f;
        float offsetY = (Mathf.PerlinNoise(0f, time) - 0.5f) * 2f;
        float offsetRot = (Mathf.PerlinNoise(time, time) - 0.5f) * 2f;

        // 5. Применяем смещение позиции (только X и Y, Z не трогаем)
        Vector3 posOffset = new Vector3(offsetX, offsetY, 0f) * (posAmplitude * throttleFactor * intensity);
        transform.localPosition = initialLocalPos + posOffset;

        // 6. Применяем тряску поворота (просто добавляем к текущему повороту)
        // ВАЖНО: Мы используем Rotate, чтобы добавить тряску поверх наклона, который задал DroneCameraModule
        float rotX = offsetX * rotAmplitude * throttleFactor * intensity;
        float rotZ = offsetRot * rotAmplitude * throttleFactor * intensity;

        // Сбрасываем поворот к базовому (если DroneCameraModule не успел это сделать)
        // и применяем шейк.
        transform.localRotation = Quaternion.Euler(transform.localEulerAngles.x + rotX, 0f, transform.localEulerAngles.z + rotZ);
    }
}