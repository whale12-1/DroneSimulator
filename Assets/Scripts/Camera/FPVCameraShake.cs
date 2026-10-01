using UnityEngine;

public class FPVCameraShake : MonoBehaviour
{
    [Header("Профиль Дрона")]
    [SerializeField] private DroneConfig config;

    private Vector3 initialLocalPos;
    private Quaternion initialLocalRot;
    private float seed;

    private void Start()
    {
        // Запоминаем исходное положение и поворот камеры относительно дрона
        initialLocalPos = transform.localPosition;
        initialLocalRot = transform.localRotation;

        // Рандомный сдвиг для уникальности шума
        seed = Random.value * 100f;
    }

    private void LateUpdate()
    {
        // Если конфиг задан и тряска отключена — возвращаем камеру в исходную позицию
        if (config != null && !config.enableCameraShake)
        {
            transform.localPosition = initialLocalPos;
            transform.localRotation = initialLocalRot;
            return;
        }

        // Считываем параметры из конфига или подставляем значения по умолчанию
        float frequency = (config != null) ? config.shakeFrequency : 35f;
        float posAmplitude = (config != null) ? config.shakePosAmplitude : 0.003f;
        float rotAmplitude = (config != null) ? config.shakeRotAmplitude : 0.2f;
        bool useThrottle = (config != null) ? config.shakeUseThrottleEffect : true;
        float minThrottleFactor = (config != null) ? config.shakeMinThrottleFactor : 0.3f;

        float throttleFactor = 1f;

        if (useThrottle)
        {
            // Берем ввод газа по вертикальной оси
            float throttleInput = Mathf.Clamp01(Mathf.Abs(Input.GetAxis("Vertical")));
            throttleFactor = Mathf.Lerp(minThrottleFactor, 1.2f, throttleInput);
        }

        // Вычисляем время с учетом частоты
        float time = (Time.time + seed) * frequency;

        // Генерация шума Перлина в диапазоне от -1 до 1
        float offsetX = (Mathf.PerlinNoise(time, 0f) - 0.5f) * 2f;
        float offsetY = (Mathf.PerlinNoise(0f, time) - 0.5f) * 2f;
        float offsetRot = (Mathf.PerlinNoise(time, time) - 0.5f) * 2f;

        // Применяем микросмещение к позиции
        Vector3 posOffset = new Vector3(offsetX, offsetY, 0f) * (posAmplitude * throttleFactor);
        transform.localPosition = initialLocalPos + posOffset;

        // Применяем микроповорот относительно исходного вращения
        Quaternion rotOffset = Quaternion.Euler(
            offsetX * rotAmplitude * throttleFactor,
            0f,
            offsetRot * rotAmplitude * throttleFactor
        );
        transform.localRotation = initialLocalRot * rotOffset;
    }
}