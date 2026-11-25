using System.Collections;
using UnityEngine;

// Кровать бункера: «E — Спать». Управление блокируется на время затемнения,
// затем день увеличивается (BunkerStoryProgress.AdvanceDay → OnDayPassed),
// экран плавно возвращается. Затемнение делает существующий ScreenFader
// (unscaled-время), второй фейдер не создаётся.
public class BunkerBedInteractable : MonoBehaviour, IInteractable
{
    [Header("Ссылки")]
    [SerializeField] private BunkerStoryProgress progress;
    [SerializeField] private ScreenFader fader;
    [SerializeField] private BunkerStorySubtitles subtitles;
    [SerializeField] private PlayerController playerController;
    [SerializeField] private PlayerInteractionDetector interactionDetector;
    [SerializeField] private InteractionUIController uiController;
    [SerializeField] private BunkerTimeManager timeManager;

    [Header("Подсказка и переход")]
    [SerializeField] private string sleepPrompt = "E — Спать";
    [Tooltip("Текст на чёрном экране, если BunkerTimeManager не назначен (старый режим без перемотки).")]
    [SerializeField] private string fadeMessage = "Проходят сутки...";
    [Tooltip("Текст на чёрном экране во время перемотки: {0} — текущее время.")]
    [SerializeField] private string sleepMessageFormat = "Сон...\n{0}";
    [Tooltip("Текст на отметке 06:00: {0} — номер нового дня.")]
    [SerializeField] private string morningMessageFormat = "День {0} — 06:00";

    [Header("Перемотка времени во сне")]
    [Tooltip("Длительность прокрутки часов на чёрном экране, сек (unscaled-время).")]
    [Min(0.1f)]
    [SerializeField] private float sleepTimelapseDuration = 3f;
    [Tooltip("Пауза на полностью чёрном экране перед началом прокрутки, сек.")]
    [Min(0f)]
    [SerializeField] private float blackPauseSeconds = 0.25f;
    [Tooltip("Пауза на отметке 06:00 перед возвратом в игру, сек.")]
    [Min(0f)]
    [SerializeField] private float morningHoldSeconds = 0.4f;
    [Tooltip("Утро, до которого идёт перемотка.")]
    [Range(0, 23)]
    [SerializeField] private int morningHour = 6;

    [Header("Сообщение после сна")]
    [SerializeField] private bool announceNewDay = true;
    [SerializeField] private string newDaySpeaker = "Персонаж";
    [SerializeField] private float newDayLineDuration = 2.5f;

    [Header("Защита")]
    [Tooltip("Пауза после завершения сна, чтобы одно нажатие не запускало сон дважды.")]
    [SerializeField] private float cooldown = 1f;

    private bool sleeping;
    private float busyUntil;

    private void Awake()
    {
        // Ссылка на часы необязательна в инспекторе: в бункере менеджер один.
        if (timeManager == null) timeManager = FindAnyObjectByType<BunkerTimeManager>();
    }

    public bool IsSleeping => sleeping;

    public string GetInteractionPrompt() => sleepPrompt;

    public bool CanInteract(GameObject interactor)
    {
        if (sleeping) return false;
        if (Time.unscaledTime < busyUntil) return false;
        if (BunkerModalStack.AnyOpen) return false;
        if (playerController != null && playerController.isSitting) return false;

        return true;
    }

    public void Interact(GameObject interactor)
    {
        if (!CanInteract(interactor)) return;

        StartCoroutine(SleepRoutine());
    }

