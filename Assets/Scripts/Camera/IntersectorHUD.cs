using UnityEngine;

public class InterceptorHUD : MonoBehaviour
{
    [Header("Ссылки на компоненты")]
    [SerializeField] private Camera cam;
    [SerializeField] private Rigidbody droneRigidbody;
    [SerializeField] private DroneCameraModule cameraModule;

    [Header("Настройки автозахвата (Layer)")]
    [SerializeField] private LayerMask droneLayer;        // Слой Drone
    [SerializeField] private float autoLockDistance = 1500f; // Макс. дистанция обнаружения

    [Header("Настройки HUD")]
    [SerializeField] private Color hudColor = new Color(0f, 0.9f, 1f, 0.95f); // Голубой / Циан
    [SerializeField] private float lineWidth = 2f;
    [SerializeField] private float leadReticleSize = 16f;

    private Texture2D pixelTexture;

    private void Awake()
    {
        if (cam == null) cam = GetComponent<Camera>();
        if (droneRigidbody == null) droneRigidbody = GetComponentInParent<Rigidbody>();
        if (cameraModule == null) cameraModule = GetComponent<DroneCameraModule>();

        pixelTexture = new Texture2D(1, 1);
        pixelTexture.SetPixel(0, 0, Color.white);
        pixelTexture.Apply();
    }

    private Transform GetNearestDroneInView()
    {
        Collider[] targets = Physics.OverlapSphere(transform.position, autoLockDistance, droneLayer);
        Transform bestTarget = null;
        float closestDist = Mathf.Infinity;

        foreach (Collider col in targets)
        {
            Transform root = col.transform.root;

            // Игнорируем дрон, на котором висит сам HUD
            if (root == transform.root || root == droneRigidbody.transform) continue;

            // Проверяем, находится ли цель перед объективом
            Vector3 screenPos = cam.WorldToScreenPoint(root.position);
            if (screenPos.z > 0)
            {
                float dist = Vector3.Distance(transform.position, root.position);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    bestTarget = root;
                }
            }
        }

        return bestTarget;
    }

    private void OnGUI()
    {
        if (cam == null || droneRigidbody == null) return;

        GUI.color = hudColor;
        GUIStyle style = new GUIStyle { fontSize = 12, fontStyle = FontStyle.Bold };
        style.normal.textColor = hudColor;

        // 1. Верхний OSD-блок
        float ownSpeedKmH = droneRigidbody.linearVelocity.magnitude * 3.6f;
        GUI.Label(new Rect(25, 25, 350, 20), "[INTERCEPTOR HUD ACTIVE]", style);
        GUI.Label(new Rect(25, 45, 350, 20), $"ALT: {transform.position.y:F1}m  |  SPD: {ownSpeedKmH:F0} km/h", style);

        // 2. Центральное перекрестие камеры
        Vector2 center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        DrawRect(new Rect(center.x - 10f, center.y - 1f, 20f, 2f));
        DrawRect(new Rect(center.x - 1f, center.y - 10f, 2f, 20f));

        // 3. Автоматический поиск цели на слое Drone
        Transform target = GetNearestDroneInView();

        if (target != null)
        {
            // Берем Rigidbody цели для вычисления вектора её скорости
            Rigidbody targetRb = target.GetComponentInParent<Rigidbody>();
            if (targetRb == null) targetRb = target.GetComponent<Rigidbody>();

            Vector3 targetPos = target.position;
            Vector3 targetVel = (targetRb != null) ? targetRb.linearVelocity : Vector3.zero;
            Vector3 ownVel = droneRigidbody.linearVelocity;

            // Дистанция и скорость сближения (Closure Speed)
            Vector3 relativePos = targetPos - transform.position;
            float distance = relativePos.magnitude;

            Vector3 relativeVel = ownVel - targetVel;
            float closureSpeed = Vector3.Dot(relativeVel, relativePos.normalized); // м/с

            // Время до встречи (TGO)
            float tgo = closureSpeed > 0.5f ? distance / closureSpeed : 0f;

            // --- Расчет упреждения (Lead Point) ---
            float interceptorSpeed = Mathf.Max(ownVel.magnitude, 15f);
            float timeToHit = distance / interceptorSpeed;
            Vector3 leadPoint3D = targetPos + (targetVel * timeToHit);

            // Проекция точек на экран
            Vector3 targetScreenPos = cam.WorldToScreenPoint(targetPos);
            Vector3 leadScreenPos = cam.WorldToScreenPoint(leadPoint3D);

            if (targetScreenPos.z > 0)
            {
                Vector2 targetGUI = new Vector2(targetScreenPos.x, Screen.height - targetScreenPos.y);
                Vector2 leadGUI = new Vector2(leadScreenPos.x, Screen.height - leadScreenPos.y);

                // А. Рамка вокруг цели
                float boxSize = Mathf.Clamp(3000f / distance, 24f, 90f);
                Rect targetRect = new Rect(targetGUI.x - boxSize * 0.5f, targetGUI.y - boxSize * 0.5f, boxSize, boxSize);
                DrawBoxOutline(targetRect, lineWidth);

                // Б. Маркер упреждения (прицел точки встречи)
                if (leadScreenPos.z > 0)
                {
                    Rect leadRect = new Rect(leadGUI.x - leadReticleSize * 0.5f, leadGUI.y - leadReticleSize * 0.5f, leadReticleSize, leadReticleSize);
                    DrawBoxOutline(leadRect, 1.5f);

                    // Крест внутри маркерной рамки
                    DrawRect(new Rect(leadGUI.x - 4f, leadGUI.y - 0.5f, 8f, 1f));
                    DrawRect(new Rect(leadGUI.x - 0.5f, leadGUI.y - 4f, 1f, 8f));

                    // В. Линия вектора между целью и точкой упреждения
                    DrawLine(targetGUI, leadGUI, 1f);
                }

                // Г. Информационный текст под рамкой цели
                GUI.Label(new Rect(targetRect.x, targetRect.y - 18f, 200f, 20f), $"TARGET: {target.name.ToUpper()}", style);

                string closureText = closureSpeed >= 0 ? $"+{closureSpeed * 3.6f:F0} km/h" : $"{closureSpeed * 3.6f:F0} km/h";
                string tgoText = tgo > 0 ? $" | TGO: {tgo:F1}s" : "";

                GUI.Label(new Rect(targetRect.x, targetRect.yMax + 2f, 250f, 20f), $"RNG: {distance:F0}m | VC: {closureText}{tgoText}", style);
            }
        }
    }

    // --- Вспомогательные методы OnGUI ---
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

    private void DrawLine(Vector2 pointA, Vector2 pointB, float width)
    {
        OverrideGUIColor();
        Matrix4x4 matrix = GUI.matrix;
        float angle = Vector2.Angle(pointB - pointA, Vector2.right);
        if (pointA.y > pointB.y) angle = -angle;

        GUIUtility.ScaleAroundPivot(new Vector2((pointB - pointA).magnitude, width), new Vector2(0, 0.5f));
        GUIUtility.RotateAroundPivot(angle, new Vector2(0, 0.5f));
        GUI.matrix = Matrix4x4.TRS(pointA, Quaternion.identity, Vector3.one) * GUI.matrix;

        GUI.DrawTexture(new Rect(0, -0.5f, 1, 1), pixelTexture);
        GUI.matrix = matrix;
    }

    private void OverrideGUIColor()
    {
        GUI.color = hudColor;
    }
}