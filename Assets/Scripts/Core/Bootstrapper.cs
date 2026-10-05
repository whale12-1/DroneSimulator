using UnityEngine;
using UnityEngine.SceneManagement;

public class Bootstrapper : MonoBehaviour
{
    [SerializeField] private MissionRegistry missionRegistry; // чтобы грузился раньше UI

    private void Start()
    {
        // Гарантируем, что сервисы-одиночки существуют
        EnsureService<SceneLoader>("SceneLoader");
        EnsureService<SettingsService>("SettingsService");

        GameStateMachine.TransitionTo(GameState.MainMenu);
        SceneLoader.Instance.LoadAsync("MainMenu");
    }

    private void EnsureService<T>(string name) where T : MonoBehaviour
    {
        if (FindFirstObjectByType<T>() != null) return;
        var go = new GameObject(name);
        go.AddComponent<T>();
    }
}