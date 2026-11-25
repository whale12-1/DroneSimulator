using System;
using System.Collections;
using UnityEngine;

// Сюжетное событие рации на стене.
// 1) Первый запуск: герой произносит стартовую реплику, затем рация начинает
//    звонить (луп + пульсирующая подсветка).
// 2) «E — Взять рацию»: у ОСНОВНОГО персонажа (Player/Visual/SWAT) вес слоя
//    WalkieArmLayer (AvatarMask RightArmOnly.mask) плавно нарастает 0 -> 1,
//    и в этом слое проигрывается обрезанный клип WalkieReach: родная правая
//    рука тянется вперёд, а ноги, туловище, голова и левая рука остаются
//    под управлением Base Layer.
// 3) В конце клипа рация переносится в WalkieTalkieHandAnchor внутри родной
//    кости mixamorig:RightHand, звонок, мигание и коллайдер выключаются, а
//    Animator плавно (CrossFadeInFixedTime) переходит в застывшую позу
//    WalkieHold. Ориентация «антенной вверх, панелью к игроку» считается один
//    раз при захвате (и уточняется после перехода в позу удержания), дальше
//    рация жёстко следует за локальным поворотом и позицией кисти.
// 4) Удержание: рука держит рацию в кадре ровно holdDuration (5 с) — всё время,
//    пока идёт реплика командира. Поза при этом ЧИСТАЯ, стоковая из Mixamo:
//    все offset-углы костей (плечо, верхняя рука, предплечье, кисть) равны
//    (0, 0, 0), LateUpdate поверх Animator ничего не доворачивает.
// 5) Возврат: рука с рацией снова протягивается к панели (WalkieReach 0 -> 1),
//    в точке максимального выноса рация встаёт на исходное место
//    (ReturnToWall), и только потом пустая рука опускается (WalkieReach 1 -> 0
//    вместе с плавным спадом веса слоя 1 -> 0). Затем вызывается
//    OnCommanderBriefingFinished — точка расширения «положить в инвентарь»
//    вместо возврата на стену.
public class BunkerWalkieTalkieEvent : MonoBehaviour, IInteractable
{
    private const string ShoulderBoneName = "mixamorig:RightShoulder";
    private const string UpperArmBoneName = "mixamorig:RightArm";
    private const string ForeArmBoneName = "mixamorig:RightForeArm";
    private const string HandBoneName = "mixamorig:RightHand";

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

    [Header("Рация в руке")]
    [Tooltip("WalkieTalkieHandAnchor внутри родной кости mixamorig:RightHand персонажа.")]
    [SerializeField] private Transform handAnchor;
    [SerializeField] private Vector3 handLocalPosition = Vector3.zero;
    [Tooltip("Доворот рации в кисти поверх выравнивания «панелью к игроку».")]
    [SerializeField] private Vector3 handLocalEuler = Vector3.zero;
    [Tooltip("Размер рации относительно её размера на стене.")]
    [SerializeField] private float handScaleMultiplier = 0.66f;
    [Tooltip("Совмещать центр габаритов модели с точкой хвата в ладони.")]
    [SerializeField] private bool alignBoundsToGrip = true;
    [SerializeField] private Vector3 gripLocalOffset = Vector3.zero;
    [Tooltip("Разворачивать рацию антенной вверх и панелью к камере в момент захвата.")]
    [SerializeField] private bool alignUprightToCamera = true;
    [SerializeField] private Collider propCollider;
    [SerializeField] private InteractionUIController uiController;
    [Tooltip("Необязательно: камера для доворота рации (по умолчанию Camera.main).")]
    [SerializeField] private Transform cameraTransform;

