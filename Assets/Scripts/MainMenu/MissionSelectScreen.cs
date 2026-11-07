using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class MissionSelectScreen : UIScreen
{
    [Header("Префаб карточки")]
    [SerializeField] private MissionCardView cardPrefab;
    [SerializeField] private Transform cardsContainer;

    [Header("Панель деталей")]
    [SerializeField] private MissionDetailsPanel detailsPanel;

    [Header("Шапка")]
    [SerializeField] private TMP_Text headerSubtitleLabel;
    [SerializeField] private TMP_Text totalScoreLabel;

    [Header("Отладка")]
    [SerializeField] private bool verboseLogging = true;

    private readonly List<MissionCardView> cards = new();
    private System.Action<MissionDefinition> onPlayCallback;
    private MissionCardView selectedCard;

    public bool IsDetailsOpen => detailsPanel != null && detailsPanel.IsOpen;

    private void Awake()
    {
        SubscribeDetails();
    }

    private void OnDestroy()
    {
        if (detailsPanel != null)
            detailsPanel.LaunchRequested -= OnLaunchRequested;
    }

    public void Populate(MissionRegistry registry, System.Action<MissionDefinition> onPlay)
    {
        onPlayCallback = onPlay;
        SubscribeDetails();
        ClearCards();
        HideDetails();

        if (registry == null) { Debug.LogError("[MissionSelect] registry == NULL"); return; }
        if (cardPrefab == null) { Debug.LogError("[MissionSelect] cardPrefab == NULL"); return; }
        if (cardsContainer == null) { Debug.LogError("[MissionSelect] cardsContainer == NULL"); return; }

        if (registry.allMissions == null || registry.allMissions.Length == 0)
        {
            Debug.LogWarning("[MissionSelect] registry.allMissions пустой");
            UpdateHeader(registry);
            return;
        }

        Log($"Populate: {registry.allMissions.Length} миссий");

        foreach (MissionDefinition mission in registry.allMissions)
        {
            if (mission == null) continue;

            MissionCardView card = Instantiate(cardPrefab, cardsContainer);
            bool unlocked = ProgressService.IsUnlocked(mission);
            bool completed = ProgressService.IsCompleted(mission);

            card.Bind(mission, unlocked, completed, OnMissionClicked);
            card.SetSelected(false);
            cards.Add(card);
        }

        UpdateHeader(registry);
    }

    public void CloseDetails()
    {
        if (selectedCard != null)
        {
            selectedCard.SetSelected(false);
            selectedCard = null;
        }

        HideDetails();
    }

    private void OnMissionClicked(MissionDefinition mission)
    {
        if (mission == null) return;

        MissionCardView card = FindCard(mission);

        if (selectedCard != null && selectedCard != card)
            selectedCard.SetSelected(false);

        selectedCard = card;
        if (selectedCard != null) selectedCard.SetSelected(true);

        ShowDetails(mission);
    }

    private void ShowDetails(MissionDefinition mission)
    {
        if (detailsPanel == null)
        {
            Debug.LogError("[MissionSelect] detailsPanel == NULL");
            return;
        }

        bool unlocked = ProgressService.IsUnlocked(mission);
        bool completed = ProgressService.IsCompleted(mission);
        MissionRecord record = ProgressService.GetRecord(mission);

        Log($"Выбрана миссия: {mission.displayName}");

        detailsPanel.Show(mission, unlocked, completed, record);
    }

    private void OnLaunchRequested(MissionDefinition mission)
    {
        if (mission == null) return;

        Log($"Запуск миссии: {mission.displayName}");
        onPlayCallback?.Invoke(mission);
    }

    private void SubscribeDetails()
    {
        if (detailsPanel == null) return;

        detailsPanel.LaunchRequested -= OnLaunchRequested;
        detailsPanel.LaunchRequested += OnLaunchRequested;
    }

    private void HideDetails()
    {
        if (detailsPanel != null) detailsPanel.Hide();
    }

    private void ClearCards()
    {
        if (cardsContainer != null)
        {
            for (int i = cardsContainer.childCount - 1; i >= 0; i--)
                Destroy(cardsContainer.GetChild(i).gameObject);
        }

        cards.Clear();
        selectedCard = null;
    }

    private MissionCardView FindCard(MissionDefinition mission)
    {
        foreach (MissionCardView card in cards)
        {
            if (card != null && card.Mission == mission)
                return card;
        }

        return null;
    }

    private void UpdateHeader(MissionRegistry registry)
    {
        int total = registry != null && registry.allMissions != null ? registry.allMissions.Length : 0;

        if (headerSubtitleLabel != null)
            headerSubtitleLabel.text = $"ОПЕРАТИВНЫЙ ТЕРМИНАЛ · ОПЕРАЦИЙ: {total}";

        if (totalScoreLabel != null)
            totalScoreLabel.text = $"ОБЩИЙ СЧЁТ: {ProgressService.Data.totalScore}";
    }

    private void Log(string message)
    {
        if (verboseLogging) Debug.Log($"[MissionSelect] {message}", this);
    }
}
