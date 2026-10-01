using UnityEngine;

public class TargetLockManager : MonoBehaviour
{
    [Header("Профиль Дрона")]
    [SerializeField] private DroneConfig config;

    [Header("Ссылки")]
    [SerializeField] private TargetDetector detector;

    [Header("Захваченная Цель")]
    public TargetObject currentLockedTarget;

    private Camera cam;

    private void Awake()
    {
        cam = GetComponent<Camera>();
        if (detector == null) detector = GetComponent<TargetDetector>();
    }

    private void Update()
    {
        // Если конфиг отсутствует или на дроне отключен автозахват — сбрасываем цель и не обрабатываем ввод
        if (config != null && !config.allowTargetLock)
        {
            if (currentLockedTarget != null) currentLockedTarget = null;
            return;
        }

        KeyCode lockKey = (config != null) ? config.targetLockKey : KeyCode.Space;

        if (Input.GetKeyDown(lockKey))
        {
            if (currentLockedTarget != null)
            {
                currentLockedTarget = null; // Сброс цели
            }
            else
            {
                TryLockTargetInCenter();
            }
        }
    }

    private void TryLockTargetInCenter()
    {
        if (detector == null || detector.detectedTargets.Count == 0) return;

        TargetObject bestTarget = null;
        float minDistanceToCenter = float.MaxValue;

        Vector3 screenCenter = new Vector3(Screen.width / 2f, Screen.height / 2f, 0f);
        float maxAllowedRadius = (config != null && config.maxLockScreenRadius > 0f)
            ? config.maxLockScreenRadius
            : float.MaxValue;

        foreach (var target in detector.detectedTargets)
        {
            if (target == null) continue;

            Vector3 targetScreenPos = cam.WorldToScreenPoint(target.TargetPosition);

            // Игнорируем объекты за спиной
            if (targetScreenPos.z < 0) continue;

            float distanceFromCenter = Vector2.Distance(screenCenter, targetScreenPos);

            // Проверяем, входит ли цель в допустимый радиус захвата
            if (distanceFromCenter <= maxAllowedRadius && distanceFromCenter < minDistanceToCenter)
            {
                minDistanceToCenter = distanceFromCenter;
                bestTarget = target;
            }
        }

        if (bestTarget != null)
        {
            currentLockedTarget = bestTarget;
        }
    }
}