    [Header("Рука персонажа (Animator + AvatarMask)")]
    [Tooltip("Animator основного персонажа Player/Visual/SWAT.")]
    [SerializeField] private Animator characterAnimator;
    [Tooltip("Слой с маской только на правую руку (Override, вес по умолчанию 0).")]
    [SerializeField] private string layerName = "WalkieArmLayer";
    [Tooltip("Пустое состояние слоя: в покое вес 0, рука живёт в Base Layer.")]
    [SerializeField] private string emptyStateName = "Empty";
    [SerializeField] private string reachStateName = "WalkieReach";
    [SerializeField] private string holdStateName = "WalkieHold";
    [Tooltip("Float-параметр контроллера, масштабирующий скорость клипов слоя.")]
    [SerializeField] private string speedParameterName = "WalkieSpeed";
    [Tooltip("Скорость проигрывания потягивания к рации.")]
    [Min(0.1f)]
    [SerializeField] private float reachSpeed = 0.85f;
    [Tooltip("Плавность перехода WalkieReach -> WalkieHold, секунды.")]
    [Min(0f)]
    [SerializeField] private float holdBlendDuration = 0.15f;
    [Tooltip("Сколько секунд рука ДЕРЖИТ рацию в кадре после входа в позу WalkieHold " +
             "(столько же длится реплика командира).")]
    [Min(0f)]
    [SerializeField] private float holdDuration = 5f;
    [Tooltip("Скорость обратного проигрывания при опускании руки.")]
    [Min(0.1f)]
    [SerializeField] private float lowerSpeed = 0.85f;
    [Tooltip("За сколько вес слоя нарастает 0 -> 1 (рука подхватывается анимацией).")]
    [Min(0f)]
    [SerializeField] private float layerWeightInDuration = 0.2f;
    [Tooltip("За сколько вес слоя уходит 1 -> 0 (рука возвращается в Idle).")]
    [Min(0f)]
    [SerializeField] private float layerWeightOutDuration = 0.25f;

    [Header("Коррекция позы (градусы, LateUpdate поверх Animator)")]
    [Tooltip("Кость mixamorig:RightShoulder: НЕ трогаем (0, 0, 0), иначе ломается куртка.")]
    [SerializeField] private Transform shoulderBone;
    [SerializeField] private Vector3 shoulderLocalEulerOffset = Vector3.zero;
    [Tooltip("Кость mixamorig:RightArm: вынос руки вперёд/в сторону.")]
    [SerializeField] private Transform upperArmBone;
    [SerializeField] private Vector3 upperArmLocalEulerOffset = Vector3.zero;
    [Tooltip("Кость mixamorig:RightForeArm: доворот предплечья, чтобы кисть с рацией была в кадре.")]
    [SerializeField] private Transform foreArmBone;
    [SerializeField] private Vector3 foreArmLocalEulerOffset = Vector3.zero;
    [Tooltip("Кость mixamorig:RightHand: выравнивание запястья (без выворота кисти).")]
    [SerializeField] private Transform handBone;
    [SerializeField] private Vector3 handLocalEulerOffset = Vector3.zero;

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
    private float wallWorldScale = 1f;
    private int wallSiblingIndex;

    private Material glowMaterial;
    private Renderer[] propRenderers;
    private bool briefingFinished;
    private bool pickupInProgress;

    private int layerIndex = -1;
    private bool layerReady;
    private bool hasSpeedParameter;
    private float layerWeight;

    // Текущее состояние доворота кости: базовый поворот Animator и наш результат.
    private struct PoseOffsetState
    {
        public Quaternion baseRotation;
        public Quaternion appliedRotation;
        public bool hasState;
    }

    private readonly PoseOffsetState[] poseStates = new PoseOffsetState[4];

