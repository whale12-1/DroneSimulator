using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Радиоприёмник на столе оператора.
// E — включить/выключить (плавный старт/остановка + подсветка шкалы),
// R или колесо мыши при взгляде на радио — переключение волны по кругу
// со щелчком настройки. Список треков перетаскивается в Inspector; если он
// пуст, играет процедурный радиошум — без ошибок в консоли.
public class BunkerRadioController : MonoBehaviour, IInteractable, ISecondaryInteractable
{
    [Header("Треки (перетащите сюда MP3/WAV)")]
    [SerializeField] private List<AudioClip> tracks = new List<AudioClip>();

    [Header("Звук")]
    [SerializeField] private AudioSource source;
    [SerializeField] private AudioSource clickSource;
    [Tooltip("Если не назначен, используется процедурный щелчок.")]
    [SerializeField] private AudioClip switchClip;
    [Range(0f, 1f)]
    [SerializeField] private float maxVolume = 0.35f;
    [SerializeField] private float fadeSpeed = 2.5f;

    [Header("Поведение")]
    [SerializeField] private bool startTurnedOn = false;

    [Header("Индикация шкалы")]
    [SerializeField] private Light dialLight;
    [SerializeField] private Renderer dialRenderer;
    [SerializeField] private Color dialColor = new Color(1f, 0.62f, 0.2f);
    [SerializeField] private float dialIntensity = 0.25f;
    [SerializeField] private float dialEmissionStrength = 1.5f;

    public bool IsOn { get; private set; }
    public int TrackIndex => tracks != null && tracks.Count > 0 ? Mathf.Clamp(currentIndex, 0, tracks.Count - 1) : -1;

    private int currentIndex;
    private Coroutine fade;
    private Material dialMaterial;

    private void Awake()
    {
        if (source == null) source = GetComponentInChildren<AudioSource>(true);
        if (source == null) source = gameObject.AddComponent<AudioSource>();
        ConfigureSource();

        EnsureClickSource();

        if (dialLight != null)
        {
            dialLight.color = dialColor;
            dialLight.intensity = 0f;
            dialLight.enabled = false;
        }

        if (dialRenderer != null)
        {
            dialMaterial = dialRenderer.material;
            ApplyDialEmission(0f);
        }
    }

    private void Start()
    {
        if (!startTurnedOn) return;

        IsOn = true;
        source.clip = CurrentClip();
        source.volume = maxVolume;
        source.Play();

        ApplyDial(true);
    }

    private void OnDisable()
    {
        if (fade != null)
        {
            StopCoroutine(fade);
            fade = null;
        }

        if (source != null && source.isPlaying) source.Stop();
    }

    private void ConfigureSource()
    {
        if (source == null) return;

        source.playOnAwake = false;
        source.loop = true;
        source.spatialBlend = 1f;
        source.rolloffMode = AudioRolloffMode.Logarithmic;
        source.minDistance = 0.6f;
        source.maxDistance = 12f;

        if (source.outputAudioMixerGroup == null)
            source.outputAudioMixerGroup = AudioBus.Radio != null ? AudioBus.Radio : AudioBus.Music;

        if (source.volume > maxVolume) source.volume = maxVolume;
    }

    // Отдельный источник для щелчка: громкость музыки может быть на нуле,
    // и PlayOneShot по нему был бы не слышен.
    private void EnsureClickSource()
    {
        if (clickSource == null)
        {
            AudioSource[] sources = GetComponentsInChildren<AudioSource>(true);
            for (int i = 0; i < sources.Length; i++)
            {
                if (sources[i] != null && sources[i] != source)
                {
                    clickSource = sources[i];
                    break;
                }
            }
        }

        if (clickSource == null)
        {
            GameObject host = new GameObject("SwitchClick");
            host.transform.SetParent(transform, false);
            clickSource = host.AddComponent<AudioSource>();
        }

        clickSource.playOnAwake = false;
        clickSource.loop = false;
        clickSource.spatialBlend = 1f;
        clickSource.minDistance = 0.5f;
        clickSource.maxDistance = 10f;
        clickSource.volume = 1f;

        if (clickSource.outputAudioMixerGroup == null) clickSource.outputAudioMixerGroup = AudioBus.Sfx;
    }

