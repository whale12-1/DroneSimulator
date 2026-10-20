using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MissionCardView : MonoBehaviour
{
    [Header("Preview")]
    [SerializeField] private Image previewImage;

    [Header("Тексты")]
    [SerializeField] private TMP_Text nameLabel;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private TMP_Text targetsText;
    [SerializeField] private TMP_Text timeText;

    [Header("Состояние")]
    [SerializeField] private GameObject lockedOverlay;
    [SerializeField] private GameObject completedBadge; // Новая галочка "Пройдено"
    [SerializeField] private Button button;

    private MissionDefinition mission;

    public void Bind(MissionDefinition def, bool unlocked, bool completed, System.Action<MissionDefinition> onClick)
    {
        mission = def;

        if (previewImage != null && def.preview != null)
            previewImage.sprite = def.preview;

        if (nameLabel != null) nameLabel.text = def.displayName;
        if (descriptionText != null) descriptionText.text = string.IsNullOrEmpty(def.description) ? "—" : def.description;

        if (targetsText != null)
        {
            if (def.objectiveType == MissionObjectiveType.DestroyAllTargets)
                targetsText.text = $"{def.targetCount} цел.";
            else if (def.objectiveType == MissionObjectiveType.SurviveTime)
                targetsText.text = "выжить";
            else targetsText.text = "—";
        }

        if (timeText != null)
            timeText.text = def.timeLimit > 0 ? $"{def.timeLimit:F0} сек" : "без лимита";

        // Состояния
        if (lockedOverlay != null)
            lockedOverlay.SetActive(!unlocked);

        if (completedBadge != null)
            completedBadge.SetActive(completed); // Включаем галочку, если пройдено

        button.interactable = unlocked;
        button.onClick.RemoveAllListeners();
        if (unlocked)
            button.onClick.AddListener(() => onClick?.Invoke(mission));
    }
}