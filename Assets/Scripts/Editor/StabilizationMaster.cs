using UnityEditor;
using UnityEngine;

public static class StabilizationMaster
{
    [MenuItem("Tools/DroneSimulator/Run Full Stabilization", false, 1)]
    public static void RunFullStabilization()
    {
        Debug.Log("<color=cyan>[StabilizationMaster]</color> ===== СТАРТ ПОЛНОЙ СТАБИЛИЗАЦИИ =====");

        Debug.Log("[StabilizationMaster] 1/9 Сброс TMP-материалов (зелёный шрифт)");
        ProjectStabilizer.ResetFontMaterials();

        Debug.Log("[StabilizationMaster] 2/9 Исправление опечатки в окне крушения");
        ProjectStabilizer.FixCrashTypo();

        Debug.Log("[StabilizationMaster] 3/9 Проверка ссылок на дроны в миссиях");
        ProjectStabilizer.ValidateMissionDroneLinks();

        Debug.Log("[StabilizationMaster] 4/9 Очистка Build Settings");
        ProjectStabilizer.CleanBuildSettings();

        Debug.Log("[StabilizationMaster] 5/9 Префабы взрыва и бомбы + звук");
        ImpactVfxBuilder.BuildImpactPrefabs();

        Debug.Log("[StabilizationMaster] 6/9 Создание аудиомикшера");
        AudioMixerBuilder.BuildMainMixer();

        Debug.Log("[StabilizationMaster] 7/9 Привязка звука к префабам дронов");
        DroneAudioBinder.AttachDroneAudio();

        Debug.Log("[StabilizationMaster] 8/9 Терминальная палитра UI");
        UIThemeApplicator.ApplyTerminalPalette();

        Debug.Log("[StabilizationMaster] 9/9 Белый цвет шрифта TMP (починка зелёного текста)");
        TmpColorFixer.FixGreenText();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("<color=lime>[StabilizationMaster]</color> ===== СТАБИЛИЗАЦИЯ ЗАВЕРШЕНА. Отчёты: Logs/ui_theme_report.txt, Logs/tmp_color_fix_report.txt =====");
    }
}
