using System.Collections.Generic;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;

// Authoring tool for the terrain detail (grass) layers.
// Rebuilds them as a solid grass carpet over every non-road, non-field cell, with sparse
// noise-driven thickets. Removes grass from the road surface (+2 m margin) and arable fields.
// Menu: Tools/DroneSimulator/Grass Detail/...
public static class RoadsideGrassBuilder
{
    const string TerrainObjectName = "Terrain";
    const string UndoLabel = "Rebuild roadside grass";

    // ---- detail resolution -----------------------------------------------------
    // 2000 m terrain / 1024 = 1.95 m per detail cell (needed to resolve a 9 m verge).
    const int DetailResolution = 1024;
    const int DetailResolutionPerPatch = 64;
    const int DetailBlockRows = 128;

    // ---- terrain layers --------------------------------------------------------
    const int RoadLayerIndex = 10;   // Pebbles_A_TerrainLayer == asphalt / gravel road surface
    const int FarmLayerIndex = 6;    // NewLayer 3 (farm_soil)     == ploughed fields
    const float RoadAlphaClear = 0.10f;

    // ---- road corridor ---------------------------------------------------------
    const float SplineRoadRadius = 11f;      // the painted road is wider than the spline curve
    const float ShoulderClear = 2.0f;        // bare ground kept next to the road edge

    // ---- solid grass carpet + sparse thickets (whole map, up to the fields) ----
    // Every allowed cell gets the base carpet, so verges, forest belts and roadside
    // meadows are covered without holes. The extra density only lives in high-noise
    // thickets; the budget clamp scales that bonus, never the carpet itself.
    const int BaseInstancesPerCell = 3;      // minimum: 3 instances in every allowed cell
    const float ThicketPeakInstances = 12f;  // dense thickets reach 8..12 instances per cell
    const float ThicketNoiseStart = 0.62f;   // clump noise where the thickets start
    const float ThicketNoiseEnd = 0.86f;     // ... and where they reach full density
    const float FarmFadeStart = 0.08f;       // density starts fading here (farm alpha)
    const float FarmFadeEnd = 0.25f;         // ... and is gone at the arable field edge
    const float FarmHardClear = 0.25f;       // never grow grass above this farm alpha

    // ---- noise (two perlin layers: big clearings + small bumps) ----------------
    const float ClumpNoiseScale = 0.02f;
    const float BumpNoiseScale = 0.09f;

    // ---- prototype sizes -------------------------------------------------------
    const float PrototypeMinWidth = 1.2f;
    const float PrototypeMaxWidth = 2.8f;
    const float PrototypeMinHeight = 1.0f;
    const float PrototypeMaxHeight = 2.5f;

    const int MaxTotalInstances = 1400000;   // global instance budget for the whole map
    const int MinTotalInstances = 1200000;   // warn below this sanity floor
    const int MaxInstancesPerCell = 12;      // dense thickets: 8..12 per cell

    // ---- detail prototypes (grass + three most expressive bushes / ferns) ------
    static readonly string[] DetailPrefabPaths =
    {
        "Assets/TerrainSampleAssets/Prefabs/Grass_A.prefab",
        "Assets/TerrainSampleAssets/Prefabs/Grass_B.prefab",
        "Assets/TerrainSampleAssets/Prefabs/Grass_C.prefab",
        "Assets/TerrainSampleAssets/Prefabs/GrassDry_A.prefab",
        "Assets/TerrainSampleAssets/Prefabs/Details/GrassDry_B.prefab",
        "Assets/TerrainSampleAssets/Prefabs/Bush_A.prefab",
        "Assets/TerrainSampleAssets/Prefabs/BushDry_A.prefab",
        "Assets/TerrainSampleAssets/Prefabs/Fern_A.prefab",
    };

    static readonly float[] DetailPrefabWeights =
    {
        0.20f, 0.18f, 0.14f, 0.09f, 0.09f,   // grass = 0.70
        0.10f, 0.08f, 0.12f,                 // Bush_A, BushDry_A, Fern_A = 0.30
    };

    // Sparse bushes in the open carpet; the full bush/fern mix only appears inside
    // the noise thickets (see Generate), so meadows read as grass, not shrubbery.
    static readonly float[] DetailMeadowWeights =
    {
        0.27f, 0.24f, 0.20f, 0.12f, 0.12f,   // grass = 0.95
        0.015f, 0.015f, 0.02f,               // Bush_A, BushDry_A, Fern_A = 0.05
    };

    // ---- custom (duplicated) grass materials, see CustomGrassMaterials ---------
    const string CustomMaterialFolder = "Assets/Materials/CustomGrass";
    const float FadeStartDistance = 190f;
    const float FadeEndDistance = 245f;

    // ---- performance settings --------------------------------------------------
    const float DetailObjectDistance = 250f;

