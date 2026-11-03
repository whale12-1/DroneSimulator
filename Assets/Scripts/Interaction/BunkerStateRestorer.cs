using System.Collections;
using UnityEngine;

public class BunkerStateRestorer : MonoBehaviour
{
    private void Start()
    {
        Debug.Log($"[BunkerStateRestorer] Start() called: ReturnedFromMission = {MissionContext.ReturnedFromMission}, time = {Time.unscaledTime:F3}");

        if (!MissionContext.ReturnedFromMission)
            return;

        MissionContext.ReturnedFromMission = false;
        StartCoroutine(RestoreStateRoutine());
    }

    private IEnumerator RestoreStateRoutine()
    {
        yield return null;

        PlayerController player = FindAnyObjectByType<PlayerController>();
        if (player != null)
            Debug.Log($"[BunkerStateRestorer] Before chair.Interact(): player position = {player.transform.position}, isSitting = {player.isSitting}");

        ChairInteractable chair = FindAnyObjectByType<ChairInteractable>();
        if (chair != null)
        {
            Debug.Log($"[BunkerStateRestorer] Calling chair.Interact() on {chair.name}");
            chair.Interact(gameObject);
        }

        if (player != null)
            Debug.Log($"[BunkerStateRestorer] After chair.Interact(): player position = {player.transform.position}, isSitting = {player.isSitting}");

        ComputerUIController computer = FindAnyObjectByType<ComputerUIController>(FindObjectsInactive.Include);
        if (computer != null)
            computer.OpenDesktop();

        MissionMenuController missionMenu = FindAnyObjectByType<MissionMenuController>(FindObjectsInactive.Include);
        if (missionMenu != null)
            missionMenu.OpenMissionMenu();
    }
}
