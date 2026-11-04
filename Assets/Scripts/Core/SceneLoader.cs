using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoader : MonoBehaviour
{
    public static SceneLoader Instance { get; private set; }

    [SerializeField] private CanvasGroup loadingScreen;
    [SerializeField] private float postLoadDelay = 1.5f;
    [SerializeField] private float restoreWaitTimeout = 3f;

    private bool isLoading = false;

    public bool IsLoading => isLoading;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError("[SceneLoader] Обнаружен дубликат SceneLoader. Лишний компонент удаляется, GameObject остаётся.");
            Destroy(this);
            return;
        }

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

    public void LoadAsync(string sceneName)
    {
        if (isLoading)
        {
            Debug.LogWarning($"[SceneLoader] Загрузка уже выполняется, повторный вызов для {sceneName} проигнорирован.");
            return;
        }

        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogError("[SceneLoader] Имя сцены не задано.");
            return;
        }

        if (!IsSceneInBuildSettings(sceneName))
        {
            Debug.LogError($"[SceneLoader] Сцена '{sceneName}' не найдена в Build Settings. Загрузка отменена.");
            return;
        }

        StartCoroutine(LoadRoutine(sceneName));
    }

    private static bool IsSceneInBuildSettings(string sceneName)
    {
        int count = SceneManager.sceneCountInBuildSettings;
        for (int i = 0; i < count; i++)
        {
            string path = SceneUtility.GetScenePathByBuildIndex(i);
            if (System.IO.Path.GetFileNameWithoutExtension(path) == sceneName)
                return true;
        }

        return false;
    }

    private IEnumerator LoadRoutine(string sceneName)
    {
        isLoading = true;
        ShowLoading();

        try
        {
            AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
            if (op == null)
            {
                Debug.LogError($"[SceneLoader] Сцена не найдена или не добавлена в Build Settings: {sceneName}");
                yield break;
            }

            op.allowSceneActivation = false;
            while (op.progress < 0.9f)
                yield return null;

            op.allowSceneActivation = true;
            while (!op.isDone)
                yield return null;

            yield return WaitUnscaled(postLoadDelay);
            yield return WaitForBunkerRestore();
        }
        finally
        {
            HideLoading();
            isLoading = false;
        }
    }

    private IEnumerator WaitUnscaled(float seconds)
    {
        float elapsed = 0f;
        while (elapsed < seconds)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    private IEnumerator WaitForBunkerRestore()
    {
        BunkerStateRestorer restorer = FindFirstObjectByType<BunkerStateRestorer>();
        if (restorer == null)
            yield break;

        float deadline = Time.unscaledTime + restoreWaitTimeout;
        while (restorer.IsRestoring && Time.unscaledTime < deadline)
            yield return null;
    }

    private void ShowLoading() { if (loadingScreen) { loadingScreen.alpha = 1; loadingScreen.blocksRaycasts = true; } }
    private void HideLoading() { if (loadingScreen) { loadingScreen.alpha = 0; loadingScreen.blocksRaycasts = false; } }
}
