using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// The URP "Universal Render Pipeline/Terrain/Lit" shader ignores TerrainLayer.diffuseRemapMax,
// so the light strip under the 3D grass cannot be darkened through the layer remap. This tool
// bakes a darkened, warm olive copy of GrassUV02 and assigns it to the layers that own the
// strip, then syncs the 3D grass fade colour to the new texture.
//
// What it does:
//   * audits all 11 terrain layers (brightness, coverage, dominance, tree weight) and reports
//     which layer is the light strip and which darker layer surrounds it,
//   * writes Assets/Textures/Terrain/GrassUV02_DarkForest.png at ~0.57 of the source brightness
//     with a warm olive shift, and imports it with sRGB / aniso 8 / bilinear / mipmaps,
//   * assigns it to NewLayer 4 (and NewLayer 2 when it is equally bright), sets NewLayer 4 to
//     a 14x14 m tile so the pattern does not shimmer from 90 m,
//   * raises terrain.basemapDistance to 750 m and calls SetBaseMapDirty() + Flush(),
//   * sets _FadeColor of the six CustomGrass materials to the new texture's mean colour.
// Menu: Tools/DroneSimulator/Terrain/Darken Grass Layer Under 3D Grass
public static class DarkGrassTerrainFix
{
    const string ScenePath = "Assets/Scenes/FirstMission(Forest).unity";
    const string MainTerrainName = "Terrain";
    const string ReportPath = "Logs/DarkGrassTerrainReport.txt";
    const string UndoLabel = "Darken grass terrain layer";

    const string SourceTexturePath = "Assets/TerrainTexturesPackFree/TerrainTextures/GrassUV02.png";
    const string TargetTextureFolder = "Assets/Textures/Terrain";
    const string TargetTexturePath = "Assets/Textures/Terrain/GrassUV02_DarkForest.png";
    const string GrassMaterialFolder = "Assets/Materials/CustomGrass";

    // 0.575 brightness with a warm olive shift: R x1.05, B x0.86.
    // Measured against the 8192 px source this lands at ~0.578 of the original luminance.
    const float BrightnessScale = 0.575f;
    const float WarmRed = 1.05f;
    const float WarmGreen = 1.00f;
    const float WarmBlue = 0.86f;

    const int OutputSize = 2048;
    const float NewLayer4TileSize = 14f;
    const float TargetBasemapDistance = 750f;
    const float LightLumaThreshold = 0.31f;   // brighter than this reads as the pale strip

    const string LightLayerPrimary = "NewLayer 4";
    const string LightLayerSecondary = "NewLayer 2";

    sealed class LayerInfo
    {
        public int index;
        public string name;
        public Vector2 tile;
        public string diffuseName;
        public string normalName;
        public Color mean;
        public float luma;
        public float painted;
        public float dominant;
        public float treeWeight;
    }

    // ============================================================================
    //  Menu
    // ============================================================================
    [MenuItem("Tools/DroneSimulator/Terrain/Darken Grass Layer Under 3D Grass", priority = 21)]
    public static void Apply()
    {
        var report = new StringBuilder();
        report.AppendLine("DarkGrassTerrainFix started " + DateTime.Now.ToString("s"));
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
                errors += Run(terrain, report);
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
            Debug.Log("[DarkGrass] Done. See " + ReportPath);
        else
            Debug.LogError("[DarkGrass] Finished with " + errors + " error(s). See " + ReportPath);
    }


