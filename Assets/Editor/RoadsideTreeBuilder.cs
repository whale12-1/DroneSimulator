using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Authoring tool for the terrain tree instances (roadside forest belts).
// Rebuilds treeInstances on the main "Terrain" only, reusing the terrain-layer masks
// from RoadsideGrassBuilder:
//   * Pebbles_A_TerrainLayer (road surface) -> hard 5.5 m clearance, no trees at all;
//   * NewLayer 3 / farm_soil (arable fields) -> no trees above 0.15 farm alpha,
//     with a noise-warped, ragged field border;
//   * everything else -> a roadside belt from 5.5 m to a Perlin-warped 16..26 m
//     (1-2 rows in narrow stretches, 4-5 in wide ones, meadow breaks between)
//     plus at most 8% sparse far-field roosts. All 3 terrain tree prototypes are
//     used with noise-clustered species.
// Menu: Tools/DroneSimulator/Trees/Generate Roadside Trees
public static class RoadsideTreeBuilder
{
    const string TerrainObjectName = "Terrain";
    const string UndoLabel = "Rebuild roadside trees";
    const string LogPrefix = "[RoadsideTrees] ";

    // ---- terrain layers (resolved by name, indices kept as a fallback) ---------
    const string RoadLayerName = "Pebbles_A_TerrainLayer";
    const string FarmLayerName = "NewLayer 3";        // farm_soil == ploughed fields
    const int RoadLayerFallback = 10;
    const int FarmLayerFallback = 6;
    const float RoadAlphaClear = 0.10f;               // any painted road weight above this is road
    const float FarmAlphaClear = 0.15f;               // hard farm_soil limit

    // ---- road corridor ---------------------------------------------------------
    const float RoadClearance = 5.5f;     // hard inner offset from the painted road edge

    // ---- roadside belt shape ---------------------------------------------------
    // Trees only grow in a band from 5.5 m to a Perlin-warped 16..26 m outer edge,
    // which reads as 1-2 rows in narrow stretches and 4-5 rows in wide ones.
    // NOTE: the whole 2000 m road network only offers ~155,000 m2 of such a band,
    // so at 5 m spacing the map physically tops out near 4,000 trunks. Raise
    // BeltOuterMax (or lower MinTreeDistance) if a higher total is required.
    const float BeltOuterMin = 16f;
    const float BeltOuterMax = 26f;
    const float BeltWidthNoiseScale = 0.008f;
    const float BeltPresenceNoiseScale = 0.006f;    // ~167 m wavelength (120-200 m band)
    const float BeltPresenceDetailScale = 0.018f;
    const float BeltPresenceStart = 0.34f;          // below this: open grass meadow
    const float BeltPresenceEnd = 0.58f;
    const float BeltThinningFloor = 0.55f;          // keeps accepted belts hole-free

    // ---- sparse far-field roosts ----------------------------------------------
    const float DistantMinDistance = 32f;           // stands > 28 m from the road edge
    const float DistantTreeShare = 0.08f;           // <= 8% of the final tree total
    const float RoostNoiseScale = 0.045f;           // small 3..6 tree groves
    const float RoostNoiseStart = 0.48f;
    const float RoostNoiseEnd = 0.70f;

    // ---- tree distribution -----------------------------------------------------
    const int TargetTreeCount = 9500;     // requested upper limit
    const float MinTreeDistance = 5.0f;   // Poisson disk radius (5.0..6.0 m target)
    const float PoissonRadiusJitter = 0.5f; // candidate distance is d..1.5d
    const int PoissonAttempts = 30;
    const float EdgeMargin = 3.0f;        // keep trees away from the terrain border

    // ---- species selection (coarse + mesoscale Perlin + local random) ----------
    const float SpeciesCoarseScale = 0.0065f;  // ~150 m regions dominated by one species
    const float SpeciesPatchScale = 0.050f;    // ~20 m clumps of 3..6 trees
    const float SpeciesJitterMin = 0.70f;
    const float SpeciesJitterMax = 1.30f;

