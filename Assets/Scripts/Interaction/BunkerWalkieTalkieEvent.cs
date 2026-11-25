using System;
using System.Collections;
using UnityEngine;

// Сюжетное событие рации на стене.
// 1) Первый запуск: герой произносит стартовую реплику, затем рация начинает
//    звонить (луп + пульсирующая подсветка).
// 2) «E — Взять рацию»: рация уезжает в руку на WalkieTalkieHandAnchor,
//    играет реплика командира.
// 3) По завершении реплики поднимается OnCommanderBriefingFinished, звонок
//    и свечение выключаются, модель возвращается на стену. Позже возврат
//    легко заменить на «положить в инвентарь» — вся точка расширения здесь.
public class BunkerWalkieTalkieEvent : MonoBehaviour, IInteractable
{
    [Header("Сюжет")]
    [SerializeField] private BunkerStoryProgress progress;
    [Tooltip("Показывать стартовую реплику героя, если интро ещё не проиграно.")]
    [SerializeField] private bool requireFirstRunIntro = true;

    [Header("Субтитры (текст и длительность редактируются)")]
    [SerializeField] private BunkerStorySubtitles subtitles;
    [SerializeField] private BunkerStoryLine firstRunLine = new BunkerStoryLine
    {
        speaker = "Персонаж",
        text = "Так, я в бункере. Что я буду делать?",
        duration = 4f
    };
    [SerializeField] private BunkerStoryLine briefingLine = new BunkerStoryLine
    {
        speaker = "Командир",
        text = "Ну что, салага, готов к службе? Пройди сначала инструктаж.",
        duration = 5f
    };

    [Header("Звонок рации")]
    [SerializeField] private AudioSource ringSource;
    [Tooltip("Если клип не назначен, используется процедурный вызов.")]
    [SerializeField] private AudioClip ringClip;
    [Range(0f, 1f)]
    [SerializeField] private float ringVolume = 0.7f;

    [Header("Подсветка")]
    [SerializeField] private Light glowLight;
    [Tooltip("Необязательно: эмиссия на инстансе материала (общий материал не меняется).")]
    [SerializeField] private Renderer glowRenderer;
    [SerializeField] private Color glowColor = new Color(1f, 0.55f, 0.2f);
    [SerializeField] private float glowMinIntensity = 0.15f;
    [SerializeField] private float glowMaxIntensity = 1.2f;
    [SerializeField] private float glowEmissionStrength = 2f;
    [SerializeField] private float glowPulseSpeed = 2.4f;

    [Header("В руке")]
    [SerializeField] private Transform handAnchor;
    [SerializeField] private Vector3 handLocalPosition = new Vector3(0.20f, -0.16f, 0.34f);
    [SerializeField] private Vector3 handLocalEuler = new Vector3(0f, 200f, 20f);
    [SerializeField] private float handScaleMultiplier = 1.6f;
    [SerializeField] private Collider propCollider;
    [SerializeField] private InteractionUIController uiController;

    [Header("Тексты")]
    [SerializeField] private string pickupPrompt = "E — Взять рацию";

    public event Action OnWalkiePickup;
    public event Action OnCommanderBriefingFinished;

    public bool IsRinging { get; private set; }
    public bool IsInHand { get; private set; }
    public bool IsBriefingFinished => briefingFinished;

    private Transform wallParent;
    private Vector3 wallLocalPosition;
    private Quaternion wallLocalRotation;
    private Vector3 wallLocalScale;
    private int wallSiblingIndex;

    private Material glowMaterial;
    private bool briefingFinished;

    private void Awake()
    {
        // Запоминаем «место на стене», чтобы вернуть рацию точно туда же.
        wallParent = transform.parent;
        wallLocalPosition = transform.localPosition;
        wallLocalRotation = transform.localRotation;
        wallLocalScale = transform.localScale;
        wallSiblingIndex = transform.GetSiblingIndex();

        if (propCollider == null) propCollider = GetComponent<Collider>();

        if (ringSource != null)
        {
            ringSource.playOnAwake = false;
            ringSource.loop = true;
            ringSource.volume = ringVolume;
            if (ringSource.outputAudioMixerGroup == null) ringSource.outputAudioMixerGroup = AudioBus.Sfx;
        }

        if (glowLight != null) glowLight.enabled = false;

        if (glowRenderer != null)
        {
            // material (не sharedMaterial) — правим только рантайм-инстанс.
            glowMaterial = glowRenderer.material;
            ApplyEmission(0f);
        }
    }

    private void Start()
    {
        // Возврат в бункер после завершённого брифинга — рация молчит.
        if (progress != null && progress.BriefingDone)
        {
            briefingFinished = true;
            return;
        }

        bool introPending = requireFirstRunIntro && progress != null && !progress.IntroSeen;

        if (introPending && subtitles != null)
            StartCoroutine(IntroThenRing());
        else
            BeginRinging();
    }

    private void OnDisable()
    {
        StopRing();
        SetGlow(false);
    }

