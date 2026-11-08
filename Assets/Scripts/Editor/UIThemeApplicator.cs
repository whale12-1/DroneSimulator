using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class UIThemeApplicator
{
    private const string ReportPath = "Logs/ui_theme_report.txt";

    private static readonly string[] TargetPrefabs =
    {
        "Assets/Prefabs/UI/SettingsPanel.prefab",
        "Assets/Prefabs/UI/MissionSelectPanel.prefab",
        "Assets/Prefabs/UI/MissionCard.prefab"
    };

    private static readonly string[] TargetScenes =
    {
        "Assets/Scenes/Bunker(main).unity",
        "Assets/Scenes/PreparingDemo.unity",
        "Assets/Scenes/FirstMission(Forest).unity"
    };

    private static readonly string[] PanelMarkers =
    {
        "panel", "card", "window", "plate", "banner", "dialog", "popup", "menu"
    };

    private static readonly string[] OverlayMarkers =
    {
        "overlay", "scrim", "dim", "backdrop", "completed"
    };

    private static readonly string[] ArtworkMarkers =
    {
        "preview", "check", "icon", "lock", "art", "thumbnail", "image", "knob", "badge"
    };

    private static readonly string[] SeparatorMarkers =
    {
        "rule", "divider", "line", "marker", "separator", "handle"
    };

    private static readonly string[] SecondaryTextMarkers =
    {
        "hint", "subtitle", "description", "note", "caption", "meta", "stats",
        "notice", "reward", "code", "info", "label", "tech", "total", "empty"
    };

    private static readonly string[] MaskTypeNames =
    {
        "Mask", "RectMask2D"
    };

    [MenuItem("Tools/DroneSimulator/UI/1. Apply Terminal Palette", false, 300)]
    public static void ApplyTerminalPalette()
    {
        List<string> report = new List<string>();
        report.Add("UI TERMINAL PALETTE REPORT — " + System.DateTime.Now.ToString("u"));
        int changedTotal = 0;

        changedTotal += ApplyToPrefabs(report);
        changedTotal += ApplyToScenes(report);

        report.Add("");
        report.Add("TOTAL CHANGES: " + changedTotal);
        File.WriteAllText(ReportPath, string.Join("\n", report));

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"<color=lime>[UIThemeApplicator]</color> Терминальная палитра применена. Изменений: {changedTotal}. Отчёт: {ReportPath}");
    }

    [MenuItem("Tools/DroneSimulator/UI/2. Open Palette Report", false, 301)]
    public static void OpenPaletteReport()
    {
        if (!File.Exists(ReportPath))
        {
            Debug.LogWarning($"[UIThemeApplicator] Отчёт ещё не создан: {ReportPath}. Сначала примените палитру.");
            return;
        }

        Debug.Log($"[UIThemeApplicator] Отчёт палитры: {ReportPath}{System.Environment.NewLine}{File.ReadAllText(ReportPath)}");
    }

    private static int ApplyToPrefabs(List<string> report)
    {
        int changed = 0;

        foreach (string path in TargetPrefabs)
        {
            if (!File.Exists(path))
            {
                report.Add("[MISSING] " + path);
                continue;
            }

            report.Add("");
            report.Add("=== PREFAB " + path + " ===");

            GameObject root = PrefabUtility.LoadPrefabContents(path);
            if (root == null)
            {
                report.Add("[ERROR] не удалось загрузить префаб");
                continue;
            }

            int local = ProcessHierarchy(root, report);
            changed += local;

            if (local > 0)
                PrefabUtility.SaveAsPrefabAsset(root, path);

            PrefabUtility.UnloadPrefabContents(root);
        }

        return changed;
    }

    private static int ApplyToScenes(List<string> report)
    {
        string previousScene = EditorSceneManager.GetActiveScene().path;
        int changed = 0;

        foreach (string path in TargetScenes)
        {
            if (!File.Exists(path))
            {
                report.Add("[MISSING] " + path);
                continue;
            }

            report.Add("");
            report.Add("=== SCENE " + path + " ===");

            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            GameObject[] roots = scene.GetRootGameObjects();
            int local = 0;

            foreach (GameObject root in roots)
                local += ProcessHierarchy(root, report);

            changed += local;

            if (local > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
        }

        if (!string.IsNullOrEmpty(previousScene) && File.Exists(previousScene))
            EditorSceneManager.OpenScene(previousScene, OpenSceneMode.Single);

        return changed;
    }

    private static int ProcessHierarchy(GameObject root, List<string> report)
    {
        Graphic[] graphics = root.GetComponentsInChildren<Graphic>(true);
        HashSet<Transform> panelTransforms = new HashSet<Transform>();

        foreach (Graphic graphic in graphics)
        {
            Image image = graphic as Image;
            if (image == null) continue;
            if (!IsPanelCandidate(image)) continue;

            panelTransforms.Add(image.transform);
        }

        int changed = 0;

        foreach (Graphic graphic in graphics)
        {
            if (graphic == null) continue;

            TMP_Text tmpText = graphic as TMP_Text;
            if (tmpText != null)
            {
                if (ApplyText(graphic, tmpText.text, report)) changed++;
                continue;
            }

            Text legacyText = graphic as Text;
            if (legacyText != null)
            {
                if (ApplyText(graphic, legacyText.text, report)) changed++;
                continue;
            }

            Image image = graphic as Image;
            if (image != null)
            {
                if (ApplyImage(image, panelTransforms, report)) changed++;
            }
        }

        changed += ApplySerializedPalette(root, report);
        return changed;
    }

    private static bool ApplyText(Graphic graphic, string content, List<string> report)
    {
        Color original = graphic.color;
        Color target;
        string reason;

        string name = Normalise(graphic.gameObject.name);

        if (IsButtonLabel(graphic.transform))
        {
            target = UIColorPalette.TextPrimary;
            reason = "button label";
        }
        else if (UIColorPalette.IsAcidic(original))
        {
            target = UIColorPalette.SemanticiseText(original);
            reason = "acidic text normalised";
        }
        else if (ContainsAny(name, SecondaryTextMarkers))
        {
            target = UIColorPalette.TextSecondary;
            reason = "secondary text";
        }
        else
        {
            target = UIColorPalette.TextPrimary;
            reason = "primary text";
        }

        target.a = original.a < 0.99f ? Mathf.Clamp(original.a, 0.45f, 1f) : 1f;

        report.Add(string.Format("TEXT  {0} | {1} | {2} -> {3} | {4}",
            BuildPath(graphic.transform), Shorten(content), Format(original), Format(target), reason));

        if (ColorsEqual(original, target)) return false;

        graphic.color = target;
        EditorUtility.SetDirty(graphic);
        return true;
    }

    private static bool ApplyImage(Image image, HashSet<Transform> panelTransforms, List<string> report)
    {
        Color original = image.color;
        string name = Normalise(image.gameObject.name);
        string path = BuildPath(image.transform);
        Color target = original;
        string reason = null;

        if (ContainsAny(name, OverlayMarkers))
        {
            target = UIColorPalette.WithAlpha(UIColorPalette.Overlay, Mathf.Max(original.a, 0.55f));
            reason = "overlay scrim";
        }
        else if (name.Contains("header"))
        {
            target = UIColorPalette.PanelHeader;
            reason = "panel header";
        }
        else if (panelTransforms.Contains(image.transform))
        {
            bool nested = HasPanelAncestor(image.transform, panelTransforms);
            target = nested ? UIColorPalette.PanelSurface : UIColorPalette.PanelBackground;
            reason = nested ? "nested panel surface" : "panel background";
        }
        else if (IsButtonPlate(image))
        {
            if (original.a < 0.15f)
            {
                report.Add(string.Format("IMG-SKIP {0} | {1} | transparent button plate", path, Format(original)));
                return false;
            }

            target = UIColorPalette.WithAlpha(UIColorPalette.Accent, original.a);
            reason = "action button";
            ApplyButtonColors(image);
        }
        else if (ContainsAny(name, ArtworkMarkers))
        {
            target = UIColorPalette.WithAlpha(UIColorPalette.TextPrimary, original.a < 0.99f ? original.a : 1f);
            reason = "artwork kept neutral";
        }
        else if (ContainsAny(name, SeparatorMarkers))
        {
            target = UIColorPalette.SemanticiseSurface(original);
            if (Brightness(original) < 0.3f) target = UIColorPalette.WithAlpha(UIColorPalette.Divider, Mathf.Max(original.a, 0.16f));
            reason = "separator normalised";
        }
        else if (UIColorPalette.IsAcidic(original))
        {
            target = UIColorPalette.SemanticiseSurface(original);
            reason = "acidic colour normalised";
        }
        else
        {
            report.Add(string.Format("IMG-KEEP {0} | {1} | sprite={2}", path, Format(original), image.sprite != null ? image.sprite.name : "null"));
            return false;
        }

        report.Add(string.Format("IMG   {0} | {1} -> {2} | sprite={3} | {4}",
            path, Format(original), Format(target), image.sprite != null ? image.sprite.name : "null", reason));

        if (ColorsEqual(original, target)) return false;

        image.color = target;
        EditorUtility.SetDirty(image);
        return true;
    }

    private static void ApplyButtonColors(Image image)
    {
        Button button = image.GetComponentInParent<Button>(true);
        if (button == null) return;

        ColorBlock block = button.colors;
        block.normalColor = Color.white;
        block.highlightedColor = new Color(1f, 0.92f, 0.92f, 1f);
        block.pressedColor = new Color(0.82f, 0.82f, 0.82f, 1f);
        block.selectedColor = Color.white;
        block.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.5f);
        block.colorMultiplier = 1f;
        block.fadeDuration = 0.08f;

        button.colors = block;
        EditorUtility.SetDirty(button);
    }

    private static int ApplySerializedPalette(GameObject root, List<string> report)
    {
        int changed = 0;

        changed += SetComponentColor(root, "MissionDetailsPanel", "completedColor", UIColorPalette.Success, report);
        changed += SetComponentColor(root, "MissionDetailsPanel", "pendingColor", UIColorPalette.Accent, report);
        changed += SetComponentColor(root, "MissionDetailsPanel", "lockedColor", UIColorPalette.Failure, report);

        changed += SetComponentColor(root, "SettingsPanel", "selectedTabColor", UIColorPalette.Accent, report);
        changed += SetComponentColor(root, "SettingsPanel", "normalTabColor", UIColorPalette.PanelSurface, report);

        changed += SetComponentColor(root, "SettingsListBuilder", "labelColor", UIColorPalette.TextPrimary, report);
        changed += SetComponentColor(root, "SettingsListBuilder", "trackColor", UIColorPalette.PanelSurface, report);
        changed += SetComponentColor(root, "SettingsListBuilder", "fillColor", UIColorPalette.Accent, report);
        changed += SetComponentColor(root, "SettingsListBuilder", "handleColor", UIColorPalette.TextPrimary, report);
        changed += SetComponentColor(root, "SettingsListBuilder", "checkColor", UIColorPalette.Success, report);

        return changed;
    }

    private static int SetComponentColor(GameObject root, string componentTypeName, string field, Color value, List<string> report)
    {
        int changed = 0;
        MonoBehaviour[] components = root.GetComponentsInChildren<MonoBehaviour>(true);

        foreach (MonoBehaviour component in components)
        {
            if (component == null) continue;
            if (component.GetType().Name != componentTypeName) continue;

            SerializedObject serialized = new SerializedObject(component);
            SerializedProperty property = serialized.FindProperty(field);
            if (property == null || property.propertyType != SerializedPropertyType.Color) continue;

            Color current = property.colorValue;
            report.Add(string.Format("FIELD {0}.{1} | {2} -> {3}", componentTypeName, field, Format(current), Format(value)));

            if (ColorsEqual(current, value)) continue;

            property.colorValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(component);
            changed++;
        }

        return changed;
    }

    private static bool IsPanelCandidate(Image image)
    {
        if (image.color.a < 0.05f) return false;

        string name = Normalise(image.gameObject.name);
        if (ContainsAny(name, OverlayMarkers)) return false;
        if (ContainsAny(name, SeparatorMarkers)) return false;
        if (name.Contains("header")) return false;
        if (HasMask(image.gameObject)) return false;

        bool named = ContainsAny(name, PanelMarkers);
        if (!named && image.sprite != null) return false;

        return named || HasTextDescendant(image.transform);
    }

    private static bool HasMask(GameObject gameObject)
    {
        foreach (string typeName in MaskTypeNames)
        {
            if (gameObject.GetComponent(typeName) != null) return true;
        }

        return false;
    }

    private static bool HasTextDescendant(Transform transform)
    {
        TMP_Text tmp = transform.GetComponentInChildren<TMP_Text>(true);
        if (tmp != null) return true;

        Text legacy = transform.GetComponentInChildren<Text>(true);
        return legacy != null;
    }

    private static bool HasPanelAncestor(Transform transform, HashSet<Transform> panelTransforms)
    {
        Transform parent = transform.parent;

        while (parent != null)
        {
            if (panelTransforms.Contains(parent)) return true;
            parent = parent.parent;
        }

        return false;
    }

    private static bool IsButtonPlate(Image image)
    {
        Button button = image.GetComponentInParent<Button>(true);
        if (button == null) return false;

        return button.targetGraphic == image;
    }

    private static bool IsButtonLabel(Transform transform)
    {
        Button button = transform.GetComponentInParent<Button>(true);
        if (button == null) return false;

        Graphic target = button.targetGraphic;
        if (target == null) return false;
        if (transform.parent == null) return false;

        return target.transform == transform.parent;
    }

    private static float Brightness(Color color)
    {
        return Mathf.Max(color.r, Mathf.Max(color.g, color.b));
    }

    private static string Normalise(string value)
    {
        return string.IsNullOrEmpty(value) ? string.Empty : value.ToLowerInvariant();
    }

    private static bool ContainsAny(string value, string[] markers)
    {
        foreach (string marker in markers)
        {
            if (value.Contains(marker)) return true;
        }

        return false;
    }

    private static string BuildPath(Transform transform)
    {
        string path = transform.name;
        Transform parent = transform.parent;

        while (parent != null)
        {
            path = parent.name + "/" + path;
            parent = parent.parent;
        }

        return path;
    }

    private static string Format(Color color)
    {
        return string.Format("r{0:F2} g{1:F2} b{2:F2} a{3:F2}", color.r, color.g, color.b, color.a);
    }

    private static string Shorten(string value)
    {
        if (string.IsNullOrEmpty(value)) return "<empty>";
        string flat = value.Replace("\n", " ").Replace("\r", " ").Trim();
        if (flat.Length <= 24) return flat;
        return flat.Substring(0, 24) + "…";
    }

    private static bool ColorsEqual(Color a, Color b)
    {
        return Mathf.Abs(a.r - b.r) < 0.002f
               && Mathf.Abs(a.g - b.g) < 0.002f
               && Mathf.Abs(a.b - b.b) < 0.002f
               && Mathf.Abs(a.a - b.a) < 0.002f;
    }
}