    // ============================================================================
    //  Menu
    // ============================================================================
    [MenuItem("Tools/DroneSimulator/Grass Detail/1. Remove LOD Groups + Rebuild Detail Prototypes", priority = 0)]
    public static void SetupDetailPrefabs()
    {
        Terrain terrain = FindTerrain();
        if (terrain == null) return;
        TerrainData data = terrain.terrainData;

        // Terrain Details do NOT support LODGroup ("LOD Group component is not supported").
        // Strip every LODGroup from the grass/bush/fern prefabs used as detail prototypes.
        var visited = new HashSet<string>();
        int removedGroups = 0;
        int preparedPrefabs = 0;
        foreach (string path in DetailPrefabPaths)
        {
            if (!visited.Add(path)) continue;
            GameObject root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (root == null)
            {
                Debug.LogWarning("[RoadsideGrass] detail prefab not found: " + path);
                continue;
            }

            foreach (LODGroup group in root.GetComponentsInChildren<LODGroup>(true))
            {
                Object.DestroyImmediate(group, true);
                removedGroups++;
            }

            PrepareDetailRenderer(root);
            PrefabUtility.SavePrefabAsset(root);
            preparedPrefabs++;
        }

        // Clean up whatever is still referenced by the current prototype list.
        foreach (DetailPrototype prototype in data.detailPrototypes)
        {
            if (prototype.prototype == null) continue;
            string path = AssetDatabase.GetAssetPath(prototype.prototype);
            if (string.IsNullOrEmpty(path) || !visited.Add(path)) continue;
            GameObject root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (root == null) continue;
            foreach (LODGroup group in root.GetComponentsInChildren<LODGroup>(true))
            {
                Object.DestroyImmediate(group, true);
                removedGroups++;
            }
            PrepareDetailRenderer(root);
            PrefabUtility.SavePrefabAsset(root);
        }

        RebuildDetailPrototypes(data);
        AssetDatabase.SaveAssets();

        int remaining = 0;
        foreach (DetailPrototype prototype in data.detailPrototypes)
        {
            if (prototype.prototype == null) continue;
            remaining += prototype.prototype.GetComponentsInChildren<LODGroup>(true).Length;
        }

        Debug.Log("[RoadsideGrass] detail prefabs prepared=" + preparedPrefabs +
                  ", LODGroups removed=" + removedGroups +
                  ", LODGroups left on detail prototypes=" + remaining +
                  ", prototypes now=" + data.detailPrototypes.Length);
    }

