using UnityEngine;
using TMPro;
using System.Collections;

public class AmbushMissionDirector : MonoBehaviour
{
    [Header("Ссылки на объекты сцены")]
    [SerializeField] private DroneFlightController reconDrone;
    [SerializeField] private VehicleHealth btrHealth;
    [SerializeField] private VehicleHealth himarsHealth;
    [SerializeField] private VehiclePatrol btrPatrol;
    [SerializeField] private VehiclePatrol himarsPatrol;
    [SerializeField] private VehicleSmartAI btrAI;
    [SerializeField] private HimarsStrike himarsStrike;
    [SerializeField] private Transform himarsEscapePoint;
    [SerializeField] private float himarsRetreatSpeed = 40f; // Скорость отступления

    [Header("UI (Титры)")]
    [SerializeField] private GameObject subtitlePanel;
    [SerializeField] private TMP_Text subtitleText;

    private bool btrDestroyed = false;
    private bool missionEnded = false;

    private void Start()
    {
        if (subtitlePanel != null) subtitlePanel.SetActive(false);
        if (btrAI != null) btrAI.enabled = false;
        if (himarsStrike != null) himarsStrike.enabled = false;

        StartCoroutine(MissionSequence());
    }

    private IEnumerator MissionSequence()
    {
        // --- ФАЗА 1: ВСТУПЛЕНИЕ ---
        if (reconDrone != null) reconDrone.isInputLocked = true;
        if (btrPatrol != null) btrPatrol.enabled = true;
        if (himarsPatrol != null) himarsPatrol.enabled = true;

        ShowSubtitle("НАБЛЮДЕНИЕ: Обнаружена техника противника. Следим за перемещением.", 4f);
        yield return new WaitForSeconds(4f);

        ShowSubtitle("ВНИМАНИЕ: РСЗО готовится к залпу. Зенитный БТР прикрывает позицию.", 4f);
        yield return new WaitForSeconds(4f);

        if (reconDrone != null) reconDrone.isInputLocked = false;
        if (himarsStrike != null) himarsStrike.enabled = true;
        if (btrAI != null) btrAI.enabled = true;

        ShowSubtitle("ЦЕЛЬ: Уничтожьте БТР прикрытия! Затем не дайте РСЗО уйти.", 5f);

        // --- ФАЗА 3: ЖДЕМ СМЕРТИ БТР ---
        while (btrHealth != null && btrHealth.currentHealth > 0)
            yield return null;

        btrDestroyed = true;
        ShowSubtitle("БТР УНИЧТОЖЕН! Внимание, РСЗО начинает отступление!", 4f);

        // ПРИНУДИТЕЛЬНО ЗАПУСКАЕМ ОТСТУПЛЕНИЕ ХАЙМЕРСА
        if (himarsPatrol != null)
        {
            himarsPatrol.StartRetreat(himarsRetreatSpeed);
        }

        // --- ФАЗА 4: ПОГОНЯ ---
        while (!missionEnded)
        {
            if (himarsHealth != null && himarsHealth.currentHealth <= 0)
            {
                EndMission(true);
                break;
            }

            if (himarsEscapePoint != null && himarsHealth != null)
            {
                float dist = Vector3.Distance(himarsHealth.transform.position, himarsEscapePoint.position);
                if (dist < 15f)
                {
                    EndMission(false);
                    break;
                }
            }
            yield return null;
        }
    }

    private void EndMission(bool success)
    {
        if (missionEnded) return;
        missionEnded = true;

        if (success)
        {
            ShowSubtitle("МИССИЯ ВЫПОЛНЕНА: РСЗО уничтожена.", 5f);
            MissionContext.MarkResult(MissionResult.Success, 2, Time.time);
        }
        else
        {
            ShowSubtitle("МИССИЯ ПРОВАЛЕНА: РСЗО скрылась с места залпа.", 5f);
            MissionContext.MarkResult(MissionResult.Failed, 1, Time.time);
        }

        Invoke(nameof(ShowDebrief), 3f);
    }

    private void ShowDebrief()
    {
        GameStateMachine.TransitionTo(GameState.Debrief);
    }

    private void ShowSubtitle(string text, float duration)
    {
        if (subtitlePanel != null) subtitlePanel.SetActive(true);
        if (subtitleText != null) subtitleText.text = text;

        if (duration > 0 && !missionEnded)
            Invoke(nameof(HideSubtitle), duration);
    }

    private void HideSubtitle()
    {
        if (subtitlePanel != null) subtitlePanel.SetActive(false);
    }
}