    private void Awake()
    {
        // Запоминаем «место на стене», чтобы вернуть рацию точно туда же.
        wallParent = transform.parent;
        wallLocalPosition = transform.localPosition;
        wallLocalRotation = transform.localRotation;
        wallLocalScale = transform.localScale;
        wallWorldScale = Mathf.Max(0.0001f, transform.lossyScale.x);
        wallSiblingIndex = transform.GetSiblingIndex();

        if (propCollider == null) propCollider = GetComponent<Collider>();
        if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;

        ResolveCharacterArm();

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
        // Слой в покое: вес 0, пустое состояние — родная рука просто висит вдоль тела.
        SetLayerWeight(0f);
        PlayState(emptyStateName, 0f);

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

        // Объект выключили в середине сцены — рация не должна остаться в руке.
        if (IsInHand) ReturnToWall();

        SetLayerWeight(0f);
        ApplyPoseOffsets();
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

        // Фаза 1: вес слоя 0 -> 1, в слое играет WalkieReach — родная рука тянется вперёд.
        yield return RaiseArm();

        // Фаза 2: захват в точке максимального выноса — рация мгновенно уходит
        // в кисть, звонок и мигание выключаются, коллайдер больше не мешает.
        IsRinging = false;
        StopRing();
        SetGlow(false);

        if (propCollider != null) propCollider.enabled = false;

        AttachToHand();
        OnWalkiePickup?.Invoke();

        // Фаза 3: застывшая поза удержания рации под реплику командира.
        PlayHold();
        yield return new WaitForSeconds(Mathf.Max(0f, holdBlendDuration));

        // Финальная посадка уже в позе WalkieHold: CrossFade поворачивает кисть,
        // поэтому ориентацию и точку хвата уточняем после перехода. Дальше рация
        // жёстко следует за локальным поворотом и позицией кисти.
        AlignPropToHand();

        // Фаза 4: рука ДЕРЖИТ рацию в кадре ровно holdDuration секунд (5 с) —
        // всё время, пока идёт реплика командира.
        yield return HoldBriefingPose();

        // Единая точка завершения разговора: сюда позже добавляется инвентарь
        // вместо возврата модели на стену.
        FinishBriefing();

        // Фаза 5: рука с рацией снова протягивается к панели, в точке максимального
        // выноса рация встаёт на исходное место, и только потом пустая рука
        // опускается вниз с плавным спадом веса слоя.
        yield return ReturnWalkieRoutine();

        pickupInProgress = false;
    }

    // Удержание рации в кадре. Рука держит её не меньше holdDuration секунд,
    // реплика командира идёт параллельно: если она короче таймера, рука
    // продолжает держать рацию, если длиннее (озвучка) — ждём и её, чтобы
    // субтитр не оборвался на середине.
    private IEnumerator HoldBriefingPose()
    {
        bool finished = subtitles == null;

        if (subtitles != null) subtitles.ShowLine(briefingLine, () => finished = true);

        float holdSeconds = Mathf.Max(0f, holdDuration);
        float elapsed = 0f;

        while (elapsed < holdSeconds || !finished)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    // Подъём руки: вес слоя нарастает, клип WalkieReach проигрывается из начала.
    // Рендерер руки отдельно не «гейтится» — рука родная и всегда на месте,
    // поэтому T-поза показаться не может.
    private IEnumerator RaiseArm()
    {
        SetLayerWeight(0f);
        PlayState(reachStateName, 0f);
        SetAnimatedSpeed(reachSpeed);

        float length = GetClipLength(reachStateName);
        float reachDuration = length > 0f ? length / Mathf.Max(0.1f, reachSpeed) : 1.1f;
        float rampDuration = Mathf.Max(0.0001f, layerWeightInDuration);

        float elapsed = 0f;

        // Ждём не только расчётную длину клипа, но и фактический конец: Animator
        // становится в состояние не в тот же кадр, поэтому по времени кисть
        // останавливалась на ~75% клипа, и захват происходил раньше максимального
        // выноса руки (замерено в Play Mode 26.11.2026: nt = 0.753).
        while (elapsed < reachDuration || GetReachNormalizedTime() < 0.98f)
        {
            elapsed += Time.deltaTime;
            SetLayerWeight(Mathf.Clamp01(elapsed / rampDuration));
            yield return null;

            // Страховка от зависания, если состояние так и не стало текущим.
            if (elapsed > reachDuration * 2f) break;
        }

        SetLayerWeight(1f);
    }

    // Нормализованное время текущего состояния слоя, если это WalkieReach.
    private float GetReachNormalizedTime()
    {
        if (!layerReady || characterAnimator == null) return 1f;

        AnimatorStateInfo info = characterAnimator.GetCurrentAnimatorStateInfo(layerIndex);
        if (!info.IsName(reachStateName)) return 0f;

        return info.normalizedTime;
    }

    private void PlayHold()
    {
        if (!layerReady) return;

        SetAnimatedSpeed(1f);

        if (holdBlendDuration > 0f)
            characterAnimator.CrossFadeInFixedTime(holdStateName, holdBlendDuration, layerIndex, 0f);
        else
            PlayState(holdStateName, 0f);
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
    }

    // Фаза 5: реалистичный возврат рации на панель.
    //  1) рука с рацией протягивается туда же, где была в момент захвата
    //     (WalkieReach проигрывается вперёд 0 -> 1);
    //  2) в точке максимального выноса (t = 1) рация встаёт на исходное место;
    //  3) пустая рука опускается: WalkieReach 1 -> 0 вместе с плавным спадом
    //     веса слоя 1 -> 0, затем состояние Empty.
    private IEnumerator ReturnWalkieRoutine()
    {
        yield return PlayReachToWall();

        ReturnToWall();

        yield return PlayLowerAndFade();
    }

    // Потягивание обратно к панели: тот же клип WalkieReach проигрывается вперёд
    // 0 -> 1, рация ещё в руке. Из позы WalkieHold входим в клип через CrossFade,
    // а дальше ведём нормализованное время вручную: Play() каждый кадр обрывает
    // блендинг, поэтому блендинг и ручная прокрутка разделены по времени.
    private IEnumerator PlayReachToWall()
    {
        if (!layerReady) yield break;

        float length = GetClipLength(reachStateName);
        if (length <= 0f) yield break;

        SetAnimatedSpeed(1f);

        float duration = length / Mathf.Max(0.1f, reachSpeed);
        float elapsed = 0f;

        if (holdBlendDuration > 0.0001f)
        {
            characterAnimator.CrossFadeInFixedTime(reachStateName, holdBlendDuration, layerIndex, 0f);

            while (elapsed < holdBlendDuration)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }
        }

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            PlayState(reachStateName, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }

        PlayState(reachStateName, 1f);
    }

