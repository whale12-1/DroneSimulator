using System;
using UnityEngine;

// Сюжетный прогресс бункера: прожитые сутки и флаги стартовых сцен.
//
// По умолчанию (persistProgress = false) прогресс живёт только в памяти текущей
// сессии: при каждом входе в Bunker(main) день = 1, интро и брифинг не пройдены,
// а старые тестовые ключи BunkerStory.* вычищаются из PlayerPrefs. Это удобно,
// чтобы проверять сюжет «как в первый раз» обычным запуском Boot -> MainMenu ->
// Bunker(main). Галочка persistProgress включает запись в PlayerPrefs, чтобы
// позже включить сохранение одной настройкой в Inspector.
public class BunkerStoryProgress : MonoBehaviour
{
    public const string DayKey = "BunkerStory.Day";
    public const string IntroSeenKey = "BunkerStory.IntroSeen";
    public const string BriefingDoneKey = "BunkerStory.BriefingDone";

    private const int FirstDay = 1;

    [Header("Сохранение прогресса")]
    [Tooltip("Выключено (по умолчанию): прогресс только в памяти сессии, ключи BunkerStory.* очищаются при старте. Включите для боевого режима.")]
    [SerializeField] private bool persistProgress = false;

    [Header("Сброс при старте (для тестов)")]
    [Tooltip("Сбросить сюжетный прогресс в начале сцены, даже если persistProgress включён.")]
    [SerializeField] private bool resetOnStart = false;

    [Header("Отладка")]
    [SerializeField] private bool verboseLogging = true;

    // Текущие сутки. Растёт при сне на кровати, событие OnDayPassed даёт точку
    // расширения для будущих сюжетных этапов и веток.
    public int CurrentDay { get; private set; } = FirstDay;

    // Показана ли стартовая реплика героя («Так, я в бункере...»).
    public bool IntroSeen { get; private set; }

    // Завершён ли брифинг командира по рации.
    public bool BriefingDone { get; private set; }

    public bool PersistProgress => persistProgress;

    public event Action<int> OnDayPassed;

    private void Awake()
    {
        if (resetOnStart || !persistProgress)
            ResetToFirstRun();
        else
            Load();
    }

    private void Load()
    {
        CurrentDay = Mathf.Max(FirstDay, PlayerPrefs.GetInt(DayKey, FirstDay));
        IntroSeen = PlayerPrefs.GetInt(IntroSeenKey, 0) == 1;
        BriefingDone = PlayerPrefs.GetInt(BriefingDoneKey, 0) == 1;

        Log($"Загружено из PlayerPrefs: day = {CurrentDay}, intro = {IntroSeen}, briefing = {BriefingDone}");
    }

    private void ResetToFirstRun()
    {
        ClearStoredProgress();

        CurrentDay = FirstDay;
        IntroSeen = false;
        BriefingDone = false;

        Log("Прогресс сброшен к первому запуску: day = 1, intro = false, briefing = false");
    }

    // Увеличивает счётчик суток, сохраняет (если включено) и поднимает событие.
    public int AdvanceDay()
    {
        CurrentDay = Mathf.Max(FirstDay, CurrentDay + 1);
        Save();

        Log($"Прожиты сутки: CurrentDay = {CurrentDay}");
        OnDayPassed?.Invoke(CurrentDay);
        return CurrentDay;
    }

    public void MarkIntroSeen()
    {
        if (IntroSeen) return;

        IntroSeen = true;
        Save();

        Log("Стартовая реплика показана: IntroSeen = true");
    }

    public void MarkBriefingDone()
    {
        if (BriefingDone) return;

        BriefingDone = true;
        Save();

        Log("Брифинг командира завершён: BriefingDone = true");
    }

    // Кнопка/метод сброса для тестирования: видна в контекстном меню компонента
    // (три точки в Inspector) и доступна другим скриптам.
    [ContextMenu("Сбросить сюжетный прогресс")]
    public void ResetStoryProgress()
    {
        ResetToFirstRun();
    }

    private void Save()
    {
        if (!persistProgress) return;

        PlayerPrefs.SetInt(DayKey, CurrentDay);
        PlayerPrefs.SetInt(IntroSeenKey, IntroSeen ? 1 : 0);
        PlayerPrefs.SetInt(BriefingDoneKey, BriefingDone ? 1 : 0);
        PlayerPrefs.Save();
    }

    // Чистим только тестовые ключи ветки бункера, не трогая настройки и прогресс
    // миссий (settings.json / progress.json).
    private void ClearStoredProgress()
    {
        bool hadAny = PlayerPrefs.HasKey(DayKey)
                   || PlayerPrefs.HasKey(IntroSeenKey)
                   || PlayerPrefs.HasKey(BriefingDoneKey);

        PlayerPrefs.DeleteKey(DayKey);
        PlayerPrefs.DeleteKey(IntroSeenKey);
        PlayerPrefs.DeleteKey(BriefingDoneKey);

        if (hadAny) PlayerPrefs.Save();
    }

    private void Log(string message)
    {
        if (verboseLogging) Debug.Log($"[BunkerStory] {message}", this);
    }
}
