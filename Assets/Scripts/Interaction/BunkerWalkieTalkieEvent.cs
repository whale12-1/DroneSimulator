using System;
using System.Collections;
using UnityEngine;

// Сюжетное событие рации на стене.
// 1) Первый запуск: герой произносит стартовую реплику, затем рация начинает
//    звонить (луп + пульсирующая подсветка).
// 2) «E — Взять рацию»: viewmodel-рука (FirstPersonRightArm) поднимается из-за
//    нижнего края и тянется вперёд; в верхней точке рация телепортируется в
//    WalkieTalkieHandAnchor, звонок и мигание выключаются.
// 3) Рука с рацией переходит в удобную позу удержания в правой нижней части
//    кадра, пока играет реплика командира.
// 4) После реплики поднимается OnCommanderBriefingFinished, рука опускается
//    за край экрана, модель возвращается на стену. Позже возврат легко
//    заменить на «положить в инвентарь» — вся точка расширения здесь.
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
        text = "Я в бункере, что мне делать?",
        duration = 4f
    };
    [SerializeField] private BunkerStoryLine briefingLine = new BunkerStoryLine
    {
        speaker = "Командир",
        text = "Ну что, Слава, готов к службе? Пройди сначала инструктаж.",
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
    [SerializeField] private Vector3 handLocalPosition = Vector3.zero;
    [SerializeField] private Vector3 handLocalEuler = new Vector3(270f, 180f, 0f);
    [SerializeField] private float handScaleMultiplier = 0.7f;
    [Tooltip("Совмещать центр габаритов модели с точкой хвата в ладони (пивот GLB не совпадает с центром).")]
    [SerializeField] private bool alignBoundsToGrip = true;
    [SerializeField] private Vector3 gripLocalOffset = Vector3.zero;
    [SerializeField] private Collider propCollider;
    [SerializeField] private InteractionUIController uiController;

    [Header("Рука от первого лица")]
    [Tooltip("Корень viewmodel-руки под Main Camera. В покое выключен.")]
    [SerializeField] private Transform armRoot;
    [Tooltip("Нижняя точка: рука опущена за край экрана и скрыта.")]
    [SerializeField] private Vector3 armRestPosition = new Vector3(0.30f, -0.55f, 0.06f);
    [SerializeField] private Vector3 armRestEuler = new Vector3(-60f, 0f, 0f);
    [Tooltip("Верхняя точка потягивания вперёд, к рации.")]
    [SerializeField] private Vector3 armReachPosition = new Vector3(0.10f, -0.18f, 0.05f);
    [SerializeField] private Vector3 armReachEuler = new Vector3(-10f, -8f, 0f);
    [Tooltip("Поза удержания рации в правой нижней части кадра.")]
    [SerializeField] private Vector3 armHoldPosition = new Vector3(0.24f, -0.19f, 0.02f);
    [SerializeField] private Vector3 armHoldEuler = new Vector3(-14f, -16f, 6f);
    [Min(0.05f)]
    [SerializeField] private float reachDuration = 0.3f;
    [Min(0f)]
    [SerializeField] private float holdBlendDuration = 0.22f;
    [Min(0.05f)]
    [SerializeField] private float lowerDuration = 0.3f;
    [Tooltip("Скрывать руку, когда она не нужна.")]
    [SerializeField] private bool hideArmWhenIdle = true;

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
    private bool pickupInProgress;

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

        // В обычном состоянии рука опущена ниже поля зрения и выключена.
        if (armRoot != null && hideArmWhenIdle) armRoot.gameObject.SetActive(false);
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
        SetArmVisible(false);
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
        if (pickupInProgress) return false;
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
        pickupInProgress = true;

        if (uiController != null) uiController.HideHint();

        // Фаза 1: рука поднимается из-за нижнего края и тянется вперёд к рации.
        yield return RaiseArm();
        yield return MoveArm(armReachPosition, armReachEuler, reachDuration);

        // Фаза 2: захват в верхней точке — рация мгновенно уходит в кисть,
        // звонок и мигание выключаются.
        IsRinging = false;
        StopRing();
        SetGlow(false);

        if (propCollider != null) propCollider.enabled = false;

        AttachToHand();
        OnWalkiePickup?.Invoke();

        // Фаза 3: удобная поза удержания в правой нижней части кадра.
        yield return MoveArm(armHoldPosition, armHoldEuler, holdBlendDuration);

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

        StartCoroutine(LowerArmAndReturnRoutine());
    }

    // Фаза 4: рука с рацией уходит за нижний край экрана и только потом рация
    // возвращается на стену — без «поп-эффекта» посреди кадра.
    private IEnumerator LowerArmAndReturnRoutine()
    {
        yield return MoveArm(armRestPosition, armRestEuler, lowerDuration);

        ReturnToWall();
        SetArmVisible(false);

        pickupInProgress = false;
    }

    private void AttachToHand()
    {
        if (handAnchor == null)
        {
            Debug.LogWarning("[BunkerWalkieTalkie] handAnchor не назначен — рация останется на стене.", this);
            return;
        }

        transform.SetParent(handAnchor, false);
        transform.localRotation = Quaternion.Euler(handLocalEuler);
        transform.localScale = wallLocalScale * Mathf.Max(0.01f, handScaleMultiplier);
        transform.localPosition = handLocalPosition;

        if (alignBoundsToGrip) AlignBoundsToGrip();

        IsInHand = true;
    }

    // Пивот GLB-модели не совпадает с центром габаритов, поэтому после переноса
    // в руку совмещаем центр рендереров с точкой хвата в ладони.
    private void AlignBoundsToGrip()
    {
        if (!TryGetPropBounds(out Bounds bounds)) return;

        Vector3 gripWorld = handAnchor.TransformPoint(gripLocalOffset);
        transform.position += gripWorld - bounds.center;
    }

    private bool TryGetPropBounds(out Bounds bounds)
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);

        bounds = new Bounds(transform.position, Vector3.zero);
        bool hasBounds = false;

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null || !renderers[i].enabled) continue;

            if (!hasBounds)
            {
                bounds = renderers[i].bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderers[i].bounds);
            }
        }

        return hasBounds;
    }

    private void ReturnToWall()
    {
        if (!IsInHand)
        {
            if (propCollider != null) propCollider.enabled = true;
            return;
        }

        transform.SetParent(wallParent, false);
        transform.localPosition = wallLocalPosition;
        transform.localRotation = wallLocalRotation;
        transform.localScale = wallLocalScale;

        if (wallParent != null)
            transform.SetSiblingIndex(Mathf.Clamp(wallSiblingIndex, 0, wallParent.childCount - 1));

        IsInHand = false;

        if (propCollider != null) propCollider.enabled = true;
    }

    private IEnumerator RaiseArm()
    {
        SetArmVisible(true);
        ApplyArmPose(armRestPosition, armRestEuler);

        // Даём кадр, чтобы рука включилась до начала движения.
        yield return null;
    }

    private void SetArmVisible(bool visible)
    {
        if (armRoot == null) return;

        if (visible)
        {
            armRoot.gameObject.SetActive(true);
            return;
        }

        if (hideArmWhenIdle)
        {
            armRoot.gameObject.SetActive(false);
            return;
        }

        ApplyArmPose(armRestPosition, armRestEuler);
    }

    // Плавный переход руки между позами: smoothstep по позиции и Slerp по
    // повороту. Время unscaled — анимация не замирает при timeScale = 0.
    private IEnumerator MoveArm(Vector3 targetPosition, Vector3 targetEuler, float duration)
    {
        if (armRoot == null) yield break;

        Vector3 startPosition = armRoot.localPosition;
        Quaternion startRotation = armRoot.localRotation;
        Quaternion targetRotation = Quaternion.Euler(targetEuler);

        duration = Mathf.Max(0.01f, duration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;

            float t = Mathf.Clamp01(elapsed / duration);
            float eased = t * t * (3f - 2f * t);

            armRoot.localPosition = Vector3.Lerp(startPosition, targetPosition, eased);
            armRoot.localRotation = Quaternion.Slerp(startRotation, targetRotation, eased);

            yield return null;
        }

        ApplyArmPose(targetPosition, targetEuler);
    }

    private void ApplyArmPose(Vector3 localPosition, Vector3 localEuler)
    {
        if (armRoot == null) return;

        armRoot.localPosition = localPosition;
        armRoot.localRotation = Quaternion.Euler(localEuler);
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
