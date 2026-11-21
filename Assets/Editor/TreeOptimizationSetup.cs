using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

// Safe (shader-preserving) optimisation pass for the three terrain tree prototypes of
// FirstMission(Forest): Conifer, Cypress_Forest_Desktop and Pine_B.
//
// The SpeedTree shader is intentionally NOT replaced - billboards and LOD cross-fades keep
// working. Only import settings, material parameters and LOD shadow flags are tuned:
//   * colour/albedo textures (branches + billboards) get mip coverage preservation, a 0.45
//     alpha reference, aniso 4 and bilinear filtering,
//   * SpeedTree materials lose their specular sparkle (glossiness 0.12), use a soft alpha
//     clip of 0.40, wind = Fastest on foliage and wind = None on bark/billboards,
//   * LOD0/LOD1 keep casting shadows, the far mesh LODs and the billboard stop casting,
//     and the billboard stops receiving shadows (big shadow-map win on a Radeon 780M).
// Menu: Tools/DroneSimulator/Trees/Apply Tree Optimization
public static class TreeOptimizationSetup
{
    const string ScenePath = "Assets/Scenes/FirstMission(Forest).unity";
    const string MainTerrainName = "Terrain";
    const string ReportPath = "Logs/TreeOptimizationReport.txt";
    const string UndoLabel = "Apply tree optimization";

    const float AlphaReference = 0.45f;
    const int AnisoLevel = 4;
    const float Glossiness = 0.12f;          // 0.08-0.15 per request
    const float AlphaClipThreshold = 0.40f;  // 0.38-0.42 per request
    const int WindFastest = 2;               // Shader Graph enum id: 2 = Fastest
    const int WindNone = 0;

    static readonly string[] WindKeywords =
    {
        "_WINDQUALITY_NONE",
        "_WINDQUALITY_FASTEST",
        "_WINDQUALITY_FAST",
        "_WINDQUALITY_BETTER",
        "_WINDQUALITY_BEST",
        "_WINDQUALITY_PALM",
    };

    static readonly string[] ColorTexturePatterns = { "_Color", "_Albedo", "_BaseColor" };

    [MenuItem("Tools/DroneSimulator/Trees/Apply Tree Optimization", priority = 20)]
    public static void ApplyTreeOptimization()
    {
        var report = new StringBuilder();
        report.AppendLine("TreeOptimizationSetup started " + DateTime.Now.ToString("s"));
        int errors = 0;

        try
        {
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            bool openedByUs = false;
            if (!scene.IsValid() || !scene.isLoaded)
            {
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
                openedByUs = true;
            }

            Terrain terrain = scene.IsValid() ? FindMainTerrain(scene) : null;
            if (terrain == null || terrain.terrainData == null)
            {
                report.AppendLine("TERRAIN NOT FOUND in " + ScenePath);
                errors++;
            }
            else
            {
                errors += OptimizeTrees(terrain, report);
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene))
                {
                    report.AppendLine("SCENE SAVE FAILED");
                    errors++;
                }
            }

