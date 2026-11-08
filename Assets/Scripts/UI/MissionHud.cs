using UnityEngine;

public class MissionHud : MonoBehaviour
{
    [Header("Ссылки")]
    [SerializeField] private DroneManager droneManager;
    [SerializeField] private MissionController missionController;

    [Header("Раскладка")]
    [SerializeField] private Vector2 margin = new Vector2(25f, 25f);
    [SerializeField] private float panelWidth = 320f;
    [SerializeField] private float rowHeight = 20f;
    [SerializeField] private float padding = 10f;
    [SerializeField] private int titleFontSize = 14;
    [SerializeField] private int rowFontSize = 13;

    [Header("Цвета")]
    [SerializeField] private Color panelColor = UIColorPalette.PanelBackground;
    [SerializeField] private Color headerColor = UIColorPalette.PanelHeader;
    [SerializeField] private Color textColor = UIColorPalette.TextPrimary;
    [SerializeField] private Color secondaryColor = UIColorPalette.TextSecondary;
    [SerializeField] private Color accentColor = UIColorPalette.Accent;
    [SerializeField] private Color successColor = UIColorPalette.Success;
    [SerializeField] private Color warningColor = UIColorPalette.Failure;

    private Texture2D pixel;
    private GUIStyle titleStyle;
    private GUIStyle rowStyle;
    private GUIStyle valueStyle;
    private float nextReferenceSearchTime;

    private float timerWarningThreshold = 30f;

    private void Awake()
    {
        pixel = new Texture2D(1, 1);
        pixel.SetPixel(0, 0, Color.white);
        pixel.Apply();
    }

    private void OnDestroy()
    {
        if (pixel == null) return;

        if (Application.isPlaying) Destroy(pixel);
        else DestroyImmediate(pixel);
    }

    private void OnGUI()
    {
        if (SceneLoader.Instance != null && SceneLoader.Instance.IsLoading) return;
        if (MissionUIManager.IsHudHidden) return;

        ResolveReferences();
        if (droneManager == null && missionController == null) return;

        int rows = CountRows();
        if (rows <= 0) return;

        EnsureStyles();

        Matrix4x4 previousMatrix = HudStyleService.Begin();

        float headerHeight = rowHeight + 6f;
        float panelHeight = headerHeight + rows * rowHeight + padding;

        Rect panelRect = new Rect(
            margin.x,
            Screen.height - panelHeight - margin.y,
            panelWidth,
            panelHeight);

        DrawRect(panelRect, HudStyleService.ApplyAlpha(panelColor));
        DrawRect(new Rect(panelRect.x, panelRect.y, panelRect.width, headerHeight), HudStyleService.ApplyAlpha(headerColor));

        string missionName = ResolveMissionName();
        DrawRowLabel(new Rect(panelRect.x + padding, panelRect.y + 3f, panelRect.width - padding * 2f, rowHeight), missionName, titleStyle);

        float y = panelRect.y + headerHeight + 3f;

        DrawRow(ref y, panelRect, "БОРТ", BuildDroneLine(), accentColor);
        DrawAmmoRow(ref y, panelRect);
        DrawRow(ref y, panelRect, "ЦЕЛИ", BuildObjectiveLine(), BuildObjectiveColor());
        DrawTimerRow(ref y, panelRect);

        HudStyleService.End(previousMatrix);
    }

    private void ResolveReferences()
    {
        if (droneManager != null && missionController != null) return;
        if (Time.unscaledTime < nextReferenceSearchTime) return;

        nextReferenceSearchTime = Time.unscaledTime + 1f;

        if (droneManager == null) droneManager = FindAnyObjectByType<DroneManager>();
        if (missionController == null) missionController = FindAnyObjectByType<MissionController>();
    }

