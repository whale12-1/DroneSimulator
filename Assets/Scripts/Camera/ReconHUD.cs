using UnityEngine;

public class ReconHUD : MonoBehaviour
{
    [Header("Профиль Дрона")]
    [SerializeField] private DroneConfig config;

    [Header("Ссылки на компоненты")]
    [SerializeField] private TargetLockManager lockManager;
    [SerializeField] private DroneCameraModule cameraModule;
    [SerializeField] private FPVZoom zoomModule;

    private Camera cam;
    private Texture2D pixelTexture;

    private void Awake()
    {
        cam = GetComponent<Camera>();
        if (lockManager == null) lockManager = GetComponent<TargetLockManager>();
        if (cameraModule == null) cameraModule = GetComponent<DroneCameraModule>();
        if (zoomModule == null) zoomModule = GetComponent<FPVZoom>();

        pixelTexture = new Texture2D(1, 1);
        pixelTexture.SetPixel(0, 0, Color.white);
        pixelTexture.Apply();
    }

    private void OnGUI()
    {
        // Проверяем тип камеры из конфига или модуля
        CameraType camType = (config != null) ? config.cameraType : (cameraModule != null ? cameraModule.CurrentCameraType : CameraType.StrikeFixed);
        if (camType != CameraType.ReconGimbal) return;

        Color hudColor = (config != null) ? config.reconColor : new Color(1f, 0.85f, 0.2f, 0.9f);
        float lineWidth = (config != null) ? config.hudLineWidth : 2f;
        float minBoxSize = (config != null) ? config.minBoxSize : 20f;
        LayerMask lrfMask = (config != null) ? config.lrfMask : (LayerMask)(~0);

        GUI.color = hudColor;
        GUIStyle style = new GUIStyle { fontSize = 12, fontStyle = FontStyle.Bold };
        style.normal.textColor = hudColor;

        // 1. Лазерный дальномер (LRF) по центру камеры
        Ray lrfRay = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        float distance = 0f;
        Vector3 targetWorldPos = Vector3.zero;

        if (Physics.Raycast(lrfRay, out RaycastHit hit, 2000f, lrfMask))
        {
            distance = hit.distance;
            targetWorldPos = hit.point;
        }

        // 2. Отрисовка центрального перекрестия дальномера
        Vector2 center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        DrawRect(new Rect(center.x - 8f, center.y - 1f, 16f, 2f));
        DrawRect(new Rect(center.x - 1f, center.y - 8f, 2f, 16f));

        // 3. Информационный блок OSD
        GUI.Label(new Rect(25, 25, 350, 20), "[RECON GIMBAL ACTIVE]", style);
        GUI.Label(new Rect(25, 45, 350, 20), $"ALT: {transform.position.y:F1}m  |  LRF DIST: {(distance > 0 ? $"{distance:F0}m" : "N/A")}", style);

        if (distance > 0)
        {
            GUI.Label(new Rect(25, 65, 350, 20), $"GRID POS: X:{targetWorldPos.x:F0} Z:{targetWorldPos.z:F0}", style);
        }

        // 4. Отрисовка динамической адаптивной рамки цели
        if (lockManager != null && lockManager.currentLockedTarget != null)
        {
            TargetObject target = lockManager.currentLockedTarget;
            Renderer targetRenderer = target.GetComponentInChildren<Renderer>();

            if (targetRenderer != null)
            {
                Bounds bounds = targetRenderer.bounds;

                Vector3[] screenPoints = new Vector3[8];
                screenPoints[0] = cam.WorldToScreenPoint(new Vector3(bounds.min.x, bounds.min.y, bounds.min.z));
                screenPoints[1] = cam.WorldToScreenPoint(new Vector3(bounds.max.x, bounds.min.y, bounds.min.z));
                screenPoints[2] = cam.WorldToScreenPoint(new Vector3(bounds.min.x, bounds.max.y, bounds.min.z));
                screenPoints[3] = cam.WorldToScreenPoint(new Vector3(bounds.max.x, bounds.max.y, bounds.min.z));
                screenPoints[4] = cam.WorldToScreenPoint(new Vector3(bounds.min.x, bounds.min.y, bounds.max.z));
                screenPoints[5] = cam.WorldToScreenPoint(new Vector3(bounds.max.x, bounds.min.y, bounds.max.z));
                screenPoints[6] = cam.WorldToScreenPoint(new Vector3(bounds.min.x, bounds.max.y, bounds.max.z));
                screenPoints[7] = cam.WorldToScreenPoint(new Vector3(bounds.max.x, bounds.max.y, bounds.max.z));

                if (screenPoints[0].z > 0)
                {
                    float minX = screenPoints[0].x, maxX = screenPoints[0].x;
                    float minY = screenPoints[0].y, maxY = screenPoints[0].y;

                    for (int i = 1; i < 8; i++)
                    {
                        if (screenPoints[i].x < minX) minX = screenPoints[i].x;
                        if (screenPoints[i].x > maxX) maxX = screenPoints[i].x;
                        if (screenPoints[i].y < minY) minY = screenPoints[i].y;
                        if (screenPoints[i].y > maxY) maxY = screenPoints[i].y;
                    }

                    float width = Mathf.Max(maxX - minX, minBoxSize);
                    float height = Mathf.Max(maxY - minY, minBoxSize);

                    float centerX = (minX + maxX) * 0.5f;
                    float centerY = Screen.height - ((minY + maxY) * 0.5f);

                    Rect targetRect = new Rect(centerX - width * 0.5f, centerY - height * 0.5f, width, height);

                    DrawBoxOutline(targetRect, lineWidth);

                    GUI.Label(new Rect(targetRect.x, targetRect.y - 18f, 200f, 20f), $"LOCKED: {target.targetName.ToUpper()}", style);
                    GUI.Label(new Rect(targetRect.x, targetRect.yMax + 2f, 200f, 20f), $"DIST: {Vector3.Distance(transform.position, target.TargetPosition):F0}m", style);
                }
            }
        }
    }

    private void DrawBoxOutline(Rect rect, float width)
    {
        DrawRect(new Rect(rect.x, rect.y, rect.width, width));
        DrawRect(new Rect(rect.x, rect.yMax - width, rect.width, width));
        DrawRect(new Rect(rect.x, rect.y, width, rect.height));
        DrawRect(new Rect(rect.xMax - width, rect.y, width, rect.height));
    }

    private void DrawRect(Rect rect)
    {
        if (pixelTexture != null) GUI.DrawTexture(rect, pixelTexture);
    }
}