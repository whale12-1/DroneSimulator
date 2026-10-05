using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public class SettingsScreen : UIScreen
{
    [Header("Аудио")]
    [SerializeField] private Slider masterVolumeSlider;
    [SerializeField] private Slider sfxVolumeSlider;
    [SerializeField] private TMP_Text masterVolumeLabel;
    [SerializeField] private TMP_Text sfxVolumeLabel;

    [Header("Графика")]
    [SerializeField] private TMP_Dropdown qualityDropdown;
    [SerializeField] private Toggle fullscreenToggle;

    [Header("Кнопки")]
    [SerializeField] private Button backButton;

    private void OnEnable()
    {
        if (SettingsService.Instance == null) return;

        var s = SettingsService.Instance.Data;

        if (masterVolumeSlider != null)
        {
            masterVolumeSlider.value = s.masterVolume;
            masterVolumeSlider.onValueChanged.RemoveAllListeners();
            masterVolumeSlider.onValueChanged.AddListener(v =>
            {
                SettingsService.Instance.SetMasterVolume(v);
                if (masterVolumeLabel != null) masterVolumeLabel.text = $"Master: {v:F2}";
            });
        }

        if (sfxVolumeSlider != null)
        {
            sfxVolumeSlider.value = s.sfxVolume;
            sfxVolumeSlider.onValueChanged.RemoveAllListeners();
            sfxVolumeSlider.onValueChanged.AddListener(v =>
            {
                SettingsService.Instance.SetSfxVolume(v);
                if (sfxVolumeLabel != null) sfxVolumeLabel.text = $"SFX: {v:F2}";
            });
        }

        if (qualityDropdown != null)
        {
            qualityDropdown.ClearOptions();
            qualityDropdown.AddOptions(new System.Collections.Generic.List<string>(QualitySettings.names));
            qualityDropdown.value = s.qualityLevel;
            qualityDropdown.onValueChanged.RemoveAllListeners();
            qualityDropdown.onValueChanged.AddListener(v => SettingsService.Instance.SetQuality(v));
        }

        if (fullscreenToggle != null)
        {
            fullscreenToggle.isOn = s.fullscreen;
            fullscreenToggle.onValueChanged.RemoveAllListeners();
            fullscreenToggle.onValueChanged.AddListener(v => SettingsService.Instance.SetFullscreen(v));
        }

        if (backButton != null)
        {
            backButton.onClick.RemoveAllListeners();
            backButton.onClick.AddListener(() => Hide());
        }
    }
}