    // ============================================================================
    //  Main pass
    // ============================================================================
    static int Run(Terrain terrain, StringBuilder report)
    {
        TerrainData data = terrain.terrainData;
        TerrainLayer[] layers = data.terrainLayers;
        if (layers == null || layers.Length == 0)
        {
            report.AppendLine("TERRAIN HAS NO LAYERS");
            return 1;
        }

        int width = data.alphamapWidth;
        int height = data.alphamapHeight;
        float[,,] maps = data.GetAlphamaps(0, 0, width, height);

        report.AppendLine("terrain: " + terrain.gameObject.name +
                          " size=" + data.size +
                          " alphamap=" + width + "x" + height +
                          " layers=" + layers.Length +
                          " basemapDistance=" + terrain.basemapDistance.ToString("F1"));

        List<LayerInfo> infos = AuditLayers(layers, maps, width, height, data.treeInstances, report);

        int primary = FindLayer(layers, LightLayerPrimary);
        int secondary = FindLayer(layers, LightLayerSecondary);
        if (primary < 0)
        {
            report.AppendLine("'" + LightLayerPrimary + "' NOT FOUND - nothing to darken");
            return 1;
        }

        bool primaryBright = infos[primary].luma >= LightLumaThreshold;
        bool secondaryBright = secondary >= 0 && infos[secondary].luma >= LightLumaThreshold;
        report.AppendLine("light layer candidates: '" + LightLayerPrimary + "' luma=" +
                          infos[primary].luma.ToString("F3") + " bright=" + primaryBright +
                          (secondary >= 0
                              ? ", '" + LightLayerSecondary + "' luma=" +
                                infos[secondary].luma.ToString("F3") + " bright=" + secondaryBright
                              : ", '" + LightLayerSecondary + "' not present"));

        if (!primaryBright)
        {
            report.AppendLine("'" + LightLayerPrimary + "' already uses a dark texture - " +
                              "rebuilding it from the source anyway so the configured " +
                              "brightness/warm tint always wins");
        }
        else
        {
            report.AppendLine("'" + LightLayerPrimary + "' is bright - baking the dark texture");
        }

        FindSurroundingDarkLayer(infos, maps, width, height, primary, secondary, report);

        var source = AssetDatabase.LoadAssetAtPath<Texture2D>(SourceTexturePath);
        if (source == null)
        {
            report.AppendLine("SOURCE TEXTURE MISSING: " + SourceTexturePath);
            return 1;
        }

        Color mean;
        if (!CreateDarkTexture(source, out mean, report))
            return 1;

        ConfigureImporter();

        var darkTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(TargetTexturePath);
        if (darkTexture == null)
        {
            report.AppendLine("DARK TEXTURE MISSING AFTER IMPORT: " + TargetTexturePath);
            return 1;
        }

        Undo.RecordObject(layers[primary], UndoLabel);
        layers[primary].diffuseTexture = darkTexture;
        layers[primary].tileSize = new Vector2(NewLayer4TileSize, NewLayer4TileSize);
        EditorUtility.SetDirty(layers[primary]);
        report.AppendLine("layer '" + layers[primary].name + "': diffuse -> " +
                          TargetTexturePath + ", tile=" + NewLayer4TileSize.ToString("F1") + " m");

        bool secondaryUsesDark = false;
        if (secondary >= 0 && layers[secondary].diffuseTexture != null)
        {
            secondaryUsesDark = string.Equals(
                AssetDatabase.GetAssetPath(layers[secondary].diffuseTexture), TargetTexturePath,
                StringComparison.OrdinalIgnoreCase);
        }
        if (secondaryBright || secondaryUsesDark)
        {
            Undo.RecordObject(layers[secondary], UndoLabel);
            layers[secondary].diffuseTexture = darkTexture;
            EditorUtility.SetDirty(layers[secondary]);
            report.AppendLine("layer '" + layers[secondary].name + "': diffuse -> " +
                              TargetTexturePath + " (bright or already using the dark texture), " +
                              "tile kept at " + layers[secondary].tileSize.x.ToString("F1") + " m");
        }
        else if (secondary >= 0)
        {
            report.AppendLine("layer '" + layers[secondary].name + "': left unchanged (not bright)");
        }

        Undo.RecordObject(terrain, UndoLabel);
        float oldBasemap = terrain.basemapDistance;
        terrain.basemapDistance = TargetBasemapDistance;
        EditorUtility.SetDirty(terrain);

        Undo.RecordObject(data, UndoLabel);
        data.SetBaseMapDirty();
        terrain.Flush();
        report.AppendLine("basemapDistance: " + oldBasemap.ToString("F1") + " -> " +
                          terrain.basemapDistance.ToString("F1") +
                          " m (SetBaseMapDirty + Flush called)");

        SyncGrassFade(mean, report);

        EditorSceneManager.MarkSceneDirty(terrain.gameObject.scene);
        AssetDatabase.SaveAssets();
        return 0;
    }

