using System;
using UnityEngine;

// Игровое время бункера: одна реальная секунда = одна игровая минута.
//
// В обычном геймплее время тикает по Time.deltaTime: при Time.timeScale = 0
// (ComputerOS, пауза, настройки — всё это держит BunkerModalStack) часы
// останавливаются сами, отдельных проверок не нужно.
//
// Во время сна (IsSleeping = true) автоматическое тиканье выключается,
// а минуты прокручивает BunkerBedInteractable через SetTime — иначе перемотка
// шла бы дважды: и корутиной кровати, и Update.
//
// Смена суток: BunkerStoryProgress.AdvanceDay() вызывается ровно один раз при
// переходе через утро (morningHour, по умолчанию 06:00), даже если игрок не спал,
// а просто дождался утра. Пока идёт сон, день двигает кровать, поэтому
// автопереход подавлен — иначе один и тот же переход считался бы дважды.
public class BunkerTimeManager : MonoBehaviour
{
    private const int MinutesPerDay = 24 * 60;

    [Header("Старт (каждый запуск сцены)")]
    [Range(0, 23)]
    [SerializeField] private int startHour = 6;
    [Range(0, 59)]
    [SerializeField] private int startMinute = 0;

    [Header("Ход времени")]
    [Tooltip("Сколько игровых минут проходит за одну реальную секунду.")]
    [Min(0f)]
    [SerializeField] private float minutesPerRealSecond = 1f;

    [Header("Смена суток")]
    [Tooltip("Утро, при переходе через которое проживаются сутки.")]
    [Range(0, 23)]
    [SerializeField] private int morningHour = 6;
    [Tooltip("Двигать день автоматически, если игрок дождался утра без сна.")]
    [SerializeField] private bool advanceDayAtMorning = true;
    [SerializeField] private BunkerStoryProgress progress;

    [Header("Отладка")]
    [SerializeField] private bool verboseLogging = true;

    private float minuteAccumulator;
    private long absoluteMinutes;   // монотонные игровые минуты от старта сцены
    private long lastMorningIndex;  // сколько раз утро уже наступало

    public int CurrentHour { get; private set; }
    public int CurrentMinute { get; private set; }
    public int TotalMinutesFromMidnight => CurrentHour * 60 + CurrentMinute;
    public string FormattedTime => FormatClock(CurrentHour, CurrentMinute);
    public bool IsSleeping { get; private set; }
    public int MorningHour => morningHour;

    // Точки расширения для сюжетных событий по расписанию.
    public event Action<int, int> OnMinuteChanged;
    public event Action<int> OnHourChanged;

    private void Awake()
    {
        if (progress == null) progress = GetComponent<BunkerStoryProgress>();
        if (progress == null) progress = FindAnyObjectByType<BunkerStoryProgress>();

        ResetToStartTime();
    }

    private void Update()
    {
        if (IsSleeping) return;
        if (minutesPerRealSecond <= 0f) return;

        minuteAccumulator += Time.deltaTime * minutesPerRealSecond;
        if (minuteAccumulator < 1f) return;

        int wholeMinutes = Mathf.FloorToInt(minuteAccumulator);
        minuteAccumulator -= wholeMinutes;
        AddMinutes(wholeMinutes);
    }

    // Возврат к стартовому времени. Вызывается в Awake: прогресс, как и раньше,
    // живёт только в памяти сессии (BunkerStoryProgress.persistProgress = false),
    // поэтому каждый запуск Boot -> MainMenu -> Bunker(main) начинается с 06:00
    // и ничего не пишется в PlayerPrefs.
    public void ResetToStartTime()
    {
        int total = Mathf.Clamp(startHour, 0, 23) * 60 + Mathf.Clamp(startMinute, 0, 59);
        absoluteMinutes = total;
        minuteAccumulator = 0f;
        lastMorningIndex = MorningIndexAt(absoluteMinutes);
        SetClock(total, force: true);

        Log($"Время установлено: {FormattedTime} (1 сек = {minutesPerRealSecond:0.##} мин)");
    }