    // ---- ragged field border ---------------------------------------------------
    const float FieldEdgeNoiseScale = 0.035f;
    const float FieldEdgeNoiseMin = 0.02f;     // forest recedes here, never above 0.15

    // ---- per-tree variation ----------------------------------------------------
    const float HeightScaleMin = 0.72f;
    const float HeightScaleMax = 1.32f;
    const float WidthScaleMin = 0.88f;
    const float WidthScaleMax = 1.12f;
    const float ColorMin = 0.85f;
    const float ColorMax = 1.0f;

    const int RandomSeed = 20261119;

    // Balanced mix of the three terrain tree prototypes (Conifer / Cypress / Pine_B).
    static readonly float[] SpeciesProportions = { 0.45f, 0.35f, 0.20f };

    // ============================================================================
    //  Menu
    // ============================================================================
    [MenuItem("Tools/DroneSimulator/Trees/Generate Roadside Trees", priority = 0)]
    public static void Generate()
    {
        Terrain terrain = FindTerrain();
        if (terrain == null) return;

        TerrainData data = terrain.terrainData;
        if (data == null)
        {
            Debug.LogError(LogPrefix + "the Terrain has no TerrainData.");
            return;
        }

        TreePrototype[] prototypes = data.treePrototypes;
        if (prototypes == null || prototypes.Length == 0)
        {
            Debug.LogError(LogPrefix + "treePrototypes is empty - nothing to plant.");
            return;
        }

        Vector3 origin = terrain.transform.position;
        Vector3 size = data.size;
        if (size.x <= 0f || size.z <= 0f)
        {
            Debug.LogError(LogPrefix + "the terrain has a degenerate size " + size + ".");
            return;
        }

        int roadLayer = ResolveLayer(data, RoadLayerName, RoadLayerFallback);
        int farmLayer = ResolveLayer(data, FarmLayerName, FarmLayerFallback);
        float[,,] alphamaps = data.GetAlphamaps(0, 0, data.alphamapWidth, data.alphamapHeight);

        int previousCount = data.treeInstanceCount;
        Undo.RegisterCompleteObjectUndo(data, UndoLabel);
        Undo.RegisterCompleteObjectUndo(terrain, UndoLabel);

        // 1. wipe the previous instances so the rebuild is deterministic ----------
        data.SetTreeInstances(new TreeInstance[0], false);

        // 2. road corridor + planting mask ----------------------------------------
        DistanceField roadDistance = BuildRoadDistanceField(terrain, roadLayer, alphamaps);
        var mask = new PlantingMask(origin, size,
            new AlphaField(data, alphamaps, roadLayer),
            new AlphaField(data, alphamaps, farmLayer),
            roadDistance);

        // 3. two independent Poisson runs: the belt (5.5..26 m) and the far field
        //    (32+ m) are separated by a bare moat, so they are disconnected
        //    habitats and one Bridson pass cannot fill both.
        var rng = new System.Random(RandomSeed);
        List<Vector2> beltPoints = BuildPoissonPoints(origin, size, mask.IsBeltPlantable, rng);
        List<Vector2> distantPoints = BuildPoissonPoints(origin, size, mask.IsDistantPlantable, rng);
        int candidateCount = beltPoints.Count + distantPoints.Count;
        if (candidateCount == 0)
        {
            Debug.LogError(LogPrefix + "no plantable spot was found - check the terrain layers.");
            return;
        }

        // 4. fill the belts first, then add at most 8% far-field roosts ------------
        int beltTarget = Mathf.Min(TargetTreeCount, beltPoints.Count);
        int maxDistant = Mathf.FloorToInt(
            beltTarget * DistantTreeShare / Mathf.Max(0.0001f, 1f - DistantTreeShare));
        int distantTarget = Mathf.Min(distantPoints.Count,
            Mathf.Min(maxDistant, Mathf.Max(0, TargetTreeCount - beltTarget)));

        List<Vector2> beltChosen = ThinToTarget(beltPoints, beltTarget, rng, BeltThinningWeight);
        List<Vector2> distantChosen = ThinToTarget(distantPoints, distantTarget, rng, RoostThinningWeight);
        distantChosen = RemoveCloseTo(distantChosen, beltChosen, MinTreeDistance);

        var planted = new List<Vector2>(beltChosen.Count + distantChosen.Count);
        planted.AddRange(beltChosen);
        planted.AddRange(distantChosen);

        if (planted.Count == 0)
        {
            Debug.LogError(LogPrefix + "no candidate survived the belt/roost budgets.");
            return;
        }

        // 6. build the TreeInstance array -----------------------------------------
        float[] proportions = BuildProportions(prototypes.Length);
        var speciesWeights = new float[proportions.Length];
        var instances = new TreeInstance[planted.Count];
        var perPrototype = new int[proportions.Length];
        var heightSum = new double[proportions.Length];

        for (int i = 0; i < planted.Count; i++)
        {
            Vector2 world = planted[i];
            float u = (world.x - origin.x) / size.x;
            float v = (world.y - origin.z) / size.z;
            float height = data.GetInterpolatedHeight(u, v);

            int prototype = PickPrototype(world, proportions, perPrototype, i, speciesWeights, rng);
            float heightScale = RandomRange(rng, HeightScaleMin, HeightScaleMax);
            float widthScale = heightScale * RandomRange(rng, WidthScaleMin, WidthScaleMax);

            instances[i] = new TreeInstance
            {
                position = new Vector3(u, Mathf.Clamp01(height / size.y), v),
                rotation = (float)(rng.NextDouble() * System.Math.PI * 2.0),
                heightScale = heightScale,
                widthScale = widthScale,
                color = CanopyColor(rng),
                lightmapColor = CanopyColor(rng),
                prototypeIndex = prototype,
            };

            perPrototype[prototype]++;
            heightSum[prototype] += heightScale;
        }

        // 7. write, save and report ----------------------------------------------
        data.SetTreeInstances(instances, true);
        EditorUtility.SetDirty(data);
        EditorUtility.SetDirty(terrain);

        Scene scene = terrain.gameObject.scene;
        if (scene.IsValid())
        {
            EditorSceneManager.MarkSceneDirty(scene);
            if (!string.IsNullOrEmpty(scene.path))
                EditorSceneManager.SaveScene(scene);
        }
        AssetDatabase.SaveAssets();

        Report(prototypes, instances.Length, previousCount, candidateCount,
            beltChosen.Count, distantChosen.Count, perPrototype, heightSum);
    }

