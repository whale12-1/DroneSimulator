using System.Collections.Generic;
using DroneSimulator.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SettingsPanel : UIScreen
{
    // Сколько секунд «Подтвердить?» ждёт повторного клика по «Сброс».
    private const float ResetConfirmSeconds = 3f;

    // Панель закрылась (X, Esc или вызов Hide из паузы). MainMenuController
    // подписан на событие, поэтому возврат главной панели не зависит от
    // persistent-вызова в сцене.
    public event System.Action Closed;

    [SerializeField] private Button[] tabButtons;
    [SerializeField] private SettingsListBuilder listBuilder;
    [SerializeField] private Button closeButton;
    [SerializeField] private Button resetButton;
    [SerializeField] private ScrollRect listScroll;
    [SerializeField] private bool hideOnStart = true;
    [SerializeField] private Color selectedTabColor = new Color(1f, 1f, 1f, 1f);
    [SerializeField] private Color normalTabColor = new Color(0.55f, 0.57f, 0.6f, 1f);

    private int currentTab;

    // Двухшаговое подтверждение «Сброс» → «Подтвердить?»: текст меняется на самой
    // кнопке, её размеры (168×44) и вёрстка шапки не затрагиваются.
    private TMP_Text resetLabel;
    private string resetLabelDefault = "Сброс";
    private float resetConfirmDeadline = -1f;

    public bool IsOpen { get; private set; }

    private void Awake()
    {
        // Панель в Screen Space canvas обязана лежать в плоскости Z = 0.
        // GraphicRaycaster для Overlay-канваса пускает луч из z = -100 в сторону +z,
        // поэтому инстанс, смещённый по Z (в MainMenu он был на -429), полностью
        // перестаёт принимать клики: кнопки и ползунки видимы, но не нажимаются.
        NormalizeZ();
        ResolveMissingReferences();
    }

    private void Start()
    {
        SettingsService.EnsureInstance();
        ResolveMissingReferences();

        if (closeButton != null) closeButton.onClick.AddListener(Hide);

        if (resetButton != null)
        {
            resetLabel = resetButton.GetComponentInChildren<TMP_Text>(true);
            if (resetLabel != null) resetLabelDefault = resetLabel.text;

            // Снимаем возможную прежнюю подписку, чтобы сброс не срабатывал дважды.
            resetButton.onClick.RemoveListener(ResetSettings);
            resetButton.onClick.RemoveListener(OnResetClicked);
            resetButton.onClick.AddListener(OnResetClicked);
        }
        else
        {
            Debug.LogWarning("[SettingsPanel] Кнопка ResetButton не найдена в иерархии панели — сброс настроек недоступен.", this);
        }

        if (tabButtons != null)
        {
            for (int i = 0; i < tabButtons.Length; i++)
            {
                if (tabButtons[i] == null) continue;
                int index = i;
                tabButtons[i].onClick.AddListener(() => SelectTab(index));
            }
        }

        // Звуки интерфейса: клики по вкладкам, сбросу, закрытию и строкам настроек.
        // Строки создаются позже (Build), поэтому UISoundService дополнительно
        // подписывается на динамические кнопки при повторном вызове Attach.
        UISoundService.Attach(gameObject);

        if (hideOnStart) Hide();
        else Show();
    }

    public override void Show()
    {
        // Страховка: панель всегда должна быть поверх остальных окон этого канваса
        // и в плоскости Z = 0, иначе часть UI перестаёт получать клики.
        NormalizeZ();
        transform.SetAsLastSibling();

        RebindService.ApplySavedBindings();

        base.Show();
        IsOpen = true;

        // В бункере настройки — ещё одно модальное окно: координатор сам решит,
        // держать ли timeScale = 0 (под ними открыта пауза) или вернуть управление.
        BunkerModalStack.NotifySettingsChanged(this);

        SelectTab(currentTab);
    }

    public override void Hide()
    {
        bool wasOpen = IsOpen;

        IsOpen = false;
        base.Hide();

        // Раскрытый дропдаун не должен «запоминаться» до следующего открытия панели,
        // а недожатое подтверждение сброса — висеть на кнопке.
        if (listBuilder != null) listBuilder.CollapseDropdown();
        CancelResetConfirmation();

        BunkerModalStack.NotifySettingsChanged(this);

        if (wasOpen) Closed?.Invoke();
    }

    private void Update()
    {
        if (resetConfirmDeadline < 0f) return;
        if (Time.unscaledTime < resetConfirmDeadline) return;

        // 3 секунды без подтверждения — возвращаем «Сброс».
        CancelResetConfirmation();
    }

    // Первый клик только взводит подтверждение, второй (в течение 3 с) выполняет сброс.
    public void OnResetClicked()
    {
        if (resetConfirmDeadline > 0f && Time.unscaledTime <= resetConfirmDeadline)
        {
            CancelResetConfirmation();
            ResetSettings();
            return;
        }

        resetConfirmDeadline = Time.unscaledTime + ResetConfirmSeconds;
        if (resetLabel != null) resetLabel.text = "Подтвердить?";
    }

    private void CancelResetConfirmation()
    {
        resetConfirmDeadline = -1f;
        if (resetLabel != null) resetLabel.text = resetLabelDefault;
    }

    public void ResetSettings()
    {
        // Панель может быть открыта в сцене, запущенной без Boot (например, прямой запуск Bunker).
        SettingsService.EnsureInstance();

        SettingsService service = SettingsService.Instance;
        if (service == null)
        {
            Debug.LogError("[SettingsPanel] SettingsService недоступен — сброс настроек невозможен.", this);
            return;
        }

        // Звуки интерфейса вешаются на кнопки панели (клик, сброс, закрытие, вкладки).
        UISoundService.Attach(gameObject);

        service.ResetAll();
        RebindService.ResetAllBindings();

        // ResetAll() заменяет Data целиком, поэтому строки нужно собрать заново:
        // дескрипторы хранят замыкания на старые объекты graphics/controls/audio/gameplay.
        if (tabButtons != null && tabButtons.Length > 0) SelectTab(currentTab);
        else if (listBuilder != null) listBuilder.Build(GetDescriptors(currentTab));

        Debug.Log("[SettingsPanel] Настройки сброшены к значениям по умолчанию.", this);
    }

    // Кнопки CloseButton/ResetButton входят в префаб, но ссылки всё равно ищем
    // по имени, если инспектор их не назначил (страховка для старых инстансов).
    private void ResolveMissingReferences()
    {
        if (listBuilder == null) listBuilder = GetComponentInChildren<SettingsListBuilder>(true);
        if (listScroll == null) listScroll = GetComponentInChildren<ScrollRect>(true);
        if (closeButton == null) closeButton = FindButton("CloseButton");
        if (resetButton == null) resetButton = FindButton("ResetButton");
    }

    private Button FindButton(string buttonName)
    {
        Button[] buttons = GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i] != null && buttons[i].name == buttonName) return buttons[i];
        }

        return null;
    }

    private void NormalizeZ()
    {
        if (!(transform is RectTransform rt)) return;

        Vector3 position = rt.localPosition;
        if (Mathf.Abs(position.z) < 0.001f) return;

        // Лог только для случая, когда Z действительно ломает рейкаст:
        // луч Overlay-канваса стартует из z = -100 и идёт в сторону +z.
        if (position.z <= -100f)
            Debug.Log($"[SettingsPanel] Z панели был {position.z:F2}. Сброшено в 0: в Screen Space canvas GraphicRaycaster не попадает в UI, смещённый по Z, и клики по слайдерам/кнопкам не работают.", this);

        rt.localPosition = new Vector3(position.x, position.y, 0f);
    }

    public void SelectTab(int index)
    {
        if (tabButtons == null || index < 0 || index >= tabButtons.Length) return;
        currentTab = index;

        for (int i = 0; i < tabButtons.Length; i++)
        {
            Button tab = tabButtons[i];
            if (tab == null) continue;

            bool isSelected = i == index;

            // ColorTint применяется только при смене состояния Selectable, поэтому
            // цвет вкладки задаём напрямую: иначе активная вкладка визуально не отличалась.
            if (tab.targetGraphic is Image image)
                image.color = isSelected ? selectedTabColor : normalTabColor;

            ColorBlock cb = tab.colors;
            cb.normalColor = Color.white;
            cb.selectedColor = Color.white;
            cb.highlightedColor = new Color(1.12f, 1.12f, 1.12f, 1f);
            cb.pressedColor = new Color(0.82f, 0.82f, 0.82f, 1f);
            cb.disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.6f);
            cb.colorMultiplier = 1f;
            cb.fadeDuration = 0.1f;
            tab.colors = cb;

            TMP_Text label = tab.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                label.color = isSelected ? Color.white : new Color(1f, 1f, 1f, 0.72f);
                label.fontStyle = isSelected ? FontStyles.Bold : FontStyles.Normal;
            }

            Transform accent = tab.transform.Find("Accent");
            if (accent != null) accent.gameObject.SetActive(isSelected);
        }

        if (listBuilder != null) listBuilder.Build(GetDescriptors(index));

        if (listScroll == null) listScroll = GetComponentInChildren<ScrollRect>(true);
        if (listScroll != null) listScroll.verticalNormalizedPosition = 1f;
    }

    private List<SettingDescriptor> GetDescriptors(int tab)
    {
        switch (tab)
        {
            case 0: return PrepareGraphicsDescriptors(GraphicsDescriptors());
            case 1: return ControlDescriptors();
            case 2: return AudioDescriptors();
            case 3: return GameplayDescriptors();
        }

        return new List<SettingDescriptor>();
    }

    private static List<SettingDescriptor> PrepareGraphicsDescriptors(List<SettingDescriptor> descriptors)
    {
        // Вкладка "Графика" — единственная, где изменение требует тяжёлого применения
        // (Screen / QualitySettings / runtime URP). Слайдеры применяем по отпусканию,
        // чтобы не дёргать SetResolution/SetQualityLevel на каждый пиксель драга.
        for (int i = 0; i < descriptors.Count; i++)
        {
            descriptors[i].AppliesHeavy = true;
            if (descriptors[i].Type == SettingType.Slider) descriptors[i].RequiresRelease = true;
        }

        return descriptors;
    }

    private List<SettingDescriptor> GraphicsDescriptors()
    {
        SettingsService svc = SettingsService.Instance;
        if (svc == null) return new List<SettingDescriptor>();
        GraphicsSettings g = svc.Data.graphics;

        return new List<SettingDescriptor>
        {
            MakeDropdown("windowMode", "Режим окна", SettingsApplier.WindowModeLabels, () => Mathf.Clamp(g.windowMode, 0, 2), v => g.windowMode = v),
            MakeDropdown("resolution", "Разрешение экрана", SettingsApplier.GetResolutionOptions(), () => SettingsApplier.FindResolutionOptionIndex(g), v => SettingsApplier.SetResolutionOption(g, v)),
            MakeToggle("vsync", "Вертикальная синхронизация", () => g.vSync, v => g.vSync = v),
            MakeSlider("renderScale", "Масштаб рендера", 0.5f, 1f, false, "F2", () => g.renderScale, v => g.renderScale = v),
            MakeSlider("fpsLimit", "Ограничение FPS (0 = без; отключает VSync)", 0f, 240f, true, "F0", () => g.fpsLimit, v => g.fpsLimit = (int)v),
            MakeSlider("quality", "Уровень качества", 0f, Mathf.Max(0, QualitySettings.names.Length - 1), true, "F0", () => g.qualityLevel, v => g.qualityLevel = (int)v),
            MakeSlider("droneFov", "FOV дрона", 70f, 110f, true, "F0", () => g.droneFov, v => g.droneFov = v),
            MakeSlider("droneFarClip", "Дальность прорисовки (дрон)", 12000f, 20000f, true, "F0", () => g.droneFarClip, v => g.droneFarClip = v),
            MakeSlider("droneShadowDistance", "Тени дрона (дистанция)", 0f, 300f, true, "F0", () => g.droneShadowDistance, v => g.droneShadowDistance = v),
            MakeSlider("bunkerShadowDistance", "Дистанция теней (бункер)", 10f, 40f, true, "F0", () => g.bunkerShadowDistance, v => g.bunkerShadowDistance = v),
            MakeToggle("bunkerSoftShadows", "Мягкие тени (бункер)", () => g.bunkerSoftShadows, v => g.bunkerSoftShadows = v),
            MakeToggle("bunkerPostProcessing", "Постобработка бункера", () => g.bunkerPostProcessing, v => g.bunkerPostProcessing = v),
            MakeToggle("bunkerMotionBlur", "Motion Blur (бункер)", () => g.bunkerMotionBlur, v => g.bunkerMotionBlur = v),
        };
    }

    private List<SettingDescriptor> ControlDescriptors()
    {
        SettingsService svc = SettingsService.Instance;
        if (svc == null) return new List<SettingDescriptor>();
        ControlSettings c = svc.Data.controls;

        return new List<SettingDescriptor>
        {
            MakeSlider("fpsSens", "Чувствительность мыши (FPS)", 0.1f, 3f, false, "F2", () => c.fpsMouseSensitivity, v => c.fpsMouseSensitivity = v),
            MakeToggle("fpsInvertY", "Инверсия Y (FPS)", () => c.fpsInvertY, v => c.fpsInvertY = v),
            MakeToggle("droneInvertY", "Инверсия Y (дрон)", () => c.droneInvertY, v => c.droneInvertY = v),
            MakeToggle("gimbalInvertY", "Инверсия Y (гимбал)", () => c.gimbalInvertY, v => c.gimbalInvertY = v),
            MakeSlider("stickSpeed", "Скорость стика", 2f, 8f, false, "F1", () => c.droneStickSpeed, v => c.droneStickSpeed = v),
            MakeSlider("centeringSpeed", "Скорость возврата стика", 3f, 12f, false, "F1", () => c.droneCenteringSpeed, v => c.droneCenteringSpeed = v),
            MakeSlider("gimbalSpeed", "Скорость гимбала", 20f, 90f, false, "F0", () => c.gimbalSpeed, v => c.gimbalSpeed = v),
            MakeSlider("throttleSpeed", "Скорость газа", 1f, 3f, false, "F1", () => c.throttleSpeed, v => c.throttleSpeed = v),
            MakeSlider("deadZone", "Мёртвая зона стика", 0f, 0.2f, false, "F2", () => c.stickDeadZone, v => c.stickDeadZone = v),
            MakeToggle("throttleHold", "Фиксация газа", () => c.throttleMode == 1, v => c.throttleMode = v ? 1 : 0),
        };
    }

    private List<SettingDescriptor> AudioDescriptors()
    {
        SettingsService svc = SettingsService.Instance;
        if (svc == null) return new List<SettingDescriptor>();
        DroneSimulator.Settings.AudioSettings a = svc.Data.audio;

        return new List<SettingDescriptor>
        {
            MakeSlider("master", "Общая громкость", 0f, 1f, false, "P0", () => a.masterVolume, v => a.masterVolume = v),
            MakeSlider("music", "Музыка", 0f, 1f, false, "P0", () => a.musicVolume, v => a.musicVolume = v),
            MakeSlider("sfx", "Эффекты", 0f, 1f, false, "P0", () => a.sfxVolume, v => a.sfxVolume = v),
            MakeSlider("motors", "Моторы дрона", 0f, 1f, false, "P0", () => a.motorsVolume, v => a.motorsVolume = v),
            MakeSlider("ew", "РЭБ / помехи", 0f, 1f, false, "P0", () => a.ewVolume, v => a.ewVolume = v),
            MakeSlider("ui", "Интерфейс", 0f, 1f, false, "P0", () => a.uiVolume, v => a.uiVolume = v),
            MakeSlider("voice", "Голос инструктора", 0f, 1f, false, "P0", () => a.voiceVolume, v => a.voiceVolume = v),
            MakeToggle("duck", "Приглушать при меню", () => a.duckOnMenus, v => a.duckOnMenus = v),
        };
    }

    private List<SettingDescriptor> GameplayDescriptors()
    {
        SettingsService svc = SettingsService.Instance;
        if (svc == null) return new List<SettingDescriptor>();
        GameplaySettings p = svc.Data.gameplay;

        return new List<SettingDescriptor>
        {
            MakeSlider("hudTransparency", "Прозрачность HUD", 0.3f, 1f, false, "P0", () => p.hudTransparency, v => p.hudTransparency = v),
            MakeSlider("hudScale", "Масштаб HUD", 0.75f, 1.5f, false, "P0", () => p.hudScale, v => p.hudScale = v),
            MakeSlider("crosshairSize", "Размер прицела", 6f, 32f, true, "F0", () => p.crosshairSize, v => p.crosshairSize = v),
            MakeSlider("hudLine", "Толщина линий HUD", 1f, 4f, true, "F0", () => p.hudLineThickness, v => p.hudLineThickness = (int)v),
            MakeToggle("targetFrame", "Рамка захвата цели", () => p.showTargetFrame, v => p.showTargetFrame = v),
            MakeToggle("subtitles", "Субтитры", () => p.subtitlesEnabled, v => p.subtitlesEnabled = v),
            MakeSlider("subtitleFont", "Размер шрифта субтитров", 14f, 28f, true, "F0", () => p.subtitleFontSize, v => p.subtitleFontSize = (int)v),
            MakeToggle("hints", "Подсказки взаимодействия", () => p.showInteractionHints, v => p.showInteractionHints = v),
            MakeToggle("fpsCounter", "Счётчик FPS", () => p.showFpsCounter, v => p.showFpsCounter = v),
            MakeSlider("shake", "Тряска камеры", 0f, 1.5f, false, "P0", () => p.cameraShakeIntensity, v => p.cameraShakeIntensity = v),
            MakeToggle("throttleShake", "Тряска от газа", () => p.throttleShake, v => p.throttleShake = v),
            MakeToggle("trainingHints", "Подсказки обучения", () => p.trainingHints, v => p.trainingHints = v),
        };
    }

    private SettingDescriptor MakeSlider(string id, string title, float min, float max, bool whole, string format, System.Func<float> get, System.Action<float> set)
    {
        return new SettingDescriptor
        {
            Id = id,
            Type = SettingType.Slider,
            Title = title,
            MinValue = min,
            MaxValue = max,
            WholeNumbers = whole,
            ValueFormat = format,
            GetFloat = get,
            SetFloat = set
        };
    }

    private SettingDescriptor MakeToggle(string id, string title, System.Func<bool> get, System.Action<bool> set)
    {
        return new SettingDescriptor
        {
            Id = id,
            Type = SettingType.Toggle,
            Title = title,
            GetBool = get,
            SetBool = set
        };
    }

    private SettingDescriptor MakeDropdown(string id, string title, string[] options, System.Func<int> get, System.Action<int> set)
    {
        return new SettingDescriptor
        {
            Id = id,
            Type = SettingType.Dropdown,
            Title = title,
            Options = options,
            GetIndex = get,
            SetIndex = set
        };
    }
}