    // Прямая установка часов и минут. Сутки не меняются: переход через утро
    // считается только при реальном ходе времени (Update / AddMinutes).
    public void SetTime(int hour, int minute)
    {
        int total = Wrap(hour * 60 + minute);
        long dayBase = absoluteMinutes - Wrap(TotalMinutesFromMidnight);
        absoluteMinutes = dayBase + total;
        lastMorningIndex = MorningIndexAt(absoluteMinutes);
        SetClock(total, force: false);
    }

    // Прибавляет минуты с корректным переходом через полночь (23:59 -> 00:00).
    public void AddMinutes(int deltaMinutes)
    {
        if (deltaMinutes == 0) return;

        absoluteMinutes += deltaMinutes;
        SetClock((int)(absoluteMinutes % MinutesPerDay), force: false);

        long morningIndex = MorningIndexAt(absoluteMinutes);
        long crossings = morningIndex - lastMorningIndex;
        if (crossings <= 0) return;

        lastMorningIndex = morningIndex;

        if (!advanceDayAtMorning || IsSleeping)
        {
            Log($"Утро {FormatClock(morningHour, 0)} прошло без смены дня (сон или флаг выключен)");
            return;
        }

        if (progress == null)
        {
            Log("Утро наступило, но BunkerStoryProgress не назначен — день не изменился");
            return;
        }

        for (long i = 0; i < crossings; i++)
        {
            int day = progress.AdvanceDay();
            Log($"Наступило утро {FormatClock(morningHour, 0)} — День {day}");
        }
    }

    // Минуты до ближайшего утра: 06:30 -> 1410, 02:15 -> 225,
    // ровно 06:00 -> полные сутки (1440).
    public int GetMinutesUntilMorning(int morningHourOverride = 6)
    {
        int morning = Mathf.Clamp(morningHourOverride, 0, 23) * 60;
        int delta = morning - TotalMinutesFromMidnight;
        if (delta <= 0) delta += MinutesPerDay;
        return delta;
    }

    // Режим сна: тиканье по Time.deltaTime выключается, время ведёт кровать.
    public void SetSleeping(bool value)
    {
        if (IsSleeping == value) return;

        IsSleeping = value;
        minuteAccumulator = 0f;

        Log(value
            ? "Сон: автоматическое тиканье времени приостановлено"
            : "Сон окончен: время снова идёт само");
    }

    public static string FormatClock(int hour, int minute)
    {
        int h = ((hour % 24) + 24) % 24;
        int m = ((minute % 60) + 60) % 60;
        return $"{h:00}:{m:00}";
    }

    private void SetClock(int totalMinutes, bool force)
    {
        int total = Wrap(totalMinutes);
        int hour = total / 60;
        int minute = total % 60;

        if (!force && hour == CurrentHour && minute == CurrentMinute) return;

        int previousHour = CurrentHour;
        CurrentHour = hour;
        CurrentMinute = minute;

        OnMinuteChanged?.Invoke(hour, minute);
        if (force || hour != previousHour) OnHourChanged?.Invoke(hour);
    }

    // Пол (floor) от деления сдвига на сутки: важно для отрицательного сдвига,
    // когда стартовое время раньше утра (например, 02:00 при morningHour = 6).
    private long MorningIndexAt(long absolute)
    {
        long offset = absolute - morningHour * 60L;
        long index = offset / MinutesPerDay;
        if (offset < 0 && offset % MinutesPerDay != 0) index--;
        return index;
    }

    private static int Wrap(int totalMinutes)
    {
        int wrapped = totalMinutes % MinutesPerDay;
        return wrapped < 0 ? wrapped + MinutesPerDay : wrapped;
    }

    private void Log(string message)
    {
        if (verboseLogging) Debug.Log($"[BunkerTime] {message}", this);
    }
}