    static void Report(TreePrototype[] prototypes, int total, int previousCount, int candidates,
        int beltCount, int distantCount, int[] perPrototype, double[] heightSum)
    {
        var report = new System.Text.StringBuilder();
        report.Append(LogPrefix).Append("generated ").Append(total)
              .Append(" trees (previous ").Append(previousCount)
              .Append("), belt=").Append(beltCount)
              .Append(", far-field roosts=").Append(distantCount)
              .Append(", Poisson candidates ").Append(candidates)
              .Append(", min spacing ").Append(MinTreeDistance.ToString("F1")).Append(" m")
              .Append(", belt 5.5..16-26 m\n");

        for (int p = 0; p < prototypes.Length; p++)
        {
            string name = prototypes[p].prefab != null ? prototypes[p].prefab.name : "prototype" + p;
            float share = total > 0 ? 100f * perPrototype[p] / total : 0f;
            float avgHeight = perPrototype[p] > 0 ? (float)(heightSum[p] / perPrototype[p]) : 0f;
            report.Append("  [").Append(p).Append("] ").Append(name)
                  .Append(" = ").Append(perPrototype[p])
                  .Append(" (").Append(share.ToString("F1")).Append("%)")
                  .Append(", avg heightScale ").Append(avgHeight.ToString("F2")).Append('\n');
        }

        if (total > 0)
            report.Append("  far-field share ").Append((100f * distantCount / total).ToString("F1"))
                  .Append("% (limit ").Append((DistantTreeShare * 100f).ToString("F0")).Append("%)\n");

        if (total < TargetTreeCount)
            report.Append("  [INFO] the 5.5..26 m belt holds ").Append(beltCount)
                  .Append(" trunks at ").Append(MinTreeDistance.ToString("F1"))
                  .Append(" m spacing; with ").Append(distantCount).Append(" roosts the total is ")
                  .Append(total).Append(" (the ").Append(TargetTreeCount)
                  .Append(" target is used as an upper cap).\n");
        if (total > 0 && distantCount > total * 0.10f)
            report.Append("  [WARNING] far-field roosts exceed 10% of the total.\n");

        Debug.Log(report.ToString());
    }

