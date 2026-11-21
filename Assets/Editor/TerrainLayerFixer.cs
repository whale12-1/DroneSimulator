using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Fixes the flat dark-green terrain patch under the forest belt and blends its hard edge into
// the neighbouring textured grass/ground layers.
//
// What it does:
//   * audits every TerrainLayer of the main "Terrain" (tile size, textures, smoothness,
//     metallic, colour statistics, painted/tree coverage) and picks the layer that causes
//     the solid dark fill,
//   * repairs that layer (6-12 m tile size, constant smoothness 0, metallic 0, a proper
//     grass/forest-floor diffuse + normal map copied from the nearest textured neighbour),
//   * raises terrain.basemapDistance to 375 m so the ground stops collapsing into the
//     blurred basemap right next to the drone,
//   * blends the alphamap edge with a 6-10 m box blur plus a light Perlin fringe while
//     keeping the road (Pebbles_A_TerrainLayer) and the arable fields untouched,
//   * syncs _FadeColor of the six CustomGrass materials with the repaired surface and
//     lowers _GroundFalloff to 0.30 so shadowed grass no longer goes black.
// Menu: Tools/DroneSimulator/Terrain/Fix Flat Grass Layer And Blend Edges
public static class TerrainLayerFixer
{
    const string ScenePath = "Assets/Scenes/FirstMission(Forest).unity";
    const string MainTerrainName = "Terrain";
    const string ReportPath = "Logs/TerrainLayerFixReport.txt";
    const string UndoLabel = "Fix flat grass layer and blend edges";

    const string RoadLayerName = "Pebbles_A_TerrainLayer";
    const string FarmLayerName = "NewLayer 3";
    const string GrassLayerFallbackPath =
        "Assets/TerrainSampleAssets/TerrainLayers/Grass_Soil_TerrainLayer.terrainlayer";
    const string GrassMaterialFolder = "Assets/Materials/CustomGrass";

    // ---- layer repair ---------------------------------------------------------
    const float TargetTileSize = 8f;              // 6-12 m per request
    const float TargetBasemapDistance = 375f;     // 350-400 m per request
    const float FlatLocalDiffThreshold = 0.018f;  // 128 px neighbour difference of a normal grass map
    const float FlatSuspectThreshold = 0.014f;    // flatter than typical grass => the solid patch
    const float SmoothnessThreshold = 0.20f;

    // ---- alphamap blending ----------------------------------------------------
    const float BlurWidthMeters = 8f;             // 6-10 m per request
    const float BlendStrength = 0.90f;
    const float EdgeNoiseAmplitude = 0.18f;       // light Perlin fringe on the border only
    const float RoadProtectAlpha = 0.06f;         // never smooth across the painted road
    const float FarmProtectAlpha = 0.15f;         // hard field limit (same value as the tree tools)
    const float FarmEdgeMaxTarget = 0.075f;       // max spill inside the natural 2-4 m field fringe

    // ---- grass material sync --------------------------------------------------
    const float GroundFalloff = 0.30f;            // 0.28-0.32 per request
    const float FadeLuminanceFloor = 0.24f;
    const float FadeLuminanceCeil = 0.38f;
    const float FadeBrightnessBoost = 1.10f;

    sealed class LayerAudit
    {
        public int index;
        public string name;
        public Vector2 tile;
        public string diffuseName;
        public string diffusePath;
        public string normalName;
        public string normalPath;
        public float smoothness;
        public float metallic;
        public Vector4 remapMax;
        public Color mean;
        public float contrast;
        public float localDiff;
        public float paintedShare;
        public float treeShare;
        public float solidShare;
        public float dominantShare;
        public float hardEdgeShare;
        public float treeWeightMean;
        public float score;
        public float luma;
        public bool hasNormal;
    }

    // ============================================================================
    //  Menu
    // ============================================================================
    [MenuItem("Tools/DroneSimulator/Terrain/Fix Flat Grass Layer And Blend Edges", priority = 20)]
    public static void FixFlatGrassLayerAndBlendEdges()
    {
        var report = new StringBuilder();
        report.AppendLine("TerrainLayerFixer started " + DateTime.Now.ToString("s"));
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

            if (!scene.IsValid())
            {
                report.AppendLine("SCENE NOT AVAILABLE: " + ScenePath);
                errors++;
            }
            else
            {
                Terrain terrain = FindMainTerrain(scene, report);
                if (terrain == null)
                {
                    report.AppendLine("TERRAIN NOT FOUND in " + ScenePath);
                    errors++;
                }
                else
                {
                    errors += FixTerrain(terrain, report);
                    if (!EditorSceneManager.SaveScene(scene))
                    {
                        report.AppendLine("SCENE SAVE FAILED");
                        errors++;
                    }
                }

                if (openedByUs)
                    EditorSceneManager.CloseScene(scene, true);
            }
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
            Debug.Log("[TerrainLayerFixer] Done. See " + ReportPath);
        else
            Debug.LogError("[TerrainLayerFixer] Finished with " + errors + " error(s). See " + ReportPath);
    }


