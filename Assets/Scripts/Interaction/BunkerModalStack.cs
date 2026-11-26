using UnityEngine;

// Единая точка учёта модальных окон бункера: ComputerOS, панель выбора миссий,
// пауза и настройки. Раньше Time.timeScale и Cursor правились в каждом окне
// по отдельности, из-за чего закрытие панели миссий без открытого рабочего стола
// оставляло игру «замороженной» (timeScale = 0, курсор свободен), хотя FSM уже
// находился в Gameplay.
//
// Состояние намеренно не хранится счётчиком: каждое окно регистрирует себя,
// а AnyOpen опрашивает живые IsOpen. Unity-объекты после выгрузки сцены
// становятся «null» по перегруженному ==, поэтому состояние не протекает
// между сценами и не требует ручного сброса.
public static class BunkerModalStack
{
    private static ComputerUIController computerUI;
    private static MissionMenuController missionMenu;
    private static BunkerPauseController pause;
    private static SettingsPanel settings;
    private static DroneShopController droneShop;

    // Есть ли вообще бункерный контекст. SettingsPanel живёт и в MainMenu,
    // где курсор должен оставаться свободным, поэтому настройки участвуют
    // в координации только когда в сцене есть бункерные окна.
    public static bool HasBunkerContext => pause != null || computerUI != null || missionMenu != null || droneShop != null;

    public static bool AnyOpen =>
        IsOpen(computerUI) || IsOpen(missionMenu) || IsOpen(pause) || IsOpen(settings) || IsOpen(droneShop);

    public static void Register(ComputerUIController controller) => computerUI = controller;
    public static void Register(MissionMenuController controller) => missionMenu = controller;
    public static void Register(BunkerPauseController controller) => pause = controller;

    // Магазин-витрина тоже модальное окно бункера: пока он открыт, timeScale = 0
    // и курсор свободен (дрон осматривают мышью), но пауза не открывается.
    public static void Register(DroneShopController controller) => droneShop = controller;

    public static void Unregister(ComputerUIController controller)
    {
        if (ReferenceEquals(computerUI, controller)) computerUI = null;
    }

    public static void Unregister(MissionMenuController controller)
    {
        if (ReferenceEquals(missionMenu, controller)) missionMenu = null;
    }

    public static void Unregister(BunkerPauseController controller)
    {
        if (ReferenceEquals(pause, controller)) pause = null;
    }

    public static void Unregister(DroneShopController controller)
    {
        if (ReferenceEquals(droneShop, controller)) droneShop = null;
    }

    public static void NotifySettingsChanged(SettingsPanel panel)
    {
        settings = panel;
        if (HasBunkerContext) Apply();
    }

    // Приводит глобальное состояние к «открыто хотя бы одно окно / всё закрыто».
    // Вызывается после изменения IsOpen у любого окна бункера.
    public static void Apply()
    {
        if (AnyOpen)
        {
            Time.timeScale = 0f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            return;
        }

        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private static bool IsOpen(ComputerUIController controller) => controller != null && controller.IsOpen;
    private static bool IsOpen(MissionMenuController controller) => controller != null && controller.IsOpen;
    private static bool IsOpen(BunkerPauseController controller) => controller != null && controller.IsOpen;
    private static bool IsOpen(SettingsPanel panel) => panel != null && panel.IsOpen;
    private static bool IsOpen(DroneShopController controller) => controller != null && controller.IsOpen;
}
