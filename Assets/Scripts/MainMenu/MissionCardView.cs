using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MissionCardView : MonoBehaviour
{
    [SerializeField] private Image previewImage;
    [SerializeField] private TMP_Text nameLabel;
    [SerializeField] private GameObject lockedOverlay;
    [SerializeField] private Button button;

    private MissionDefinition mission;

    public void Bind(MissionDefinition def, bool unlocked, System.Action<MissionDefinition> onClick)
    {
        mission = def;

        if (previewImage != null && def.preview != null) previewImage.sprite = def.preview;
        if (nameLabel != null) nameLabel.text = def.displayName;
        if (lockedOverlay != null) lockedOverlay.SetActive(!unlocked);

        button.interactable = unlocked;
        button.onClick.RemoveAllListeners();
        if (unlocked) button.onClick.AddListener(() => onClick?.Invoke(mission));
    }
}