    // ============================================================================
    //  Species + variability
    // ============================================================================
    static float[] BuildProportions(int prototypeCount)
    {
        var proportions = new float[prototypeCount];
        if (prototypeCount <= 0) return proportions;

        float total = 0f;
        bool useMix = prototypeCount == SpeciesProportions.Length;
        for (int i = 0; i < prototypeCount; i++)
        {
            proportions[i] = useMix ? SpeciesProportions[i] : 1f;
            total += proportions[i];
        }
        for (int i = 0; i < prototypeCount; i++) proportions[i] /= total;
        return proportions;
    }

    // Coarse Perlin picks the region's dominant species, the mesoscale Perlin forms
    // 3..6 tree clumps and the local jitter scatters single "intruder" trees, so the
    // belt never reads as one uniform species carpet. A light quota correction keeps
    // the final shares on 45/35/20 without erasing the clusters.
    static int PickPrototype(Vector2 world, float[] proportions, int[] placedCounts,
        int placedTotal, float[] weights, System.Random rng)
    {
        float total = 0f;
        for (int i = 0; i < proportions.Length; i++)
        {
            float coarse = Mathf.PerlinNoise(
                world.x * SpeciesCoarseScale + i * 41.7f + 11.3f,
                world.y * SpeciesCoarseScale + i * 67.9f + 5.7f);
            float patch = Mathf.PerlinNoise(
                world.x * SpeciesPatchScale + i * 23.1f + 91.7f,
                world.y * SpeciesPatchScale + i * 51.3f + 47.9f);
            float jitter = RandomRange(rng, SpeciesJitterMin, SpeciesJitterMax);

            float quota = 1f;
            if (placedTotal >= 200)
            {
                float actual = placedCounts[i] / (float)placedTotal;
                quota = Mathf.Clamp(1f + 6f * (proportions[i] - actual), 0.5f, 2f);
            }

            float weight = proportions[i] * (0.10f + 1.90f * coarse) *
                           (0.35f + 1.30f * patch) * jitter * quota;
            weights[i] = weight;
            total += weight;
        }

        float roll = (float)rng.NextDouble() * total;
        for (int i = 0; i < weights.Length; i++)
        {
            roll -= weights[i];
            if (roll <= 0f) return i;
        }
        return weights.Length - 1;
    }

    // Slight brightness + hue jitter keeps canopies from merging into one flat mass.
    static Color CanopyColor(System.Random rng)
    {
        float brightness = RandomRange(rng, ColorMin, ColorMax);
        float r = Mathf.Clamp(brightness * RandomRange(rng, 0.97f, 1.03f), ColorMin, ColorMax);
        float g = Mathf.Clamp(brightness * RandomRange(rng, 0.97f, 1.03f), ColorMin, ColorMax);
        float b = Mathf.Clamp(brightness * RandomRange(rng, 0.97f, 1.03f), ColorMin, ColorMax);
        return new Color(r, g, b, 1f);
    }

    static float RandomRange(System.Random rng, float min, float max)
    {
        return min + (float)rng.NextDouble() * (max - min);
    }

    // ============================================================================
    //  Belt shape + density masks
    // ============================================================================
    // Outer edge of the belt: 16 m in narrow stretches, 26 m in wide ones.
    static float BeltOuter(Vector2 world)
    {
        float n = Mathf.PerlinNoise(
            world.x * BeltWidthNoiseScale + 17.3f,
            world.y * BeltWidthNoiseScale + 5.9f);
        return Mathf.Lerp(BeltOuterMin, BeltOuterMax, n);
    }