    public string GetInteractionPrompt()
    {
        int count = tracks != null ? tracks.Count : 0;

        if (count == 0)
            return "E — Вкл/Выкл · R/колесо — шум эфира";

        string station = $"{TrackIndex + 1}/{count}";

        return IsOn
            ? $"E — Вкл/Выкл · R/колесо — волна {station}"
            : $"E — Вкл/Выкл · R/колесо — выкл ({station})";
    }

    public bool CanInteract(GameObject interactor) => !BunkerModalStack.AnyOpen;

    public void Interact(GameObject interactor)
    {
        if (IsOn) TurnOff();
        else TurnOn();
    }

    public bool CanInteractSecondary(GameObject interactor) => !BunkerModalStack.AnyOpen;

    // direction: 0 — клавиша R (следующая волна), +1 / -1 — колесо мыши.
    public void OnSecondaryInteract(GameObject interactor, float direction)
    {
        ChangeStation(direction < 0f ? -1 : 1);
    }

    public void TurnOn()
    {
        if (IsOn) return;

        IsOn = true;

        source.clip = CurrentClip();
        source.loop = true;
        source.volume = 0f;
        source.Play();

        FadeTo(maxVolume, false);
        ApplyDial(true);
    }

    public void TurnOff()
    {
        if (!IsOn) return;

        IsOn = false;
        ApplyDial(false);

        if (source.isPlaying) FadeTo(0f, true);
    }

    // Переключение волны по кругу. При выключенном радио меняется только
    // выбранная станция и звучит щелчок — музыка не запускается.
    public void ChangeStation(int delta)
    {
        PlaySwitchClick();

        if (tracks == null || tracks.Count == 0) return;

        currentIndex = (currentIndex + delta + tracks.Count) % tracks.Count;

        if (IsOn)
        {
            source.clip = tracks[currentIndex];
            source.loop = true;
            source.Play();
        }
    }

    private void PlaySwitchClick()
    {
        if (clickSource == null) return;

        clickSource.PlayOneShot(switchClip != null ? switchClip : BunkerProceduralAudio.Click(), 0.8f);
    }

    private AudioClip CurrentClip()
    {
        if (tracks != null && tracks.Count > 0)
        {
            currentIndex = Mathf.Clamp(currentIndex, 0, tracks.Count - 1);
            return tracks[currentIndex];
        }

        return BunkerProceduralAudio.RadioStatic();
    }

    private void FadeTo(float target, bool stopWhenDone)
    {
        if (source == null) return;

        if (fade != null)
        {
            StopCoroutine(fade);
            fade = null;
        }

        fade = StartCoroutine(FadeRoutine(target, stopWhenDone));
    }

    private IEnumerator FadeRoutine(float target, bool stopWhenDone)
    {
        float step = fadeSpeed * Mathf.Max(0.05f, maxVolume);

        while (!Mathf.Approximately(source.volume, target))
        {
            source.volume = Mathf.MoveTowards(source.volume, target, Time.unscaledDeltaTime * step);
            yield return null;
        }

        source.volume = target;

        if (stopWhenDone && source.isPlaying) source.Stop();

        fade = null;
    }

    private void ApplyDial(bool on)
    {
        if (dialLight != null)
        {
            dialLight.enabled = on;
            dialLight.intensity = on ? dialIntensity : 0f;
        }

        ApplyDialEmission(on ? 1f : 0f);
    }

    private void ApplyDialEmission(float strength)
    {
        if (dialMaterial == null) return;

        Color emission = dialColor * (strength * dialEmissionStrength);

        if (dialMaterial.HasProperty("emissiveFactor")) dialMaterial.SetColor("emissiveFactor", emission);
        if (dialMaterial.HasProperty("_EmissionColor")) dialMaterial.SetColor("_EmissionColor", emission);
    }
}