            if (openedByUs)
                EditorSceneManager.CloseScene(scene, true);
        }
        catch (Exception exception)
        {
            errors++;
            report.AppendLine("EXCEPTION: " + exception);
            Debug.LogException(exception);
        }

        AssetDatabase.SaveAssets();
        report.AppendLine("RESULT errors=" + errors);
        WriteReport(report);

        if (errors == 0)
            Debug.Log("[TreeOptimization] Applied. See " + ReportPath);
        else
            Debug.LogError("[TreeOptimization] Finished with " + errors + " error(s). See " + ReportPath);
    }

    static int OptimizeTrees(Terrain terrain, StringBuilder report)
    {
        TerrainData data = terrain.terrainData;
        TreePrototype[] prototypes = data.treePrototypes;
        report.AppendLine("tree prototypes: " + (prototypes != null ? prototypes.Length : 0) +
                          " on '" + terrain.gameObject.name + "'");

        var prefabPaths = new List<string>();
        if (prototypes != null)
        {
            for (int i = 0; i < prototypes.Length; i++)
            {
                TreePrototype prototype = prototypes[i];
                if (prototype == null || prototype.prefab == null)
                {
                    report.AppendLine("prototype[" + i + "]: <null>");
                    continue;
                }

                string path = AssetDatabase.GetAssetPath(prototype.prefab);
                report.AppendLine("prototype[" + i + "]: " + prototype.prefab.name + " -> " + path);
                if (!string.IsNullOrEmpty(path) && !prefabPaths.Contains(path))
                    prefabPaths.Add(path);
            }
        }

        if (prefabPaths.Count == 0)
        {
            report.AppendLine("NO TREE PREFABS FOUND");
            return 1;
        }

        var materialPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var texturePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string prefabPath in prefabPaths)
            CollectMaterialsAndTextures(prefabPath, materialPaths, texturePaths, report);

        int errors = 0;
        errors += ConfigureTextureImporters(texturePaths, report);
        errors += ConfigureMaterials(materialPaths, report);
        errors += OptimizePrefabLods(prefabPaths, report);
        return errors;
    }

    static void CollectMaterialsAndTextures(string prefabPath, HashSet<string> materialPaths,
        HashSet<string> texturePaths, StringBuilder report)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
        {
            report.AppendLine("prefab load failed: " + prefabPath);
            return;
        }

        foreach (Renderer renderer in prefab.GetComponentsInChildren<Renderer>(true))
        {
            foreach (Material material in renderer.sharedMaterials)
            {
                if (material == null) continue;
                string materialPath = AssetDatabase.GetAssetPath(material);
                if (string.IsNullOrEmpty(materialPath) || !materialPaths.Add(materialPath))
                    continue;

                foreach (string property in material.GetTexturePropertyNames())
                {
                    var texture = material.GetTexture(property);
                    if (texture == null) continue;
                    string texturePath = AssetDatabase.GetAssetPath(texture);
                    if (!string.IsNullOrEmpty(texturePath))
                        texturePaths.Add(texturePath);
                }
            }
        }

        // Billboard colour sheets are not always wired through a material property, so also
        // sweep the prefab's Textures folder for *_Color / *_Albedo / *_BaseColor files.
        string folder = Path.Combine(Path.GetDirectoryName(prefabPath) ?? string.Empty, "Textures")
            .Replace('\\', '/');
        if (AssetDatabase.IsValidFolder(folder))
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { folder }))
                texturePaths.Add(AssetDatabase.GUIDToAssetPath(guid));
        }

        report.AppendLine("collected from " + Path.GetFileName(prefabPath) + ": " +
                          materialPaths.Count + " material(s), " + texturePaths.Count + " texture(s) so far");
    }


    // ============================================================================
    //  1. Colour/albedo texture importers
    // ============================================================================
    static int ConfigureTextureImporters(HashSet<string> texturePaths, StringBuilder report)
    {
        int updated = 0;
        int skippedNoAlpha = 0;
        int errors = 0;

        var ordered = new List<string>(texturePaths);
        ordered.Sort(StringComparer.OrdinalIgnoreCase);

        foreach (string path in ordered)
        {
            string fileName = Path.GetFileNameWithoutExtension(path);
            if (!MatchesColorPattern(fileName))
                continue;

            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                report.AppendLine("TEXTURE IMPORTER MISSING: " + path);
                errors++;
                continue;
            }

            bool hasAlpha;
            try
            {
                hasAlpha = importer.DoesSourceTextureHaveAlpha();
            }
            catch (Exception exception)
            {
                report.AppendLine("alpha check failed for " + path + ": " + exception.Message);
                errors++;
                continue;
            }

            if (!hasAlpha)
            {
                skippedNoAlpha++;
                report.AppendLine("texture skipped (no alpha): " + path);
                continue;
            }

            importer.mipmapEnabled = true;
            importer.mipMapsPreserveCoverage = true;
            importer.alphaTestReferenceValue = AlphaReference;
            importer.anisoLevel = AnisoLevel;
            importer.filterMode = FilterMode.Bilinear;
            importer.SaveAndReimport();

            updated++;
            report.AppendLine("texture OK: " + path +
                              " (mipPreserve=1, alphaRef=" + AlphaReference.ToString("F2") +
                              ", aniso=" + AnisoLevel + ", bilinear)");
        }

        report.AppendLine("colour textures updated=" + updated +
                          ", skipped without alpha=" + skippedNoAlpha);
        return errors;
    }

    static bool MatchesColorPattern(string fileName)
    {
        foreach (string pattern in ColorTexturePatterns)
        {
            if (fileName.IndexOf(pattern, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }
        return false;
    }


    // ============================================================================
    //  2. SpeedTree material tuning (shader untouched)
    // ============================================================================
    static int ConfigureMaterials(HashSet<string> materialPaths, StringBuilder report)
    {
        int errors = 0;
        int configured = 0;
        var shaders = new Dictionary<Shader, string>();

        var ordered = new List<string>(materialPaths);
        ordered.Sort(StringComparer.OrdinalIgnoreCase);

        foreach (string path in ordered)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                report.AppendLine("MATERIAL MISSING: " + path);
                errors++;
                continue;
            }

            string name = material.name.ToLowerInvariant();
            bool isBillboard = name.Contains("billboard");
            bool isBark = name.Contains("bark");
            bool foliage = !isBillboard && !isBark;
            int wind = foliage ? WindFastest : WindNone;

            Undo.RecordObject(material, UndoLabel);

            float oldWind = material.HasProperty("_WindQuality") ? material.GetFloat("_WindQuality") : -1f;
            float oldGloss = material.HasProperty("_Glossiness") ? material.GetFloat("_Glossiness") :
                (material.HasProperty("_Smoothness") ? material.GetFloat("_Smoothness") : -1f);
            float oldClip = material.HasProperty("_AlphaClipThreshold")
                ? material.GetFloat("_AlphaClipThreshold")
                : (material.HasProperty("_Cutoff") ? material.GetFloat("_Cutoff") : -1f);

            foreach (string keyword in WindKeywords)
                material.DisableKeyword(keyword);
            if (wind == WindFastest)
                material.EnableKeyword("_WINDQUALITY_FASTEST");

            // _WindQuality is the boolean "Wind Enabled" switch of the SpeedTree Shader Graph;
            // _WINDQUALITY is the enum index (2 = Fastest, 0 = none) used by the material drawer.
            if (material.HasProperty("_WindQuality"))
                material.SetFloat("_WindQuality", wind == WindFastest ? 1f : 0f);
            if (material.HasProperty("_WINDQUALITY"))
                material.SetFloat("_WINDQUALITY", (float)wind);

            if (material.HasProperty("_Glossiness"))
                material.SetFloat("_Glossiness", Glossiness);
            if (material.HasProperty("_Smoothness"))
                material.SetFloat("_Smoothness", Glossiness);
            if (material.HasProperty("_AlphaClipThreshold"))
                material.SetFloat("_AlphaClipThreshold", AlphaClipThreshold);
            if (material.HasProperty("_Cutoff"))
                material.SetFloat("_Cutoff", AlphaClipThreshold);

            EditorUtility.SetDirty(material);
            configured++;

            if (material.shader != null && !shaders.ContainsKey(material.shader))
                shaders.Add(material.shader, material.shader.name);

            report.AppendLine("material '" + material.name + "' shader='" +
                              (material.shader != null ? material.shader.name : "<null>") + "'" +
                              " wind=" + (wind == WindNone ? "None" : "Fastest") +
                              " (was _WindQuality=" + oldWind.ToString("F1") + ")" +
                              " glossiness " +
                              (oldGloss < 0f ? "n/a" : oldGloss.ToString("F2") + " -> " + Glossiness.ToString("F2")) +
                              " alphaClip " +
                              (oldClip < 0f ? "n/a" : oldClip.ToString("F2") + " -> " + AlphaClipThreshold.ToString("F2")));
        }

        AssetDatabase.SaveAssets();
        report.AppendLine("materials configured=" + configured);

        // SRP Batcher compatibility (informational - the shader itself is never replaced).
        foreach (KeyValuePair<Shader, string> pair in shaders)
        {
            // Warm the shader up first: without a compiled pass the compatibility database
            // reports "Not initialized" instead of the real code (same probe the grass tool uses).
            Material probe = null;
            try
            {
                probe = new Material(pair.Key);
                probe.SetPass(0);
            }
            catch (Exception exception)
            {
                report.AppendLine("shader probe failed for '" + pair.Value + "': " + exception.Message);
            }

            CheckSrpBatcher(pair.Key, pair.Value, report);
            errors += ReportShaderMessages(pair.Key, pair.Value, report);

            if (probe != null)
                UnityEngine.Object.DestroyImmediate(probe);
        }

        return errors;
    }

    static int ReportShaderMessages(Shader shader, string label, StringBuilder report)
    {
        int errorCount = 0;
        try
        {
            ShaderMessage[] messages = ShaderUtil.GetShaderMessages(shader);
            if (messages == null || messages.Length == 0)
            {
                report.AppendLine("shader messages '" + label + "': none");
                return 0;
            }

            foreach (ShaderMessage message in messages)
            {
                bool isError = message.severity.ToString()
                    .IndexOf("Error", StringComparison.OrdinalIgnoreCase) >= 0;
                if (isError) errorCount++;
                report.AppendLine((isError ? "SHADER ERROR '" : "shader warning '") + label +
                                  "': " + message.message + " [" + message.platform + " " +
                                  message.file + ":" + message.line + "]");
            }
        }
        catch (Exception exception)
        {
            report.AppendLine("shader message check failed for '" + label + "': " + exception.Message);
        }
        return errorCount;
    }

    static void CheckSrpBatcher(Shader shader, string label, StringBuilder report)
    {
        try
        {
            int subShader = 0;
            MethodInfo activeSubShader = typeof(ShaderUtil).GetMethod(
                "GetShaderActiveSubshaderIndex",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (activeSubShader != null)
            {
                object result = activeSubShader.Invoke(null, new object[] { shader });
                if (result is int)
                    subShader = (int)result;
            }

            MethodInfo compatibility = typeof(ShaderUtil).GetMethod(
                "GetSRPBatcherCompatibilityCode",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (compatibility == null)
            {
                report.AppendLine("SRP Batcher '" + label + "': compatibility API unavailable");
                return;
            }

            int code = Convert.ToInt32(compatibility.Invoke(null, new object[] { shader, subShader }));
            string reasonText = string.Empty;
            if (code != 0)
            {
                MethodInfo reasonMethod = typeof(ShaderUtil).GetMethod(
                    "GetSRPBatcherCompatibilityIssueReason",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                if (reasonMethod != null)
                {
                    object reason = reasonMethod.Invoke(null, new object[] { shader, subShader, code });
                    reasonText = reason != null ? reason.ToString() : string.Empty;
                }
            }

            // Unity reports "Not initialized" until its SRP Batcher database has been warmed by
            // a rendered frame. That is not an incompatibility, so it is reported separately.
            bool pending = reasonText.IndexOf("Not initialized", StringComparison.OrdinalIgnoreCase) >= 0;
            report.AppendLine("SRP Batcher '" + label + "': " +
                              (code == 0
                                  ? "compatible"
                                  : pending
                                      ? "pending (engine not initialized, shader untouched)"
                                      : "NOT compatible") +
                              " (code=" + code + ", subShader=" + subShader + ")");
            if (reasonText.Length > 0)
                report.AppendLine("  reason: " + reasonText);
        }
        catch (Exception exception)
        {
            report.AppendLine("SRP Batcher check failed for '" + label + "': " + exception.Message);
        }
    }


    // ============================================================================
    //  3. LOD / shadow optimisation on the prefabs
    // ============================================================================
    static int OptimizePrefabLods(List<string> prefabPaths, StringBuilder report)
    {
        int errors = 0;

        foreach (string prefabPath in prefabPaths)
        {
            GameObject contents = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                var group = contents.GetComponentInChildren<LODGroup>(true);
                if (group == null)
                {
                    report.AppendLine("LODGroup missing in " + prefabPath);
                    errors++;
                    continue;
                }

                LOD[] lods = group.GetLODs();
                report.AppendLine("prefab '" + Path.GetFileNameWithoutExtension(prefabPath) + "': " +
                                  lods.Length + " LOD(s), lastLODBillboard=" + group.lastLODBillboard);

                bool changed = false;
                for (int i = 0; i < lods.Length; i++)
                {
                    bool farMesh = i >= 2;
                    bool isLast = i == lods.Length - 1;
                    bool billboard = isLast && (group.lastLODBillboard || LodHasBillboardMaterial(lods[i]));

                    foreach (Renderer renderer in lods[i].renderers)
                    {
                        if (renderer == null) continue;

                        ShadowCastingMode beforeCast = renderer.shadowCastingMode;
                        bool beforeReceive = renderer.receiveShadows;
                        ShadowCastingMode wanted = farMesh
                            ? ShadowCastingMode.Off
                            : ShadowCastingMode.On;

                        if (renderer.shadowCastingMode != wanted)
                        {
                            renderer.shadowCastingMode = wanted;
                            changed = true;
                        }

                        if (billboard && renderer.receiveShadows)
                        {
                            renderer.receiveShadows = false;
                            changed = true;
                        }

                        report.AppendLine("  LOD" + i + " renderer '" + renderer.name + "': cast " +
                                          beforeCast + " -> " + renderer.shadowCastingMode +
                                          ", receive " + beforeReceive + " -> " + renderer.receiveShadows +
                                          (billboard ? " (billboard)" : string.Empty));
                    }
                }

                if (changed)
                {
                    PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
                    report.AppendLine("  prefab saved: " + prefabPath);
                }
                else
                {
                    report.AppendLine("  prefab already optimised: " + prefabPath);
                }
            }
            catch (Exception exception)
            {
                errors++;
                report.AppendLine("LOD optimisation failed for " + prefabPath + ": " + exception);
                Debug.LogException(exception);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        AssetDatabase.SaveAssets();
        return errors;
    }

    static bool LodHasBillboardMaterial(LOD lod)
    {
        if (lod.renderers == null) return false;
        foreach (Renderer renderer in lod.renderers)
        {
            if (renderer == null) continue;
            foreach (Material material in renderer.sharedMaterials)
            {
                if (material != null &&
                    material.name.IndexOf("billboard", StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
        }
        return false;
    }

    // ============================================================================
    //  Scene / report helpers
    // ============================================================================
    static Terrain FindMainTerrain(Scene scene)
    {
        Terrain named = null;
        Terrain largest = null;
        float largestArea = -1f;

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Terrain terrain in root.GetComponentsInChildren<Terrain>(true))
            {
                if (terrain.gameObject.name == MainTerrainName)
                    named = terrain;

                float area = terrain.terrainData != null
                    ? terrain.terrainData.size.x * terrain.terrainData.size.z
                    : 0f;
                if (area > largestArea)
                {
                    largestArea = area;
                    largest = terrain;
                }
            }
        }

        return named != null ? named : largest;
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
            Debug.LogWarning("[TreeOptimization] could not write the report: " + exception.Message);
        }
    }
}