    // Large-scale presence mask (120-200 m wavelength). 0 means an open meadow
    // break in the belt, 1 means the belt runs at full strength.
    static float BeltPresence(Vector2 world)
    {
        float big = Mathf.PerlinNoise(
            world.x * BeltPresenceNoiseScale + 73.1f,
            world.y * BeltPresenceNoiseScale + 29.7f);
        float detail = Mathf.PerlinNoise(
            world.x * BeltPresenceDetailScale + 141.7f,
            world.y * BeltPresenceDetailScale + 97.3f);
        float noise = big * 0.78f + detail * 0.22f;
        return Mathf.SmoothStep(0f, 1f,
            Mathf.InverseLerp(BeltPresenceStart, BeltPresenceEnd, noise));
    }

    // Within an existing belt the density stays high (0.55..1) so the strip has
    // no internal holes; only the meadow breaks remove trees completely.
    static float BeltThinningWeight(Vector2 world)
    {
        return Mathf.Lerp(BeltThinningFloor, 1f, BeltPresence(world));
    }

    // Far-field trees are grouped into small groves: most candidates get a tiny
    // weight, the roost noise peaks get a full one.
    static float RoostThinningWeight(Vector2 world)
    {
        float n = Mathf.PerlinNoise(
            world.x * RoostNoiseScale + 19.7f,
            world.y * RoostNoiseScale + 63.3f);
        float t = Mathf.SmoothStep(0f, 1f,
            Mathf.InverseLerp(RoostNoiseStart, RoostNoiseEnd, n));
        return Mathf.Max(0.001f, t);
    }

    // Picks a density-biased subset of the Poisson points. Keeping the candidates
    // with the lowest hash / weight ratio is equivalent to weighted thinning but
    // hits the budget exactly.
    static List<Vector2> ThinToTarget(List<Vector2> candidates, int target,
        System.Random rng, System.Func<Vector2, float> weight)
    {
        if (target >= candidates.Count) return candidates;
        if (target <= 0) return new List<Vector2>();

        var keys = new float[candidates.Count];
        var order = new int[candidates.Count];
        for (int i = 0; i < candidates.Count; i++)
        {
            keys[i] = (float)rng.NextDouble() / Mathf.Max(0.001f, weight(candidates[i]));
            order[i] = i;
        }
        System.Array.Sort(keys, order);

        var planted = new List<Vector2>(target);
        for (int i = 0; i < target; i++) planted.Add(candidates[order[i]]);
        return planted;
    }

    // The belt and far field are sampled in separate Poisson runs, so a far-field
    // roost could land within the radius of a belt trunk. Drop those few points.
    static List<Vector2> RemoveCloseTo(List<Vector2> points, List<Vector2> reference,
        float minDistance)
    {
        if (points.Count == 0 || reference.Count == 0) return points;

        float sqrMin = minDistance * minDistance;
        var result = new List<Vector2>(points.Count);
        for (int i = 0; i < points.Count; i++)
        {
            Vector2 p = points[i];
            bool tooClose = false;
            for (int j = 0; j < reference.Count; j++)
            {
                Vector2 other = reference[j];
                float dx = other.x - p.x;
                float dz = other.y - p.y;
                if (dx * dx + dz * dz < sqrMin)
                {
                    tooClose = true;
                    break;
                }
            }
            if (!tooClose) result.Add(p);
        }
        return result;
    }

