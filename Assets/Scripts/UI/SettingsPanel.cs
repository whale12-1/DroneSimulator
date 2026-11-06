using System.Collections.Generic;
using DroneSimulator.Settings;
using UnityEngine;
using UnityEngine.UI;

public class SettingsPanel : UIScreen
{
    [SerializeField] private Button[] tabButtons;
    [SerializeField] private SettingsListBuilder listBuilder;
    [SerializeField] private Button closeButton;
    [SerializeField] private bool hideOnStart = true;
    [SerializeField] private Color selectedTabColor = new Color(0.3f, 0.5f, 0.8f, 1f);
    [SerializeField] private Color normalTabColor = new Color(0.16f, 0.17f, 0.19f, 1f);

    private int currentTab;

    public bool IsOpen { get; private set; }

    private void Awake()
    {
        // Панель в Screen Space canvas обязана лежать в плоскости Z = 0.
        // GraphicRaycaster для Overlay-канваса пускает луч из z = -100 в сторону +z,
        // поэтому инстанс, смещённый по Z (в MainMenu он был на -429), полностью
        // перестаёт принимать клики: кнопки и ползунки видимы, но не нажимаются.
        NormalizeZ();
    }

    private void Start()
    {
        SettingsService.EnsureInstance();

        if (closeButton != null) closeButton.onClick.AddListener(Hide);

        if (tabButtons != null)
        {
            for (int i = 0; i < tabButtons.Length; i++)
            {
                if (tabButtons[i] == null) continue;
                int index = i;
                tabButtons[i].onClick.AddListener(() => SelectTab(index));
            }
        }

        if (hideOnStart) Hide();
        else Show();
    }

    public override void Show()
    {
        // Страховка: панель всегда должна быть поверх остальных окон этого канваса
        // и в плоскости Z = 0, иначе часть UI перестаёт получать клики.
        NormalizeZ();
        transform.SetAsLastSibling();

        base.Show();
        IsOpen = true;
        SelectTab(currentTab);
    }

    public override void Hide()
    {
        IsOpen = false;
        base.Hide();
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
            if (tabButtons[i] == null) continue;
            ColorBlock cb = tabButtons[i].colors;
            cb.normalColor = i == index ? selectedTabColor : normalTabColor;
            cb.selectedColor = cb.normalColor;
            cb.highlightedColor = cb.normalColor;
            cb.pressedColor = cb.normalColor;
            tabButtons[i].colors = cb;
        }

        if (listBuilder != null) listBuilder.Build(GetDescriptors(index));
    }

    private List<SettingDescriptor> GetDescriptors(int tab)
    {
        switch (tab)
        {
            case 0: return GraphicsDescriptors();
            case 1: return ControlDescriptors();
            case 2: return AudioDescriptors();
            case 3: return GameplayDescriptors();
        }

        return new List<SettingDescriptor>();
    }

    private List<SettingDescriptor> GraphicsDescriptors()
    {
        SettingsService svc = SettingsService.Instance;
        if (svc == null) return new List<SettingDescriptor>();
        GraphicsSettings g = svc.Data.graphics;

        return new List<SettingDescriptor>
        {
            MakeToggle("fullscreen", "Полный экран", () => g.fullscreen, v => g.fullscreen = v),
            MakeToggle("vsync", "Вертикальная синхронизация", () => g.vSync, v => g.vSync = v),
            MakeSlider("renderScale", "Масштаб рендера", 0.5f, 1f, false, "F2", () => g.renderScale, v => g.renderScale = v),
            MakeSlider("fpsLimit", "Ограничение FPS (0 = без)", 0f, 240f, true, "F0", () => g.fpsLimit, v => g.fpsLimit = (int)v),
            MakeSlider("quality", "Уровень качества", 0f, Mathf.Max(0, QualitySettings.names.Length - 1), true, "F0", () => g.qualityLevel, v => g.qualityLevel = (int)v),
            MakeToggle("separateMission", "Отдельные настройки для миссии", () => g.separateMissionSettings, v => g.separateMissionSettings = v),
            MakeSlider("droneFov", "FOV дрона", 70f, 110f, true, "F0", () => g.droneFov, v => g.droneFov = v),
            MakeSlider("droneFarClip", "Дальность прорисовки (дрон)", 500f, 3000f, true, "F0", () => g.droneFarClip, v => g.droneFarClip = v),
            MakeSlider("droneShadowDistance", "Тени дрона (дистанция)", 0f, 300f, true, "F0", () => g.droneShadowDistance, v => g.droneShadowDistance = v),
            MakeToggle("droneFog", "Туман у дрона", () => g.droneFog, v => g.droneFog = v),
            MakeToggle("droneLensDistortion", "Дисторсия объектива", () => g.droneLensDistortion, v => g.droneLensDistortion = v),
            MakeSlider("bunkerShadowDistance", "Дистанция теней (бункер)", 10f, 40f, true, "F0", () => g.bunkerShadowDistance, v => g.bunkerShadowDistance = v),
            MakeToggle("bunkerSoftShadows", "Мягкие тени (бункер)", () => g.bunkerSoftShadows, v => g.bunkerSoftShadows = v),
            MakeToggle("bunkerPostProcessing", "Постобработка бункера", () => g.bunkerPostProcessing, v => g.bunkerPostProcessing = v),
            MakeToggle("bunkerMotionBlur", "Motion Blur (бункер)", () => g.bunkerMotionBlur, v => g.bunkerMotionBlur = v),
            MakeSlider("ewDistortion", "Искажения РЭБ", 0f, 1f, false, "P0", () => g.ewVisualDistortion, v => g.ewVisualDistortion = v),
            MakeSlider("nightVisionGain", "Усиление ПНВ", 0.5f, 3f, false, "F1", () => g.nightVisionGain, v => g.nightVisionGain = v),
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
            MakeSlider("droneSens", "Чувствительность мыши (дрон)", 0.1f, 3f, false, "F2", () => c.droneMouseSensitivity, v => c.droneMouseSensitivity = v),
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
            MakeToggle("mono", "Моно-режим", () => a.monoMode, v => a.monoMode = v),
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
            MakeToggle("dangerousFx", "Уменьшить опасные эффекты", () => p.reduceDangerousEffects, v => p.reduceDangerousEffects = v),
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
}