    // ============================================================================
    //  Layer diagnostics
    // ============================================================================
    static List<LayerInfo> AuditLayers(TerrainLayer[] layers, float[,,] maps,
        int width, int height, TreeInstance[] trees, StringBuilder report)
    {
        int layerCount = layers.Length;
        var infos = new List<LayerInfo>(layerCount);
        for (int i = 0; i < layerCount; i++)
        {
            TerrainLayer layer = layers[i];
            var info = new LayerInfo
            {
                index = i,
                name = layer != null ? layer.name : "<null>",
                tile = layer != null ? layer.tileSize : Vector2.zero,
                diffuseName = layer != null && layer.diffuseTexture != null
                    ? layer.diffuseTexture.name
                    : "<none>",
                normalName = layer != null && layer.normalMapTexture != null
                    ? layer.normalMapTexture.name
                    : "<none>",
            };

            if (layer != null)
            {
                Color mean;
                if (ComputeTextureMean(layer.diffuseTexture as Texture2D, out mean))
                    info.mean = mean;
                info.luma = Luminance(info.mean);
            }
            infos.Add(info);
        }

        float texels = width * (float)height;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int best = -1;
                float bestWeight = 0f;
                for (int l = 0; l < layerCount; l++)
                {
                    float weight = maps[y, x, l];
                    if (weight <= 0f) continue;
                    infos[l].painted += weight;
                    if (weight > bestWeight)
                    {
                        bestWeight = weight;
                        best = l;
                    }
                }
                if (best >= 0 && bestWeight > 0.5f)
                    infos[best].dominant += 1f;
            }
        }

        for (int i = 0; i < layerCount; i++)
        {
            infos[i].painted /= texels;
            infos[i].dominant /= texels;
        }

        if (trees != null && trees.Length > 0)
        {
            for (int t = 0; t < trees.Length; t++)
            {
                int tx = Mathf.Clamp(Mathf.RoundToInt(trees[t].position.x * (width - 1)), 0, width - 1);
                int ty = Mathf.Clamp(Mathf.RoundToInt(trees[t].position.z * (height - 1)), 0, height - 1);
                for (int l = 0; l < layerCount; l++)
                    infos[l].treeWeight += maps[ty, tx, l];
            }
            for (int i = 0; i < layerCount; i++)
                infos[i].treeWeight /= trees.Length;
        }

        report.AppendLine("layer audit:");
        for (int i = 0; i < infos.Count; i++)
        {
            LayerInfo info = infos[i];
            report.AppendLine("  [" + info.index + "] '" + info.name + "'" +
                              " tile=" + info.tile.x.ToString("F1") + "m" +
                              " diffuse='" + info.diffuseName + "'" +
                              " normal='" + info.normalName + "'" +
                              " mean=(" + info.mean.r.ToString("F2") + "," +
                              info.mean.g.ToString("F2") + "," + info.mean.b.ToString("F2") + ")" +
                              " luma=" + info.luma.ToString("F3") +
                              " painted=" + (info.painted * 100f).ToString("F1") + "%" +
                              " dominant=" + (info.dominant * 100f).ToString("F1") + "%" +
                              " treeWeight=" + info.treeWeight.ToString("F2"));
        }

        return infos;
    }

    // Finds the darker textured layer that borders the light strip most (reported only).
    static int FindSurroundingDarkLayer(List<LayerInfo> infos, float[,,] maps,
        int width, int height, int light, int otherLight, StringBuilder report)
    {
        // Use a fixed reference so a re-run (when the light layer already carries the dark
        // texture) still reports the darker ground that borders the strip.
        float referenceLuma = Mathf.Max(infos[light].luma, LightLumaThreshold);
        int best = -1;
        double bestEdge = -1d;
        for (int i = 0; i < infos.Count; i++)
        {
            if (i == light || i == otherLight) continue;
            LayerInfo info = infos[i];
            if (info.painted < 0.005f) continue;                         // negligible area
            if (info.luma >= referenceLuma - 0.02f) continue;            // not darker
            string lower = info.name.ToLowerInvariant();
            if (lower.Contains("pebbles")) continue;                     // road, not the surrounding ground

            double edge = 0d;
            for (int y = 0; y < height; y += 2)
            {
                for (int x = 0; x < width; x += 2)
                {
                    float lw = maps[y, x, light];
                    if (lw < 0.20f || lw > 0.80f) continue;
                    edge += maps[y, x, i];
                }
            }

            if (edge > bestEdge)
            {
                bestEdge = edge;
                best = i;
            }
        }

        if (best < 0)
        {
            report.AppendLine("surrounding dark layer: none found (all neighbours are as bright)");
            return -1;
        }

        report.AppendLine("surrounding dark layer: layer[" + best + "] '" + infos[best].name +
                          "' luma=" + infos[best].luma.ToString("F3") +
                          " (edge weight " + bestEdge.ToString("F1") + ")");
        return best;
    }

    static int FindLayer(TerrainLayer[] layers, string layerName)
    {
        for (int i = 0; i < layers.Length; i++)
        {
            if (layers[i] != null &&
                string.Equals(layers[i].name, layerName, StringComparison.OrdinalIgnoreCase))
                return i;
        }
        return -1;
    }


    // ============================================================================
    //  Dark texture generation
    // ============================================================================
    static bool CreateDarkTexture(Texture2D source, out Color mean, StringBuilder report)
    {
        mean = Color.black;
        RenderTexture rt = null;
        Texture2D readable = null;
        RenderTexture previous = RenderTexture.active;
        try
        {
            if (!AssetDatabase.IsValidFolder(TargetTextureFolder))
            {
                string parent = Path.GetDirectoryName(TargetTextureFolder).Replace('\\', '/');
                string name = Path.GetFileName(TargetTextureFolder);
                AssetDatabase.CreateFolder(parent, name);
            }

            rt = RenderTexture.GetTemporary(OutputSize, OutputSize, 0,
                RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            Graphics.Blit(source, rt);
            RenderTexture.active = rt;

            readable = new Texture2D(OutputSize, OutputSize, TextureFormat.RGBA32, false, false);
            readable.ReadPixels(new Rect(0, 0, OutputSize, OutputSize), 0, 0);
            readable.Apply();

            Color32[] pixels = readable.GetPixels32();
            double sumR = 0d, sumG = 0d, sumB = 0d;
            double sourceR = 0d, sourceG = 0d, sourceB = 0d;
            for (int i = 0; i < pixels.Length; i++)
            {
                sourceR += pixels[i].r;
                sourceG += pixels[i].g;
                sourceB += pixels[i].b;

                byte r = ScaleChannel(pixels[i].r, WarmRed);
                byte g = ScaleChannel(pixels[i].g, WarmGreen);
                byte b = ScaleChannel(pixels[i].b, WarmBlue);
                pixels[i] = new Color32(r, g, b, pixels[i].a);
                sumR += r;
                sumG += g;
                sumB += b;
            }

            readable.SetPixels32(pixels);
            readable.Apply();

            byte[] png = readable.EncodeToPNG();
            if (png == null || png.Length == 0)
            {
                report.AppendLine("EncodeToPNG returned no data");
                return false;
            }

            File.WriteAllBytes(Path.Combine(Directory.GetCurrentDirectory(), TargetTexturePath), png);

            int count = pixels.Length;
            Color sourceMean = new Color((float)(sourceR / count / 255d),
                                         (float)(sourceG / count / 255d),
                                         (float)(sourceB / count / 255d), 1f);
            mean = new Color((float)(sumR / count / 255d),
                             (float)(sumG / count / 255d),
                             (float)(sumB / count / 255d), 1f);

            float sourceLuma = Luminance(sourceMean);
            float outputLuma = Luminance(mean);

            report.AppendLine("dark texture written: " + TargetTexturePath +
                              " (" + OutputSize + "x" + OutputSize + ", " + png.Length + " bytes)");
            report.AppendLine("  brightness scale=" + BrightnessScale.ToString("F2") +
                              " (R x" + WarmRed.ToString("F2") + ", G x" + WarmGreen.ToString("F2") +
                              ", B x" + WarmBlue.ToString("F2") + ")");
            report.AppendLine("  source mean=(" + sourceMean.r.ToString("F3") + "," +
                              sourceMean.g.ToString("F3") + "," + sourceMean.b.ToString("F3") +
                              ") luma=" + sourceLuma.ToString("F3"));
            report.AppendLine("  output mean=(" + mean.r.ToString("F3") + "," +
                              mean.g.ToString("F3") + "," + mean.b.ToString("F3") +
                              ") luma=" + outputLuma.ToString("F3") +
                              " ratio=" + (outputLuma / Mathf.Max(sourceLuma, 0.0001f)).ToString("F3") +
                              " (target 0.55-0.60)");

            AssetDatabase.ImportAsset(TargetTexturePath, ImportAssetOptions.ForceUpdate);
            return true;
        }
        catch (Exception exception)
        {
            report.AppendLine("dark texture generation failed: " + exception);
            return false;
        }
        finally
        {
            RenderTexture.active = previous;
            if (rt != null) RenderTexture.ReleaseTemporary(rt);
            if (readable != null) UnityEngine.Object.DestroyImmediate(readable);
        }
    }

    static byte ScaleChannel(byte value, float warm)
    {
        float scaled = value * BrightnessScale * warm;
        return (byte)Mathf.Clamp(Mathf.RoundToInt(scaled), 0, 255);
    }

    static void ConfigureImporter()
    {
        var importer = AssetImporter.GetAtPath(TargetTexturePath) as TextureImporter;
        if (importer == null)
        {
            Debug.LogError("[DarkGrass] TextureImporter missing for " + TargetTexturePath);
            return;
        }

        importer.textureType = TextureImporterType.Default;
        importer.sRGBTexture = true;
        importer.anisoLevel = 8;
        importer.filterMode = FilterMode.Bilinear;
        importer.mipmapEnabled = true;
        importer.alphaIsTransparency = false;
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.maxTextureSize = OutputSize;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        importer.SaveAndReimport();
    }


    // ============================================================================
    //  Grass fade colour sync
    // ============================================================================
    static void SyncGrassFade(Color mean, StringBuilder report)
    {
        Color fade = new Color(mean.r, mean.g, mean.b, 1f);
        string[] guids = AssetDatabase.FindAssets("t:Material", new[] { GrassMaterialFolder });
        int updated = 0;
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null || !material.HasProperty("_FadeColor"))
                continue;

            Undo.RecordObject(material, UndoLabel);
            Color before = material.GetColor("_FadeColor");
            material.SetColor("_FadeColor", fade);
            EditorUtility.SetDirty(material);
            updated++;
            report.AppendLine("material '" + material.name + "': _FadeColor (" +
                              before.r.ToString("F3") + "," + before.g.ToString("F3") + "," +
                              before.b.ToString("F3") + ") -> (" + fade.r.ToString("F3") + "," +
                              fade.g.ToString("F3") + "," + fade.b.ToString("F3") + ")" +
                              " luma=" + Luminance(fade).ToString("F3"));
        }

        AssetDatabase.SaveAssets();
        report.AppendLine("_FadeColor synced on " + updated + " grass material(s)");
    }


    // ============================================================================
    //  Helpers
    // ============================================================================
    static bool ComputeTextureMean(Texture2D texture, out Color mean)
    {
        mean = Color.black;
        if (texture == null) return false;

        const int size = 128;
        RenderTexture rt = RenderTexture.GetTemporary(size, size, 0,
            RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
        Texture2D readable = null;
        RenderTexture previous = RenderTexture.active;
        try
        {
            Graphics.Blit(texture, rt);
            RenderTexture.active = rt;
            readable = new Texture2D(size, size, TextureFormat.RGBA32, false, true);
            readable.ReadPixels(new Rect(0, 0, size, size), 0, 0);
            readable.Apply();

            Color[] pixels = readable.GetPixels();
            double r = 0d, g = 0d, b = 0d;
            for (int i = 0; i < pixels.Length; i++)
            {
                r += pixels[i].r;
                g += pixels[i].g;
                b += pixels[i].b;
            }
            mean = new Color((float)(r / pixels.Length), (float)(g / pixels.Length),
                (float)(b / pixels.Length), 1f);
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogWarning("[DarkGrass] texture stats failed for " +
                             (texture != null ? texture.name : "<null>") + ": " + exception.Message);
            return false;
        }
        finally
        {
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);
            if (readable != null) UnityEngine.Object.DestroyImmediate(readable);
        }
    }

    static float Luminance(Color color)
    {
        return color.r * 0.2126f + color.g * 0.7152f + color.b * 0.0722f;
    }

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
            Debug.LogWarning("[DarkGrass] could not write the report: " + exception.Message);
        }
    }
}