    // Duplicates the grass Shader Graph + materials into Assets/Materials/CustomGrass so the
    // originals inside TerrainSampleAssets stay untouched, tunes the built-in distance fade
    // (190..245 m) and assigns the copies to every detail prefab.
    [MenuItem("Tools/DroneSimulator/Grass Detail/0. Duplicate Grass Materials + Distance Fade", priority = -1)]
    public static void CreateCustomGrassMaterials()
    {
        Terrain terrain = FindTerrain();
        if (terrain == null) return;
        TerrainData data = terrain.terrainData;

        if (!AssetDatabase.IsValidFolder("Assets/Materials"))
            AssetDatabase.CreateFolder("Assets", "Materials");
        if (!AssetDatabase.IsValidFolder(CustomMaterialFolder))
            AssetDatabase.CreateFolder("Assets/Materials", "CustomGrass");

        string sourceShaderPath = null;
        foreach (DetailPrototype prototype in data.detailPrototypes)
        {
            if (prototype.prototype == null) continue;
            MeshRenderer renderer = prototype.prototype.GetComponentInChildren<MeshRenderer>(true);
            if (renderer == null || renderer.sharedMaterial == null || renderer.sharedMaterial.shader == null) continue;
            sourceShaderPath = AssetDatabase.GetAssetPath(renderer.sharedMaterial.shader);
            if (!string.IsNullOrEmpty(sourceShaderPath)) break;
        }
        if (string.IsNullOrEmpty(sourceShaderPath))
        {
            Debug.LogError("[RoadsideGrass] could not resolve the grass shader from the detail prototypes.");
            return;
        }

        string shaderName = System.IO.Path.GetFileNameWithoutExtension(sourceShaderPath);
        string shaderCopyPath = CustomMaterialFolder + "/" + shaderName + "_Fade.shadergraph";
        if (AssetDatabase.LoadAssetAtPath<Object>(shaderCopyPath) == null)
            AssetDatabase.CopyAsset(sourceShaderPath, shaderCopyPath);
        AssetDatabase.ImportAsset(shaderCopyPath, ImportAssetOptions.ForceUpdate);

        Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(shaderCopyPath);
        if (shader == null)
        {
            Debug.LogError("[RoadsideGrass] failed to load the duplicated shader: " + shaderCopyPath);
            return;
        }

        var cache = new Dictionary<string, Material>();
        int prefabsTouched = 0;

        foreach (DetailPrototype prototype in data.detailPrototypes)
        {
            if (prototype.prototype == null) continue;
            string prefabPath = AssetDatabase.GetAssetPath(prototype.prototype);
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            bool dirty = false;
            try
            {
                foreach (MeshRenderer renderer in root.GetComponentsInChildren<MeshRenderer>(true))
                {
                    Material[] materials = renderer.sharedMaterials;
                    for (int i = 0; i < materials.Length; i++)
                    {
                        Material source = materials[i];
                        if (source == null) continue;

                        Material custom;
                        if (!cache.TryGetValue(source.name, out custom))
                        {
                            string sourcePath = AssetDatabase.GetAssetPath(source);
                            string destPath = CustomMaterialFolder + "/" + source.name + "_Custom.asset";
                            custom = AssetDatabase.LoadAssetAtPath<Material>(destPath);
                            if (custom == null)
                            {
                                if (AssetDatabase.CopyAsset(sourcePath, destPath))
                                    custom = AssetDatabase.LoadAssetAtPath<Material>(destPath);
                                else
                                    Debug.LogWarning("[RoadsideGrass] could not copy material: " + sourcePath);
                            }
                            if (custom != null)
                            {
                                custom.shader = shader;
                                custom.SetFloat("Distance_Fade_Start", FadeStartDistance);
                                custom.SetFloat("Distance_Fade_End", FadeEndDistance);
                                custom.enableInstancing = true;
                                EditorUtility.SetDirty(custom);
                            }
                            cache[source.name] = custom;
                        }

                        if (custom != null)
                        {
                            materials[i] = custom;
                            dirty = true;
                        }
                    }
                    renderer.sharedMaterials = materials;
                }
            }
            finally
            {
                if (dirty)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                    prefabsTouched++;
                }
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        AssetDatabase.SaveAssets();

        var report = new System.Text.StringBuilder();
        report.Append("[RoadsideGrass] custom grass materials in ").Append(CustomMaterialFolder)
              .Append(": ").Append(cache.Count).Append(" copied, prefabs updated=").Append(prefabsTouched)
              .Append(", fade ").Append(FadeStartDistance).Append("..").Append(FadeEndDistance).Append(" m");
        foreach (Material material in cache.Values)
        {
            if (material == null) continue;
            report.Append("\n  ").Append(material.name)
                  .Append(" fadeStart=").Append(material.GetFloat("Distance_Fade_Start").ToString("F0"))
                  .Append(" fadeEnd=").Append(material.GetFloat("Distance_Fade_End").ToString("F0"))
                  .Append(" shader=").Append(material.shader != null ? material.shader.name : "null");
        }
        Debug.Log(report.ToString());
    }

    static void PrepareDetailRenderer(GameObject root)
    {
        // Detail instances are GPU-instanced and must not cast shadows.
        foreach (MeshRenderer renderer in root.GetComponentsInChildren<MeshRenderer>(true))
        {
            MeshFilter filter = renderer.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null) continue;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            foreach (Material material in renderer.sharedMaterials)
            {
                if (material != null && !material.enableInstancing)
                {
                    material.enableInstancing = true;
                    EditorUtility.SetDirty(material);
                }
            }
        }
    }

    // Rebuilds TerrainData.detailPrototypes from DetailPrefabPaths (grass + bushes + ferns).
    static void RebuildDetailPrototypes(TerrainData data)
    {
        var prototypes = new List<DetailPrototype>();
        foreach (string path in DetailPrefabPaths)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                Debug.LogWarning("[RoadsideGrass] detail prefab not found: " + path);
                continue;
            }

