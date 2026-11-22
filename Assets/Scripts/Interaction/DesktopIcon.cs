using UnityEngine;

public class DesktopIcon : MonoBehaviour
{
    [SerializeField] private MissionMenuController missionMenu;

    private void Awake()
    {
        // Клик по иконке рабочего стола озвучиваем в шине UI, как остальные кнопки.
        // Объект активен только при открытом ComputerOS, Attach идемпотентен.
        UISoundService.Attach(gameObject);
    }

    public void OnIconClicked()
    {
        if (missionMenu == null)
            missionMenu = FindAnyObjectByType<MissionMenuController>(FindObjectsInactive.Include);

        if (missionMenu != null)
            missionMenu.OpenMissionMenu();
    }
}
