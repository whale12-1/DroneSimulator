using UnityEngine;

public class StrikeHUD : MonoBehaviour
{
    [Header("Профиль Дрона")]
    [SerializeField] private DroneConfig config;

    [Header("Ссылки на компоненты")]
    [SerializeField] private DroneCameraModule cameraModule;
    [SerializeField] private EWReceiver ewReceiver;

    private Texture2D pixelTexture;

    private void Awake()
    {
        if (cameraModule == null) cameraModule = GetComponent<DroneCameraModule>();
        if (ewReceiver == null) ewReceiver = GetComponent<EWReceiver>();

        pixelTexture = new Texture2D(1, 1);
        pixelTexture.SetPixel(0, 0, Color.white);
        pixelTexture.Apply();
    }

    private void OnGUI()
    {
        // 1. Проверяем режим камеры и флаг отображения HUD
        CameraType camType = (config != null) ? config.cameraType : (cameraModule != null ? cameraModule.CurrentCameraType : CameraType.StrikeFixed);
        bool isEnabled = (config != null) ? config.showStrikeHUD : true;

        if (camType == CameraType.ReconGimbal || !isEnabled) return;

        // 2. Считываем визуальные параметры из конфига
        Color activeHudColor = (config != null) ? config.hudColor : new Color(0.1f, 1.0f, 0.2f, 0.85f);
        float crosshairSize = (config != null) ? config.crosshairSize : 14f;
        Vector2 boxSize = (config != null) ? config.targetBoxSize : new Vector2(160f, 110f);
        float lineWidth = (config != null) ? config.hudLineWidth : 2f;

        GUI.color = activeHudColor;
        Vector2 center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);

        // 3. Центральное перекрестие (+)
        DrawRect(new Rect(center.x - crosshairSize, center.y - (lineWidth / 2f), crosshairSize * 2f, lineWidth));
        DrawRect(new Rect(center.x - (lineWidth / 2f), center.y - crosshairSize, lineWidth, crosshairSize * 2f));

        // 4. Прямоугольник захвата цели (Target Box)
        Rect targetRect = new Rect(center.x - boxSize.x / 2f, center.y - boxSize.y / 2f, boxSize.x, boxSize.y);
        DrawBoxOutline(targetRect, lineWidth);

        // 5. Системные надписи OSD
        GUIStyle textStyle = new GUIStyle
        {
            fontSize = 13,
            fontStyle = FontStyle.Bold
        };
        textStyle.normal.textColor = activeHudColor;

        string droneLabel = (config != null && !string.IsNullOrEmpty(config.droneName)) ? $"[{config.droneName.ToUpper()}]" : "[STRIKE FPV]";
        string warheadLabel = (config != null) ? $"ARMED / {config.warheadType}" : "ARMED / PG-7V";

        GUI.Label(new Rect(targetRect.x, targetRect.y - 20f, 250f, 20f), droneLabel, textStyle);
        GUI.Label(new Rect(targetRect.x, targetRect.yMax + 4f, 250f, 20f), warheadLabel, textStyle);

        // 6. Предупреждение о РЭБ
        if (ewReceiver != null && ewReceiver.jamIntensity > 0.1f)
        {
            GUIStyle warningStyle = new GUIStyle(textStyle);
            warningStyle.normal.textColor = Color.red;
            GUI.Label(new Rect(targetRect.x, targetRect.y - 38f, 280f, 20f), $"[WARNING: EW JAMMING {(ewReceiver.jamIntensity * 100f):F0}%]", warningStyle);
        }
    }

    private void DrawBoxOutline(Rect rect, float width)
    {
        // Верх
        DrawRect(new Rect(rect.x, rect.y, rect.width, width));
        // Низ
        DrawRect(new Rect(rect.x, rect.yMax - width, rect.width, width));
        // Лево
        DrawRect(new Rect(rect.x, rect.y, width, rect.height));
        // Право
        DrawRect(new Rect(rect.xMax - width, rect.y, width, rect.height));
    }

    private void DrawRect(Rect rect)
    {
        if (pixelTexture != null)
        {
            GUI.DrawTexture(rect, pixelTexture);
        }
    }
}