    private IEnumerator SleepRoutine()
    {
        sleeping = true;
        busyUntil = Time.unscaledTime + Mathf.Max(0f, cooldown);

        if (uiController != null) uiController.HideHint();

        int startMinutes = timeManager != null
            ? timeManager.TotalMinutesFromMidnight
            : morningHour * 60;

        int minutesToMorning = timeManager != null
            ? Mathf.Max(0, timeManager.GetMinutesUntilMorning(morningHour))
            : 0;

        // Режим сна включаем до затемнения: тиканье по Time.deltaTime больше не
        // мешает перемотке, а автопереход через 06:00 не сработает вторым разом.
        if (timeManager != null) timeManager.SetSleeping(true);

        // Управление отключаем только на время перехода: камера остаётся,
        // мышь и WASD не двигают персонажа.
        if (interactionDetector != null) interactionDetector.enabled = false;
        if (playerController != null) playerController.enabled = false;

        // 1) Плавно уходим в чёрный. Корутина возвращает управление только после
        //    полного затемнения, поэтому цифры начинают бежать уже на чёрном
        //    экране, а не во время плавного ухода в темноту.
        string sleepMessage = timeManager != null || string.IsNullOrEmpty(fadeMessage)
            ? FormatSleepMessage(startMinutes)
            : fadeMessage;

        if (fader != null) yield return fader.FadeOut(sleepMessage, blackPauseSeconds);
        else yield return new WaitForSecondsRealtime(1f);

        // 2) Прокрутка: smoothstep даёт лёгкий разгон в середине и замедление к 06:00.
        float duration = Mathf.Max(0.1f, sleepTimelapseDuration);
        float elapsed = 0f;
        int shownMinutes = -1;

        while (elapsed < duration && minutesToMorning > 0)
        {
            elapsed += Time.unscaledDeltaTime;

            float t = Mathf.Clamp01(elapsed / duration);
            float eased = t * t * (3f - 2f * t);
            int scrolled = Mathf.Clamp(Mathf.RoundToInt(minutesToMorning * eased), 0, minutesToMorning);

            if (scrolled != shownMinutes)
            {
                shownMinutes = scrolled;
                ApplyScrolledMinutes(startMinutes, scrolled);
            }

            yield return null;
        }

        // 3) Фиксируем ровно 06:00 — даже если последний кадр не дошёл до конца.
        if (timeManager != null) timeManager.SetTime(morningHour, 0);

        int day = 1;
        if (progress != null)
        {
            day = progress.AdvanceDay();
        }
        else
        {
            Debug.LogWarning("[BunkerBedInteractable] BunkerStoryProgress не назначен: день не изменился.", this);
        }

        // 4) Короткая пауза на отметке 06:00: игрок видит новый день до возврата в игру.
        if (fader != null)
        {
            fader.SetMessage(day > 0
                ? string.Format(morningMessageFormat, day)
                : FormatSleepMessage(morningHour * 60));
        }

        if (morningHoldSeconds > 0f) yield return new WaitForSecondsRealtime(morningHoldSeconds);

        if (timeManager != null) timeManager.SetSleeping(false);

        if (fader != null) yield return fader.FadeIn();
        else yield return new WaitForSecondsRealtime(1f);

        if (playerController != null) playerController.enabled = true;
        if (interactionDetector != null) interactionDetector.enabled = true;

        if (announceNewDay && subtitles != null && day > 0)
        {
            subtitles.ShowLine(new BunkerStoryLine
            {
                speaker = newDaySpeaker,
                text = $"День {day}.",
                duration = newDayLineDuration
            });
        }

        sleeping = false;
    }

    // «Сон...\nHH:mm»: totalMinutes уже может быть больше 1440 (переход через полночь).
    private string FormatSleepMessage(int totalMinutes)
    {
        int wrapped = ((totalMinutes % 1440) + 1440) % 1440;
        return string.Format(sleepMessageFormat, BunkerTimeManager.FormatClock(wrapped / 60, wrapped % 60));
    }

    // Перематывает часы на scrolledMinutes от момента засыпания и обновляет
    // текст на чёрном экране. Без менеджера времени прокручивается только текст.
    private void ApplyScrolledMinutes(int startMinutes, int scrolledMinutes)
    {
        int total = startMinutes + scrolledMinutes;

        if (timeManager != null) timeManager.SetTime((total / 60) % 24, total % 60);
        if (fader != null) fader.SetMessage(FormatSleepMessage(total));
    }

}
