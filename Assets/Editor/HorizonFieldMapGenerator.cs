using UnityEngine;

// CPU generators for the two horizon textures:
//   * Macro map  (sRGB): a high-resolution farmland mosaic - rectangular/trapezoid
//     fields with different crops, dark hedgerow belts along the field borders and
//     large forest massifs that break the grid.
//   * Detail map (linear, tileable): multi-octave micro noise used at three world
//     scales by HorizonFields.shader so the far ground never looks "soapy".
public static class HorizonFieldMapGenerator
{
    const float CellWidth = 340f;
    const float CellHeight = 430f;
    const float GridRotationCos = 0.9428f;  // cos(0.34 rad)
    const float GridRotationSin = 0.3335f;  // sin(0.34 rad)
    const float GridShear = 0.12f;

    static readonly Color[] FieldPalette =
    {
        new Color(0.34f, 0.26f, 0.18f), // ploughed soil
        new Color(0.30f, 0.40f, 0.18f), // winter crop
        new Color(0.55f, 0.48f, 0.30f), // stubble
        new Color(0.40f, 0.46f, 0.25f), // meadow
        new Color(0.26f, 0.34f, 0.16f), // dark crop
        new Color(0.45f, 0.50f, 0.27f), // young crop
        new Color(0.37f, 0.36f, 0.24f), // fallow
        new Color(0.30f, 0.44f, 0.20f)  // grass ley
    };

    static readonly Color HedgerowColor = new Color(0.15f, 0.23f, 0.11f);
    static readonly Color ForestColor = new Color(0.12f, 0.19f, 0.09f);
    static readonly Color DirtTrackColor = new Color(0.42f, 0.36f, 0.26f);

    public static Color32[] CreateFieldMap(int size, Vector2 center, float span, int seed)
    {
        Color32[] pixels = new Color32[size * size];

        // Coarse, cheap noise grids: forest massifs and slow tonal variation.
        const int Grid = 257;
        float[] forest = new float[Grid * Grid];
        float[] tone = new float[Grid * Grid];
        float halfSpan = span * 0.5f;
        for (int gy = 0; gy < Grid; gy++)
        {
            float wz = center.y - halfSpan + span * gy / (Grid - 1f);
            for (int gx = 0; gx < Grid; gx++)
            {
                float wx = center.x - halfSpan + span * gx / (Grid - 1f);
                forest[gy * Grid + gx] = HorizonNoise.Fbm(wx / 2600f, wz / 2600f, 4, seed + 301);
                tone[gy * Grid + gx] = HorizonNoise.Fbm(wx / 900f, wz / 900f, 2, seed + 307);
            }
        }

        float pixelSize = span / size;
        for (int y = 0; y < size; y++)
        {
            float wz = center.y - halfSpan + (y + 0.5f) * pixelSize;
            float gyf = Mathf.Clamp((wz - (center.y - halfSpan)) / span, 0f, 1f) * (Grid - 1);
            int gy0 = Mathf.Min(Mathf.FloorToInt(gyf), Grid - 2);
            float fy = gyf - gy0;

            for (int x = 0; x < size; x++)
            {
                float wx = center.x - halfSpan + (x + 0.5f) * pixelSize;
                float gxf = Mathf.Clamp((wx - (center.x - halfSpan)) / span, 0f, 1f) * (Grid - 1);
                int gx0 = Mathf.Min(Mathf.FloorToInt(gxf), Grid - 2);
                float fx = gxf - gx0;

                float forestValue = Mathf.Lerp(
                    Mathf.Lerp(forest[gy0 * Grid + gx0], forest[gy0 * Grid + gx0 + 1], fx),
                    Mathf.Lerp(forest[(gy0 + 1) * Grid + gx0], forest[(gy0 + 1) * Grid + gx0 + 1], fx),
                    fy);
                float toneValue = Mathf.Lerp(
                    Mathf.Lerp(tone[gy0 * Grid + gx0], tone[gy0 * Grid + gx0 + 1], fx),
                    Mathf.Lerp(tone[(gy0 + 1) * Grid + gx0], tone[(gy0 + 1) * Grid + gx0 + 1], fx),
                    fy);

                pixels[y * size + x] = ComputeFieldColor(wx, wz, forestValue, toneValue, seed);
            }
        }

        return pixels;
    }

