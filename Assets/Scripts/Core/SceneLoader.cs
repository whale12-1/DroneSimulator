// Core/SceneLoader.cs
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoader : MonoBehaviour
{
    public static SceneLoader Instance { get; private set; }

    [SerializeField] private CanvasGroup loadingScreen;

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public async void LoadAsync(string sceneName)
    {
        var op = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(sceneName);
        op.allowSceneActivation = false;
        while (op.progress < 0.9f) await System.Threading.Tasks.Task.Yield();
        op.allowSceneActivation = true;
    }

    private void ShowLoading() { if (loadingScreen) { loadingScreen.alpha = 1; loadingScreen.blocksRaycasts = true; } }
    private void HideLoading() { if (loadingScreen) { loadingScreen.alpha = 0; loadingScreen.blocksRaycasts = false; } }
}