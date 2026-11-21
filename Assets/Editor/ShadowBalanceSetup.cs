using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Lifts the scene ambient a little so the forest-belt shadows stay readable instead of
// collapsing into black under the tree canopies (Environment Lighting -> Intensity Multiplier).
// Menu: Tools/DroneSimulator/Lighting/Balance Forest Shadows
public static class ShadowBalanceSetup
{
    const string ScenePath = "Assets/Scenes/FirstMission(Forest).unity";
    const string ReportPath = "Logs/ShadowBalanceReport.txt";
    const float TargetAmbientIntensity = 1.30f; // 1.25-1.35 per request
    const float MinimumAllowed = 1.25f;

    [MenuItem("Tools/DroneSimulator/Lighting/Balance Forest Shadows", priority = 30)]
    public static void BalanceForestShadows()
    {
        var report = new StringBuilder();
        report.AppendLine("ShadowBalanceSetup started " + DateTime.Now.ToString("s"));

        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        bool openedByUs = false;
        if (!scene.IsValid() || !scene.isLoaded)
        {
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            openedByUs = true;
        }

        if (!scene.IsValid())
        {
            report.AppendLine("SCENE NOT AVAILABLE: " + ScenePath);
        }
        else
        {
            float before = RenderSettings.ambientIntensity;
            if (before <= 1.1f)
            {
                RenderSettings.ambientIntensity = Mathf.Clamp(TargetAmbientIntensity,
                    MinimumAllowed, 1.35f);
                report.AppendLine("ambientIntensity: " + before.ToString("F3") + " -> " +
                                  RenderSettings.ambientIntensity.ToString("F3"));
            }
            else
            {
                report.AppendLine("ambientIntensity already " + before.ToString("F3") +
                                  " (> 1.1), left unchanged");
            }

            EditorSceneManager.MarkSceneDirty(scene);
            report.AppendLine("scene saved=" + EditorSceneManager.SaveScene(scene));
        }

        if (openedByUs)
            EditorSceneManager.CloseScene(scene, true);

        WriteReport(report);
        Debug.Log("[ShadowBalance] Done. See " + ReportPath);
    }

    static void WriteReport(StringBuilder report)
    {
        try
        {
            string root = Directory.GetCurrentDirectory();
            string path = Path.Combine(root, ReportPath);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, report.ToString());
        }
        catch (Exception exception)
        {
            Debug.LogWarning("[ShadowBalance] could not write the report: " + exception.Message);
        }
    }
}