    // ============================================================================
    //  Terrain fix
    // ============================================================================
    static int FixTerrain(Terrain terrain, StringBuilder report)
    {
        TerrainData data = terrain.terrainData;
        if (data == null)
        {
            report.AppendLine("TERRAIN DATA MISSING on " + terrain.gameObject.name);
            return 1;
        }

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
                          " basemapDistance=" + terrain.basemapDistance.ToString("F1") +
                          " heightmapPixelError=" + terrain.heightmapPixelError.ToString("F1"));

        int roadIndex = FindLayerIndex(layers, RoadLayerName);
        int farmIndex = FindLayerIndex(layers, FarmLayerName);
        report.AppendLine("protected layers: road='" + RoadLayerName + "' index=" + roadIndex +
                          ", farm='" + FarmLayerName + "' index=" + farmIndex);

        List<LayerAudit> audits = AuditLayers(data, layers, maps, width, height, report);
        int suspect = PickSuspect(audits, roadIndex, farmIndex, report);
        if (suspect < 0)
        {
            report.AppendLine("no layer matched the flat dark-green signature; " +
                              "the alphamaps are left untouched");
            ApplyBasemapDistance(terrain, report);
            return 0;
        }

        LayerAudit suspectAudit = audits[suspect];
        int neighbour = PickBlendNeighbour(audits, maps, width, height, suspect, roadIndex, farmIndex, report);
        LayerAudit source = neighbour >= 0 ? audits[neighbour] : null;
        if (source != null && !source.hasNormal)
            source = null;

        if (source == null)
        {
            TerrainLayer fallback = AssetDatabase.LoadAssetAtPath<TerrainLayer>(GrassLayerFallbackPath);
            if (fallback != null)
            {
                source = AuditSingleLayer(fallback, -1);
                report.AppendLine("neighbour grass layer unusable, falling back to " + GrassLayerFallbackPath);
            }
        }

        bool textureReplaced = RepairLayer(layers[suspect], suspectAudit, source, report);

        // Re-read the repaired layer so the fade colour matches what the GPU sees.
        LayerAudit repaired = AuditSingleLayer(layers[suspect], suspect);
        if (textureReplaced && source != null)
        {
            repaired.mean = source.mean;
            repaired.remapMax = Vector4.one;
        }

        int blendErrors = BlendAlphamaps(data, terrain, maps, layers, suspect, roadIndex, farmIndex,
            neighbour, report);

