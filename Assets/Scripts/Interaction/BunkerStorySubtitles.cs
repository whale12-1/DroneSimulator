using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

// Реплика сюжета: кто говорит, что говорит, сколько держать на экране и
// опциональная озвучка. Редактируется в Inspector у BunkerStorySubtitles
// и у объектов-источников (кровать, рация).
[Serializable]
public class BunkerStoryLine
{
    public string speaker = "Персонаж";

    [TextArea(2, 4)]
    public string text;

    [Min(0.5f)]
    public float duration = 4f;

    public AudioClip voice;
}

// Канал субтитров бункера: очередь реплик, плавное появление/скрытие и
// озвучка (если клип назначен). Работает на unscaled-времени, поэтому
// реплика не «зависает», если открыть паузу.
public class BunkerStorySubtitles : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private CanvasGroup group;
    [SerializeField] private TMP_Text speakerText;
    [SerializeField] private TMP_Text lineText;

    [Header("Озвучка (опционально)")]
    [SerializeField] private AudioSource voiceSource;

    [Header("Настройки")]
    [SerializeField] private float defaultDuration = 4f;
    [SerializeField] private float fadeSpeed = 8f;
    [Tooltip("Отступ плашки от нижнего края, если она оказалась за пределами канваса.")]
    [SerializeField] private float defaultBottomOffset = 64f;

    private readonly Queue<Entry> pending = new Queue<Entry>();
    private Coroutine pump;

    public bool IsPlaying { get; private set; }

    private struct Entry
    {
        public BunkerStoryLine line;
        public Action onFinished;
    }

    private void Awake()
    {
        if (group == null) group = GetComponent<CanvasGroup>();

        if (group != null)
        {
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;
        }

        if (voiceSource != null)
        {
            voiceSource.playOnAwake = false;
            voiceSource.loop = false;
        }
    }

    private void Start()
    {
        EnsureOnScreenLayout();
    }

    // Страховка от «невидимых» субтитров: если якорь/смещение уводят плашку
    // за пределы канваса, возвращаем её к нижнему краю над SittingHint.
    // Именно так ломался вид: pivot внизу + anchoredPosition.y = -365 держали
    // плашку целиком ниже экрана, хотя alpha доходила до 1.
    private void EnsureOnScreenLayout()
    {
        RectTransform rect = transform as RectTransform;
        if (rect == null) return;

        RectTransform canvasRect = rect.parent as RectTransform;
        if (canvasRect == null) return;

        Rect area = canvasRect.rect;
        if (area.height < 1f) return;

        Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(canvasRect, rect);

        bool belowScreen = bounds.max.y < area.yMin + 1f;
        bool aboveScreen = bounds.min.y > area.yMax - 1f;

        if (!belowScreen && !aboveScreen) return;

        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, defaultBottomOffset);

        Debug.Log($"[BunkerStorySubtitles] Плашка субтитров была за пределами канваса и возвращена к нижнему краю (y = {defaultBottomOffset}).", this);
    }

    // Ставит реплику в очередь. onFinished вызывается после скрытия строки —
    // так рация запускает звонок только после слов героя.
    public void ShowLine(BunkerStoryLine line, Action onFinished = null)
    {
        if (line == null || string.IsNullOrWhiteSpace(line.text))
        {
            onFinished?.Invoke();
            return;
        }

        pending.Enqueue(new Entry { line = line, onFinished = onFinished });

        if (pump == null)
            pump = StartCoroutine(Pump());
    }

    // Немедленно скрывает субтитры и очищает очередь.
    public void Hide()
    {
        pending.Clear();
        StopPump();

        if (voiceSource != null && voiceSource.isPlaying)
            voiceSource.Stop();

        if (group != null) group.alpha = 0f;
        IsPlaying = false;
    }

    private void StopPump()
    {
        if (pump == null) return;

        StopCoroutine(pump);
        pump = null;
    }

    private IEnumerator Pump()
    {
        while (pending.Count > 0)
        {
            Entry entry = pending.Dequeue();
            yield return ShowOne(entry.line);

            entry.onFinished?.Invoke();
        }

        pump = null;
    }

    private IEnumerator ShowOne(BunkerStoryLine line)
    {
        IsPlaying = true;

        if (speakerText != null) speakerText.text = line.speaker;
        if (lineText != null) lineText.text = line.text;

        float duration = line.duration > 0.01f ? line.duration : defaultDuration;

        if (voiceSource != null && line.voice != null)
        {
            voiceSource.clip = line.voice;
            voiceSource.Play();

            // Не обрезаем реплику, если озвучка длиннее текстового таймера.
            duration = Mathf.Max(duration, line.voice.length);
        }

        yield return FadeTo(1f);
        yield return new WaitForSecondsRealtime(Mathf.Max(0.2f, duration));
        yield return FadeTo(0f);

        if (voiceSource != null) voiceSource.Stop();

        IsPlaying = false;
    }

    private IEnumerator FadeTo(float target)
    {
        if (group == null) yield break;

        while (!Mathf.Approximately(group.alpha, target))
        {
            group.alpha = Mathf.MoveTowards(group.alpha, target, Time.unscaledDeltaTime * fadeSpeed);
            yield return null;
        }

        group.alpha = target;
    }
}