    // ============================================================================
    //  Poisson disk sampling (Bridson) inside one plantable habitat
    // ============================================================================
    static List<Vector2> BuildPoissonPoints(Vector3 origin, Vector3 size,
        System.Func<Vector2, bool> isPlantable, System.Random rng)
    {
        float cell = MinTreeDistance / Mathf.Sqrt(2f);
        int gridW = Mathf.Max(1, Mathf.CeilToInt(size.x / cell));
        int gridH = Mathf.Max(1, Mathf.CeilToInt(size.z / cell));
        var grid = new int[gridW * gridH];
        for (int i = 0; i < grid.Length; i++) grid[i] = -1;

        // Candidate sets stay in the thousands; the capacity merely avoids repeated
        // reallocations before the thinning step.
        var points = new List<Vector2>(TargetTreeCount * 4);
        var active = new List<int>(TargetTreeCount);

        // The mask is not connected: meadow breaks cut the belt into separate
        // segments and fields cut the far field into islands. A plain Bridson pass
        // fills only the component of its first seed, so keep restarting from a
        // coarse seed scan until every segment has been filled.
        const float seedStep = 8f;
        int seedCols = Mathf.Max(1, Mathf.CeilToInt(size.x / seedStep));
        int seedRows = Mathf.Max(1, Mathf.CeilToInt(size.z / seedStep));
        int seedCount = seedCols * seedRows;
        int seedCursor = 0;

        const int maxPoints = 200000; // safety cap, the 2000 m map tops out below it
        while (points.Count < maxPoints)
        {
            // 1. grow the current component until its active list is exhausted ----
            while (active.Count > 0 && points.Count < maxPoints)
            {
                int activeIndex = rng.Next(active.Count);
                Vector2 basePoint = points[active[activeIndex]];
                bool accepted = false;

                for (int attempt = 0; attempt < PoissonAttempts; attempt++)
                {
                    float angle = (float)(rng.NextDouble() * System.Math.PI * 2.0);
                    float radius = MinTreeDistance *
                        (1f + PoissonRadiusJitter * (float)rng.NextDouble());
                    var candidate = new Vector2(
                        basePoint.x + Mathf.Cos(angle) * radius,
                        basePoint.y + Mathf.Sin(angle) * radius);

                    if (!InBounds(candidate, origin, size)) continue;
                    if (!isPlantable(candidate)) continue;
                    if (HasNeighbor(points, grid, gridW, gridH, cell, origin, candidate)) continue;

                    points.Add(candidate);
                    active.Add(points.Count - 1);
                    StoreGrid(grid, gridW, gridH, cell, origin, candidate, points.Count - 1);
                    accepted = true;
                }

                if (!accepted)
                {
                    // swap-remove keeps the active list O(1) and avoids quadratic stalls
                    active[activeIndex] = active[active.Count - 1];
                    active.RemoveAt(active.Count - 1);
                }
            }

            // 2. seed the next disconnected segment --------------------------------
            bool seeded = false;
            for (int i = 0; i < seedCount; i++)
            {
                int index = seedCursor + i;
                if (index >= seedCount) index -= seedCount;
                int sx = index % seedCols;
                int sz = index / seedCols;

                var seed = new Vector2(
                    origin.x + (sx + 0.25f + 0.5f * (float)rng.NextDouble()) * seedStep,
                    origin.z + (sz + 0.25f + 0.5f * (float)rng.NextDouble()) * seedStep);
                if (!InBounds(seed, origin, size)) continue;
                if (!isPlantable(seed)) continue;
                if (HasNeighbor(points, grid, gridW, gridH, cell, origin, seed)) continue;

                points.Add(seed);
                active.Add(points.Count - 1);
                StoreGrid(grid, gridW, gridH, cell, origin, seed, points.Count - 1);
                seedCursor = index + 1;
                seeded = true;
                break;
            }

            if (!seeded) break;
        }
        return points;
    }

    static bool InBounds(Vector2 point, Vector3 origin, Vector3 size)
    {
        return point.x >= origin.x + EdgeMargin &&
               point.x <= origin.x + size.x - EdgeMargin &&
               point.y >= origin.z + EdgeMargin &&
               point.y <= origin.z + size.z - EdgeMargin;
    }

