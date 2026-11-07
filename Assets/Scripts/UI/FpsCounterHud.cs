using UnityEngine;

public class FpsCounterHud : MonoBehaviour
{
    private const string HostName = "FpsCounterHUD";
    private const float Smoothing = 0.1f;
    private const float Margin = 12f;
    private const int BaseFontSize = 16;

    private static FpsCounterHud instance;

    private readonly GUIStyle counterStyle = new GUIStyle();
    private float smoothedDeltaTime;
    private bool styleInitialized;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateHost()
    {
        if (instance != null) return;

        GameObject host = new GameObject(HostName);
        DontDestroyOnLoad(host);
        host.AddComponent<FpsCounterHud>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    private void Update()
    {
        float unscaledDeltaTime = Time.unscaledDeltaTime;
        if (unscaledDeltaTime <= 0f) return;

        if (smoothedDeltaTime <= 0f)
            smoothedDeltaTime = unscaledDeltaTime;
        else
            smoothedDeltaTime = Mathf.Lerp(smoothedDeltaTime, unscaledDeltaTime, Smoothing);
    }

    private void OnGUI()
    {
        if (!ShouldDraw()) return;

        float fps = smoothedDeltaTime > 0.0001f ? 1f / smoothedDeltaTime : 0f;
        float scale = HudStyleService.Scale;
        float alpha = HudStyleService.Transparency;

        if (!styleInitialized)
        {
            counterStyle.alignment = TextAnchor.MiddleRight;
            counterStyle.fontStyle = FontStyle.Bold;
            counterStyle.normal.textColor = Color.white;
            styleInitialized = true;
        }

        counterStyle.fontSize = Mathf.Max(8, Mathf.RoundToInt(BaseFontSize * scale));

        string text = "FPS: " + fps.ToString("F0");
        Vector2 size = counterStyle.CalcSize(new GUIContent(text));
        float margin = Margin * scale;

        Rect rect = new Rect(Screen.width - size.x - margin, margin, size.x, size.y);

        Color previousColor = GUI.color;
        GUI.color = new Color(0.65f, 1f, 0.65f, alpha);
        GUI.Label(rect, text, counterStyle);
        GUI.color = previousColor;
    }

    private static bool ShouldDraw()
    {
        SettingsService service = SettingsService.Instance;
        if (service == null || service.Data == null || service.Data.gameplay == null) return false;
        if (!service.Data.gameplay.showFpsCounter) return false;
        if (GameStateMachine.Current != GameState.Gameplay) return false;
        if (Time.timeScale <= 0f) return false;
        if (SceneLoader.Instance != null && SceneLoader.Instance.IsLoading) return false;

        return true;
    }
}
