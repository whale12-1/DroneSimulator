using UnityEngine;

public class TargetLockManager : MonoBehaviour, IPrimaryActionHandler
{
    [Header("Профиль Дрона")]
    [SerializeField] private DroneConfig config;

    [Header("Ссылки")]
    [SerializeField] private TargetDetector detector;

    [Header("Захваченная Цель")]
    public TargetObject currentLockedTarget;

    private Camera cam;

    public int PrimaryPriority => 30;

    private void Awake()
    {
        cam = GetComponent<Camera>();
        if (detector == null) detector = GetComponent<TargetDetector>();
    }

    private void OnEnable()
    {
        GameInput.RegisterPrimaryHandler(this);
    }

    private void OnDisable()
    {
        GameInput.UnregisterPrimaryHandler(this);

        // При выключении камеры/смене дрона захват не должен «висеть» на следующей машине
        currentLockedTarget = null;
    }

    private void Update()
    {
        if (currentLockedTarget != null && config != null && !config.allowTargetLock)
        {
            currentLockedTarget = null;
        }
    }

    public bool WantsPrimaryAction()
    {
        if (config != null && !config.allowTargetLock) return false;

        // Если цель уже захвачена, нажатие обязано обработать именно этот компонент:
        // Пробел по той же цели снимает захват, по другой цели — переключает на неё.
        // Иначе боевые обработчики (сброс боеприпаса / подрыв) «съедают» повторный Пробел.
        if (currentLockedTarget != null) return true;

        // Без захвата забираем действие только если под прицелом реально есть цель,
        // чтобы на ударных дронах Пробел продолжал работать как сброс/подрыв.
        return FindLockCandidate() != null;
    }

    public void OnPrimaryAction()
    {
        TargetObject candidate = FindLockCandidate();

        // Если навёлся на другую цель — сразу переключаем лок на неё
        if (candidate != null && candidate != currentLockedTarget)
        {
            currentLockedTarget = candidate;
            return;
        }

        // Если нажали пробел по той же цели или в пустоту — снимаем захват
        currentLockedTarget = null;
    }

    private TargetObject FindLockCandidate()
    {
        if (cam == null) return null;
        if (detector == null || detector.detectedTargets.Count == 0) return null;

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

        return bestTarget;
    }
}