    static bool HasNeighbor(List<Vector2> points, int[] grid, int gridW, int gridH,
        float cell, Vector3 origin, Vector2 candidate)
    {
        int gx = Mathf.FloorToInt((candidate.x - origin.x) / cell);
        int gz = Mathf.FloorToInt((candidate.y - origin.z) / cell);
        float sqrMin = MinTreeDistance * MinTreeDistance;

        for (int dz = -2; dz <= 2; dz++)
        {
            int z = gz + dz;
            if (z < 0 || z >= gridH) continue;
            for (int dx = -2; dx <= 2; dx++)
            {
                int x = gx + dx;
                if (x < 0 || x >= gridW) continue;

                int index = grid[z * gridW + x];
                if (index < 0) continue;

                Vector2 other = points[index];
                float ddx = other.x - candidate.x;
                float ddz = other.y - candidate.y;
                if (ddx * ddx + ddz * ddz < sqrMin) return true;
            }
        }
        return false;
    }

    static void StoreGrid(int[] grid, int gridW, int gridH, float cell, Vector3 origin,
        Vector2 point, int index)
    {
        int gx = Mathf.Clamp(Mathf.FloorToInt((point.x - origin.x) / cell), 0, gridW - 1);
        int gz = Mathf.Clamp(Mathf.FloorToInt((point.y - origin.z) / cell), 0, gridH - 1);
        grid[gz * gridW + gx] = index;
    }

