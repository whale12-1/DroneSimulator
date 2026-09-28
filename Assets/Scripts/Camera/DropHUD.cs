using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DropHUD : MonoBehaviour
{
    [Header("Ссылки на объекты")]
    [SerializeField] private Camera mainCamera;
    [SerializeField] private Rigidbody droneRigidbody;
    [SerializeField] private DroneDropMechanism dropMechanism;

    [Header("Элементы UI")]
    [Tooltip("Иконка/крестик прицела на Canvas, который будет смещаться в точку падения")]
    [SerializeField] private RectTransform impactReticle;
    [SerializeField] private TextMeshProUGUI altitudeText;  // Высота (ALT)
    [SerializeField] private TextMeshProUGUI distanceText;  // Дистанция до цели (RNG)
    [SerializeField] private TextMeshProUGUI ammoText;      // Боезапас (AMMO)

    [Header("Настройки баллистики прицела")]
    [SerializeField] private Transform dropPoint;         // Точка сброса под брюхом дрона
    [SerializeField] private float maxPredictionTime = 5f; // Макс. время полета мины для просчета
    [SerializeField] private float timeStep = 0.05f;      // Шаг точности траектории
    [SerializeField] private LayerMask groundLayer;       // Слои земли, зданий и техники

    private void Update()
    {
        // 1. Проверяем, существует ли дрон сброса и активен ли его GameObject
        if (dropMechanism == null || !dropMechanism.gameObject.activeInHierarchy || !dropMechanism.enabled)
        {
            HideReticle();
            return;
        }

        UpdateTextInfo();
        UpdateDynamicReticle();
    }

    private void UpdateTextInfo()
    {
        // 1. Отображение боезапаса
        if (ammoText != null && dropMechanism != null)
        {
            ammoText.text = $"BOMBS: {dropMechanism.GetAmmoCount()}";
        }

        // 2. Высота над поверхностью (барометр/радиовысотомер)
        if (altitudeText != null)
        {
            if (Physics.Raycast(droneRigidbody.transform.position, Vector3.down, out RaycastHit hit, 1000f, groundLayer))
            {
                altitudeText.text = $"ALT: {hit.distance:F1}m";
            }
            else
            {
                altitudeText.text = $"ALT: {droneRigidbody.transform.position.y:F1}m";
            }
        }
    }

    private void UpdateDynamicReticle()
    {
        if (impactReticle == null || mainCamera == null || dropPoint == null || droneRigidbody == null)
            return;

        // Просчитываем физическую точку падения с учетом инерции дрона и гравитации
        Vector3 predictedImpactPoint = PredictImpactPoint();

        // Вычисляем дистанцию до предполагаемой точки взрыва
        float distanceToImpact = Vector3.Distance(dropPoint.position, predictedImpactPoint);
        if (distanceText != null)
        {
            distanceText.text = $"RNG: {distanceToImpact:F1}m";
        }

        // Проецируем 3D-координату точки удара в 2D-координату экрана
        Vector3 screenPoint = mainCamera.WorldToScreenPoint(predictedImpactPoint);

        // Если точка находится в поле зрения камеры (перед ней)
        if (screenPoint.z > 0)
        {
            impactReticle.gameObject.SetActive(true);
            impactReticle.position = screenPoint;
        }
        else
        {
            impactReticle.gameObject.SetActive(false); // Скрываем прицел, если точка позади кадра
        }
    }

    /// <summary>
    /// Математический симулятор траектории бомбы (CCIP)
    /// </summary>
    private Vector3 PredictImpactPoint()
    {
        Vector3 currentPosition = dropPoint.position;
        // В Unity 6 используется linearVelocity, в старых версиях — velocity
        Vector3 currentVelocity = droneRigidbody.linearVelocity;
        Vector3 gravity = Physics.gravity;

        Vector3 previousPosition = currentPosition;

        // Пошагово моделируем падение мины вперед по времени
        for (float t = 0; t < maxPredictionTime; t += timeStep)
        {
            // Формула равноускоренного движения: P(t) = P0 + V0*t + 0.5*g*t^2
            currentPosition = dropPoint.position + currentVelocity * t + 0.5f * gravity * (t * t);

            // Проверяем пересечение отрезка траектории с землей или объектами
            if (Physics.Linecast(previousPosition, currentPosition, out RaycastHit hit, groundLayer))
            {
                return hit.point; // Возвращаем точную точку касания
            }

            previousPosition = currentPosition;
        }

        return currentPosition;
    }

    // Вызывается автоматически, когда скрипт DropHUD выключается
    private void OnDisable()
    {
        HideReticle();
    }

    private void HideReticle()
    {
        if (impactReticle != null && impactReticle.gameObject.activeSelf)
        {
            impactReticle.gameObject.SetActive(false);
        }
    }
}