    // Опускание пустой руки: клип WalkieReach прокручивается от конца к началу,
    // а за последние layerWeightOutDuration секунд вес слоя плавно уходит 1 -> 0 —
    // рука мягко возвращается в Idle базового слоя.
    // Нормализованное время выставляется вручную: отрицательная скорость у
    // незацикленного клипа упирается в конец (speed = 0), поэтому такой способ
    // надёжнее, чем Animator.speed < 0.
    private IEnumerator PlayLowerAndFade()
    {
        if (!layerReady) yield break;

        float length = GetClipLength(reachStateName);
        if (length <= 0f) yield break;

        SetAnimatedSpeed(1f);

        float duration = length / Mathf.Max(0.1f, lowerSpeed);
        float fadeDuration = Mathf.Clamp(layerWeightOutDuration, 0f, duration);
        float fadeStart = Mathf.Max(0f, duration - fadeDuration);

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            PlayState(reachStateName, Mathf.Clamp01(1f - elapsed / duration));

            float weight = 1f;
            if (elapsed > fadeStart)
            {
                weight = fadeDuration <= 0.0001f
                    ? 0f
                    : Mathf.Lerp(1f, 0f, (elapsed - fadeStart) / fadeDuration);
            }

            SetLayerWeight(weight);

            yield return null;
        }

