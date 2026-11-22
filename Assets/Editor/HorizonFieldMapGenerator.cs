using UnityEngine;

// CPU generators for the two horizon textures:
//   * Macro map (sRGB): an organic, domain-warped land-cover mosaic - irregular dark
//     ploughed patches, brown dry grass/stubble, muted grey-olive meadow and soft
//     forest massifs. No rectangular field grid and no sharp hedge lines.
//   * Detail map (linear, tileable): multi-octave micro noise used at three world
//     scales by HorizonFields.shader so the far ground never looks "soapy".
public static class HorizonFieldMapGenerator
{
    // Muted, overcast palette close to the main terrain's dark soil and olive grass.
    static readonly Color PloughDark = new Color(0.205f, 0.172f, 0.145f);
    static readonly Color WetSoil = new Color(0.262f, 0.224f, 0.182f);
    static readonly Color DryGrass = new Color(0.360f, 0.338f, 0.268f);
    static readonly Color Meadow = new Color(0.295f, 0.315f, 0.235f);
    static readonly Color DarkOlive = new Color(0.228f, 0.265f, 0.185f);
    static readonly Color ForestColor = new Color(0.108f, 0.132f, 0.098f);

    public static Color32[] CreateFieldMap(int size, Vector2 center, float span, int seed)
    {
        Color32[] pixels = new Color32[size * size];
        float halfSpan = span * 0.5f;

        // Coarse organic grids: domain-warped land cover, forest massifs and slow tone.
        const int Grid = 513;
        float[] land = new float[Grid * Grid];
        float[] forest = new float[Grid * Grid];
        float[] tone = new float[Grid * Grid];
        for (int gy = 0; gy < Grid; gy++)
        {
            float wz = center.y - halfSpan + span * gy / (Grid - 1f);
            for (int gx = 0; gx < Grid; gx++)
            {
                float wx = center.x - halfSpan + span * gx / (Grid - 1f);

                // Domain warp: low-frequency offsets bend every following octave, which
                // turns the grid-like value noise into irregular natural patches.
                float qx = HorizonNoise.Fbm(wx / 2600f, wz / 2600f, 3, seed + 301) - 0.5f;
                float qz = HorizonNoise.Fbm(wx / 2600f + 5.2f, wz / 2600f + 1.3f, 3, seed + 307) - 0.5f;
                float rx = HorizonNoise.Fbm(wx / 950f + qx * 2.6f, wz / 950f + qz * 2.6f, 3, seed + 311) - 0.5f;
                float rz = HorizonNoise.Fbm(wx / 950f + 3.7f + qx * 2.6f, wz / 950f + 4.9f + qz * 2.6f, 3, seed + 313) - 0.5f;

                float large = HorizonNoise.Fbm(wx / 2300f + rx * 1.8f, wz / 2300f + rz * 1.8f, 4, seed + 317);
                float medium = HorizonNoise.Fbm(wx / 780f + rx * 1.2f, wz / 780f + rz * 1.2f, 3, seed + 319);
                land[gy * Grid + gx] = Mathf.Clamp01(0.5f + (large * 0.68f + medium * 0.32f - 0.5f) * 2.1f);

                forest[gy * Grid + gx] = HorizonNoise.Fbm(
                    wx / 1450f + rx * 2.2f, wz / 1450f + rz * 2.2f, 4, seed + 331);
                tone[gy * Grid + gx] = HorizonNoise.Fbm(wx / 5400f, wz / 5400f, 2, seed + 337);
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

                float landValue = BilinearGrid(land, Grid, gxf, gyf);
                float forestValue = BilinearGrid(forest, Grid, gxf, gyf);
                float toneValue = BilinearGrid(tone, Grid, gxf, gyf);

                Color color = LandColor(landValue);
                color *= 0.93f + HorizonNoise.Hash(
                    Mathf.FloorToInt(wx / 17f), Mathf.FloorToInt(wz / 17f), seed + 461) * 0.14f;
                color *= 0.97f + HorizonNoise.Hash(
                    Mathf.FloorToInt(wx / 43f), Mathf.FloorToInt(wz / 43f), seed + 463) * 0.06f;
                color *= 0.92f + toneValue * 0.16f;

                float forestBlend = Mathf.SmoothStep(0f, 1f,
                    Mathf.InverseLerp(0.52f, 0.64f, forestValue + (toneValue - 0.5f) * 0.10f));
                if (forestBlend > 0.001f)
                {
                    float mottling = 0.90f + HorizonNoise.Hash(
                        Mathf.FloorToInt(wx / 15f), Mathf.FloorToInt(wz / 15f), seed + 467) * 0.20f;
                    color = Color.Lerp(color, ForestColor * mottling, forestBlend);
                }

                pixels[y * size + x] = color;
            }
        }

        return pixels;
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

    static Color LandColor(float value)
    {
        float t = Mathf.Clamp01(value);
        if (t < 0.28f)
        {
            return Color.Lerp(PloughDark, WetSoil, SmoothStep01(t / 0.28f));
        }
        if (t < 0.45f)
        {
            return Color.Lerp(WetSoil, DryGrass, SmoothStep01((t - 0.28f) / 0.17f));
        }
        if (t < 0.60f)
        {
            return Color.Lerp(DryGrass, Meadow, SmoothStep01((t - 0.45f) / 0.15f));
        }
        return Color.Lerp(Meadow, DarkOlive, SmoothStep01((t - 0.60f) / 0.40f));
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