        ApplyBasemapDistance(terrain, report);
        SyncGrassMaterials(repaired, report);
        EditorSceneManager.MarkSceneDirty(terrain.gameObject.scene);
        return blendErrors;
    }

    static void ApplyBasemapDistance(Terrain terrain, StringBuilder report)
    {
        Undo.RecordObject(terrain, UndoLabel);
        float before = terrain.basemapDistance;
        terrain.basemapDistance = Mathf.Clamp(TargetBasemapDistance, 350f, 400f);
        EditorUtility.SetDirty(terrain);
        report.AppendLine("basemapDistance: " + before.ToString("F1") + " -> " +
                          terrain.basemapDistance.ToString("F1") + " m");
    }


    // ============================================================================
    //  Layer audit
    // ============================================================================
    static List<LayerAudit> AuditLayers(TerrainData data, TerrainLayer[] layers,
        float[,,] maps, int width, int height, StringBuilder report)
    {
        TreeInstance[] trees = data.treeInstances;
        int layerCount = layers.Length;
        var audits = new List<LayerAudit>(layerCount);
        for (int i = 0; i < layerCount; i++)
            audits.Add(AuditSingleLayer(layers[i], i));

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
                    audits[l].paintedShare += weight;
                    if (weight > 0.9f) audits[l].solidShare += 1f;
                    if (weight > bestWeight)
                    {
                        bestWeight = weight;
                        best = l;
                    }
                }
                if (best >= 0 && bestWeight > 0.5f)
                    audits[best].dominantShare += 1f;

                if (x + 1 < width)
                {
                    for (int l = 0; l < layerCount; l++)
                        if (IsHardCrossing(maps[y, x, l], maps[y, x + 1, l]))
                            audits[l].hardEdgeShare += 1f;
                }
                if (y + 1 < height)
                {
                    for (int l = 0; l < layerCount; l++)
                        if (IsHardCrossing(maps[y, x, l], maps[y + 1, x, l]))
                            audits[l].hardEdgeShare += 1f;
                }
            }
        }

        for (int i = 0; i < layerCount; i++)
        {
            audits[i].paintedShare /= texels;
            audits[i].solidShare /= texels;
            audits[i].dominantShare /= texels;
            audits[i].hardEdgeShare /= 2f * texels;
        }

        if (trees != null && trees.Length > 0)
        {
            for (int t = 0; t < trees.Length; t++)
            {
                int tx = Mathf.Clamp(Mathf.RoundToInt(trees[t].position.x * (width - 1)), 0, width - 1);
                int ty = Mathf.Clamp(Mathf.RoundToInt(trees[t].position.z * (height - 1)), 0, height - 1);
                for (int l = 0; l < layerCount; l++)
                {
                    float weight = maps[ty, tx, l];
                    audits[l].treeWeightMean += weight;
                    if (weight > 0.35f) audits[l].treeShare += 1f;
                }
            }
            for (int i = 0; i < layerCount; i++)
            {
                audits[i].treeWeightMean /= trees.Length;
                audits[i].treeShare /= trees.Length;
            }
        }

        for (int i = 0; i < audits.Count; i++)
        {
            LayerAudit a = audits[i];
            a.score = ScoreLayer(a);
            report.AppendLine(
                "layer[" + a.index + "] '" + a.name + "'" +
                " tile=" + a.tile.x.ToString("F2") + "x" + a.tile.y.ToString("F2") + " m" +
                " diffuse='" + a.diffuseName + "'" +
                " normal='" + a.normalName + "'" +
                " smoothness=" + a.smoothness.ToString("F2") +
                " metallic=" + a.metallic.ToString("F2") +
                " remapMax=" + a.remapMax +
                " painted=" + (a.paintedShare * 100f).ToString("F1") + "%" +
                " solid=" + (a.solidShare * 100f).ToString("F1") + "%" +
                " dominant=" + (a.dominantShare * 100f).ToString("F1") + "%" +
                " trees=" + (a.treeShare * 100f).ToString("F1") + "%" +
                " treeWeight=" + a.treeWeightMean.ToString("F2") +
                " hardEdges=" + a.hardEdgeShare.ToString("F4") +
                " meanColor=(" + a.mean.r.ToString("F2") + "," + a.mean.g.ToString("F2") + "," +
                a.mean.b.ToString("F2") + ")" +
                " contrast=" + a.contrast.ToString("F3") +
                " localDiff=" + a.localDiff.ToString("F4") +
                " luma=" + a.luma.ToString("F3") +
                " score=" + a.score.ToString("F1"));
        }

        return audits;
    }

    static LayerAudit AuditSingleLayer(TerrainLayer layer, int index)
    {
        var audit = new LayerAudit
        {
            index = index,
            name = layer != null ? layer.name : "<null>",
        };

        if (layer == null)
            return audit;

        audit.tile = layer.tileSize;
        var diffuse = layer.diffuseTexture as Texture2D;
        var normal = layer.normalMapTexture as Texture2D;
        audit.hasNormal = normal != null;
        audit.diffuseName = diffuse != null ? diffuse.name : "<none>";
        audit.diffusePath = diffuse != null ? AssetDatabase.GetAssetPath(diffuse) : string.Empty;
        audit.normalName = normal != null ? normal.name : "<none>";
        audit.normalPath = normal != null ? AssetDatabase.GetAssetPath(normal) : string.Empty;
        audit.smoothness = layer.smoothness;
        audit.metallic = layer.metallic;
        audit.remapMax = layer.diffuseRemapMax;

        Color mean;
        float contrast;
        float localDiff;
        if (ComputeTextureStats(diffuse, out mean, out contrast, out localDiff))
        {
            audit.mean = mean;
            audit.contrast = contrast;
            audit.localDiff = localDiff;
        }
        audit.luma = Luminance(audit.mean);
        return audit;
    }


    static float ScoreLayer(LayerAudit a)
    {
        float score = 0f;

        // Flatness: how much high-frequency detail survives a 128 px downsample. The solid
        // dark-green patch has almost none (GrassUV02 ~0.021, forest ground ~0.010).
        float flatness = Mathf.Clamp01(1f - a.localDiff / FlatLocalDiffThreshold);
        score += flatness * 40f;

        // A solid owner of a large area with a hard painted border is the visual patch.
        score += Mathf.Clamp01(a.solidShare / 0.06f) * 18f;
        score += Mathf.Clamp01(a.dominantShare / 0.08f) * 18f;
        score += Mathf.Clamp01(a.hardEdgeShare / 0.004f) * 16f;
        score += Mathf.Clamp01(a.treeWeightMean) * 12f;

        // Darker than the surrounding grass reads as a flat dark stain.
        score += Mathf.Clamp01((0.31f - a.luma) / 0.10f) * 14f;

        if (!a.hasNormal) score += 10f;
        if (a.smoothness > SmoothnessThreshold) score += 8f;
        if (a.tile.x > 12f || a.tile.y > 12f) score += 8f;

        if (IsProtectedName(a.name)) score -= 200f;
        if (a.paintedShare < 0.005f) score -= 60f;
        return score;
    }

    static bool IsProtectedName(string name)
    {
        if (string.IsNullOrEmpty(name)) return false;
        if (name.IndexOf("Pebbles_A", StringComparison.OrdinalIgnoreCase) >= 0) return true;
        if (name.IndexOf("farm_soil", StringComparison.OrdinalIgnoreCase) >= 0) return true;
        if (name.IndexOf("NewLayer 3", StringComparison.OrdinalIgnoreCase) >= 0) return true;
        return false;
    }

    static int PickSuspect(List<LayerAudit> audits, int roadIndex, int farmIndex, StringBuilder report)
    {
        // The solid dark patch is, first of all, the layer whose texture has the least
        // surviving detail. Solid coverage, a hard painted border and darkness only break
        // ties between layers of similar flatness.
        int best = -1;
        float bestKey = float.MaxValue;
        for (int i = 0; i < audits.Count; i++)
        {
            if (i == roadIndex || i == farmIndex) continue;
            LayerAudit a = audits[i];
            if (a.paintedShare < 0.02f) continue;

            float key = a.localDiff
                        - a.solidShare * 0.02f
                        - a.hardEdgeShare * 0.5f
                        + a.luma * 0.01f;
            if (key < bestKey)
            {
                bestKey = key;
                best = i;
            }
        }

        if (best < 0 || audits[best].localDiff > FlatSuspectThreshold)
        {
            report.AppendLine("no flat dark layer found (flatness threshold " +
                              FlatSuspectThreshold.ToString("F4") + ")");
            return -1;
        }

        LayerAudit suspect = audits[best];
        report.AppendLine("SUSPECT layer[" + best + "] '" + suspect.name + "' score=" +
                          suspect.score.ToString("F1") +
                          " (tile " + suspect.tile.x.ToString("F1") + " m, normal=" + suspect.hasNormal +
                          ", localDiff=" + suspect.localDiff.ToString("F4") +
                          ", luma=" + suspect.luma.ToString("F3") +
                          ", painted=" + (suspect.paintedShare * 100f).ToString("F1") + "%" +
                          ", solid=" + (suspect.solidShare * 100f).ToString("F1") + "%" +
                          ", dominant=" + (suspect.dominantShare * 100f).ToString("F1") + "%" +
                          ", hardEdges=" + suspect.hardEdgeShare.ToString("F4") +
                          ", treeWeight=" + suspect.treeWeightMean.ToString("F2") + ")");
        return best;
    }

    // Pick the textured ground layer that borders the suspect layer most.
    static int PickBlendNeighbour(List<LayerAudit> audits, float[,,] maps, int width, int height,
        int suspect, int roadIndex, int farmIndex, StringBuilder report)
    {
        int best = -1;
        double bestScore = -1d;
        for (int i = 0; i < audits.Count; i++)
        {
            if (i == suspect || i == roadIndex || i == farmIndex) continue;
            LayerAudit a = audits[i];
            if (!a.hasNormal)
                continue;
            if (a.name.IndexOf("pebbles", StringComparison.OrdinalIgnoreCase) >= 0 ||
                a.name.IndexOf("rock", StringComparison.OrdinalIgnoreCase) >= 0 ||
                a.name.IndexOf("sand", StringComparison.OrdinalIgnoreCase) >= 0 ||
                a.name.IndexOf("snow", StringComparison.OrdinalIgnoreCase) >= 0)
                continue;

            double edgeWeight = 0d;
            for (int y = 0; y < height; y += 2)
            {
                for (int x = 0; x < width; x += 2)
                {
                    float t = maps[y, x, suspect];
                    if (t < 0.20f || t > 0.80f) continue;
                    edgeWeight += maps[y, x, i];
                }
            }

            // Grass-like layers with a readable mid-tone are the best blend targets; flat dark
            // forest-floor textures would drag the fade colour back down.
            double weighted = edgeWeight;
            if (a.name.IndexOf("grass", StringComparison.OrdinalIgnoreCase) >= 0 ||
                a.name.IndexOf("soil", StringComparison.OrdinalIgnoreCase) >= 0 ||
                a.name.IndexOf("moss", StringComparison.OrdinalIgnoreCase) >= 0)
                weighted *= 1.6;
            weighted *= a.luma >= 0.28f ? 1.3 : 0.5;

            if (weighted > bestScore)
            {
                bestScore = weighted;
                best = i;
            }
        }

        if (best >= 0)
            report.AppendLine("blend neighbour: layer[" + best + "] '" + audits[best].name +
                              "' (edge weight " + bestScore.ToString("F1") + ")");
        else
            report.AppendLine("blend neighbour: none found, using " + GrassLayerFallbackPath);
        return best;
    }


    // ============================================================================
    //  Layer repair
    // ============================================================================
    static bool RepairLayer(TerrainLayer layer, LayerAudit audit, LayerAudit source, StringBuilder report)
    {
        if (layer == null) return false;

        // A stretched tile (bigger than 12 m) or a very flat/dark diffuse is what produces the
        // solid colour patch. Once repaired the tile size is 6-12 m, so a re-run is idempotent.
        bool flatTexture = audit.localDiff < FlatLocalDiffThreshold * 0.72f;
        bool stretchedTile = audit.tile.x > 12f || audit.tile.y > 12f;
        bool needsTexture = !audit.hasNormal || flatTexture || stretchedTile ||
                            audit.luma < 0.18f || string.IsNullOrEmpty(audit.diffusePath);
        bool replaced = false;

        Undo.RecordObject(layer, UndoLabel);

        if (needsTexture && source != null)
        {
            // Reuse the textures of the neighbouring grass/ground layer: this guarantees the same
            // tone and grain as the layer the patch borders, so no seam remains once blended.
            var diffuse = AssetDatabase.LoadAssetAtPath<Texture2D>(source.diffusePath);
            var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(source.normalPath);
            if (diffuse != null)
            {
                layer.diffuseTexture = diffuse;
                replaced = true;
            }
            if (normal != null)
            {
                layer.normalMapTexture = normal;
                layer.normalScale = 1f;
                replaced = true;
            }

            report.AppendLine("layer texture replaced from '" + source.name + "': diffuse='" +
                              (layer.diffuseTexture != null ? layer.diffuseTexture.name : "<none>") +
                              "' normal='" +
                              (layer.normalMapTexture != null ? layer.normalMapTexture.name : "<none>") + "'");
        }

        // Always land in the 6-12 m band. A texture swap gets the canonical 8 m scale; an
        // already repaired layer keeps its current (in-band) tiling so a re-run is idempotent.
        float wantedTile = replaced ? TargetTileSize : layer.tileSize.x;
        if (wantedTile < 6f || wantedTile > 12f)
            wantedTile = TargetTileSize;

        report.AppendLine("layer repair: tile " + layer.tileSize.x.ToString("F2") +
                          " -> " + wantedTile.ToString("F2") + " m, smoothness " +
                          layer.smoothness.ToString("F2") + " -> 0, metallic " +
                          layer.metallic.ToString("F2") + " -> 0, remapMax " + layer.diffuseRemapMax +
                          " -> (1,1,1,1)");

        layer.tileSize = new Vector2(wantedTile, wantedTile);
        layer.tileOffset = Vector2.zero;
        layer.smoothness = 0f;
        layer.metallic = 0f;
        layer.diffuseRemapMin = Vector4.zero;
        layer.diffuseRemapMax = Vector4.one;
        layer.normalScale = 1f;

        // m_SmoothnessSource = 0 (Constant). Set through SerializedObject so the fix does not
        // depend on the enum member spelling of the current Unity version.
        var serialized = new SerializedObject(layer);
        SerializedProperty smoothnessSource = serialized.FindProperty("m_SmoothnessSource");
        if (smoothnessSource != null)
        {
            smoothnessSource.intValue = 0;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        EditorUtility.SetDirty(layer);
        AssetDatabase.SaveAssets();
        return replaced;
    }

    // ============================================================================
    //  Texture statistics (works for non-readable imported textures)
    // ============================================================================
    static bool ComputeTextureStats(Texture2D texture, out Color mean, out float contrast,
        out float localDiff)
    {
        mean = Color.black;
        contrast = 0f;
        localDiff = 0f;
        if (texture == null) return false;

        const int size = 128;
        RenderTexture rt = RenderTexture.GetTemporary(size, size, 0, RenderTextureFormat.ARGB32,
            RenderTextureReadWrite.Linear);
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
            Vector3 sum = Vector3.zero;
            for (int i = 0; i < pixels.Length; i++)
                sum += new Vector3(pixels[i].r, pixels[i].g, pixels[i].b);
            sum /= pixels.Length;
            mean = new Color(sum.x, sum.y, sum.z, 1f);

            float meanLuma = Luminance(mean);
            float deviation = 0f;
            float neighbourDiff = 0f;
            int pairs = 0;
            for (int y = 0; y < size; y++)
            {
                int row = y * size;
                for (int x = 0; x < size; x++)
                {
                    int i = row + x;
                    float l = Luminance(pixels[i]);
                    deviation += Mathf.Abs(l - meanLuma);
                    if (x + 1 < size)
                    {
                        neighbourDiff += Mathf.Abs(l - Luminance(pixels[i + 1]));
                        pairs++;
                    }
                }
            }

            contrast = deviation / pixels.Length;
            localDiff = pairs > 0 ? neighbourDiff / pairs : 0f;
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogWarning("[TerrainLayerFixer] texture stats failed for " +
                             (texture != null ? texture.name : "<null>") + ": " + exception.Message);
            return false;
        }
        finally
        {
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);
            if (readable != null)
                UnityEngine.Object.DestroyImmediate(readable);
        }
    }

    static float Luminance(Color color)
    {
        return color.r * 0.2126f + color.g * 0.7152f + color.b * 0.0722f;
    }


    // ============================================================================
    //  Alphamap edge blending
    // ============================================================================
    static int BlendAlphamaps(TerrainData data, Terrain terrain, float[,,] maps,
        TerrainLayer[] layers, int target, int road, int farm, int primaryNeighbour,
        StringBuilder report)
    {
        int width = data.alphamapWidth;
        int height = data.alphamapHeight;
        int layerCount = layers.Length;
        int count = width * height;

        float[] targetChannel = new float[count];
        float[] roadMask = new float[count];
        float[] farmMask = new float[count];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int i = y * width + x;
                targetChannel[i] = maps[y, x, target];
                roadMask[i] = road >= 0 ? maps[y, x, road] : 0f;
                farmMask[i] = farm >= 0 ? maps[y, x, farm] : 0f;
            }
        }

        float cellX = data.size.x / width;
        float cellZ = data.size.z / height;
        float cell = (cellX + cellZ) * 0.5f;
        int radius = Mathf.Max(1, Mathf.RoundToInt(BlurWidthMeters * 0.5f / Mathf.Max(cell, 0.0001f)));
        int passes = cell > 2.5f ? 1 : 3;

        float[] blurred = (float[])targetChannel.Clone();
        int hardEdges = 0;
        bool[] hardMask = new bool[count];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int i = y * width + x;
                if (x + 1 < width && IsHardCrossing(targetChannel[i], targetChannel[i + 1]))
                {
                    hardEdges++;
                    hardMask[i] = true;
                    hardMask[i + 1] = true;
                }
                if (y + 1 < height && IsHardCrossing(targetChannel[i], targetChannel[i + width]))
                {
                    hardEdges++;
                    hardMask[i] = true;
                    hardMask[i + width] = true;
                }
            }
        }

        if (hardEdges < 50)
        {
            report.AppendLine("alphamap already blended (hard edges=" + hardEdges +
                              ") - blend skipped, road/farm untouched");
            return 0;
        }

        // Only texels around a genuine hard step are allowed to change. Once the edge is soft
        // there is no mask left, so repeated runs never widen the transition any further.
        int dilate = Mathf.Clamp(radius, 1, 4);
        for (int pass = 0; pass < dilate; pass++)
        {
            bool[] source = (bool[])hardMask.Clone();
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int i = y * width + x;
                    if (hardMask[i]) continue;
                    if ((x > 0 && source[i - 1]) ||
                        (x < width - 1 && source[i + 1]) ||
                        (y > 0 && source[i - width]) ||
                        (y < height - 1 && source[i + width]))
                        hardMask[i] = true;
                }
            }
        }

        if (radius <= 1)
        {
            BinomialBlur(blurred, width, height);
        }
        else
        {
            for (int pass = 0; pass < passes; pass++)
                BoxBlur(blurred, width, height, radius);
        }

        // Chamfer distance (in texels) from the painted road: keep its hard edge intact.
        float[] roadDistance = new float[count];
        const float far = 1e9f;
        for (int i = 0; i < count; i++)
            roadDistance[i] = roadMask[i] > RoadProtectAlpha ? 0f : far;
        float d1 = 1f, d2 = 1.4142136f;
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                int i = y * width + x;
                float v = roadDistance[i];
                if (x > 0) v = Mathf.Min(v, roadDistance[i - 1] + d1);
                if (y > 0) v = Mathf.Min(v, roadDistance[i - width] + d1);
                if (x > 0 && y > 0) v = Mathf.Min(v, roadDistance[i - width - 1] + d2);
                if (x < width - 1 && y > 0) v = Mathf.Min(v, roadDistance[i - width + 1] + d2);
                roadDistance[i] = v;
            }
        for (int y = height - 1; y >= 0; y--)
            for (int x = width - 1; x >= 0; x--)
            {
                int i = y * width + x;
                float v = roadDistance[i];
                if (x < width - 1) v = Mathf.Min(v, roadDistance[i + 1] + d1);
                if (y < height - 1) v = Mathf.Min(v, roadDistance[i + width] + d1);
                if (x < width - 1 && y < height - 1) v = Mathf.Min(v, roadDistance[i + width + 1] + d2);
                if (x > 0 && y < height - 1) v = Mathf.Min(v, roadDistance[i + width - 1] + d2);
                roadDistance[i] = v;
            }

        report.AppendLine("alphamap blend: cell=" + cell.ToString("F3") + " m, hard edges=" + hardEdges +
                          ", blur radius=" + radius + " texel(s) x" +
                          (radius <= 1 ? "binomial" : passes + " box pass(es)") + " (~" +
                          ((radius * 2 + 1) * cell).ToString("F1") + " m), Perlin fringe=" +
                          EdgeNoiseAmplitude.ToString("F2"));


        int changed = 0, skippedRoad = 0, farmClamped = 0;
        float roadMaxDelta = 0f, farmMaxDelta = 0f;
        float bandLimit = radius * 1.5f;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int i = y * width + x;
                if (roadMask[i] > RoadProtectAlpha || roadDistance[i] < bandLimit)
                {
                    skippedRoad++;
                    continue;
                }
                if (!hardMask[i])
                    continue;

                float t0 = targetChannel[i];
                float bt = blurred[i];
                float band = Mathf.Clamp01(Mathf.Abs(t0 - bt) * 5f);
                if (band < 0.002f) continue;

                float wx = x * cellX;
                float wz = y * cellZ;
                float n1 = Mathf.PerlinNoise(wx * 0.09f + 13.7f, wz * 0.09f + 7.1f);
                float n2 = Mathf.PerlinNoise(wx * 0.33f + 41.3f, wz * 0.33f + 3.9f);
                float noise = ((n1 * 0.65f + n2 * 0.35f) - 0.5f) * 2f;

                float nt = Mathf.Lerp(t0, bt, BlendStrength) +
                           noise * EdgeNoiseAmplitude * band * BlendStrength;
                nt = Mathf.Clamp01(nt);

                if (farmMask[i] > FarmProtectAlpha)
                {
                    nt = Mathf.Min(nt, Mathf.Max(t0, FarmEdgeMaxTarget));
                    farmClamped++;
                }

                float roadA = roadMask[i];
                float farmA = farmMask[i];
                float remaining = Mathf.Clamp01(1f - roadA - farmA);
                if (remaining <= 0.0001f) continue;

                float others = nt;
                for (int l = 0; l < layerCount; l++)
                {
                    if (l == target || l == road || l == farm) continue;
                    others += maps[y, x, l];
                }

                if (others <= 1e-5f)
                {
                    if (primaryNeighbour >= 0 && primaryNeighbour != target &&
                        primaryNeighbour != road && primaryNeighbour != farm)
                        maps[y, x, primaryNeighbour] += remaining;
                    maps[y, x, target] = remaining;
                }
                else
                {
                    float scale = remaining / others;
                    maps[y, x, target] = nt * scale;
                    for (int l = 0; l < layerCount; l++)
                    {
                        if (l == target || l == road || l == farm) continue;
                        maps[y, x, l] *= scale;
                    }
                }

                changed++;
                if (road >= 0)
                    roadMaxDelta = Mathf.Max(roadMaxDelta, Mathf.Abs(maps[y, x, road] - roadA));
                if (farm >= 0)
                    farmMaxDelta = Mathf.Max(farmMaxDelta, Mathf.Abs(maps[y, x, farm] - farmA));
            }
        }

        if (changed == 0)
        {
            report.AppendLine("alphamap blend: nothing to change");
            return 0;
        }

        Undo.RegisterCompleteObjectUndo(data, UndoLabel);
        data.SetAlphamaps(0, 0, maps);
        EditorUtility.SetDirty(data);
        EditorUtility.SetDirty(terrain);

        report.AppendLine("alphamap blend: changed=" + changed + " texels, road-protected=" +
                          skippedRoad + ", field-clamped=" + farmClamped +
                          ", roadDelta=" + roadMaxDelta.ToString("F4") +
                          ", farmDelta=" + farmMaxDelta.ToString("F4"));
        return 0;
    }

    // Center-weighted 3x3 kernel: at the 3.9 m alphamap cell this is an ~8 m transition,
    // right inside the requested 6-10 m band (a plain 3-tap box would be too wide here).
    static void BinomialBlur(float[] values, int width, int height)
    {
        float[] temp = new float[values.Length];
        for (int y = 0; y < height; y++)
        {
            int row = y * width;
            for (int x = 0; x < width; x++)
            {
                float left = values[row + Mathf.Max(x - 1, 0)];
                float center = values[row + x];
                float right = values[row + Mathf.Min(x + 1, width - 1)];
                temp[row + x] = left * 0.25f + center * 0.5f + right * 0.25f;
            }
        }

        for (int y = 0; y < height; y++)
        {
            int upRow = Mathf.Max(y - 1, 0) * width;
            int row = y * width;
            int downRow = Mathf.Min(y + 1, height - 1) * width;
            for (int x = 0; x < width; x++)
            {
                values[row + x] = temp[upRow + x] * 0.25f +
                                  temp[row + x] * 0.5f +
                                  temp[downRow + x] * 0.25f;
            }
        }
    }

    // A hard edge is a 0.5+ jump that also crosses the alpha 0.5 level. A narrow but already
    // smoothed transition (or Perlin fringe) never qualifies, which keeps re-runs idempotent.
    static bool IsHardCrossing(float a, float b)
    {
        if (Mathf.Abs(a - b) <= 0.5f) return false;
        return (a - 0.5f) * (b - 0.5f) <= 0f;
    }

    static void BoxBlur(float[] values, int width, int height, int radius)
    {
        int count = values.Length;
        float[] temp = new float[count];

        for (int y = 0; y < height; y++)
        {
            int row = y * width;
            float sum = 0f;
            int samples = 0;
            for (int k = -radius; k <= radius; k++)
            {
                sum += values[row + Mathf.Clamp(k, 0, width - 1)];
                samples++;
            }
            for (int x = 0; x < width; x++)
            {
                temp[row + x] = sum / samples;
                int add = Mathf.Clamp(x + radius + 1, 0, width - 1);
                int sub = Mathf.Clamp(x - radius, 0, width - 1);
                sum += values[row + add] - values[row + sub];
            }
        }

        for (int x = 0; x < width; x++)
        {
            float sum = 0f;
            int samples = 0;
            for (int k = -radius; k <= radius; k++)
            {
                sum += temp[Mathf.Clamp(k, 0, height - 1) * width + x];
                samples++;
            }
            for (int y = 0; y < height; y++)
            {
                values[y * width + x] = sum / samples;
                int add = Mathf.Clamp(y + radius + 1, 0, height - 1);
                int sub = Mathf.Clamp(y - radius, 0, height - 1);
                sum += temp[add * width + x] - temp[sub * width + x];
            }
        }
    }


    // ============================================================================
    //  Grass material sync (_FadeColor / _GroundFalloff)
    // ============================================================================
    static void SyncGrassMaterials(LayerAudit repaired, StringBuilder report)
    {
        Color surface = new Color(
            repaired.mean.r * repaired.remapMax.x,
            repaired.mean.g * repaired.remapMax.y,
            repaired.mean.b * repaired.remapMax.z,
            1f);

        float luma = Luminance(surface);
        float targetLuma = Mathf.Clamp(Mathf.Max(luma * FadeBrightnessBoost, FadeLuminanceFloor),
            FadeLuminanceFloor, FadeLuminanceCeil);
        float scale = targetLuma / Mathf.Max(luma, 0.0001f);
        Color fade = new Color(surface.r * scale, surface.g * scale, surface.b * scale, 1f);

        report.AppendLine("_FadeColor reference from '" + repaired.name + "': surface=(" +
                          surface.r.ToString("F3") + "," + surface.g.ToString("F3") + "," +
                          surface.b.ToString("F3") + ") luma=" + luma.ToString("F3") +
                          " -> fade=(" + fade.r.ToString("F3") + "," + fade.g.ToString("F3") + "," +
                          fade.b.ToString("F3") + ") luma=" + Luminance(fade).ToString("F3"));

        string[] guids = AssetDatabase.FindAssets("t:Material", new[] { GrassMaterialFolder });
        int updated = 0;
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null || !material.HasProperty("_FadeColor"))
                continue;

            Undo.RecordObject(material, "Sync grass fade colour");
            Color before = material.GetColor("_FadeColor");
            material.SetColor("_FadeColor", fade);
            if (material.HasProperty("_GroundFalloff"))
                material.SetFloat("_GroundFalloff", GroundFalloff);

            EditorUtility.SetDirty(material);
            updated++;
            report.AppendLine("material '" + material.name + "': _FadeColor (" +
                              before.r.ToString("F3") + "," + before.g.ToString("F3") + "," +
                              before.b.ToString("F3") + ") -> (" + fade.r.ToString("F3") + "," +
                              fade.g.ToString("F3") + "," + fade.b.ToString("F3") + ")" +
                              ", _GroundFalloff=" + GroundFalloff.ToString("F2"));
        }

        AssetDatabase.SaveAssets();
        report.AppendLine("grass materials updated: " + updated);
    }

    // ============================================================================
    //  Scene / asset helpers
    // ============================================================================
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

    static int FindLayerIndex(TerrainLayer[] layers, string layerName)
    {
        if (layers == null) return -1;
        for (int i = 0; i < layers.Length; i++)
        {
            if (layers[i] != null &&
                string.Equals(layers[i].name, layerName, StringComparison.OrdinalIgnoreCase))
                return i;
        }
        return -1;
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
            Debug.LogWarning("[TerrainLayerFixer] could not write the report: " + exception.Message);
        }
    }
}

