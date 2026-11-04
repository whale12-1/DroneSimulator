using UnityEngine;

public class CloseMissionBoardButton : MonoBehaviour
{
    [SerializeField] private GameObject missionBoardWindow;
    [SerializeField] private GameObject computerOS;

    public void OnCloseClicked()
    {
        if (missionBoardWindow != null)
        {
            missionBoardWindow.SetActive(false);
        }

        if (computerOS != null)
        {
            computerOS.SetActive(true);
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Time.timeScale = 0f;
    }
}
