using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MissionCardView : MonoBehaviour
{
    [Header("Preview")]
    [SerializeField] private Image previewImage;

    [Header("Тексты")]
    [SerializeField] private TMP_Text nameLabel;
    [SerializeField] private TMP_Text metaText;

    [Header("Состояние")]
    [SerializeField] private GameObject lockedOverlay;
    [SerializeField] private GameObject completedBadge;
    [SerializeField] private GameObject selectedFrame;
    [SerializeField] private Button button;

    private MissionDefinition mission;

    public MissionDefinition Mission => mission;

    public void Bind(MissionDefinition def, bool unlocked, bool completed, System.Action<MissionDefinition> onClick)
    {
        mission = def;

        if (def == null) return;

        if (previewImage != null && def.preview != null)
            previewImage.sprite = def.preview;

        if (nameLabel != null) nameLabel.text = def.displayName;
        if (metaText != null) metaText.text = BuildMeta(def);

        if (lockedOverlay != null) lockedOverlay.SetActive(!unlocked);
        if (completedBadge != null) completedBadge.SetActive(completed);
        if (selectedFrame != null) selectedFrame.SetActive(false);

        if (button != null)
        {
            button.interactable = true;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => onClick?.Invoke(mission));
        }
    }

    public void SetSelected(bool selected)
    {
        if (selectedFrame != null) selectedFrame.SetActive(selected);
    }

    private static string BuildMeta(MissionDefinition def)
    {
        string target = def.objectiveType switch
        {
            MissionObjectiveType.DestroyAllTargets => MissionText.Targets(def.targetCount),
            MissionObjectiveType.SurviveTime => "выжить",
            MissionObjectiveType.ReachPoint => "точка назначения",
            _ => "—"
        };

        return $"{target} · {MissionText.TimeLimit(def.timeLimit)}";
    }
}