    // ============================================================================
    //  Road distance mask
    // ============================================================================
    // The belt offset is measured from the actual Pebbles_A carriageway edge, as
    // requested. Sampling the painted layer (rather than the spline corridor) also
    // keeps the 5.5 m shoulder honest where the paint is wider than the spline.
    static DistanceField BuildRoadDistanceField(Terrain terrain, int roadLayer, float[,,] maps)
    {
        TerrainData data = terrain.terrainData;
        int w = data.alphamapWidth;
        int h = data.alphamapHeight;
        float cellX = data.size.x / w;
        float cellZ = data.size.z / h;

        var mask = new bool[w * h];
        int painted = 0;
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                bool road = maps[y, x, roadLayer] > 0.2f;
                mask[y * w + x] = road;
                if (road) painted++;
            }
        }

        // No blur here: for trees the clearance must stay a true minimum. The grass
        // builder blurs its field for a soft density fade, which would let a few
        // trunks creep inside the 5.5 m flight corridor.
        float[] distance = ChamferDistance(mask, w, h, Mathf.Min(cellX, cellZ));
        Debug.Log(LogPrefix + "road mask: painted cells=" + painted +
                  ", alpha cell=" + cellX.ToString("F2") + " m");
        return new DistanceField(distance, w, h);
    }

    // Two-pass chamfer distance transform (approximate Euclidean). Half a cell is
    // subtracted so the result approximates the distance to the painted border.
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

    // ============================================================================
    //  Masks
    // ============================================================================
    // Hard planting rules: road surface + 5 m shoulder, arable fields (farm alpha
    // above a noise-warped 0.02..0.15 limit - never above the hard 0.15 cap).
    sealed class PlantingMask
    {
        readonly Vector3 origin;
        readonly Vector3 size;
        readonly AlphaField roadAlpha;
        readonly AlphaField farmAlpha;
        readonly DistanceField roadDistance;

        public PlantingMask(Vector3 origin, Vector3 size, AlphaField roadAlpha,
            AlphaField farmAlpha, DistanceField roadDistance)
        {
            this.origin = origin;
            this.size = size;
            this.roadAlpha = roadAlpha;
            this.farmAlpha = farmAlpha;
            this.roadDistance = roadDistance;
        }

        // Shared hard rules: no road surface, no arable field, 5.5 m minimum offset.
        // minDistance is the conservative cell-minimum (real clearance), while
        // centerDistance is the interpolated value used for the outer belt edge.
        bool PassesSurfaceRules(Vector2 world, out float minDistance, out float centerDistance)
        {
            float u = (world.x - origin.x) / size.x;
            float v = (world.y - origin.z) / size.z;

            // SampleMin keeps the whole surrounding alpha cell clear, not just the
            // interpolated point, so the 5.5 m offset is a real hard limit.
            if (roadAlpha.SampleMax(u, v) > RoadAlphaClear)
            {
                minDistance = 0f;
                centerDistance = 0f;
                return false;
            }
            minDistance = roadDistance.SampleMin(u, v);
            centerDistance = roadDistance.Sample(u, v);
            if (minDistance < RoadClearance) return false;

            // The limit wanders with Perlin noise for a ragged border, but it is
            // clamped to 0.02..0.15 so farm_soil is never planted.
            float farm = farmAlpha.SampleMax(u, v);
            float edgeNoise = Mathf.PerlinNoise(
                world.x * FieldEdgeNoiseScale + 41.7f,
                world.y * FieldEdgeNoiseScale + 83.1f);
            float farmLimit = Mathf.Lerp(FieldEdgeNoiseMin, FarmAlphaClear, edgeNoise);
            return farm <= farmLimit;
        }

        // Roadside belt: 5.5 m .. Perlin-warped 16..26 m, with meadow breaks from
        // the large-scale presence mask.
        public bool IsBeltPlantable(Vector2 world)
        {
            float minDistance, centerDistance;
            if (!PassesSurfaceRules(world, out minDistance, out centerDistance)) return false;
            if (centerDistance > BeltOuter(world)) return false;
            return BeltPresence(world) > 0f;
        }

        // Far field: open ground beyond 32 m, thinned down to sparse roosts.
        public bool IsDistantPlantable(Vector2 world)
        {
            float minDistance, centerDistance;
            if (!PassesSurfaceRules(world, out minDistance, out centerDistance)) return false;
            return minDistance >= DistantMinDistance;
        }
    }

    // One terrain-layer weight map, sampled in normalized terrain space.
    sealed class AlphaField
    {
        readonly float[] values;
        readonly int width;
        readonly int height;

        public AlphaField(TerrainData data, float[,,] maps, int layer)
        {
            width = data.alphamapWidth;
            height = data.alphamapHeight;
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

    // Distance (metres) to the road corridor, bilinearly sampled in terrain space.
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

        // Min of the four surrounding cells - a conservative clearance test.
        public float SampleMin(float u, float v)
        {
            float fx = Mathf.Clamp(u * (width - 1), 0f, width - 1.001f);
            float fy = Mathf.Clamp(v * (height - 1), 0f, height - 1.001f);
            int x0 = (int)fx;
            int y0 = (int)fy;
            int x1 = Mathf.Min(x0 + 1, width - 1);
            int y1 = Mathf.Min(y0 + 1, height - 1);
            float m = values[y0 * width + x0];
            m = Mathf.Min(m, values[y0 * width + x1]);
            m = Mathf.Min(m, values[y1 * width + x0]);
            m = Mathf.Min(m, values[y1 * width + x1]);
            return m;
        }
    }

    // ============================================================================
    //  Scene helpers
    // ============================================================================
    static Terrain FindTerrain()
    {
        var go = GameObject.Find(TerrainObjectName);
        if (go == null)
        {
            Debug.LogError(LogPrefix + "'" + TerrainObjectName + "' object was not found in the open scene.");
            return null;
        }
        var terrain = go.GetComponent<Terrain>();
        if (terrain == null)
            Debug.LogError(LogPrefix + "'" + TerrainObjectName + "' has no Terrain component.");
        return terrain;
    }

    static int ResolveLayer(TerrainData data, string layerName, int fallback)
    {
        TerrainLayer[] layers = data.terrainLayers;
        if (layers != null)
        {
            for (int i = 0; i < layers.Length; i++)
            {
                if (layers[i] != null &&
                    string.Equals(layers[i].name, layerName, System.StringComparison.OrdinalIgnoreCase))
                    return i;
            }
        }

        int index = Mathf.Clamp(fallback, 0, Mathf.Max(0, (layers != null ? layers.Length : 0) - 1));
        Debug.LogWarning(LogPrefix + "terrain layer '" + layerName +
                         "' was not found, falling back to index " + index + ".");
        return index;
    }
}
