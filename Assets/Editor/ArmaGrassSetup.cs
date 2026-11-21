using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

// Applies the URP_ArmaGrass optimization to the terrain vegetation of FirstMission(Forest):
//   * texture importer settings for the seven detail BaseColor maps,
//   * switches the six CustomGrass materials to URP_ArmaGrass and applies presets,
//   * syncs the main Terrain detail distance with the shader distance fade.
// Menu: Tools/DroneSimulator/Grass/Apply Arma Grass Optimization
public static class ArmaGrassSetup
{
    const string ShaderAssetPath = "Assets/Shaders/URP_ArmaGrass.shader";
    const string ShaderName = "DroneSimulator/URP_ArmaGrass";
    const string MaterialFolder = "Assets/Materials/CustomGrass";
    const string DetailTextureFolder = "Assets/TerrainSampleAssets/Textures/Details/";
    const string ScenePath = "Assets/Scenes/FirstMission(Forest).unity";
    const string MainTerrainName = "Terrain";
    const string ReportPath = "Logs/ArmaGrassSetupReport.txt";
    const float DetailObjectDistance = 200f;

    // BaseColor maps of the terrain detail prototypes (Assets/TerrainSampleAssets/Textures/Details).
    static readonly string[] BaseColorTextureNames =
    {
        "Bush_A_BaseColor",
        "BushDry_BaseColor",
        "Fern_BaseColor",
        "Grass_A_BaseColor",
        "Grass_C_BaseColor",
        "GrassDry_BaseColor",
        "Heather_A_BaseColor",
    };

    static readonly string[] GrassMaterialNames =
    {
        "Grass_Custom",
        "Grass_C_Custom",
        "GrassDry_Custom",
    };

    static readonly string[] BushMaterialNames =
    {
        "Bush_Custom",
        "BushDry_Custom",
        "Fern_Custom",
    };