    static Color32 ComputeFieldColor(float wx, float wz, float forestMask, float tone, int seed)
    {
        float u = wx * GridRotationCos - wz * GridRotationSin + 50000f;
        float v = wx * GridRotationSin + wz * GridRotationCos + 50000f;
        float shearedU = u - v * GridShear;

        int iu = Mathf.FloorToInt(shearedU / CellWidth);
        int iv = Mathf.FloorToInt(v / CellHeight);
        float fu = shearedU / CellWidth - iu;
        float fv = v / CellHeight - iv;

        // Many fields are split in half, which reads as a patchwork of strips.
        bool split = HorizonNoise.Hash(iu, iv, seed + 401) > 0.62f;
        int sub = 0;
        float edgeV;
        if (split)
        {
            sub = fv < 0.5f ? 0 : 1;
            float local = fv < 0.5f ? fv * 2f : (fv - 0.5f) * 2f;
            edgeV = Mathf.Min(local, 1f - local) * (CellHeight * 0.5f);
        }
        else
        {
            edgeV = Mathf.Min(fv, 1f - fv) * CellHeight;
        }

        float edgeU = Mathf.Min(fu, 1f - fu) * CellWidth;
        float edgeDistance = Mathf.Min(edgeU, edgeV);

        int cropIndex = (int)(HorizonNoise.Hash(iu, iv * 7 + sub, seed + 409) * FieldPalette.Length);
        cropIndex = cropIndex % FieldPalette.Length;
        float brightness = 0.88f + HorizonNoise.Hash(iu, iv * 13 + sub, seed + 419) * 0.24f;
        Color color = FieldPalette[cropIndex] * (brightness * 0.88f);

        // In-field variation that survives MIP filtering at 1-4 km: 23 m clumps plus
        // broad crop rows. Without it large fields read as flat colour slabs.
        float mottle = 0.92f + HorizonNoise.Hash(
            Mathf.FloorToInt(wx / 23f), Mathf.FloorToInt(wz / 23f), seed + 461) * 0.16f;
        mottle *= 0.97f + 0.06f * Mathf.Sin(v * 0.13f);
        color *= mottle;

        float hedgeU = HorizonNoise.Hash(iu, 977, seed + 421) > 0.42f
            ? 10f + HorizonNoise.Hash(iu, 983, seed + 431) * 16f : 0f;
        float hedgeV = HorizonNoise.Hash(iv, 1597, seed + 433) > 0.48f
            ? 10f + HorizonNoise.Hash(iv, 1613, seed + 439) * 16f : 0f;
        float hedgeSplit = split && HorizonNoise.Hash(iu, iv, seed + 443) > 0.35f
            ? 12f + HorizonNoise.Hash(iu, iv, seed + 449) * 14f : 0f;

        float hedgeWidth = 0f;
        if (hedgeU > 0f && edgeU < hedgeU)
        {
            hedgeWidth = hedgeU;
        }
        if (hedgeV > 0f && edgeV < hedgeV && hedgeV > hedgeWidth)
        {
            hedgeWidth = hedgeV;
        }
        if (split && hedgeSplit > 0f)
        {
            float splitDistance = Mathf.Abs(fv - 0.5f) * CellHeight;
            if (splitDistance < hedgeSplit && hedgeSplit > hedgeWidth)
            {
                hedgeWidth = hedgeSplit;
            }
        }

        float micro = 0.86f + HorizonNoise.Hash(
            Mathf.FloorToInt(wx / 13f), Mathf.FloorToInt(wz / 13f), seed + 453) * 0.28f;

        if (hedgeWidth > 0f && edgeDistance <= hedgeWidth)
        {
            color = Color.Lerp(color, HedgerowColor * micro, 0.92f);
        }
        else if (hedgeWidth > 0f && edgeDistance <= hedgeWidth + 5.5f)
        {
            color = Color.Lerp(color, DirtTrackColor * micro, 0.85f);
        }

        float forestBlend = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.56f, 0.64f, forestMask));
        if (forestBlend > 0.001f)
        {
            float mottling = 0.88f + HorizonNoise.Hash(
                Mathf.FloorToInt(wx / 17f), Mathf.FloorToInt(wz / 17f), seed + 457) * 0.24f;
            color = Color.Lerp(color, ForestColor * mottling, forestBlend);
        }

        float toneScale = 0.90f + tone * 0.20f;
        color *= toneScale;
        return color;
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

