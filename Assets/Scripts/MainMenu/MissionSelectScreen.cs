using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class MissionSelectScreen : UIScreen
{
    [Header("Префаб карточки")]
    [SerializeField] private MissionCardView cardPrefab;
    [SerializeField] private Transform cardsContainer;

    [Header("Отладка")]
    [SerializeField] private bool verboseLogging = true;

    private System.Action<MissionDefinition> onPlayCallback;

    public void Populate(MissionRegistry registry, System.Action<MissionDefinition> onPlay)
    {
        onPlayCallback = onPlay;

        if (registry == null) { Debug.LogError("[MissionSelect] registry == NULL"); return; }
        if (cardPrefab == null) { Debug.LogError("[MissionSelect] cardPrefab == NULL"); return; }
        if (cardsContainer == null) { Debug.LogError("[MissionSelect] cardsContainer == NULL"); return; }

        // Чистим контейнер
        for (int i = cardsContainer.childCount - 1; i >= 0; i--)
            Destroy(cardsContainer.GetChild(i).gameObject);

        if (registry.allMissions == null || registry.allMissions.Length == 0)
        {
            Debug.LogWarning("[MissionSelect] registry.allMissions пустой");
            return;
        }

        Log($"Populate: {registry.allMissions.Length} миссий");

        foreach (var mission in registry.allMissions)
        {
            if (mission == null) continue;

            MissionCardView card = Instantiate(cardPrefab, cardsContainer);
            bool unlocked = ProgressService.IsUnlocked(mission);

            Log($"  Карточка: {mission.displayName} (id={mission.id}, unlocked={unlocked})");

            // Клик по карточке = сразу запуск миссии
            card.Bind(mission, unlocked, OnMissionClicked);
        }
    }

    private void OnMissionClicked(MissionDefinition mission)
    {
        Log($"Клик по миссии: {mission.displayName}");
        onPlayCallback?.Invoke(mission);
    }

    private void Log(string msg)
    {
        if (verboseLogging) Debug.Log($"[MissionSelect] {msg}", this);
    }
}