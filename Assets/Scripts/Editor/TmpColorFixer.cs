using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class TmpColorFixer
{
    private const string ReportPath = "Logs/tmp_color_fix_report.txt";
    private const string PrefabRoot = "Assets/Prefabs";
    private const string FaceColorProperty = "_FaceColor";

    private static readonly string[] TargetScenes =
    {
        "Assets/Scenes/Bunker(main).unity",
        "Assets/Scenes/MainMenu.unity",
        "Assets/Scenes/PreparingDemo.unity",
        "Assets/Scenes/FirstMission(Forest).unity",
        "Assets/Scenes/Boot.unity"
    };

    private static readonly string[] TmpColorFields =
    {
        "m_fontColor",
        "m_fontColor32",
        "m_faceColor",
        "m_underlineColor",
        "m_strikethroughColor"
    };

    private static readonly string[] TmpGradientFields =
    {
        "topLeft",
        "topRight",
        "bottomLeft",
        "bottomRight"
    };

    private static readonly string[] SuccessMarkers =
    {
        "success", "complete", "done", "выполн", "успех", "пройден", "одобр"
    };

    private static readonly string[] FailureMarkers =
    {
        "fail", "locked", "error", "провал", "заблок", "недоступ", "ошиб"
    };

    [MenuItem("Tools/DroneSimulator/Text/1. Fix TMP Green Text", false, 400)]
    public static void FixGreenText()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[TmpColorFixer] Play Mode активен. Выйдите из Play Mode и запустите починку цвета текста повторно.");
            return;
        }

        List<string> report = new List<string>();
        report.Add("TMP COLOR FIX REPORT — " + System.DateTime.Now.ToString("u"));
        report.Add("");
        report.Add("MATERIAL: " + FaceColorProperty + " -> Color.white для всех шрифтовых материалов.");
        report.Add("TMP FIELDS: m_fontColor / m_fontColor32 / m_faceColor / m_underlineColor / m_strikethroughColor / градиент -> белый или цвет UIColorPalette.");
        report.Add("PREFABS: " + PrefabRoot);
        report.Add("SCENES: " + string.Join(", ", TargetScenes));
        report.Add("");

        int materials = FixFontMaterials(report);
        report.Add("");
        int prefabs = FixPrefabs(report);
        report.Add("");
        int scenes = FixScenes(report);

        report.Add("");
        report.Add("==============================================");
        report.Add("MATERIALS FIXED: " + materials);
        report.Add("PREFABS CHANGED: " + prefabs);
        report.Add("SCENES CHANGED: " + scenes);

        Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
        File.WriteAllText(ReportPath, string.Join("\n", report));

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"<color=lime>[TmpColorFixer]</color> Зелёный цвет шрифта вычищен. Материалы: {materials}, правок в префабах: {prefabs}, правок в сценах: {scenes}. Отчёт: {ReportPath}");
    }

    [MenuItem("Tools/DroneSimulator/Text/2. Open TMP Color Report", false, 401)]
    public static void OpenReport()
    {
        if (!File.Exists(ReportPath))
        {
            Debug.LogWarning($"[TmpColorFixer] Отчёт ещё не создан: {ReportPath}. Сначала запустите починку цвета текста.");
            return;
        }

        Debug.Log($"[TmpColorFixer] Отчёт: {ReportPath}{System.Environment.NewLine}{File.ReadAllText(ReportPath)}");
    }

    private static int FixFontMaterials(List<string> report)
    {
        report.Add("=== FONT MATERIALS ===");
        int changed = 0;
        HashSet<Material> visited = new HashSet<Material>();

        foreach (string guid in AssetDatabase.FindAssets("t:TMP_FontAsset"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path)) continue;

            foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                Material material = asset as Material;
                if (material == null) continue;
                if (!visited.Add(material)) continue;

                changed += ApplyWhiteFaceColor(material, path, report);
            }
        }

        foreach (string guid in AssetDatabase.FindAssets("t:Material"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path)) continue;

            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) continue;
            if (!IsFontMaterial(material)) continue;
            if (!visited.Add(material)) continue;

            changed += ApplyWhiteFaceColor(material, path, report);
        }

        AssetDatabase.SaveAssets();
        return changed;
    }

    private static int ApplyWhiteFaceColor(Material material, string path, List<string> report)
    {
        if (material == null) return 0;
        if (!material.HasProperty(FaceColorProperty)) return 0;

        Color current = material.GetColor(FaceColorProperty);

        if (IsWhite(current))
        {
            report.Add("MAT-KEEP " + path + " | " + material.name + " | " + Format(current));
            return 0;
        }

        material.SetColor(FaceColorProperty, Color.white);
        EditorUtility.SetDirty(material);
        report.Add("MAT-FIX  " + path + " | " + material.name + " | " + Format(current) + " -> " + Format(Color.white));
        return 1;
    }

    private static bool IsFontMaterial(Material material)
    {
        if (material == null) return false;
        if (!material.HasProperty(FaceColorProperty)) return false;
        if (material.name.Contains("LiberationSans")) return true;
        if (material.shader == null) return false;

        return material.shader.name.Contains("TextMeshPro");
    }

    private static int FixPrefabs(List<string> report)
    {
        report.Add("=== PREFABS ===");
        int changed = 0;
        int files = 0;

        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { PrefabRoot }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path)) continue;

            GameObject root = PrefabUtility.LoadPrefabContents(path);

            if (root == null)
            {
                report.Add("[MISSING] " + path);
                continue;
            }

            List<string> local = new List<string>();
            int localChanges = ProcessHierarchy(root, path, local);

            if (localChanges > 0)
            {
                PrefabUtility.SaveAsPrefabAsset(root, path);
                changed += localChanges;
                files++;
                report.Add("");
                report.Add("=== PREFAB " + path + " (" + localChanges + ") ===");
                report.AddRange(local);
            }

            PrefabUtility.UnloadPrefabContents(root);
        }

        report.Add("");
        report.Add("PREFABS TOUCHED: " + files);
        return changed;
    }

    private static int FixScenes(List<string> report)
    {
        report.Add("=== SCENES ===");
        string previousScene = EditorSceneManager.GetActiveScene().path;
        int changed = 0;
        int files = 0;

        foreach (string path in TargetScenes)
        {
            if (!File.Exists(path))
            {
                report.Add("[MISSING] " + path);
                continue;
            }

            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            GameObject[] roots = scene.GetRootGameObjects();
            List<string> local = new List<string>();
            int localChanges = 0;

            foreach (GameObject root in roots)
                localChanges += ProcessHierarchy(root, path, local);

            if (localChanges > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                changed += localChanges;
                files++;
                report.Add("");
                report.Add("=== SCENE " + path + " (" + localChanges + ") ===");
                report.AddRange(local);
            }
        }

        if (!string.IsNullOrEmpty(previousScene) && File.Exists(previousScene))
            EditorSceneManager.OpenScene(previousScene, OpenSceneMode.Single);

        report.Add("");
        report.Add("SCENES TOUCHED: " + files);
        return changed;
    }

    private static int ProcessHierarchy(GameObject root, string source, List<string> report)
    {
        int changed = 0;

        foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
        {
            if (text == null) continue;
            if (FixSharedMaterial(text, source, report)) changed++;
            changed += ApplyTmpColors(text, source, report);
        }

        foreach (Text text in root.GetComponentsInChildren<Text>(true))
        {
            if (text == null) continue;
            if (ApplyLegacyColor(text, source, report)) changed++;
        }

        return changed;
    }

    private static bool FixSharedMaterial(TMP_Text text, string source, List<string> report)
    {
        Material shared = text.fontSharedMaterial;
        if (shared == null) return false;
        if (!shared.HasProperty(FaceColorProperty)) return false;

        Color current = shared.GetColor(FaceColorProperty);
        if (IsWhite(current)) return false;

        shared.SetColor(FaceColorProperty, Color.white);
        EditorUtility.SetDirty(shared);
        report.Add("TMP-MAT " + source + " :: " + BuildPath(text.transform) + " | " + shared.name + " | " + Format(current) + " -> " + Format(Color.white));
        return true;
    }

    private static int ApplyTmpColors(TMP_Text text, string source, List<string> report)
    {
        SerializedObject serialized = new SerializedObject(text);
        SerializedProperty fontColor = serialized.FindProperty("m_fontColor");

        if (fontColor == null || fontColor.propertyType != SerializedPropertyType.Color) return 0;

        string path = BuildPath(text.transform);
        string content = Shorten(text.text);
        int changed = 0;

        Color original = fontColor.colorValue;
        Color target = ResolveTarget(text.gameObject.name, text.text, original);

        if (ColorsEqual(original, target))
        {
            ReportKept(original, source, path, content, report);
        }
        else
        {
            fontColor.colorValue = target;
            changed++;
            report.Add("TMP-COL " + source + " :: " + path + " | " + content + " | " + Format(original) + " -> " + Format(target));
        }

        Color effective = fontColor.colorValue;

        foreach (string field in TmpColorFields)
        {
            if (field == "m_fontColor") continue;

            SerializedProperty property = serialized.FindProperty(field);
            if (property == null || property.propertyType != SerializedPropertyType.Color) continue;

            Color current = property.colorValue;
            Color resolved = field == "m_fontColor32" ? effective : ResolveTarget(text.gameObject.name, text.text, current);

            if (ColorsEqual(current, resolved)) continue;

            property.colorValue = resolved;
            changed++;
            report.Add("TMP-FLD " + source + " :: " + path + " | " + field + " | " + Format(current) + " -> " + Format(resolved));
        }

        SerializedProperty gradient = serialized.FindProperty("m_fontColorGradient");

        if (gradient != null)
        {
            foreach (string corner in TmpGradientFields)
            {
                SerializedProperty property = gradient.FindPropertyRelative(corner);
                if (property == null || property.propertyType != SerializedPropertyType.Color) continue;

                Color current = property.colorValue;
                Color resolved = ResolveTarget(text.gameObject.name, text.text, current);

                if (ColorsEqual(current, resolved)) continue;

                property.colorValue = resolved;
                changed++;
                report.Add("TMP-GRD " + source + " :: " + path + " | " + corner + " | " + Format(current) + " -> " + Format(resolved));
            }
        }

        if (changed == 0) return 0;

        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(text);
        return changed;
    }

    private static bool ApplyLegacyColor(Text text, string source, List<string> report)
    {
        Color original = text.color;
        Color target = ResolveTarget(text.gameObject.name, text.text, original);

        if (ColorsEqual(original, target))
        {
            ReportKept(original, source, BuildPath(text.transform), Shorten(text.text), report);
            return false;
        }

        text.color = target;
        EditorUtility.SetDirty(text);
        report.Add("UI-COL " + source + " :: " + BuildPath(text.transform) + " | " + Shorten(text.text) + " | " + Format(original) + " -> " + Format(target));
        return true;
    }

    private static void ReportKept(Color color, string source, string path, string content, List<string> report)
    {
        if (!IsGreenTint(color)) return;

        report.Add("GREEN-KEEP " + source + " :: " + path + " | " + content + " | " + Format(color) + " | вне кислотного диапазона палитры");
    }

    private static Color ResolveTarget(string name, string content, Color original)
    {
        if (IsWhite(original)) return original;
        if (IsPaletteColor(original)) return original;
        if (!IsGreenTint(original)) return original;
        if (!UIColorPalette.IsAcidic(original)) return original;

        string marker = Normalise(name) + " " + Normalise(content);
        Color target = UIColorPalette.TextPrimary;

        if (ContainsAny(marker, SuccessMarkers)) target = UIColorPalette.Success;
        else if (ContainsAny(marker, FailureMarkers)) target = UIColorPalette.Failure;

        target.a = original.a;
        return target;
    }

    private static bool IsGreenTint(Color color)
    {
        if (color.g <= color.r + 0.06f) return false;
        return color.g > color.b + 0.06f;
    }

    private static bool IsPaletteColor(Color color)
    {
        return ColorsEqual(color, UIColorPalette.TextPrimary)
               || ColorsEqual(color, UIColorPalette.TextSecondary)
               || ColorsEqual(color, UIColorPalette.Success)
               || ColorsEqual(color, UIColorPalette.Failure)
               || ColorsEqual(color, UIColorPalette.Accent)
               || ColorsEqual(color, UIColorPalette.AccentHover)
               || ColorsEqual(color, UIColorPalette.AccentPressed);
    }

    private static bool IsWhite(Color color)
    {
        return color.r >= 0.999f
               && color.g >= 0.999f
               && color.b >= 0.999f
               && color.a >= 0.999f;
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
        return string.Format("r{0:F3} g{1:F3} b{2:F3} a{3:F3}", color.r, color.g, color.b, color.a);
    }

    private static string Shorten(string value)
    {
        if (string.IsNullOrEmpty(value)) return "<empty>";

        string flat = value.Replace("\n", " ").Replace("\r", " ").Trim();
        if (flat.Length <= 28) return flat;

        return flat.Substring(0, 28) + "…";
    }

    private static bool ColorsEqual(Color a, Color b)
    {
        return Mathf.Abs(a.r - b.r) < 0.002f
               && Mathf.Abs(a.g - b.g) < 0.002f
               && Mathf.Abs(a.b - b.b) < 0.002f
               && Mathf.Abs(a.a - b.a) < 0.002f;
    }
}
