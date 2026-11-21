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
    private DroneDropMechanism dropMechanism;
    private DroneCollision collision;

    public int PrimaryPriority => 30;

    private bool WeaponsReady
    {
        get
        {
            if (dropMechanism != null && dropMechanism.HasPayload) return true;
            if (collision != null && collision.CanDetonateManually) return true;
            return false;
        }
    }

    private void Awake()
    {
        cam = GetComponent<Camera>();
        if (detector == null) detector = GetComponent<TargetDetector>();

        dropMechanism = GetComponentInParent<DroneDropMechanism>();
        collision = GetComponentInParent<DroneCollision>();
    }

    private void OnEnable()
    {
        GameInput.RegisterPrimaryHandler(this);
    }

    private void OnDisable()
    {
        GameInput.UnregisterPrimaryHandler(this);
    }

    private void Update()
    {
        if (config != null && !config.allowTargetLock && currentLockedTarget != null)
        {
            currentLockedTarget = null;
        }
    }

    public bool WantsPrimaryAction()
    {
        if (config != null && !config.allowTargetLock) return false;

        if (currentLockedTarget != null) return !WeaponsReady;

        return FindLockCandidate() != null;
    }

    public void OnPrimaryAction()
    {
        if (currentLockedTarget != null)
        {
            currentLockedTarget = null;
            return;
        }

        currentLockedTarget = FindLockCandidate();
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