    private IEnumerator IntroThenRing()
    {
        bool finished = false;
        subtitles.ShowLine(firstRunLine, () => finished = true);

        while (!finished) yield return null;

        if (progress != null) progress.MarkIntroSeen();

        BeginRinging();
    }

    public void BeginRinging()
    {
        if (IsRinging || IsInHand || briefingFinished) return;

        IsRinging = true;

        EnsureRingSource();
        ringSource.clip = ringClip != null ? ringClip : BunkerProceduralAudio.Ring();
        ringSource.volume = ringVolume;
        ringSource.loop = true;
        ringSource.Play();

        SetGlow(true);
    }

    private void EnsureRingSource()
    {
        if (ringSource != null) return;

        GameObject host = new GameObject("RingAudio");
        host.transform.SetParent(transform, false);

        ringSource = host.AddComponent<AudioSource>();
        ringSource.playOnAwake = false;
        ringSource.loop = true;
        ringSource.spatialBlend = 1f;
        ringSource.minDistance = 0.4f;
        ringSource.maxDistance = 8f;
        ringSource.outputAudioMixerGroup = AudioBus.Sfx;

        Debug.LogWarning("[BunkerWalkieTalkie] ringSource не назначен — создан автоматически.", this);
    }

    private void Update()
    {
        if (!IsRinging) return;

        float k = (Mathf.Sin(Time.unscaledTime * glowPulseSpeed) + 1f) * 0.5f;

        if (glowLight != null)
            glowLight.intensity = Mathf.Lerp(glowMinIntensity, glowMaxIntensity, k);

        ApplyEmission(Mathf.Lerp(0.25f, 1f, k));
    }

    public string GetInteractionPrompt() => pickupPrompt;

    public bool CanInteract(GameObject interactor)
    {
        if (!IsRinging || IsInHand || briefingFinished) return false;
        if (BunkerModalStack.AnyOpen) return false;

        return true;
    }

    public void Interact(GameObject interactor)
    {
        if (!CanInteract(interactor)) return;

        StartCoroutine(PickupRoutine());
    }

    private IEnumerator PickupRoutine()
    {
        IsRinging = false;
        StopRing();
        SetGlow(false);

        if (uiController != null) uiController.HideHint();
        if (propCollider != null) propCollider.enabled = false;

        if (handAnchor != null)
        {
            transform.SetParent(handAnchor, false);
            transform.localPosition = handLocalPosition;
            transform.localRotation = Quaternion.Euler(handLocalEuler);
            transform.localScale = wallLocalScale * Mathf.Max(0.01f, handScaleMultiplier);
            IsInHand = true;
        }
        else
        {
            Debug.LogWarning("[BunkerWalkieTalkie] handAnchor не назначен — рация останется на стене.", this);
        }

        OnWalkiePickup?.Invoke();

        if (subtitles != null)
        {
            bool finished = false;
            subtitles.ShowLine(briefingLine, () => finished = true);

            while (!finished) yield return null;
        }
        else
        {
            yield return new WaitForSecondsRealtime(Mathf.Max(0.5f, briefingLine.duration));
        }

        FinishBriefing();
    }

    // Единая точка завершения разговора: сюда позже добавляется инвентарь
    // вместо возврата модели на стену.
    private void FinishBriefing()
    {
        if (briefingFinished) return;

        briefingFinished = true;
        IsRinging = false;

        StopRing();
        SetGlow(false);

        OnCommanderBriefingFinished?.Invoke();

        if (progress != null) progress.MarkBriefingDone();

        StartCoroutine(ReturnToWallRoutine());
    }

    private IEnumerator ReturnToWallRoutine()
    {
        yield return new WaitForSecondsRealtime(0.35f);

        if (IsInHand)
        {
            transform.SetParent(wallParent, false);
            transform.localPosition = wallLocalPosition;
            transform.localRotation = wallLocalRotation;
            transform.localScale = wallLocalScale;

            if (wallParent != null)
                transform.SetSiblingIndex(Mathf.Clamp(wallSiblingIndex, 0, wallParent.childCount - 1));

            IsInHand = false;
        }

        if (propCollider != null) propCollider.enabled = true;
    }

    private void StopRing()
    {
        if (ringSource != null && ringSource.isPlaying) ringSource.Stop();
    }

    private void SetGlow(bool on)
    {
        if (glowLight != null)
        {
            glowLight.enabled = on;
            glowLight.intensity = on ? glowMinIntensity : 0f;
        }

        ApplyEmission(on ? 0.25f : 0f);
    }

    private void ApplyEmission(float strength)
    {
        if (glowMaterial == null) return;

        Color emission = glowColor * (strength * glowEmissionStrength);

        if (glowMaterial.HasProperty("emissiveFactor")) glowMaterial.SetColor("emissiveFactor", emission);
        if (glowMaterial.HasProperty("_EmissionColor")) glowMaterial.SetColor("_EmissionColor", emission);
    }
}
