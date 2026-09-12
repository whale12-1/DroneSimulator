using UnityEngine;

public class TargetLockManager : MonoBehaviour
{
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
        // Нажатие 'Space' фиксирует или сбрасывает цель
        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (currentLockedTarget != null)
            {
                currentLockedTarget = null; // Сброс
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

        foreach (var target in detector.detectedTargets)
        {
            Vector3 targetScreenPos = cam.WorldToScreenPoint(target.TargetPosition);

            // Игнорируем объекты за спиной
            if (targetScreenPos.z < 0) continue;

            float distanceFromCenter = Vector2.Distance(screenCenter, targetScreenPos);

            if (distanceFromCenter < minDistanceToCenter)
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