using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Stopwatch = System.Diagnostics.Stopwatch;

// Builds the far horizon for FirstMission(Forest):
//   * replaces the five low-res BackgroundTerrain objects with a light ring mesh
//     (main terrain edge -> 10 km circle) that inherits the terrain edge heights and
//     grows 40-130 m multi-layer ridges further out;
//   * paints the ring with a 4K procedural farmland / forest-belt map plus a tileable
//     micro-noise detail map (matte URP shader, world-space sampling, no UV seams);
//   * adds one combined forest-silhouette card mesh (single draw call) on the ridges;
//   * raises every drone camera far clip plane to 15 km and saves the scene.
// Menu: Tools/DroneSimulator/Environment/Build Horizon Environment
public static class HorizonEnvironmentBuilder
{
    const string ScenePath = "Assets/Scenes/FirstMission(Forest).unity";
    const string GeneratedFolder = "Assets/Horizon/Generated";
    const string MaterialFolder = "Assets/Materials/Horizon";
    const string ReportPath = "Logs/HorizonEnvironmentReport.txt";

    const string GroundShaderName = "DroneSimulator/HorizonFields";
    const string TreeShaderName = "DroneSimulator/HorizonTreeSilhouette";

    const string RootName = "Horizon_Environment";
    const string GroundObjectName = "Horizon_Ground";
    const string ForestObjectName = "Horizon_Forest_Silhouette";

    const string MacroTexturePath = GeneratedFolder + "/Horizon_Fields_4096.png";
    const string DetailTexturePath = GeneratedFolder + "/Horizon_Detail_1024.png";
    const string ForestTexturePath = GeneratedFolder + "/Horizon_ForestStrips_2048.png";
    const string GroundMeshPath = GeneratedFolder + "/Horizon_Ground_Mesh.asset";
    const string ForestMeshPath = GeneratedFolder + "/Horizon_ForestSilhouette_Mesh.asset";
    const string GroundMaterialPath = MaterialFolder + "/Horizon_Fields_URP.mat";
    const string ForestMaterialPath = MaterialFolder + "/Horizon_ForestSilhouette.mat";

    const int Seed = 20261122;
    const float OuterRadius = 10000f;
    const int PerimeterSamples = 2048;
    const int MacroTextureSize = 4096;
    const int DetailTextureSize = 1024;
    const int ForestAtlasSize = 2048;
    const int MaxForestCards = 60000;
    const float MinCameraFarClip = 15000f;

    static readonly string[] ForestSourceGuids =
    {
        "85a29d61543e03447acfbd4eb59ff7cd", // Conifer_Billboard_Color.png
        "06497a1c0614b104fbe1e29ff947a759", // Pine_B_Billboard_Color.png
        "9db7c8b514cd7234e97656d7a26c7b97"  // Cypress_Forest_Desktop_Billboard_Color.tif
    };

    static readonly string[] BackgroundTerrainNames =
    {
        "BackgroundTerrain", "BackgroundTerrain1", "BackgrounTerrain2",
        "BackgroundTerrain3", "BackgroundTerrain4"
    };

    [MenuItem("Tools/DroneSimulator/Environment/Build Horizon Environment", priority = 40)]
    public static void BuildHorizonEnvironment()
    {
        if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("[HorizonEnvironmentBuilder] Exit Play Mode before building the horizon: " +
                           "scene objects are saved by the builder and cannot be marked dirty in Play Mode.");
            return;
        }

        StringBuilder report = new StringBuilder();
        Stopwatch timer = Stopwatch.StartNew();
        report.AppendLine("HorizonEnvironmentBuilder started " + DateTime.Now.ToString("s"));

        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        if (!scene.IsValid() || !scene.isLoaded)
        {
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            report.AppendLine("scene opened: " + ScenePath);
        }