            bool bush = path.Contains("/Bush") || path.Contains("/Fern");
            prototypes.Add(new DetailPrototype
            {
                prototype = prefab,
                usePrototypeMesh = true,
                renderMode = DetailRenderMode.VertexLit,
                minWidth = PrototypeMinWidth,
                maxWidth = PrototypeMaxWidth,
                minHeight = PrototypeMinHeight,
                maxHeight = PrototypeMaxHeight,
                noiseSeed = prototypes.Count + 1,
                noiseSpread = 0.6f,
                healthyColor = bush ? new Color(0.30f, 0.58f, 0.22f) : new Color(0.26f, 0.76f, 0.18f),
                dryColor = bush ? new Color(0.62f, 0.58f, 0.20f) : new Color(0.74f, 0.68f, 0.16f),
                useInstancing = true,
                targetCoverage = 1f,
                useDensityScaling = false,
                alignToGround = 0f,
                positionJitter = 0.5f,
            });
        }

        data.detailPrototypes = prototypes.ToArray();
        data.RefreshPrototypes();
    }

    static float[] CurrentPrototypeWeights(TerrainData data, float[] sourceWeights)
    {
        var weights = new float[data.detailPrototypes.Length];
        float sum = 0f;
        for (int i = 0; i < weights.Length; i++)
        {
            GameObject prefab = data.detailPrototypes[i].prototype;
            string path = prefab != null ? AssetDatabase.GetAssetPath(prefab) : string.Empty;
            float weight = 0f;
            for (int s = 0; s < DetailPrefabPaths.Length; s++)
            {
                if (DetailPrefabPaths[s] == path) { weight = sourceWeights[s]; break; }
            }
            if (weight <= 0f) weight = 1f;
            weights[i] = weight;
            sum += weight;
        }
        if (sum <= 0f) return weights;
        for (int i = 0; i < weights.Length; i++) weights[i] /= sum;
        return weights;
    }

    [MenuItem("Tools/DroneSimulator/Grass Detail/2. Apply Grass Performance Settings", priority = 1)]
    public static void ApplyPerformanceSettings()
    {
        Terrain terrain = FindTerrain();
        if (terrain == null) return;
        TerrainData data = terrain.terrainData;

        Undo.RegisterCompleteObjectUndo(terrain, "Grass performance settings");
        terrain.detailObjectDistance = DetailObjectDistance;
        terrain.detailObjectDensity = 1f;
        terrain.collectDetailPatches = true;
        EditorUtility.SetDirty(terrain);

        // TerrainDistanceBoost would otherwise raise the distance back to 800 m at runtime.
        var boost = terrain.GetComponent<TerrainDistanceBoost>();
        if (boost != null)
        {
            Undo.RecordObject(boost, "Grass performance settings");
            boost.detailDistance = DetailObjectDistance;
            EditorUtility.SetDirty(boost);
        }

        // Detail shadows are driven by the prototype renderers; verify they are off.
        int shadowCasters = 0;
        foreach (DetailPrototype prototype in data.detailPrototypes)
        {
            if (prototype.prototype == null) continue;
            foreach (MeshRenderer renderer in prototype.prototype.GetComponentsInChildren<MeshRenderer>(true))
                if (renderer.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.Off) shadowCasters++;
        }

        Debug.Log("[RoadsideGrass] Performance settings applied: detailObjectDistance=" + terrain.detailObjectDistance +
                  " m, detailObjectDensity=" + terrain.detailObjectDensity +
                  ", instancing=" + terrain.drawInstanced +
                  ", prototypes still casting shadows=" + shadowCasters);
    }

    [MenuItem("Tools/DroneSimulator/Grass Detail/3. Generate Roadside + Tree Belt Grass", priority = 2)]
    public static void Generate()
    {
        Terrain terrain = FindTerrain();
        if (terrain == null) return;

        TerrainData data = terrain.terrainData;
        Vector3 origin = terrain.transform.position;
        Vector3 size = data.size;

        // Exact instance counts per cell (InstanceCountMode is baked into the terrain
        // asset and the property is read-only) + the agreed 250 m draw distance, both on
        // the Terrain component and in TerrainDistanceBoost (which overrides it at runtime).
        if (data.detailScatterMode != DetailScatterMode.InstanceCountMode)
            Debug.LogWarning("[RoadsideGrass] detailScatterMode is " + data.detailScatterMode +
                             "; expected InstanceCountMode.");
        terrain.detailObjectDistance = DetailObjectDistance;
        terrain.detailObjectDensity = 1f;
        terrain.collectDetailPatches = true;
        var distanceBoost = terrain.GetComponent<TerrainDistanceBoost>();
        if (distanceBoost != null) distanceBoost.detailDistance = DetailObjectDistance;
        EditorUtility.SetDirty(terrain);
        if (distanceBoost != null) EditorUtility.SetDirty(distanceBoost);

        Undo.RegisterCompleteObjectUndo(data, UndoLabel);
        Undo.RegisterCompleteObjectUndo(terrain, UndoLabel);

        if (data.detailResolution != DetailResolution)
            data.SetDetailResolution(DetailResolution, DetailResolutionPerPatch);

        int dw = data.detailWidth;
        int dh = data.detailHeight;
        int prototypeCount = data.detailPrototypes.Length;

        // --- 1. wipe every previous detail layer --------------------------------
        ClearDetailLayers(data);
        Debug.Log("[RoadsideGrass] detail resolution " + dw + "x" + dh + " (" +
                  (size.x / dw).ToString("F2") + " m per cell), prototypes=" + prototypeCount);

        // --- 2. masks that decide where grass is allowed -------------------------
        var roadAlpha = new AlphaField(data, RoadLayerIndex);
        var farmAlpha = new AlphaField(data, FarmLayerIndex);
        DistanceField roadDistance = BuildRoadDistanceField(terrain);

        // --- 3. solid carpet everywhere + sparse noise thickets ------------------
        // No histogram threshold any more: every allowed cell gets the base carpet,
        // so the verges, forest belts and roadside meadows are covered without holes.
        var fadeGrid = new float[dw * dh];
        var thicketGrid = new float[dw * dh];
        int allowedCells = 0;

        for (int z = 0; z < dh; z++)
        {
            for (int x = 0; x < dw; x++)
            {
                float u = (x + 0.5f) / dw;
                float v = (z + 0.5f) / dh;

                float dist = roadDistance.Sample(u, v);
                float road = roadAlpha.SampleMax(u, v);
                float farm = farmAlpha.Sample(u, v);

                float wx = origin.x + u * size.x;
                float wz = origin.z + v * size.z;

                // hard exclusions: road surface (+2 m margin) and arable fields
                if (road > RoadAlphaClear || farm > FarmHardClear || dist < ShoulderClear)
                    continue;

                // soft edge of the arable fields: 0.08 .. 0.25 farm alpha
                float fieldFade = 1f - Mathf.SmoothStep(FarmFadeStart, FarmFadeEnd, farm);
                if (fieldFade <= 0.001f) continue;

                // two perlin layers -> organic clump pattern
                float big = Mathf.PerlinNoise(wx * ClumpNoiseScale + 137.13f, wz * ClumpNoiseScale + 71.71f);
                float small = Mathf.PerlinNoise(wx * BumpNoiseScale + 311.70f, wz * BumpNoiseScale + 19.30f);
                float noise = Mathf.Clamp01(big * 0.62f + small * 0.38f);

                // the carpet is flat, the extra density only appears in the thickets
                float thicket = Mathf.SmoothStep(0f, 1f,
                    Mathf.InverseLerp(ThicketNoiseStart, ThicketNoiseEnd, noise));

                int index = z * dw + x;
                fadeGrid[index] = fieldFade;
                thicketGrid[index] = thicket;
                allowedCells++;
            }
        }

        // --- 4. keep the budget by scaling the thicket bonus only ----------------
        // The 3-per-cell carpet always survives; only the density above it is scaled,
        // so the map never gets empty patches back.
        float bonusScale = 1f;
        long estimated = EstimateTotalInstances(fadeGrid, thicketGrid, dw * dh, 1f);
        if (estimated > MaxTotalInstances)
        {
            float low = 0f, high = 1f;
            for (int i = 0; i < 20; i++)
            {
                float mid = (low + high) * 0.5f;
                if (EstimateTotalInstances(fadeGrid, thicketGrid, dw * dh, mid) > MaxTotalInstances)
                    high = mid;
                else
                    low = mid;
            }
            bonusScale = low;
            estimated = EstimateTotalInstances(fadeGrid, thicketGrid, dw * dh, bonusScale);
        }
        Debug.Log("[RoadsideGrass] budget " + MaxTotalInstances + ", estimated " + estimated +
                  " instances (thicket bonus x" + bonusScale.ToString("F3") + "), allowed cells=" +
                  allowedCells + " (" + (100f * allowedCells / (dw * dh)).ToString("F1") + "% of map)");

        // --- 5. distribute the exact per-cell count over the prototypes ----------
        float[] thicketWeights = CurrentPrototypeWeights(data, DetailPrefabWeights);
        float[] meadowWeights = CurrentPrototypeWeights(data, DetailMeadowWeights);
        var layerData = new int[prototypeCount][];
        for (int i = 0; i < prototypeCount; i++) layerData[i] = new int[dw * dh];
        var perLayerTotals = new long[prototypeCount];
        long totalInstances = 0;
        int coveredCells = 0;
        int maxDensity = 0;

        for (int z = 0; z < dh; z++)
        {
            for (int x = 0; x < dw; x++)
            {
                int index = z * dw + x;
                float fade = fadeGrid[index];
                if (fade <= 0f) continue;

                float thicket = thicketGrid[index];
                float density = (BaseInstancesPerCell +
                                 (ThicketPeakInstances - BaseInstancesPerCell) * thicket * bonusScale) * fade;
                int target = Mathf.Clamp(Mathf.RoundToInt(density), 0, MaxInstancesPerCell);
                if (target <= 0) continue;

                // Rotated cumulative rounding with a random phase: every cell gets exactly
                // `target` instances, and each prototype appears with its true weight even
                // at a 3-per-cell carpet (no species is lost to rounding).
                int rotation = Mathf.Clamp((int)(Hash01(x, z) * prototypeCount), 0, prototypeCount - 1);
                float accumulator = Hash01(x * 7 + 13, z * 11 + 3);
                int assigned = 0;
                for (int k = 0; k < prototypeCount; k++)
                {
                    int p = rotation + k;
                    if (p >= prototypeCount) p -= prototypeCount;
                    accumulator += target * Mathf.Lerp(meadowWeights[p], thicketWeights[p], thicket);
                    int upTo = Mathf.Min(target, Mathf.FloorToInt(accumulator));
                    int count = upTo - assigned;
                    assigned = upTo;
                    if (count <= 0) continue;

                    layerData[p][index] = count;
                    perLayerTotals[p] += count;
                    totalInstances += count;
                }

                coveredCells++;
                if (target > maxDensity) maxDensity = target;
            }
        }

        // --- 6. write the layers back --------------------------------------------
        for (int layer = 0; layer < prototypeCount; layer++)
        {
            int[] grid = layerData[layer];
            for (int rowBase = 0; rowBase < dh; rowBase += DetailBlockRows)
            {
                int rows = Mathf.Min(DetailBlockRows, dh - rowBase);
                var chunk = new int[rows, dw];
                for (int r = 0; r < rows; r++)
                    for (int x = 0; x < dw; x++)
                        chunk[r, x] = grid[(rowBase + r) * dw + x];
                data.SetDetailLayer(0, rowBase, layer, chunk);
            }
        }

        EditorUtility.SetDirty(data);
        AssetDatabase.SaveAssets();

        var report = new System.Text.StringBuilder();
        report.Append("[RoadsideGrass] generated ").Append(totalInstances)
              .Append(" instances in ").Append(coveredCells)
              .Append(" cells (").Append((100f * coveredCells / (dw * dh)).ToString("F1"))
              .Append("% of map), carpet ").Append(BaseInstancesPerCell)
              .Append("/cell, peak ").Append(maxDensity).Append(" per cell, budget ")
              .Append(MaxTotalInstances).Append(": ");
        for (int layer = 0; layer < prototypeCount; layer++)
        {
            string name = data.detailPrototypes[layer].prototype != null
                ? data.detailPrototypes[layer].prototype.name
                : "layer" + layer;
            report.Append(name).Append('=').Append(perLayerTotals[layer]);
            if (layer < prototypeCount - 1) report.Append(", ");
        }
        if (totalInstances < MinTotalInstances)
            report.Append(" [WARNING: below the ").Append(MinTotalInstances).Append(" target]");
        Debug.Log(report.ToString());
    }

    // Rounded instance total for a given thicket bonus scale (1 = full thickets).
    static long EstimateTotalInstances(float[] fadeGrid, float[] thicketGrid, int count, float bonusScale)
    {
        long total = 0;
        for (int i = 0; i < count; i++)
        {
            float fade = fadeGrid[i];
            if (fade <= 0f) continue;
            float density = (BaseInstancesPerCell +
                             (ThicketPeakInstances - BaseInstancesPerCell) * thicketGrid[i] * bonusScale) * fade;
            total += Mathf.Clamp(Mathf.RoundToInt(density), 0, MaxInstancesPerCell);
        }
        return total;
    }

    [MenuItem("Tools/DroneSimulator/Grass Detail/4. Report Detail Layer Stats", priority = 3)]
    public static void ReportStats()
    {
        Terrain terrain = FindTerrain();
        if (terrain == null) return;
        TerrainData data = terrain.terrainData;

        int dw = data.detailWidth, dh = data.detailHeight;
        var report = new System.Text.StringBuilder();
        report.Append("[RoadsideGrass] report ").Append(dw).Append('x').Append(dh)
              .Append(" scatter=").Append(data.detailScatterMode).Append('\n');

        long grand = 0;
        for (int layer = 0; layer < data.detailPrototypes.Length; layer++)
        {
            int[,] cells = data.GetDetailLayer(0, 0, dw, dh, layer);
            long sum = 0;
            int max = 0;
            int cellsWithGrass = 0;
            for (int z = 0; z < dh; z++)
            {
                for (int x = 0; x < dw; x++)
                {
                    int value = cells[z, x];
                    sum += value;
                    if (value > max) max = value;
                    if (value > 0) cellsWithGrass++;
                }
            }
            grand += sum;
            string name = data.detailPrototypes[layer].prototype != null
                ? data.detailPrototypes[layer].prototype.name
                : "layer" + layer;
            report.Append("  ").Append(layer).Append(' ').Append(name)
                  .Append(" total=").Append(sum)
                  .Append(" cells=").Append(cellsWithGrass)
                  .Append(" max/cell=").Append(max).Append('\n');
        }
        report.Append("  GRAND TOTAL=").Append(grand);
        Debug.Log(report.ToString());
    }

    [MenuItem("Tools/DroneSimulator/Grass Detail/5. Clear All Detail Layers", priority = 4)]
    public static void ClearAll()
    {
        Terrain terrain = FindTerrain();
        if (terrain == null) return;
        TerrainData data = terrain.terrainData;

        Undo.RegisterCompleteObjectUndo(data, "Clear grass detail layers");
        ClearDetailLayers(data);
        EditorUtility.SetDirty(data);
        AssetDatabase.SaveAssets();
        Debug.Log("[RoadsideGrass] all detail layers cleared.");
    }

    [MenuItem("Tools/DroneSimulator/Grass Detail/6. Capture Verification Screenshots", priority = 5)]
    public static void CaptureVerificationShots()
    {
        // 1) drone flight over the road junction, 2) drone flight over a forest belt / field border
        CaptureShot("check_1_drone_75m", new Vector3(-15f, 88f, 1185f), new Vector3(-45f, 13f, 1090f), 55f);
        CaptureShot("check_2_belt_70m", new Vector3(430f, 83f, 1330f), new Vector3(520f, 13f, 1420f), 60f);
        Debug.Log("[RoadsideGrass] verification screenshots captured.");
    }

    static void CaptureShot(string fileName, Vector3 position, Vector3 target, float fov)
    {
        const int width = 640;
        const int height = 360;
        var go = new GameObject("~GrassShotCamera");
        var cam = go.AddComponent<Camera>();
        cam.fieldOfView = fov;
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 3000f;
        cam.clearFlags = CameraClearFlags.Skybox;
        go.transform.position = position;
        go.transform.rotation = Quaternion.LookRotation((target - position).normalized, Vector3.up);

        var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        rt.Create();
        cam.targetTexture = rt;
        cam.Render();

        RenderTexture.active = rt;
        var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        tex.Apply();
        RenderTexture.active = null;

        string dir = "Assets/Debug/Screenshots";
        System.IO.Directory.CreateDirectory(dir);
        string filePath = System.IO.Path.Combine(dir, fileName + ".jpg");
        System.IO.File.WriteAllBytes(filePath, tex.EncodeToJPG(92));

        cam.targetTexture = null;
        Object.DestroyImmediate(tex);
        Object.DestroyImmediate(go);
        rt.Release();
        Object.DestroyImmediate(rt);
        Debug.Log("[RoadsideGrass] captured " + dir + "/" + fileName);
    }

    // ============================================================================
    //  Helpers
    // ============================================================================
    static Terrain FindTerrain()
    {
        var go = GameObject.Find(TerrainObjectName);
        if (go == null)
        {
            Debug.LogError("[RoadsideGrass] '" + TerrainObjectName + "' object was not found in the open scene.");
            return null;
        }
        var terrain = go.GetComponent<Terrain>();
        if (terrain == null)
            Debug.LogError("[RoadsideGrass] '" + TerrainObjectName + "' has no Terrain component.");
        return terrain;
    }

    static void ClearDetailLayers(TerrainData data)
    {
        int dw = data.detailWidth, dh = data.detailHeight;
        var empty = new int[dh, dw];
        for (int layer = 0; layer < data.detailPrototypes.Length; layer++)
            data.SetDetailLayer(0, 0, layer, empty);
    }

    static float Hash01(int x, int y)
    {
        unchecked
        {
            uint h = (uint)(x * 73856093) ^ (uint)(y * 19349663);
            h ^= h >> 13;
            h *= 0x5bd1e995;
            h ^= h >> 15;
            return (h & 0xFFFFFF) / 16777216f;
        }
    }

    // Rasterises the painted road (Pebbles layer) plus a corridor around the road splines
    // and turns the mask into a distance-to-road field measured in metres.
    static DistanceField BuildRoadDistanceField(Terrain terrain)
    {
        TerrainData data = terrain.terrainData;
        Vector3 origin = terrain.transform.position;
        int w = data.alphamapWidth;
        int h = data.alphamapHeight;
        float cellX = data.size.x / w;
        float cellZ = data.size.z / h;

        float[,,] maps = data.GetAlphamaps(0, 0, w, h);
        var mask = new bool[w * h];
        int painted = 0;
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                bool road = maps[y, x, RoadLayerIndex] > 0.2f;
                mask[y * w + x] = road;
                if (road) painted++;
            }
        }

        int radiusX = Mathf.Max(1, Mathf.CeilToInt(SplineRoadRadius / cellX));
        int radiusZ = Mathf.Max(1, Mathf.CeilToInt(SplineRoadRadius / cellZ));
        var containers = Object.FindObjectsByType<SplineContainer>(FindObjectsInactive.Include);
        int stamped = 0;
        foreach (SplineContainer container in containers)
        {
            if (!container.gameObject.scene.IsValid()) continue;
            foreach (Spline spline in container.Splines)
            {
                const int steps = 400;
                for (int i = 0; i <= steps; i++)
                {
                    float3 point, tangent, up;
                    SplineUtility.Evaluate(spline, (float)i / steps, out point, out tangent, out up);
                    Vector3 world = container.transform.TransformPoint((Vector3)point);
                    int cx = Mathf.RoundToInt((world.x - origin.x) / cellX);
                    int cz = Mathf.RoundToInt((world.z - origin.z) / cellZ);
                    for (int dz = -radiusZ; dz <= radiusZ; dz++)
                    {
                        int z = cz + dz;
                        if (z < 0 || z >= h) continue;
                        for (int dx = -radiusX; dx <= radiusX; dx++)
                        {
                            int x = cx + dx;
                            if (x < 0 || x >= w) continue;
                            float nx = dx * cellX / SplineRoadRadius;
                            float nz = dz * cellZ / SplineRoadRadius;
                            if (nx * nx + nz * nz > 1f) continue;
                            mask[z * w + x] = true;
                        }
                    }
                    stamped++;
                }
            }
        }

        float[] distance = ChamferDistance(mask, w, h, Mathf.Min(cellX, cellZ));
        Blur(distance, w, h, 2);
        Debug.Log("[RoadsideGrass] road mask: painted cells=" + painted + ", spline samples=" + stamped +
                  ", alpha cell=" + cellX.ToString("F2") + " m");
        return new DistanceField(distance, w, h);
    }

    // Two-pass chamfer distance transform (approximate Euclidean). Half a cell is subtracted
    // so the result approximates the distance to the painted border, not to a cell centre.
    static float[] ChamferDistance(bool[] mask, int w, int h, float cellSize)
    {
        const float D1 = 1f;
        const float D2 = 1.4142136f;
        var d = new float[w * h];
        for (int i = 0; i < d.Length; i++) d[i] = mask[i] ? 0f : 1e9f;

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int i = y * w + x;
                float v = d[i];
                if (x > 0) v = Mathf.Min(v, d[i - 1] + D1);
                if (y > 0) v = Mathf.Min(v, d[i - w] + D1);
                if (x > 0 && y > 0) v = Mathf.Min(v, d[i - w - 1] + D2);
                if (x < w - 1 && y > 0) v = Mathf.Min(v, d[i - w + 1] + D2);
                d[i] = v;
            }
        }
        for (int y = h - 1; y >= 0; y--)
        {
            for (int x = w - 1; x >= 0; x--)
            {
                int i = y * w + x;
                float v = d[i];
                if (x < w - 1) v = Mathf.Min(v, d[i + 1] + D1);
                if (y < h - 1) v = Mathf.Min(v, d[i + w] + D1);
                if (x < w - 1 && y < h - 1) v = Mathf.Min(v, d[i + w + 1] + D2);
                if (x > 0 && y < h - 1) v = Mathf.Min(v, d[i + w - 1] + D2);
                d[i] = v;
            }
        }

        float bias = cellSize * 0.5f;
        for (int i = 0; i < d.Length; i++) d[i] = Mathf.Max(0f, d[i] * cellSize - bias);
        return d;
    }

    static void Blur(float[] values, int w, int h, int passes)
    {
        var temp = new float[values.Length];
        for (int pass = 0; pass < passes; pass++)
        {
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float sum = 0f;
                    int count = 0;
                    for (int dz = -1; dz <= 1; dz++)
                    {
                        int z = y + dz;
                        if (z < 0 || z >= h) continue;
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int xx = x + dx;
                            if (xx < 0 || xx >= w) continue;
                            sum += values[z * w + xx];
                            count++;
                        }
                    }
                    temp[y * w + x] = sum / count;
                }
            }
            System.Array.Copy(temp, values, values.Length);
        }
    }

    // One terrain-layer weight map, sampled in normalized terrain space.
    sealed class AlphaField
    {
        readonly float[] values;
        readonly int width;
        readonly int height;

        public AlphaField(TerrainData data, int layer)
        {
            width = data.alphamapWidth;
            height = data.alphamapHeight;
            float[,,] maps = data.GetAlphamaps(0, 0, width, height);
            values = new float[width * height];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    values[y * width + x] = maps[y, x, layer];
        }

        public float Sample(float u, float v)
        {
            float fx = Mathf.Clamp(u * (width - 1), 0f, width - 1.001f);
            float fy = Mathf.Clamp(v * (height - 1), 0f, height - 1.001f);
            int x0 = (int)fx;
            int y0 = (int)fy;
            int x1 = Mathf.Min(x0 + 1, width - 1);
            int y1 = Mathf.Min(y0 + 1, height - 1);
            float tx = fx - x0;
            float ty = fy - y0;
            float a = Mathf.Lerp(values[y0 * width + x0], values[y0 * width + x1], tx);
            float b = Mathf.Lerp(values[y1 * width + x0], values[y1 * width + x1], tx);
            return Mathf.Lerp(a, b, ty);
        }

        // Max of the four surrounding cells - used for hard "do not grow here" tests.
        public float SampleMax(float u, float v)
        {
            float fx = Mathf.Clamp(u * (width - 1), 0f, width - 1.001f);
            float fy = Mathf.Clamp(v * (height - 1), 0f, height - 1.001f);
            int x0 = (int)fx;
            int y0 = (int)fy;
            int x1 = Mathf.Min(x0 + 1, width - 1);
            int y1 = Mathf.Min(y0 + 1, height - 1);
            float m = values[y0 * width + x0];
            m = Mathf.Max(m, values[y0 * width + x1]);
            m = Mathf.Max(m, values[y1 * width + x0]);
            m = Mathf.Max(m, values[y1 * width + x1]);
            return m;
        }
    }

    // Distance (metres) to the road corridor, bilinearly sampled in normalized terrain space.
    sealed class DistanceField
    {
        readonly float[] values;
        readonly int width;
        readonly int height;

        public DistanceField(float[] data, int width, int height)
        {
            values = data;
            this.width = width;
            this.height = height;
        }

        public float Sample(float u, float v)
        {
            float fx = Mathf.Clamp(u * (width - 1), 0f, width - 1.001f);
            float fy = Mathf.Clamp(v * (height - 1), 0f, height - 1.001f);
            int x0 = (int)fx;
            int y0 = (int)fy;
            int x1 = Mathf.Min(x0 + 1, width - 1);
            int y1 = Mathf.Min(y0 + 1, height - 1);
            float tx = fx - x0;
            float ty = fy - y0;
            float a = Mathf.Lerp(values[y0 * width + x0], values[y0 * width + x1], tx);
            float b = Mathf.Lerp(values[y1 * width + x0], values[y1 * width + x1], tx);
            return Mathf.Lerp(a, b, ty);
        }
    }
}
