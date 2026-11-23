using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Единая палитра и метрики интерфейса для экранов MainMenu и Bunker(main).
/// Значения сняты с эталонного главного меню (MainPanel: кнопки 720x60 на спрайте
/// "UI Starter Pack_1" из HUF Pack.png, красная кнопка выхода #DB143D,
/// шрифт LiberationSans SDF 24).
/// Класс не содержит ссылок на ассеты, поэтому доступен и в рантайме, и в Editor-билдерах.
/// </summary>
public static class TacticalUITheme
{
    // ---------------------------------------------------------------- палитра

    /// <summary>Тёмно-красный акцент главного меню (ExitButton).</summary>
    public static readonly Color AccentRed = new Color(0.86f, 0.08f, 0.24f, 1f);

    /// <summary>Осветлённый акцент для наведения на красные кнопки.</summary>
    public static readonly Color AccentRedHover = new Color(0.96f, 0.08f, 0.24f, 1f);

    /// <summary>Тело модальной панели.</summary>
    public static readonly Color PanelFill = new Color(0.045f, 0.05f, 0.055f, 0.985f);

    /// <summary>Вложенные панели (хедер, правая панель деталей).</summary>
    public static readonly Color PanelFillSoft = new Color(0.075f, 0.08f, 0.09f, 0.98f);

    /// <summary>Нейтральная металлическая плашка контролов (дропдаун, вкладка).</summary>
    public static readonly Color ControlPlate = new Color(0.16f, 0.17f, 0.19f, 1f);

    /// <summary>Подсветка контролов под курсором.</summary>
    public static readonly Color ControlPlateHover = new Color(0.22f, 0.23f, 0.25f, 1f);

    /// <summary>Затемнение под модальным окном.</summary>
    public static readonly Color Scrim = new Color(0f, 0f, 0f, 0.62f);

    /// <summary>Лёгкое затемнение списка миссий/строк настроек.</summary>
    public static readonly Color ScrimSoft = new Color(0f, 0f, 0f, 0.28f);

    public static readonly Color TextPrimary = Color.white;
    public static readonly Color TextSecondary = new Color(1f, 1f, 1f, 0.62f);
    public static readonly Color TextCaption = new Color(1f, 1f, 1f, 0.45f);
    public static readonly Color Steel = new Color(0.72f, 0.75f, 0.78f, 1f);
    public static readonly Color TrackDark = new Color(0.18f, 0.19f, 0.2f, 1f);
    public static readonly Color Divider = new Color(1f, 1f, 1f, 0.16f);
    public static readonly Color FrameTint = new Color(0.62f, 0.64f, 0.66f, 0.85f);

    /// <summary>Цвет неактивной вкладки настроек.</summary>
    public static readonly Color TabIdle = new Color(0.55f, 0.57f, 0.6f, 1f);

    /// <summary>Семантические статусы миссий (приглушённые, без синего).</summary>
    public static readonly Color StatusCompleted = new Color(0.55f, 0.72f, 0.45f, 1f);
    public static readonly Color StatusPending = new Color(0.9f, 0.68f, 0.22f, 1f);
    public static readonly Color StatusLocked = new Color(0.86f, 0.2f, 0.24f, 1f);

    // ---------------------------------------------------------------- метрики

    public const float PrimaryButtonHeight = 60f;
    public const float SecondaryButtonHeight = 44f;
    public const float RowHeight = 50f;
    public const float RowSpacing = 6f;

    public const float SettingsModalWidth = 1400f;
    public const float SettingsModalHeight = 920f;

    public const float MissionCardWidth = 840f;
    public const float MissionCardHeight = 150f;

    /// <summary>Фон вертикального списка карточек миссий (подложка Scroll View).</summary>
    public static readonly Color MissionListFill = new Color(0.015f, 0.016f, 0.018f, 0.96f);

    /// <summary>Заливка плашки карточки миссии — контрастнее фона списка.</summary>
    public static readonly Color MissionCardFill = new Color(0.13f, 0.14f, 0.155f, 1f);

    public const float SafeMargin = 64f;

    // ---------------------------------------------------------------- шрифты

    public const float FontTitle = 32f;
    public const float FontModalTitle = 30f;
    public const float FontButton = 24f;
    public const float FontButtonSecondary = 20f;
    public const float FontLabel = 17f;
    public const float FontValue = 16f;
    public const float FontBody = 15f;
    public const float FontCaption = 11f;

    // ---------------------------------------------------------------- хелперы

    /// <summary>Единый ColorBlock кнопок: как у Play/Settings/Exit в главном меню.</summary>
    public static ColorBlock ButtonColorBlock()
    {
        ColorBlock colors = ColorBlock.defaultColorBlock;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.96f, 0.96f, 0.96f, 1f);
        colors.pressedColor = new Color(0.78f, 0.78f, 0.78f, 1f);
        colors.selectedColor = Color.white;
        colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.6f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.1f;
        return colors;
    }
}
