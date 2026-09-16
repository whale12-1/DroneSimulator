using UnityEngine;

public class ReconHUD : MonoBehaviour
{
    [Header("Ссылки на компоненты")]
    [SerializeField] private TargetLockManager lockManager;
    [SerializeField] private DroneCameraModule cameraModule;
    [SerializeField] private FPVZoom zoomModule;
    [SerializeField] private LayerMask lrfMask; // Земля и техника для дальномера

    [Header("Визуал OSD")]
    [SerializeField] private Color reconColor = new Color(1f, 0.85f, 0.2f, 0.9f); // Янтарный/Желтый цвет
    [SerializeField] private float lineWidth = 2f;

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
        // Отрисовываем OSD только в режиме разведки ReconGimbal
        if (cameraModule != null && cameraModule.cameraType != CameraType.ReconGimbal) return;

        GUI.color = reconColor;
        GUIStyle style = new GUIStyle { fontSize = 12, fontStyle = FontStyle.Bold };
        style.normal.textColor = reconColor;

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

        // 3. Информационный блок OSD (Альтитуда, Расстояние, Координаты)
        GUI.Label(new Rect(25, 25, 350, 20), "[RECON GIMBAL ACTIVE]", style);
        GUI.Label(new Rect(25, 45, 350, 20), $"ALT: {transform.position.y:F1}m  |  LRF DIST: {(distance > 0 ? $"{distance:F0}m" : "N/A")}", style);

        if (distance > 0)
        {
            GUI.Label(new Rect(25, 65, 350, 20), $"GRID POS: X:{targetWorldPos.x:F0} Z:{targetWorldPos.z:F0}", style);
        }

        // 4. Отрисовка динамической рамки захваченной цели (Target Tracking)
        if (lockManager != null && lockManager.currentLockedTarget != null)
        {
            TargetObject target = lockManager.currentLockedTarget;
            Vector3 screenPos = cam.WorldToScreenPoint(target.TargetPosition);

            // Проверяем, что цель находится перед камерой
            if (screenPos.z > 0)
            {
                // Перевод координаты Y из системы Unity в OnGUI (сверху вниз)
                float guiY = Screen.height - screenPos.y;
                float boxSize = 50f;
                Rect targetRect = new Rect(screenPos.x - boxSize / 2f, guiY - boxSize / 2f, boxSize, boxSize);

                DrawBoxOutline(targetRect, lineWidth);

                // Текст над/под рамкой цели
                GUI.Label(new Rect(targetRect.x, targetRect.y - 18f, 200f, 20f), $"LOCKED: {target.targetName.ToUpper()}", style);
                GUI.Label(new Rect(targetRect.x, targetRect.yMax + 2f, 200f, 20f), $"DIST: {Vector3.Distance(transform.position, target.TargetPosition):F0}m", style);
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