using UnityEngine;

public class FPVCameraShake : MonoBehaviour
{
    [Header("Частота и Амплитуда")]
    [Tooltip("Частота вибрации моторов (чем выше, тем чаще мелкая тряска)")]
    [SerializeField] private float frequency = 35f;

    [Tooltip("Амплитуда смещения позиции в метрах (для моторов достаточно 0.002-0.005)")]
    [SerializeField] private float posAmplitude = 0.003f;

    [Tooltip("Амплитуда поворота по углам в градусах")]
    [SerializeField] private float rotAmplitude = 0.2f;

    [Header("Зависимость от Газа (Throttle)")]
    [SerializeField] private bool useThrottleEffect = true;
    [SerializeField] private float minThrottleFactor = 0.3f; // Тряска на холостых оборотах

    private Vector3 initialLocalPos;
    private Quaternion initialLocalRot;
    private float seed;

    private void Start()
    {
        // Запоминаем исходное положение камеры относительно дрона
        initialLocalPos = transform.localPosition;
        initialLocalRot = transform.localRotation;

        // Рандомный сдвиг для уникальности шума
        seed = Random.value * 100f;
    }

    private void LateUpdate()
    {
        float throttleFactor = 1f;

        if (useThrottleEffect)
        {
            // Берем ввод газа по вертикальной оси W/S или вертикальному маппингу
            float throttleInput = Mathf.Clamp01(Mathf.Abs(Input.GetAxis("Vertical")));
            throttleFactor = Mathf.Lerp(minThrottleFactor, 1.2f, throttleInput);
        }

        // Вычисляем время с учетом частоты
        float time = (Time.time + seed) * frequency;

        // Генерация шума в диапазоне от -1 до 1
        float offsetX = (Mathf.PerlinNoise(time, 0f) - 0.5f) * 2f;
        float offsetY = (Mathf.PerlinNoise(0f, time) - 0.5f) * 2f;
        float offsetRot = (Mathf.PerlinNoise(time, time) - 0.5f) * 2f;

        // Применяем микросмещение к позиции
        Vector3 posOffset = new Vector3(offsetX, offsetY, 0f) * (posAmplitude * throttleFactor);
        transform.localPosition = initialLocalPos + posOffset;

        // Применяем микроповорот по оси Z (крены) и X (тангаж)
        Quaternion rotOffset = Quaternion.Euler(
            offsetX * rotAmplitude * throttleFactor,
            0f,
            offsetRot * rotAmplitude * throttleFactor
        );
        transform.localRotation *= rotOffset;
    }
}