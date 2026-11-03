// Core/SceneLoader.cs
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoader : MonoBehaviour
{
    public static SceneLoader Instance { get; private set; }

    [SerializeField] private CanvasGroup loadingScreen;

    private bool isLoading = false;

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public static void EnsureInstance()
    {
        if (Instance == null)
        {
            GameObject go = new GameObject("SceneLoader");
            go.AddComponent<SceneLoader>();
        }
    }

    public async void LoadAsync(string sceneName)
    {
        if (isLoading) return;
        isLoading = true;

        ShowLoading();

        var op = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(sceneName);
        if (op == null)
        {
            HideLoading();
            isLoading = false;
            return;
        }
        op.allowSceneActivation = false;
        while (op.progress < 0.9f) await System.Threading.Tasks.Task.Yield();
        op.allowSceneActivation = true;
        while (!op.isDone) await System.Threading.Tasks.Task.Yield();

        float hold = 1.0f;
        while (hold > 0f)
        {
            hold -= Time.unscaledDeltaTime;
            await System.Threading.Tasks.Task.Yield();
        }

        HideLoading();
        isLoading = false;
    }

    private void ShowLoading() { if (loadingScreen) { loadingScreen.alpha = 1; loadingScreen.blocksRaycasts = true; } }
    private void HideLoading() { if (loadingScreen) { loadingScreen.alpha = 0; loadingScreen.blocksRaycasts = false; } }
}