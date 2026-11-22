using UnityEngine;

// CPU generators for the two horizon textures:
//   * Macro map (sRGB): organic mixing of the two real Terrain layer colours - the
//     dark plough soil and the dark olive grass. Grass gathers under/around the
//     silhouette forest, open land stays soil; only a few percent of micro-noise.
//   * Detail map (linear, tileable): multi-octave micro noise used at three world
//     scales by HorizonFields.shader so the far ground never looks "soapy".
public static class HorizonFieldMapGenerator
{
    // The macro map is built ONLY from the two real Terrain layer colours (soil and
    // grass) passed in by the builder - no other crops, no cold grey-green, no black
    // forest patches. The forest silhouette mesh supplies the dark cover instead.
    public static Color32[] CreateFieldMap(
        int size, Vector2 center, float span, int seed, Color soilColor, Color grassColor)
    {
        Color32[] pixels = new Color32[size * size];
        float halfSpan = span * 0.5f;

        // Coarse organic grids: the very same forest score used by the tree placement
        // (so grass sits under and around the silhouette forest), plus a slow meadow
        // mask that adds a few soft grassy patches to the open land.
        const int Grid = 513;
        float[] forest = new float[Grid * Grid];
        float[] meadow = new float[Grid * Grid];
        for (int gy = 0; gy < Grid; gy++)
        {
            float wz = center.y - halfSpan + span * gy / (Grid - 1f);
            for (int gx = 0; gx < Grid; gx++)
            {
                float wx = center.x - halfSpan + span * gx / (Grid - 1f);
                forest[gy * Grid + gx] = ForestScore(wx, wz, seed);
                meadow[gy * Grid + gx] = HorizonNoise.Fbm(wx / 1300f, wz / 1300f, 3, seed + 341);
            }
        }

        float pixelSize = span / size;
        for (int y = 0; y < size; y++)
        {
            float wz = center.y - halfSpan + (y + 0.5f) * pixelSize;
            float gyf = Mathf.Clamp((wz - (center.y - halfSpan)) / span, 0f, 1f) * (Grid - 1);

            for (int x = 0; x < size; x++)
            {
                float wx = center.x - halfSpan + (x + 0.5f) * pixelSize;
                float gxf = Mathf.Clamp((wx - (center.x - halfSpan)) / span, 0f, 1f) * (Grid - 1);

                float forestScore = BilinearGrid(forest, Grid, gxf, gyf);
                float meadowScore = BilinearGrid(meadow, Grid, gxf, gyf);

                // Grass dominates under/around forest masses and soft edges; the open
                // plain stays soil with occasional low-contrast meadow patches.
                float grass = SmoothStep01(Mathf.InverseLerp(0.44f, 0.58f, forestScore));
                float meadowBlend = SmoothStep01(Mathf.InverseLerp(0.54f, 0.66f, meadowScore)) * 0.22f;
                if (meadowBlend > grass)
                {
                    grass = meadowBlend;
                }

                Color color = Color.Lerp(soilColor, grassColor, grass);

                // Only +/-4% local brightness variation here (the shader adds a few
                // more percent), so the ground never turns into dark blotches.
                color *= 0.98f + HorizonNoise.Hash(
                    Mathf.FloorToInt(wx / 17f), Mathf.FloorToInt(wz / 17f), seed + 461) * 0.04f;
                color *= 0.99f + HorizonNoise.Hash(
                    Mathf.FloorToInt(wx / 43f), Mathf.FloorToInt(wz / 43f), seed + 463) * 0.02f;

                pixels[y * size + x] = color;
            }
        }

        return pixels;
    }

    // Must match HorizonMeshGenerator.OrganicScore so the grass patches sit exactly
    // under the silhouette forest belts and their edges.
    static float ForestScore(float x, float z, int seed)
    {
        float cluster = HorizonNoise.Fbm(x / 620f, z / 620f, 3, seed + 201);
        float belt = HorizonNoise.Fbm(x / 2500f, z / 820f, 3, seed + 211);
        return cluster * 0.62f + belt * 0.38f;
    }

    static float BilinearGrid(float[] grid, int resolution, float x, float y)
    {
        int x0 = Mathf.Min(Mathf.FloorToInt(x), resolution - 2);
        int y0 = Mathf.Min(Mathf.FloorToInt(y), resolution - 2);
        x0 = Mathf.Max(x0, 0);
        y0 = Mathf.Max(y0, 0);
        float fx = x - x0;
        float fy = y - y0;
        float bottom = Mathf.Lerp(grid[y0 * resolution + x0], grid[y0 * resolution + x0 + 1], fx);
        float top = Mathf.Lerp(grid[(y0 + 1) * resolution + x0], grid[(y0 + 1) * resolution + x0 + 1], fx);
        return Mathf.Lerp(bottom, top, fy);
    }

    static float SmoothStep01(float value)
    {
        float t = Mathf.Clamp01(value);
        return t * t * (3f - 2f * t);
    }



    public static Color32[] CreateDetailMap(int size, int seed)
    {
        Color32[] pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            float v = (y + 0.5f) / size;
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size;

                float noise = 0.5f;
                noise += (HorizonNoise.FbmPeriodic(u * 4f, v * 4f, 4, 3, seed + 601) - 0.5f) * 0.55f;
                noise += (HorizonNoise.FbmPeriodic(u * 9f, v * 9f, 9, 3, seed + 607) - 0.5f) * 0.30f;
                noise += (HorizonNoise.FbmPeriodic(u * 23f, v * 23f, 23, 2, seed + 611) - 0.5f) * 0.16f;
                noise += (HorizonNoise.FbmPeriodic(u * 57f, v * 57f, 57, 1, seed + 613) - 0.5f) * 0.08f;

                byte value = (byte)Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01(noise) * 255f), 0, 255);
                pixels[y * size + x] = new Color32(value, value, value, 255);
            }
        }

        return pixels;
    }
}