    private void EnsureStyles()
    {
        if (rowStyle != null) return;

        titleStyle = new GUIStyle { fontSize = titleFontSize, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
        titleStyle.normal.textColor = HudStyleService.Opaque(textColor);

        rowStyle = new GUIStyle { fontSize = rowFontSize, fontStyle = FontStyle.Normal, alignment = TextAnchor.MiddleLeft };
        rowStyle.normal.textColor = HudStyleService.Opaque(secondaryColor);

        valueStyle = new GUIStyle { fontSize = rowFontSize, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleRight };
        valueStyle.normal.textColor = HudStyleService.Opaque(textColor);
    }

    private int CountRows()
    {
        int rows = 1;

        DroneDropMechanism dropMechanism = FindActiveDropMechanism();
        if (dropMechanism != null) rows++;

        if (ResolveObjectiveAvailable()) rows++;
        if (ResolveTimerAvailable()) rows++;

        return rows;
    }

    private bool ResolveObjectiveAvailable()
    {
        if (missionController == null) return false;
        MissionDefinition mission = missionController.Mission;
        return mission != null && mission.objectiveType == MissionObjectiveType.DestroyAllTargets;
    }

    private bool ResolveTimerAvailable()
    {
        if (missionController == null) return false;
        return missionController.Mission != null;
    }

    private string ResolveMissionName()
    {
        if (missionController != null && missionController.Mission != null)
        {
            string displayName = missionController.Mission.displayName;
            if (!string.IsNullOrWhiteSpace(displayName)) return displayName.ToUpperInvariant();
        }

        return "ОПЕРАЦИЯ";
    }

    private string BuildDroneLine()
    {
        if (droneManager == null) return "—";

        int total = droneManager.ActiveSlotCount;
        int current = droneManager.ActiveDroneNumber;
        int alive = droneManager.AliveDronesCount;

        string currentText = current > 0 ? current.ToString() : "—";
        return $"{currentText} / {total}   ЖИВЫХ: {alive}";
    }

    private void DrawAmmoRow(ref float y, Rect panelRect)
    {
        DroneDropMechanism dropMechanism = FindActiveDropMechanism();
        if (dropMechanism == null) return;

        Color color = dropMechanism.AmmoCount > 0 ? textColor : warningColor;
        DrawRow(ref y, panelRect, "БОЕЗАПАС", dropMechanism.AmmoCount.ToString(), color);
    }

    private string BuildObjectiveLine()
    {
        if (missionController == null) return "—";

        MissionDefinition mission = missionController.Mission;
        if (mission == null) return "—";

        switch (mission.objectiveType)
        {
            case MissionObjectiveType.DestroyAllTargets:
                int target = Mathf.Max(1, missionController.TargetCount);
                return $"{missionController.DestroyedTargets} / {target}";

            case MissionObjectiveType.SurviveTime:
                return "ВЫЖИТЬ";

            case MissionObjectiveType.ReachPoint:
                return "ВЫЙТИ В ТОЧКУ";
        }

        return "—";
    }

    private Color BuildObjectiveColor()
    {
        if (missionController == null) return textColor;

        MissionDefinition mission = missionController.Mission;
        if (mission == null) return textColor;
        if (mission.objectiveType != MissionObjectiveType.DestroyAllTargets) return textColor;

        int target = Mathf.Max(1, missionController.TargetCount);
        return missionController.DestroyedTargets >= target ? successColor : textColor;
    }

    private void DrawTimerRow(ref float y, Rect panelRect)
    {
        if (missionController == null) return;

        float limit = missionController.TimeLimit;
        float remaining = missionController.TimeRemaining;

        if (limit > 0f)
        {
            Color color = remaining <= timerWarningThreshold ? warningColor : textColor;
            DrawRow(ref y, panelRect, "ДО КОНЦА", FormatTime(remaining), color);
            return;
        }

        DrawRow(ref y, panelRect, "ВРЕМЯ", FormatTime(missionController.ElapsedTime), secondaryColor);
    }

    private void DrawRow(ref float y, Rect panelRect, string caption, string value, Color valueColor)
    {
        float width = panelRect.width - padding * 2f;

        DrawRowLabel(new Rect(panelRect.x + padding, y, width * 0.45f, rowHeight), caption, rowStyle);

        valueStyle.normal.textColor = HudStyleService.Opaque(valueColor);
        DrawRowLabel(new Rect(panelRect.x + padding, y, width, rowHeight), value, valueStyle);

        y += rowHeight;
    }

    private void DrawRowLabel(Rect rect, string text, GUIStyle style)
    {
        Color previous = GUI.color;
        GUI.color = HudStyleService.ApplyAlpha(Color.white);
        GUI.Label(rect, text, style);
        GUI.color = previous;
    }

    private DroneDropMechanism FindActiveDropMechanism()
    {
        if (droneManager == null) return null;

        GameObject activeDrone = droneManager.GetActiveDrone();
        if (activeDrone == null) return null;

        DroneDropMechanism dropMechanism = activeDrone.GetComponentInChildren<DroneDropMechanism>(true);
        if (dropMechanism == null || !dropMechanism.HasPayload) return null;
        return dropMechanism;
    }

    private static string FormatTime(float seconds)
    {
        float clamped = Mathf.Max(0f, seconds);
        int total = Mathf.FloorToInt(clamped);
        int hours = total / 3600;
        int minutes = (total % 3600) / 60;
        int secs = total % 60;

        if (hours > 0) return $"{hours:D2}:{minutes:D2}:{secs:D2}";
        return $"{minutes:D2}:{secs:D2}";
    }

    private void DrawRect(Rect rect, Color color)
    {
        if (pixel == null) return;

        Color previous = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, pixel);
        GUI.color = previous;
    }
}
