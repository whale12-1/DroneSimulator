using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MissionDetailsPanel : MonoBehaviour
{
    [SerializeField] private CanvasGroup group;
    [SerializeField] private GameObject emptyState;
    [SerializeField] private GameObject contentRoot;
    [SerializeField] private Image bannerImage;
    [SerializeField] private TMP_Text bannerCodeText;
    [SerializeField] private Image statusIcon;
    [SerializeField] private TMP_Text statusLabel;
    [SerializeField] private TMP_Text rewardLabel;
    [SerializeField] private TMP_Text titleLabel;
    [SerializeField] private TMP_Text descriptionLabel;
    [SerializeField] private TMP_Text timeValue;
    [SerializeField] private TMP_Text scoreValue;
    [SerializeField] private TMP_Text completionsValue;
    [SerializeField] private TMP_Text targetsValue;
    [SerializeField] private TMP_Text noteText;
    [SerializeField] private TMP_Text[] goalLines;
    [SerializeField] private Image[] goalMarkers;
    [SerializeField] private TMP_Text techLine;
    [SerializeField] private Button launchButton;
    [SerializeField] private TMP_Text launchLabel;
    [SerializeField] private Color completedColor = new Color(0.45f, 0.80f, 0.42f, 1f);
    [SerializeField] private Color pendingColor = new Color(0.92f, 0.72f, 0.25f, 1f);
    [SerializeField] private Color lockedColor = new Color(0.90f, 0.45f, 0.45f, 1f);
    [SerializeField] private float revealDuration = 0.16f;

    public event Action<MissionDefinition> LaunchRequested;

    public bool IsOpen { get; private set; }

    private MissionDefinition current;
    private bool unlocked;
    private bool initialized;
    private Coroutine revealRoutine;

    private void Awake()
    {
        EnsureInit();
        ShowEmpty();
    }

    public void Show(MissionDefinition mission, bool isUnlocked, bool isCompleted, MissionRecord record)
    {
        if (mission == null)
        {
            ShowEmpty();
            return;
        }

        EnsureInit();

        current = mission;
        unlocked = isUnlocked;
        IsOpen = true;

        if (emptyState != null) emptyState.SetActive(false);
        if (contentRoot != null) contentRoot.SetActive(true);
        if (group != null)
        {
            group.alpha = 1f;
            group.interactable = true;
            group.blocksRaycasts = true;
        }

        ApplyBanner(mission);
        ApplyStatus(isUnlocked, isCompleted);
        ApplyReward(mission, isUnlocked);
        ApplyDescription(mission);
        ApplyStats(mission, isCompleted, record);
        ApplyGoals(mission, isCompleted);
        ApplyTech(mission);
        ApplyNote(mission, isUnlocked, isCompleted, record);
        ApplyLaunch(isUnlocked);

        Reveal();
    }

    public void ShowEmpty()
    {
        EnsureInit();

        current = null;
        unlocked = false;
        IsOpen = false;

        if (emptyState != null) emptyState.SetActive(true);
        if (contentRoot != null) contentRoot.SetActive(false);
        if (group != null)
        {
            group.alpha = 1f;
            group.interactable = true;
            group.blocksRaycasts = true;
        }
    }

    public void Hide()
    {
        ShowEmpty();
    }

    public void LaunchCurrent()
    {
        OnLaunchClicked();
    }

    private void EnsureInit()
    {
        if (initialized) return;
        initialized = true;

        if (group == null) group = GetComponent<CanvasGroup>();

        if (launchButton != null)
        {
            launchButton.onClick.RemoveAllListeners();
            launchButton.onClick.AddListener(OnLaunchClicked);
        }
    }

    private void OnLaunchClicked()
    {
        if (!IsOpen || current == null || !unlocked) return;
        LaunchRequested?.Invoke(current);
    }

    private void ApplyBanner(MissionDefinition mission)
    {
        if (bannerImage == null) return;

        bool hasPreview = mission.preview != null;
        bannerImage.sprite = hasPreview ? mission.preview : null;
        bannerImage.color = hasPreview ? Color.white : new Color(0.10f, 0.11f, 0.13f, 1f);

        if (bannerCodeText == null) return;

        bannerCodeText.text = hasPreview ? string.Empty : $"ID: {mission.id.ToUpperInvariant()}";
        bannerCodeText.gameObject.SetActive(!hasPreview);
    }

    private void ApplyStatus(bool isUnlocked, bool isCompleted)
    {
        Color color = isUnlocked ? (isCompleted ? completedColor : pendingColor) : lockedColor;
        string text = isUnlocked ? (isCompleted ? "ВЫПОЛНЕНО" : "НЕ ВЫПОЛНЕНО") : "ЗАБЛОКИРОВАНО";

        if (statusIcon != null) statusIcon.color = color;
        if (statusLabel != null)
        {
            statusLabel.text = text;
            statusLabel.color = Color.white;
        }
    }

    private void ApplyReward(MissionDefinition mission, bool isUnlocked)
    {
        if (rewardLabel == null) return;

        if (!isUnlocked)
        {
            rewardLabel.text = "ДОСТУП ЗАКРЫТ";
            rewardLabel.color = new Color(1f, 1f, 1f, 0.45f);
            return;
        }

        int maxScore = ProgressService.ComputeScore(mission, MissionResult.Success, 0f, mission.targetCount);
        rewardLabel.text = $"НАГРАДА +{maxScore}";
        rewardLabel.color = new Color(1f, 1f, 1f, 0.7f);
    }

    private void ApplyDescription(MissionDefinition mission)
    {
        if (titleLabel != null) titleLabel.text = mission.displayName;

        if (descriptionLabel == null) return;

        descriptionLabel.text = string.IsNullOrWhiteSpace(mission.description)
            ? "Описание операции не задано."
            : mission.description;
    }

    private void ApplyStats(MissionDefinition mission, bool isCompleted, MissionRecord record)
    {
        bool hasStats = isCompleted && record != null && record.bestTime > 0f;
        bool hasRecord = isCompleted && record != null;

        SetValue(timeValue, hasStats ? MissionText.Duration(record.bestTime) : "—:—");
        SetValue(scoreValue, hasRecord && record.bestScore > 0 ? record.bestScore.ToString() : "—");
        SetValue(completionsValue, hasRecord ? record.completions.ToString() : (isCompleted ? "—" : "0"));
        SetValue(targetsValue, hasRecord ? $"{record.bestTargets} / {mission.targetCount}" : (isCompleted ? $"— / {mission.targetCount}" : $"0 / {mission.targetCount}"));
    }

    private void ApplyGoals(MissionDefinition mission, bool isCompleted)
    {
        Color color = isCompleted ? completedColor : new Color(1f, 1f, 1f, 0.22f);
        SetGoal(0, MissionText.Objective(mission), color);
        SetGoal(1, "Не потерять все борта", color);
        SetGoal(2, $"Лимит времени: {MissionText.TimeLimit(mission.timeLimit)}", color);
    }

    private void SetGoal(int index, string text, Color color)
    {
        if (goalLines != null && index < goalLines.Length && goalLines[index] != null)
            goalLines[index].text = text;

        if (goalMarkers != null && index < goalMarkers.Length && goalMarkers[index] != null)
            goalMarkers[index].color = color;
    }

    private void ApplyTech(MissionDefinition mission)
    {
        if (techLine == null) return;

        int drones = mission.availableDrones != null ? mission.availableDrones.Length : 0;
        techLine.text = $"ТЕХНИКА: {MissionText.DroneRole(mission.defaultDrone)} · БОРТОВ: {drones}";
    }

    private void ApplyNote(MissionDefinition mission, bool isUnlocked, bool isCompleted, MissionRecord record)
    {
        if (noteText == null) return;

        if (!isUnlocked)
        {
            string required = BuildRequiredList(mission);
            noteText.text = string.IsNullOrEmpty(required) ? "ТРЕБУЕТСЯ ДОСТУП" : $"ТРЕБУЕТСЯ: пройти «{required}»";
            noteText.color = new Color(1f, 1f, 1f, 0.85f);
            noteText.gameObject.SetActive(true);
            return;
        }

        if (isCompleted && (record == null || record.bestTime <= 0f))
        {
            noteText.text = "Время и счёт появятся после следующего прохождения";
            noteText.color = new Color(1f, 1f, 1f, 0.5f);
            noteText.gameObject.SetActive(true);
            return;
        }

        if (!string.IsNullOrWhiteSpace(mission.tacticalHint))
        {
            noteText.text = $"СОВЕТ: {mission.tacticalHint}";
            noteText.color = new Color(1f, 1f, 1f, 0.7f);
            noteText.gameObject.SetActive(true);
            return;
        }

        if (!isCompleted)
        {
            noteText.text = "Результат появится после первого прохождения";
            noteText.color = new Color(1f, 1f, 1f, 0.5f);
            noteText.gameObject.SetActive(true);
            return;
        }

        noteText.text = string.Empty;
        noteText.gameObject.SetActive(false);
    }

    private void ApplyLaunch(bool isUnlocked)
    {
        if (launchLabel != null)
            launchLabel.text = isUnlocked ? "ЗАПУСТИТЬ МИССИЮ" : "НЕДОСТУПНО";

        if (launchButton != null)
            launchButton.interactable = isUnlocked;
    }

    private static string BuildRequiredList(MissionDefinition mission)
    {
        if (mission.prerequisites == null || mission.prerequisites.Length == 0) return string.Empty;

        System.Text.StringBuilder builder = new System.Text.StringBuilder();
        for (int i = 0; i < mission.prerequisites.Length; i++)
        {
            MissionDefinition prerequisite = mission.prerequisites[i];
            if (prerequisite == null) continue;

            if (builder.Length > 0) builder.Append(", ");
            builder.Append(prerequisite.displayName);
        }

        return builder.ToString();
    }

    private static void SetValue(TMP_Text label, string value)
    {
        if (label != null) label.text = value;
    }

    private void Reveal()
    {
        if (!isActiveAndEnabled || group == null) return;

        if (revealRoutine != null) StopCoroutine(revealRoutine);
        revealRoutine = StartCoroutine(RevealRoutine());
    }

    private IEnumerator RevealRoutine()
    {
        float duration = Mathf.Max(0.01f, revealDuration);
        float time = 0f;

        while (time < duration)
        {
            time += Time.unscaledDeltaTime;
            group.alpha = Mathf.Lerp(0.45f, 1f, Mathf.Clamp01(time / duration));
            yield return null;
        }

        group.alpha = 1f;
        revealRoutine = null;
    }
}