    // Fallback if the old Shader Graph properties cannot be inspected anymore.
    static readonly Dictionary<string, string> BaseColorByMaterial =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Bush_Custom", "Bush_A_BaseColor" },
            { "BushDry_Custom", "BushDry_BaseColor" },
            { "Fern_Custom", "Fern_BaseColor" },
            { "Grass_Custom", "Grass_A_BaseColor" },
            { "Grass_C_Custom", "Grass_C_BaseColor" },
            { "GrassDry_Custom", "GrassDry_BaseColor" },
        };

    // #4A4E36 - muted olive/soil tone used to dissolve the far grass boundary.
    static readonly Color FadeColor = new Color(0.2901961f, 0.3058824f, 0.2117647f, 1f);
    static readonly Color MacroTintGrass = new Color(0.92f, 1.00f, 0.88f, 1f);
    static readonly Color MacroTintBush = new Color(0.95f, 1.00f, 0.92f, 1f);

    [MenuItem("Tools/DroneSimulator/Grass/Apply Arma Grass Optimization", priority = 10)]
    public static void ApplyArmaGrassOptimization()
    {
        var report = new StringBuilder();
        report.AppendLine("ArmaGrassSetup started " + DateTime.Now.ToString("s"));
        int errors = 0;

        try
        {
            errors += ConfigureTextureImporters(report);

            Shader shader = ResolveShader(report);
            errors += ValidateShader(shader, report);
            errors += ConfigureMaterials(shader, report);
            errors += ConfigureTerrain(report);
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
            Debug.Log("[ArmaGrass] Optimization applied. See " + ReportPath);
        else
            Debug.LogError("[ArmaGrass] Finished with " + errors + " error(s). See " + ReportPath);
    }

    static void WriteReport(StringBuilder report)
    {
        try
        {
            string root = Directory.GetCurrentDirectory();
            string directory = Path.Combine(root, "Logs");
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(root, ReportPath), report.ToString());
        }
        catch (Exception exception)
        {
            Debug.LogWarning("[ArmaGrass] Could not write the report: " + exception.Message);
        }
    }

    // ------------------------------------------------------------------ textures
    static int ConfigureTextureImporters(StringBuilder report)
    {
        var wanted = new HashSet<string>(BaseColorTextureNames, StringComparer.OrdinalIgnoreCase);
        var found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int updated = 0;
        int errors = 0;

        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets" });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string fileName = Path.GetFileNameWithoutExtension(path);
            if (!wanted.Contains(fileName))
                continue;
            if (!path.StartsWith(DetailTextureFolder, StringComparison.OrdinalIgnoreCase))
                continue;
            found.Add(fileName);

            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                report.AppendLine("TEXTURE IMPORTER MISSING: " + path);
                errors++;
                continue;
            }

            importer.mipmapEnabled = true;
            importer.mipMapsPreserveCoverage = true;
            importer.alphaTestReferenceValue = 0.45f;
            importer.anisoLevel = 4;
            importer.filterMode = FilterMode.Bilinear;
            importer.SaveAndReimport();

            updated++;
            report.AppendLine("texture OK: " + path +
                              " (mipPreserve=1, alphaRef=0.45, aniso=4, bilinear)");
        }

        foreach (string name in BaseColorTextureNames)
        {
            if (found.Contains(name))
                continue;
            report.AppendLine("TEXTURE MISSING: " + name);
            errors++;
        }

        report.AppendLine("textures updated=" + updated + "/" + BaseColorTextureNames.Length);
        return errors;
    }

    static Shader ResolveShader(StringBuilder report)
    {
        Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderAssetPath);
        if (shader == null)
            shader = Shader.Find(ShaderName);

        if (shader == null)
        {
            report.AppendLine("SHADER MISSING: " + ShaderAssetPath);
            Debug.LogError("[ArmaGrass] Shader not found: " + ShaderAssetPath);
        }
        else
        {
            report.AppendLine("shader loaded: " + shader.name + " (" + ShaderAssetPath + ")");
        }

        return shader;
    }

    // ------------------------------------------------------------------ materials
    static int ConfigureMaterials(Shader shader, StringBuilder report)
    {
        if (shader == null)
        {
            report.AppendLine("materials skipped: shader is missing");
            return 1;
        }

        int errors = 0;
        foreach (string materialName in GrassMaterialNames)
            errors += ConfigureMaterial(materialName, true, shader, report);
        foreach (string materialName in BushMaterialNames)
            errors += ConfigureMaterial(materialName, false, shader, report);

        report.AppendLine("materials configured=" +
                          (GrassMaterialNames.Length + BushMaterialNames.Length - errors));
        return errors;
    }

    static int ConfigureMaterial(string materialName, bool grassPreset, Shader shader, StringBuilder report)
    {
        string path = MaterialFolder + "/" + materialName + ".asset";
        if (!File.Exists(path))
            path = MaterialFolder + "/" + materialName + ".mat";

        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            report.AppendLine("MATERIAL MISSING: " + path);
            return 1;
        }

        string expectedName = Path.GetFileNameWithoutExtension(path);
        string previousName = material.name;
        if (material.name != expectedName)
        {
            var serialized = new SerializedObject(material);
            SerializedProperty nameProperty = serialized.FindProperty("m_Name");
            if (nameProperty != null)
            {
                nameProperty.stringValue = expectedName;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            EditorUtility.SetDirty(material);
            report.AppendLine("material name fixed: '" + previousName + "' -> '" + expectedName + "'");
        }

        Texture baseMap = FindCurrentBaseColorTexture(material);
        if (baseMap == null)
        {
            baseMap = LoadBaseColorFallback(materialName);
            if (baseMap != null)
                report.AppendLine("base map recovered by name for " + materialName + ": " + baseMap.name);
        }
        if (baseMap == null)
        {
            report.AppendLine("BASE MAP NOT FOUND: " + materialName);
            return 1;
        }

        // Switch the shader but keep the current color texture.
        material.shader = shader;
        material.SetTexture("_BaseMap", baseMap);
        material.SetColor("_BaseColor", Color.white);
        material.SetFloat("_MacroBrightnessStrength", 1f);
        material.SetFloat("_ReceiveShadows", 1f);
        material.SetFloat("_WindSpeed", 1.4f);

        if (grassPreset)
        {
            material.SetFloat("_NormalUpBlend", 0.95f);
            material.SetFloat("_GroundFalloff", 0.45f);
            material.SetFloat("_AlphaClipThreshold", 0.38f);
            material.SetFloat("_BacklightStrength", 0.25f);
            material.SetFloat("_MacroScale", 35f);
            material.SetFloat("_MacroVariationStrength", 0.18f);
            material.SetFloat("_FadeStart", 130f);
            material.SetFloat("_FadeEnd", 190f);
            material.SetFloat("_WindCutoffStart", 35f);
            material.SetFloat("_WindCutoffEnd", 55f);
            material.SetFloat("_WindStrength", 0.15f);
            material.SetColor("_MacroTintColor", MacroTintGrass);
            material.SetColor("_FadeColor", FadeColor);
        }
        else
        {
            material.SetFloat("_NormalUpBlend", 0.45f);
            material.SetFloat("_GroundFalloff", 0.40f);
            material.SetFloat("_AlphaClipThreshold", 0.35f);
            material.SetFloat("_BacklightStrength", 0.15f);
            material.SetFloat("_MacroScale", 40f);
            material.SetFloat("_MacroVariationStrength", 0.12f);
            material.SetFloat("_FadeStart", 150f);
            material.SetFloat("_FadeEnd", 205f);
            material.SetFloat("_WindCutoffStart", 40f);
            material.SetFloat("_WindCutoffEnd", 60f);
            material.SetFloat("_WindStrength", 0.10f);
            material.SetColor("_MacroTintColor", MacroTintBush);
        }

        material.shaderKeywords = new string[0];
        material.enableInstancing = true;
        material.renderQueue = (int)RenderQueue.AlphaTest;
        EditorUtility.SetDirty(material);

        report.AppendLine("material OK: " + path +
                          " shader=" + shader.name +
                          " baseMap=" + baseMap.name +
                          " fade=" + material.GetFloat("_FadeStart").ToString("F0") +
                          ".." + material.GetFloat("_FadeEnd").ToString("F0") +
                          " instancing=" + material.enableInstancing);
        return 0;
    }

    static Texture FindCurrentBaseColorTexture(Material material)
    {
        if (material.HasProperty("_BaseMap"))
        {
            Texture current = material.GetTexture("_BaseMap");
            if (IsBaseColorTexture(current))
                return current;
        }

        foreach (string propertyName in material.GetTexturePropertyNames())
        {
            Texture texture = material.GetTexture(propertyName);
            if (IsBaseColorTexture(texture))
                return texture;
        }

        return null;
    }

    static bool IsBaseColorTexture(Texture texture)
    {
        if (texture == null)
            return false;
        string path = AssetDatabase.GetAssetPath(texture);
        if (string.IsNullOrEmpty(path))
            return false;
        return Path.GetFileNameWithoutExtension(path)
            .EndsWith("_BaseColor", StringComparison.OrdinalIgnoreCase);
    }

    static Texture LoadBaseColorFallback(string materialName)
    {
        string baseColorName;
        if (!BaseColorByMaterial.TryGetValue(materialName, out baseColorName))
            return null;

        string[] guids = AssetDatabase.FindAssets(baseColorName + " t:Texture2D", new[] { "Assets" });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.Equals(Path.GetFileNameWithoutExtension(path), baseColorName,
                    StringComparison.OrdinalIgnoreCase))
                return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        return null;
    }

    // ------------------------------------------------------------------ validation
    static int ValidateShader(Shader shader, StringBuilder report)
    {
        if (shader == null)
            return 1;

        int errors = 0;
        Material probe = new Material(shader);
        try
        {
            // Force a variant compile so ShaderUtil reports up-to-date data.
            probe.SetPass(0);

            report.AppendLine("shader: " + shader.name +
                              " supported=" + shader.isSupported +
                              " passes=" + shader.passCount);
            if (!shader.isSupported)
                errors++;

            ShaderMessage[] messages = ShaderUtil.GetShaderMessages(shader);
            if (messages != null)
            {
                foreach (ShaderMessage message in messages)
                {
                    bool isError = message.severity.ToString()
                        .IndexOf("Error", StringComparison.OrdinalIgnoreCase) >= 0;
                    report.AppendLine((isError ? "SHADER ERROR: " : "shader warning: ") +
                                      message.message + " [" + message.platform +
                                      " " + message.file + ":" + message.line + "]");
                    if (isError)
                        errors++;
                }
            }

            int subShader = 0;
            MethodInfo activeSubShaderMethod = typeof(ShaderUtil).GetMethod(
                "GetShaderActiveSubshaderIndex",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (activeSubShaderMethod != null)
            {
                object result = activeSubShaderMethod.Invoke(null, new object[] { shader });
                if (result is int)
                    subShader = (int)result;
            }

            MethodInfo compatibilityMethod = typeof(ShaderUtil).GetMethod(
                "GetSRPBatcherCompatibilityCode",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (compatibilityMethod == null)
            {
                report.AppendLine("SRP Batcher: compatibility API unavailable");
            }
            else
            {
                int code = Convert.ToInt32(compatibilityMethod.Invoke(
                    null, new object[] { shader, subShader }));
                report.AppendLine("SRP Batcher: " + (code == 0 ? "compatible" : "NOT compatible") +
                                  " (code=" + code + ", subShader=" + subShader + ")");
                if (code != 0)
                {
                    errors++;
                    MethodInfo reasonMethod = typeof(ShaderUtil).GetMethod(
                        "GetSRPBatcherCompatibilityIssueReason",
                        BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                    if (reasonMethod != null)
                    {
                        object reason = reasonMethod.Invoke(
                            null, new object[] { shader, subShader, code });
                        report.AppendLine("SRP Batcher reason: " + reason);
                    }
                }
            }
        }
        catch (Exception exception)
        {
            report.AppendLine("SHADER VALIDATION EXCEPTION: " + exception);
            errors++;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(probe);
        }

        return errors;
    }

    // ------------------------------------------------------------------ terrain / scene
    static int ConfigureTerrain(StringBuilder report)
    {
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
            return 1;
        }

        Terrain terrain = FindMainTerrain(scene, report);
        if (terrain == null)
        {
            report.AppendLine("TERRAIN NOT FOUND in " + ScenePath);
            if (openedByUs)
                EditorSceneManager.CloseScene(scene, true);
            return 1;
        }

        Undo.RegisterCompleteObjectUndo(terrain, "Arma grass detail distance");
        terrain.detailObjectDistance = DetailObjectDistance;
        terrain.drawInstanced = true;
        EditorUtility.SetDirty(terrain);

        // TerrainDistanceBoost overrides detailObjectDistance in Start(), so keep it in sync,
        // otherwise the shader fade (190/205 m) would not match the actual detail distance.
        TerrainDistanceBoost boost = terrain.GetComponent<TerrainDistanceBoost>();
        float boostDistance = -1f;
        if (boost != null)
        {
            Undo.RecordObject(boost, "Arma grass detail distance");
            boost.detailDistance = DetailObjectDistance;
            boostDistance = boost.detailDistance;
            EditorUtility.SetDirty(boost);
        }

        TerrainData data = terrain.terrainData;
        if (data != null)
        {
            DetailPrototype[] prototypes = data.detailPrototypes;
            bool instancingChanged = false;
            for (int i = 0; i < prototypes.Length; i++)
            {
                DetailPrototype prototype = prototypes[i];
                if (prototype == null)
                    continue;

                report.AppendLine("prototype[" + i + "]: " +
                                  (prototype.prototype != null ? prototype.prototype.name : "<none>") +
                                  " mesh=" + prototype.usePrototypeMesh +
                                  " instancing=" + prototype.useInstancing +
                                  " renderMode=" + prototype.renderMode);

                if (prototype.usePrototypeMesh && !prototype.useInstancing)
                {
                    prototype.useInstancing = true;
                    prototypes[i] = prototype;
                    instancingChanged = true;
                }
            }

            if (instancingChanged)
            {
                data.detailPrototypes = prototypes;
                data.RefreshPrototypes();
                report.AppendLine("detail instancing enabled on mesh prototypes");
            }
        }

        EditorSceneManager.MarkSceneDirty(scene);
        bool saved = EditorSceneManager.SaveScene(scene);

        report.AppendLine("terrain: " + terrain.gameObject.name +
                          " detailObjectDistance=" + terrain.detailObjectDistance.ToString("F0") +
                          " drawInstanced=" + terrain.drawInstanced +
                          " boost.detailDistance=" + boostDistance.ToString("F0") +
                          " sceneSaved=" + saved);

        if (openedByUs && saved)
            EditorSceneManager.CloseScene(scene, true);

        return saved ? 0 : 1;
    }

    static Terrain FindMainTerrain(Scene scene, StringBuilder report)
    {
        Terrain named = null;
        Terrain largest = null;
        float largestArea = -1f;

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Terrain[] terrains = root.GetComponentsInChildren<Terrain>(true);
            foreach (Terrain terrain in terrains)
            {
                float area = terrain.terrainData != null
                    ? terrain.terrainData.size.x * terrain.terrainData.size.z
                    : 0f;
                report.AppendLine("terrain candidate: " + terrain.gameObject.name +
                                  " area=" + area.ToString("F0"));

                if (terrain.gameObject.name == MainTerrainName)
                    named = terrain;
                if (area > largestArea)
                {
                    largestArea = area;
                    largest = terrain;
                }
            }
        }

        return named != null ? named : largest;
    }
}
