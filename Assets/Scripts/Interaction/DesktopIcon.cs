using UnityEngine;

public class DesktopIcon : MonoBehaviour
{
    [SerializeField] private MissionMenuController missionMenu;

    public void OnIconClicked()
    {
        if (missionMenu == null)
            missionMenu = FindAnyObjectByType<MissionMenuController>(FindObjectsInactive.Include);

        if (missionMenu != null)
            missionMenu.OpenMissionMenu();
    }
}
