using TMPro;
using UnityEngine;

// Часы и день на HUD бункера: «ДЕНЬ 1  |  06:00».
//
// Обновление идёт только по событиям BunkerTimeManager.OnMinuteChanged и
// BunkerStoryProgress.OnDayPassed: в Update ничего не опрашивается, поэтому
// виджет не даёт аллокаций на кадр. Строка собирается один раз в игровую минуту.
public class BunkerClockHUD : MonoBehaviour
{
    [Header("Ссылки")]
    [SerializeField] private TMP_Text clockText;
    [SerializeField] private BunkerTimeManager timeManager;
    [SerializeField] private BunkerStoryProgress storyProgress;

    [Header("Формат")]
    [Tooltip("Первое поле — номер дня, второе — время HH:mm.")]
    [SerializeField] private string dayTimeFormat = "ДЕНЬ {0}  |  {1}";
    [SerializeField] private string fallbackTime = "--:--";

    private bool subscribed;

    private void Awake()
    {
        if (clockText == null) clockText = GetComponentInChildren<TMP_Text>(true);
        if (timeManager == null) timeManager = FindAnyObjectByType<BunkerTimeManager>();
        if (storyProgress == null) storyProgress = FindAnyObjectByType<BunkerStoryProgress>();
    }

    private void OnEnable()
    {
        Subscribe();
        Refresh();
    }

    private void Start()
    {
        // Страховка от порядка Awake: к этому моменту BunkerTimeManager уже
        // выставил стартовое время 06:00.
        Refresh();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    // Пересобирает строку «ДЕНЬ N  |  HH:mm». Вызывается по событиям и в Start.
    public void Refresh()
    {
        if (clockText == null) return;

        string time = timeManager != null ? timeManager.FormattedTime : fallbackTime;
        int day = storyProgress != null ? Mathf.Max(1, storyProgress.CurrentDay) : 1;

        clockText.text = string.Format(dayTimeFormat, day, time);
    }

    private void Subscribe()
    {
        if (subscribed) return;
        subscribed = true;

        if (timeManager != null) timeManager.OnMinuteChanged += HandleMinuteChanged;
        if (storyProgress != null) storyProgress.OnDayPassed += HandleDayPassed;
    }

    private void Unsubscribe()
    {
        if (!subscribed) return;
        subscribed = false;

        if (timeManager != null) timeManager.OnMinuteChanged -= HandleMinuteChanged;
        if (storyProgress != null) storyProgress.OnDayPassed -= HandleDayPassed;
    }

    private void HandleMinuteChanged(int hour, int minute) => Refresh();
    private void HandleDayPassed(int day) => Refresh();
}