        PlayState(reachStateName, 0f);
        SetLayerWeight(0f);
        PlayState(emptyStateName, 0f);
    }

    private void AttachToHand()
    {
        if (handAnchor == null)
        {
            Debug.LogWarning("[BunkerWalkieTalkie] handAnchor не назначен — рация останется на стене.", this);
            return;
        }

        transform.SetParent(handAnchor, false);

        // Родитель — кость кисти персонажа, а скелет SWAT отмасштабирован (1.2),
        // поэтому локальный масштаб компенсирует lossyScale анкера: мировой размер
        // рации остаётся прежним (0.0264 x handScaleMultiplier).
        float anchorScale = Mathf.Max(0.0001f, handAnchor.lossyScale.x);
        float targetWorldScale = wallWorldScale * Mathf.Max(0.01f, handScaleMultiplier);
        transform.localScale = Vector3.one * (targetWorldScale / anchorScale);

        transform.localPosition = handLocalPosition;
        transform.localRotation = Quaternion.Euler(handLocalEuler);

        // Габариты кэшируем один раз: посадка в ладонь считается по ним.
        propRenderers = GetComponentsInChildren<Renderer>(true);

        IsInHand = true;
        AlignPropToHand();
    }

    // Пока рация в руке, доворачивать её в мире каждый кадр нельзя: при опускании
    // руки мировая вертикаль оставалась бы «прибитой» и рация выкручивалась бы из
    // кисти. Поэтому ориентация и точка хвата считаются один раз (в момент захвата
    // и после перехода в позу удержания), дальше рация жёстко следует за локальным
    // поворотом и позицией кисти.
    private void AlignPropToHand()
    {
        if (handAnchor == null) return;

        if (alignUprightToCamera && cameraTransform != null) AlignUprightToCamera();
        else transform.localRotation = Quaternion.Euler(handLocalEuler);

        if (alignBoundsToGrip) AlignBoundsToGrip();
    }

    // Рация в кисти должна стоять антенной вверх и смотреть панелью на игрока.
    // Локальные оси КОРНЯ модели (их и поворачивает transform.rotation):
    // +Z — высота (антенна), -Y — лицевая панель; на стене при (270, 270, 0)
    // +Z смотрит в мир +Y, а -Y — в комнату (-X). Внутри GLB у меша свои оси
    // (+Y антенна, +Z панель) из-за узла GLTF_SceneRootNode с Rx = 90°, но код
    // работает с корнем, поэтому ориентация задаётся именно парой (+Z, -Y).
    private void AlignUprightToCamera()
    {
        Vector3 looking = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up);
        if (looking.sqrMagnitude < 0.01f) looking = Vector3.ProjectOnPlane(cameraTransform.up, Vector3.up);
        if (looking.sqrMagnitude < 0.01f) looking = Vector3.forward;

        looking.Normalize();

        Quaternion upright = Quaternion.LookRotation(Vector3.up, looking);
        transform.rotation = upright * Quaternion.Euler(handLocalEuler);
    }

    // Пивот модели рации не совпадает с центром габаритов, поэтому после
    // переноса в руку совмещаем центр рендереров с точкой хвата в ладони.
    private void AlignBoundsToGrip()
    {
        if (!TryGetPropBounds(out Bounds bounds)) return;

        Vector3 gripWorld = handAnchor.TransformPoint(gripLocalOffset);
        transform.position += gripWorld - bounds.center;
    }

    private bool TryGetPropBounds(out Bounds bounds)
    {
        Renderer[] renderers = propRenderers != null ? propRenderers : GetComponentsInChildren<Renderer>(true);

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

    // Кость после Animator: доворот плеча/руки/предплечья, чтобы родная кисть
    // с рацией была видна в кадре. Углы масштабируются текущим весом слоя,
    // поэтому коррекция плавно появляется и исчезает вместе с анимацией руки.
    private void LateUpdate()
    {
        ApplyPoseOffsets();
    }

    private void ApplyPoseOffsets()
    {
        ApplyPoseOffset(0, shoulderBone, shoulderLocalEulerOffset);
        ApplyPoseOffset(1, upperArmBone, upperArmLocalEulerOffset);
        ApplyPoseOffset(2, foreArmBone, foreArmLocalEulerOffset);
        ApplyPoseOffset(3, handBone, handLocalEulerOffset);
    }

    private void ApplyPoseOffset(int slot, Transform bone, Vector3 offset)
    {
        if (bone == null) return;

        PoseOffsetState state = poseStates[slot];
        bool active = layerWeight > 0.0001f && offset.sqrMagnitude > 0.0001f;

        Quaternion current = bone.localRotation;

        if (!active)
        {
            // Наш прошлый доворот мог остаться, если Animator в кадре кость не писал
            // (CullUpdateTransforms / timeScale = 0) — возвращаем базовый поворот.
            if (state.hasState && Quaternion.Angle(current, state.appliedRotation) < 0.05f)
                bone.localRotation = state.baseRotation;

            state.hasState = false;
            poseStates[slot] = state;
            return;
        }

        Quaternion baseRotation = current;

        // Если Animator в этом кадре кость не перезаписал, current — это уже наш
        // прошлый доворот: берём сохранённую базу, иначе рука закручивалась бы
        // на offset каждый кадр.
        if (state.hasState && Quaternion.Angle(current, state.appliedRotation) < 0.05f)
            baseRotation = state.baseRotation;

        Quaternion applied = baseRotation * Quaternion.Euler(offset * layerWeight);

        bone.localRotation = applied;

        state.baseRotation = baseRotation;
        state.appliedRotation = applied;
        state.hasState = true;
        poseStates[slot] = state;
    }

    // Поиск слоя, костей и параметра скорости у Animator персонажа.
    private void ResolveCharacterArm()
    {
        if (characterAnimator == null)
        {
            Debug.LogError("[BunkerWalkieTalkie] characterAnimator не назначен — анимация руки не запустится.", this);
            layerReady = false;
            return;
        }

        layerIndex = characterAnimator.GetLayerIndex(layerName);
        layerReady = layerIndex >= 0;

        if (!layerReady)
        {
            Debug.LogError("[BunkerWalkieTalkie] В контроллере " + characterAnimator.runtimeAnimatorController +
                           " нет слоя " + layerName + ".", this);
        }

        if (shoulderBone == null) shoulderBone = FindBone(characterAnimator.transform, ShoulderBoneName);
        if (upperArmBone == null) upperArmBone = FindBone(characterAnimator.transform, UpperArmBoneName);
        if (foreArmBone == null) foreArmBone = FindBone(characterAnimator.transform, ForeArmBoneName);
        if (handBone == null) handBone = FindBone(characterAnimator.transform, HandBoneName);

        hasSpeedParameter = HasParameter(characterAnimator, speedParameterName);
        if (!hasSpeedParameter && layerReady)
        {
            Debug.LogWarning("[BunkerWalkieTalkie] В Animator нет параметра " + speedParameterName +
                             " — скорость клипа фиксированная.", this);
        }
    }

    private static bool HasParameter(Animator animator, string parameterName)
    {
        if (animator == null || string.IsNullOrEmpty(parameterName)) return false;

        AnimatorControllerParameter[] parameters = animator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].name == parameterName) return true;
        }

        return false;
    }

    private void SetLayerWeight(float weight)
    {
        layerWeight = Mathf.Clamp01(weight);

        if (!layerReady) return;

        // Animator мог быть уже уничтожен при выгрузке сцены или остановке Play Mode:
        // Unity бросает MissingReferenceException на SetLayerWeight уничтоженного объекта.
        if (characterAnimator == null)
        {
            layerReady = false;
            return;
        }

        characterAnimator.SetLayerWeight(layerIndex, layerWeight);
    }

    private void PlayState(string stateName, float normalizedTime)
    {
        if (!layerReady || string.IsNullOrEmpty(stateName)) return;
        if (characterAnimator == null) return;

        int hash = Animator.StringToHash(stateName);
        if (!characterAnimator.HasState(layerIndex, hash)) return;

        characterAnimator.Play(hash, layerIndex, normalizedTime);
    }

    private void SetAnimatedSpeed(float value)
    {
        if (!layerReady || !hasSpeedParameter) return;
        if (characterAnimator == null) return;

        characterAnimator.SetFloat(speedParameterName, Mathf.Max(0.01f, value));
    }

    private static Transform FindBone(Transform root, string boneName)
    {
        Transform[] all = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
            if (all[i].name == boneName) return all[i];

        return null;
    }

    // Длительность клипа состояния из контроллера персонажа: на ней построены
    // ожидание конца WalkieReach и время обратного проигрывания.
    private float GetClipLength(string stateName)
    {
        if (characterAnimator == null) return 0f;

        RuntimeAnimatorController controller = characterAnimator.runtimeAnimatorController;
        if (controller == null) return 0f;

        AnimationClip[] clips = controller.animationClips;
        for (int i = 0; i < clips.Length; i++)
        {
            if (clips[i] != null && clips[i].name == stateName) return clips[i].length;
        }

        return 0f;
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
