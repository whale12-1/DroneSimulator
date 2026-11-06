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

    [Header("Интеграция с MissionController")]
    [Tooltip("MissionController на объекте MissionManager. Если не назначено, найдётся автоматически.")]
    [SerializeField] private MissionController missionController;

    [Header("UI (Титры)")]
    [SerializeField] private GameObject subtitlePanel;
    [SerializeField] private TMP_Text subtitleText;

    private bool btrDestroyed = false;
    private bool missionEnded = false;

    // Результат, который передадим в MissionController после короткой паузы с титрами.
    private MissionResult pendingResult;
    private int pendingTargetsDestroyed;

    private void Start()
    {
        // Находим MissionController и отключаем его автозавершение:
        // этой миссией управляет AmbushMissionDirector (свой сценарий "БТР -> погоня за РСЗО").
        if (missionController == null)
            missionController = FindFirstObjectByType<MissionController>();

        if (missionController != null)
            missionController.autoComplete = false;

        ValidateReferences();

        if (subtitlePanel != null) subtitlePanel.SetActive(false);
        if (btrAI != null) btrAI.enabled = false;
        if (himarsStrike != null) himarsStrike.enabled = false;

        StartCoroutine(MissionSequence());
    }

    private void ValidateReferences()
    {
        if (btrHealth == null) Debug.LogError("[AmbushMissionDirector] Не назначена ссылка btrHealth!");
        if (himarsHealth == null) Debug.LogError("[AmbushMissionDirector] Не назначена ссылка himarsHealth!");
        if (himarsEscapePoint == null) Debug.LogError("[AmbushMissionDirector] Не назначена ссылка himarsEscapePoint!");
        if (himarsPatrol == null) Debug.LogError("[AmbushMissionDirector] Не назначена ссылка himarsPatrol (нужна для отступления РСЗО)!");
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
            pendingResult = MissionResult.Success;
            pendingTargetsDestroyed = 2;
        }
        else
        {
            ShowSubtitle("МИССИЯ ПРОВАЛЕНА: РСЗО скрылась с места залпа.", 5f);
            pendingResult = MissionResult.Failed;
            pendingTargetsDestroyed = 1;
        }

        // Даём титрам отыграть 3 секунды, затем показываем дебрифинг.
        Invoke(nameof(ShowDebrief), 3f);
    }

    private void ShowDebrief()
    {
        if (missionController != null)
        {
            // Единый путь завершения: результат, прогресс и переход в Debrief делает MissionController.
            missionController.CompleteMission(pendingResult, pendingTargetsDestroyed);
        }
        else
        {
            // Fallback, если MissionController по какой-то причине не найден в сцене.
            MissionContext.MarkResult(pendingResult, pendingTargetsDestroyed, Time.time);
            GameStateMachine.TransitionTo(GameState.Debrief);
        }
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