        try
        {
            Build(scene, report);
        }
        catch (Exception exception)
        {
            report.AppendLine("ERROR: " + exception);
            Debug.LogException(exception);
        }
        finally
        {
            timer.Stop();
            report.AppendLine("finished in " + timer.Elapsed.TotalSeconds.ToString("F1") + " s");
            WriteReport(report);
        }
    }

    static void Build(Scene scene, StringBuilder report)
    {
        Terrain terrain = FindMainTerrain();
        if (terrain == null)
        {
            report.AppendLine("MAIN TERRAIN NOT FOUND - aborting");
            return;
        }

        Shader groundShader = Shader.Find(GroundShaderName);
        Shader treeShader = Shader.Find(TreeShaderName);
        if (groundShader == null || treeShader == null)
        {
            report.AppendLine("SHADER NOT FOUND (ground=" + (groundShader != null) +
                              ", tree=" + (treeShader != null) + ") - check Assets/Shaders");
            return;
        }

        EnsureFolder(GeneratedFolder);
        EnsureFolder(MaterialFolder);

        Vector3 terrainSize = terrain.terrainData.size;
        float innerHalf = Mathf.Min(terrainSize.x, terrainSize.z) * 0.5f - 4f;
        if (Mathf.Abs(terrainSize.x - terrainSize.z) > 1f)
        {
            report.AppendLine("WARNING: main terrain is not square (" + terrainSize.x + " x " + terrainSize.z +
                              "); the ring uses the smaller half extent");
        }

        HorizonHeightField field = new HorizonHeightField(terrain, Seed);
        report.AppendLine("main terrain '" + terrain.name + "' position=" + terrain.transform.position +
                          " size=" + terrainSize + " center=" + field.Center);

        // Real outer-layer colour of the main Terrain, sampled around its border ring.
        // Stored in the ring mesh vertex colours so the seam continues exactly the
        // soil/grass the player sees at the Terrain edge.
        report.AppendLine("[Terrain border colours]");
        Color[] borderColors = HorizonTerrainBorderColor.SampleBorderColors(
            terrain, field.Center, innerHalf, 512, report);
        Color hazeColor = SampleSkyHorizonColor(report);

        report.AppendLine("[Textures] macro fields " + MacroTextureSize + "x" + MacroTextureSize);
        Color32[] macroPixels = HorizonFieldMapGenerator.CreateFieldMap(
            MacroTextureSize, field.Center, OuterRadius * 2f, Seed);
        HorizonTextureGenerator.WriteTextureAsset(MacroTexturePath, macroPixels, MacroTextureSize, MacroTextureSize);
        HorizonTextureGenerator.ConfigureTexture(MacroTexturePath, true, MacroTextureSize,
            TextureWrapMode.Clamp, 4, false, 0.5f);
        macroPixels = null;

        report.AppendLine("[Textures] detail noise " + DetailTextureSize + "x" + DetailTextureSize);
        Color32[] detailPixels = HorizonFieldMapGenerator.CreateDetailMap(DetailTextureSize, Seed);
        HorizonTextureGenerator.WriteTextureAsset(DetailTexturePath, detailPixels, DetailTextureSize, DetailTextureSize);
        HorizonTextureGenerator.ConfigureTexture(DetailTexturePath, false, DetailTextureSize,
            TextureWrapMode.Repeat, 4, false, 0.5f);
        detailPixels = null;

        report.AppendLine("[Textures] forest strip atlas " + ForestAtlasSize + "x" + ForestAtlasSize);
        Color32[] atlasPixels = HorizonTextureGenerator.BuildForestAtlas(
            ForestAtlasSize, ForestSourceGuids, Seed, report);
        HorizonTextureGenerator.WriteTextureAsset(ForestTexturePath, atlasPixels, ForestAtlasSize, ForestAtlasSize);
        HorizonTextureGenerator.ConfigureTexture(ForestTexturePath, true, ForestAtlasSize,
            TextureWrapMode.Clamp, 2, true, 0.5f);
        atlasPixels = null;

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        Texture2D macroTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(MacroTexturePath);
        Texture2D detailTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(DetailTexturePath);
        Texture2D forestTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(ForestTexturePath);
        report.AppendLine("  loaded: macro=" + (macroTexture != null) + " detail=" + (detailTexture != null) +
                          " forest=" + (forestTexture != null));

        report.AppendLine("[Materials]");
        Material groundMaterial = GetOrCreateMaterial(GroundMaterialPath, groundShader);
        groundMaterial.SetTexture("_MacroMap", macroTexture);
        groundMaterial.SetTexture("_DetailMap", detailTexture);
        groundMaterial.SetColor("_BaseColor", Color.white);
        groundMaterial.SetVector("_MacroCenter", new Vector4(field.Center.x, 0f, field.Center.y, 0f));
        groundMaterial.SetFloat("_MacroSpan", OuterRadius * 2f);
        groundMaterial.SetFloat("_DetailTilingA", 96f);
        groundMaterial.SetFloat("_DetailTilingB", 13f);
        groundMaterial.SetFloat("_DetailTilingC", 3.1f);
        groundMaterial.SetFloat("_DetailStrengthA", 0.30f);
        groundMaterial.SetFloat("_DetailStrengthB", 0.16f);
        groundMaterial.SetFloat("_DetailStrengthC", 0.10f);
        groundMaterial.SetFloat("_SlopeDarken", 0.40f);
        Color hazeLinear = hazeColor.linear;
        hazeLinear = new Color(hazeLinear.r * 0.94f, hazeLinear.g * 0.94f, hazeLinear.b * 0.94f, 1f);

        groundMaterial.SetColor("_NearColor", Color.white);
        groundMaterial.SetFloat("_NearBlendStart", 0f);
        groundMaterial.SetFloat("_NearBlendEnd", 220f);
        groundMaterial.SetColor("_HazeColor", hazeLinear);
        groundMaterial.SetFloat("_HazeStart", 1800f);
        groundMaterial.SetFloat("_HazeEnd", 9000f);
        groundMaterial.SetFloat("_HazeStrength", 0.70f);
        EditorUtility.SetDirty(groundMaterial);

        Material forestMaterial = GetOrCreateMaterial(ForestMaterialPath, treeShader);
        forestMaterial.SetTexture("_BaseMap", forestTexture);
        forestMaterial.SetColor("_BaseColor", Color.white);
        forestMaterial.SetFloat("_AlphaCutoff", 0.32f);
        forestMaterial.SetFloat("_RootDarken", 0.55f);
        forestMaterial.SetColor("_HazeColor", hazeLinear);
        forestMaterial.SetFloat("_HazeStart", 1800f);
        forestMaterial.SetFloat("_HazeEnd", 9000f);
        forestMaterial.SetFloat("_HazeStrength", 0.70f);
        EditorUtility.SetDirty(forestMaterial);
        report.AppendLine("  " + GroundMaterialPath);
        report.AppendLine("  " + ForestMaterialPath);

        report.AppendLine("[Meshes]");
        HorizonMeshGenerator.Stats groundStats;
        Mesh groundMesh = HorizonMeshGenerator.BuildGroundMesh(
            field, innerHalf, OuterRadius, PerimeterSamples, borderColors, out groundStats);
        groundMesh = SaveMeshAsset(groundMesh, GroundMeshPath);

        HorizonMeshGenerator.Stats forestStats;
        Mesh forestMesh = HorizonMeshGenerator.BuildForestMesh(
            field, terrain, Seed, MaxForestCards, out forestStats);
        forestMesh = SaveMeshAsset(forestMesh, ForestMeshPath);

        report.AppendLine("  ground: vertices=" + groundStats.Vertices + " triangles=" + groundStats.Triangles +
                          " height=[" + groundStats.MinHeight.ToString("F1") + ".." +
                          groundStats.MaxHeight.ToString("F1") + "]");
        report.AppendLine("  forest: cards=" + forestStats.Cards + " vertices=" + forestStats.Vertices +
                          " triangles=" + forestStats.Triangles + " height=[" +
                          forestStats.MinHeight.ToString("F1") + ".." +
                          forestStats.MaxHeight.ToString("F1") + "]");

        report.AppendLine("[Scene]");
        GameObject root = FindSceneObject(scene, RootName);
        if (root == null)
        {
            root = new GameObject(RootName);
        }
        root.transform.position = new Vector3(field.Center.x, 0f, field.Center.y);
        root.transform.rotation = Quaternion.identity;
        root.transform.localScale = Vector3.one;
        SetStatic(root);

        GameObject groundObject = EnsureChild(root.transform, GroundObjectName);
        ApplyRenderer(groundObject, groundMesh, groundMaterial);

        GameObject forestObject = EnsureChild(root.transform, ForestObjectName);
        ApplyRenderer(forestObject, forestMesh, forestMaterial);

        report.AppendLine("  " + RootName + "/" + GroundObjectName + " -> " + groundMaterial.name);
        report.AppendLine("  " + RootName + "/" + ForestObjectName + " -> " + forestMaterial.name);

        report.AppendLine("[Background terrains disabled]");
        foreach (string terrainName in BackgroundTerrainNames)
        {
            GameObject background = FindSceneObject(scene, terrainName);
            if (background == null)
            {
                report.AppendLine("  " + terrainName + ": not found");
                continue;
            }

            bool wasActive = background.activeSelf;
            background.SetActive(false);
            report.AppendLine("  " + terrainName + ": active " + wasActive + " -> false (asset kept for rollback)");
        }

        report.AppendLine("[Camera far clip planes]");
        int camerasUpdated = 0;
        Camera[] cameras = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include);
        foreach (Camera camera in cameras)
        {
            float previous = camera.farClipPlane;
            if (previous < MinCameraFarClip)
            {
                camera.farClipPlane = MinCameraFarClip;
                camerasUpdated++;
            }
            report.AppendLine("  scene camera '" + camera.name + "' " + previous + " -> " + camera.farClipPlane);
        }
        report.AppendLine("  updated " + camerasUpdated + " scene camera(s)");

        int prefabsUpdated = 0;
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/Drones" });
        foreach (string guid in prefabGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject contents = PrefabUtility.LoadPrefabContents(path);
            bool changed = false;
            foreach (Camera camera in contents.GetComponentsInChildren<Camera>(true))
            {
                if (camera.farClipPlane < MinCameraFarClip)
                {
                    camera.farClipPlane = MinCameraFarClip;
                    changed = true;
                }
            }

            if (changed)
            {
                PrefabUtility.SaveAsPrefabAsset(contents, path);
                prefabsUpdated++;
                report.AppendLine("  prefab updated: " + path);
            }
            PrefabUtility.UnloadPrefabContents(contents);
        }
        report.AppendLine("  updated " + prefabsUpdated + " drone prefab(s)");

        EditorSceneManager.MarkSceneDirty(scene);
        bool saved = EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        report.AppendLine("[Save] scene saved=" + saved + ", assets saved");
        SceneView.RepaintAll();
    }

    static Terrain FindMainTerrain()
    {
        Terrain[] terrains = UnityEngine.Object.FindObjectsByType<Terrain>(FindObjectsInactive.Exclude);
        Terrain fallback = null;
        foreach (Terrain terrain in terrains)
        {
            if (terrain.name == "Terrain")
            {
                return terrain;
            }
            if (fallback == null ||
                terrain.terrainData.heightmapResolution > fallback.terrainData.heightmapResolution)
            {
                fallback = terrain;
            }
        }
        return fallback;
    }

    static GameObject FindSceneObject(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == name)
            {
                return root;
            }
        }
        return null;
    }

    static GameObject EnsureChild(Transform parent, string name)
    {
        Transform existing = parent.Find(name);
        if (existing != null)
        {
            return existing.gameObject;
        }
        GameObject child = new GameObject(name);
        child.transform.SetParent(parent, false);
        return child;
    }

    static void SetStatic(GameObject gameObject)
    {
        GameObjectUtility.SetStaticEditorFlags(gameObject, StaticEditorFlags.BatchingStatic);
    }

    static void ApplyRenderer(GameObject gameObject, Mesh mesh, Material material)
    {
        MeshFilter filter = gameObject.GetComponent<MeshFilter>();
        if (filter == null)
        {
            filter = gameObject.AddComponent<MeshFilter>();
        }
        filter.sharedMesh = mesh;

        MeshRenderer renderer = gameObject.GetComponent<MeshRenderer>();
        if (renderer == null)
        {
            renderer = gameObject.AddComponent<MeshRenderer>();
        }
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.Off;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        renderer.allowOcclusionWhenDynamic = false;
        SetStatic(gameObject);
    }

    static Material GetOrCreateMaterial(string path, Shader shader)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(shader);
            material.name = Path.GetFileNameWithoutExtension(path);
            AssetDatabase.CreateAsset(material, path);
        }
        else if (material.shader != shader)
        {
            material.shader = shader;
        }
        material.enableInstancing = false;
        return material;
    }

    // Reuses the existing mesh asset when possible so its GUID (and therefore the
    // scene references) stays stable between rebuilds. Returns the mesh that actually
    // lives in the asset - the caller must assign that one to the MeshFilter.
    static Mesh SaveMeshAsset(Mesh mesh, string path)
    {
        Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing == null)
        {
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        existing.Clear();
        existing.indexFormat = mesh.indexFormat;
        existing.vertices = mesh.vertices;
        existing.normals = mesh.normals;
        existing.uv = mesh.uv;
        Color[] colors = mesh.colors;
        if (colors != null && colors.Length > 0)
        {
            existing.colors = colors;
        }
        existing.triangles = mesh.triangles;
        existing.RecalculateBounds();
        existing.name = mesh.name;
        EditorUtility.SetDirty(existing);

        UnityEngine.Object.DestroyImmediate(mesh);
        return existing;
    }

    static void EnsureFolder(string path)
    {
        string[] parts = path.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[i]);
            }
            current = next;
        }
    }

    // Renders the CloudySky skybox without any scene geometry and averages the rows
    // just above the horizon. That exact grey is used as the shader-level aerial
    // perspective colour, so the far ridges melt into the sky instead of turning into
    // a black line (global RenderSettings fog stays disabled).
    static Color SampleSkyHorizonColor(StringBuilder report)
    {
        GameObject probeObject = new GameObject("TMP_HorizonSkyProbe");
        Camera camera = probeObject.AddComponent<Camera>();
        camera.cullingMask = 0;
        camera.clearFlags = CameraClearFlags.Skybox;
        camera.fieldOfView = 60f;
        camera.nearClipPlane = 0.1f;
        camera.farClipPlane = 1000f;
        camera.allowMSAA = false;
        camera.allowHDR = false;
        probeObject.transform.position = new Vector3(0f, 500f, 0f);
        probeObject.transform.rotation = Quaternion.identity;

        RenderTexture target = RenderTexture.GetTemporary(
            64, 64, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        camera.targetTexture = target;
        camera.Render();

        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = target;
        Texture2D readable = new Texture2D(64, 64, TextureFormat.RGBA32, false, false);
        readable.ReadPixels(new Rect(0, 0, 64, 64), 0, 0);
        readable.Apply(false, false);
        RenderTexture.active = previous;

        Color32[] pixels = readable.GetPixels32();
        double r = 0, g = 0, b = 0;
        int count = 0;
        for (int y = 33; y <= 42; y++)
        {
            for (int x = 0; x < 64; x++)
            {
                Color32 pixel = pixels[y * 64 + x];
                r += pixel.r;
                g += pixel.g;
                b += pixel.b;
                count++;
            }
        }

        camera.targetTexture = null;
        UnityEngine.Object.DestroyImmediate(readable);
        RenderTexture.ReleaseTemporary(target);
        UnityEngine.Object.DestroyImmediate(probeObject);

        Color color = new Color(
            (float)(r / Mathf.Max(1, count)) / 255f,
            (float)(g / Mathf.Max(1, count)) / 255f,
            (float)(b / Mathf.Max(1, count)) / 255f,
            1f);
        report.AppendLine("  sky horizon colour sRGB=(" + Mathf.RoundToInt(color.r * 255f) + "," +
                          Mathf.RoundToInt(color.g * 255f) + "," +
                          Mathf.RoundToInt(color.b * 255f) + ")");
        return color;
    }

    static void WriteReport(StringBuilder report)
    {
        string path = Path.Combine(Directory.GetCurrentDirectory(), ReportPath);
        string directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
        File.WriteAllText(path, report.ToString());
        Debug.Log(report.ToString());
    }
}
