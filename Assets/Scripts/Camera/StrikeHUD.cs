using UnityEngine;

public class StrikeHUD : MonoBehaviour
{
    private DroneCameraModule CameraModule;
    [Header("Настройки Военного прицела (Strike HUD)")]
    [SerializeField] private bool showStrikeHUD = true;
    [SerializeField] private Color hudColor = new Color(0.1f, 1.0f, 0.2f, 0.85f); // Зеленый армейский цвет
    [SerializeField] private float crosshairSize = 14f;
    [SerializeField] private Vector2 targetBoxSize = new Vector2(160f, 110f);
    [SerializeField] private float lineWidth = 2f;
    private Texture2D pixelTexture;

    private void Awake()
    {
        CameraModule = GetComponent<DroneCameraModule>();
        pixelTexture = new Texture2D(1, 1);
        pixelTexture.SetPixel(0, 0, Color.white);
        pixelTexture.Apply();
    }


    private void OnGUI()
    {
        if (CameraModule == null || CameraModule.cameraType == CameraType.ReconGimbal || !showStrikeHUD) return;
        GUI.color = hudColor;
        Vector2 center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);

        // 1. Центральное перекрестие (+)
        DrawRect(new Rect(center.x - crosshairSize, center.y - (lineWidth / 2f), crosshairSize * 2f, lineWidth));
        DrawRect(new Rect(center.x - (lineWidth / 2f), center.y - crosshairSize, lineWidth, crosshairSize * 2f));

        // 2. Зеленый прямоугольник захвата цели (Target Box)
        float boxW = targetBoxSize.x;
        float boxH = targetBoxSize.y;
        Rect targetRect = new Rect(center.x - boxW / 2f, center.y - boxH / 2f, boxW, boxH);

        DrawBoxOutline(targetRect, lineWidth);

        // 3. Военный системный текст OSD
        GUIStyle textStyle = new GUIStyle();
        textStyle.normal.textColor = hudColor;
        textStyle.fontSize = 13;
        textStyle.fontStyle = FontStyle.Bold;

        GUI.Label(new Rect(targetRect.x, targetRect.y - 20f, 200f, 20f), "[STRIKE FPV]", textStyle);
        GUI.Label(new Rect(targetRect.x, targetRect.yMax + 4f, 200f, 20f), "ARMED / PG-7V", textStyle);
        // Внутри OnGUI() класса DroneCameraModule:
        EWReceiver ew = GetComponent<EWReceiver>();
        if (ew != null && ew.jamIntensity > 0.1f)
        {
            GUI.color = Color.red;
            GUI.Label(new Rect(targetRect.x, targetRect.y - 38f, 250f, 20f), $"[WARNING: EW JAMMING {(ew.jamIntensity * 100f):F0}%]", textStyle);
        }
    }

    private void DrawRect(Rect rect)
    {
        if (pixelTexture != null)
        {
            GUI.DrawTexture(rect, pixelTexture);
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

}
