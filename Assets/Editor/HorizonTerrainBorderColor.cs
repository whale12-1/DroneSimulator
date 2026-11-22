using UnityEngine;

// Samples the real outer-layer colour of the main Terrain around its border ring
// (splat weights x average layer albedo) so the horizon ring can blend its first
// 150-250 m into exactly the ground that is visible at the Terrain edge instead of
// a generic "near grass" colour. The result is stored per-perimeter-vertex in the
// ring mesh vertex colours and consumed by HorizonFields.shader.
public static class HorizonTerrainBorderColor
{
    // Finds the two dominant ground layers of the main Terrain (field/plough soil and
    // grass) and returns their average albedo. The horizon macro map is built ONLY from
    // these two colours, so the far ground shares the exact grade of the playable terrain.
    public static void FindBaseColors(
        Terrain terrain,
        int sampleGrid,
        out Color soilColor,
        out Color grassColor,
        System.Text.StringBuilder report)
    {
        TerrainData data = terrain.terrainData;
        TerrainLayer[] layers = data.terrainLayers;
        if (layers == null || layers.Length == 0)
        {
            soilColor = new Color(0.345f, 0.251f, 0.157f);
            grassColor = new Color(0.208f, 0.200f, 0.098f);
            report.AppendLine("  base colours fallback (no terrain layers)");
            return;
        }

        int resolution = Mathf.Max(4, sampleGrid);
        int alphaResolution = Mathf.Max(1, data.alphamapResolution);
        double[] weights = new double[layers.Length];
        int samples = 0;
        for (int sy = 0; sy < resolution; sy++)
        {
            for (int sx = 0; sx < resolution; sx++)
            {
                int ax = Mathf.Clamp(Mathf.RoundToInt((sx + 0.5f) / resolution * (alphaResolution - 1)),
                    0, alphaResolution - 1);
                int az = Mathf.Clamp(Mathf.RoundToInt((sy + 0.5f) / resolution * (alphaResolution - 1)),
                    0, alphaResolution - 1);
                float[,,] alpha = data.GetAlphamaps(ax, az, 1, 1);
                for (int layer = 0; layer < layers.Length; layer++)
                {
                    weights[layer] += alpha[0, 0, layer];
                }
                samples++;
            }
        }

        int soilIndex = -1;
        int grassIndex = -1;
        double soilWeight = -1;
        double grassWeight = -1;
        for (int i = 0; i < layers.Length; i++)
        {
            string textureName = layers[i] != null && layers[i].diffuseTexture != null
                ? layers[i].diffuseTexture.name.ToLowerInvariant() : string.Empty;
            double weight = weights[i] / Mathf.Max(1, samples);
            if (textureName.Contains("soil") || textureName.Contains("farm") || textureName.Contains("plough"))
            {
                if (weight > soilWeight)
                {
                    soilWeight = weight;
                    soilIndex = i;
                }
            }
            else if (textureName.Contains("grass") || textureName.Contains("darkforest") ||
                     textureName.Contains("moss"))
            {
                if (weight > grassWeight)
                {
                    grassWeight = weight;
                    grassIndex = i;
                }
            }
        }

        if (soilIndex < 0)
        {
            soilIndex = HighestWeightIndex(weights);
        }
        if (grassIndex < 0 || grassIndex == soilIndex)
        {
            grassIndex = NextHighestWeightIndex(weights, soilIndex);
        }

        soilColor = AverageTextureColor(layers[soilIndex].diffuseTexture);
        grassColor = AverageTextureColor(layers[grassIndex].diffuseTexture);
        report.AppendLine("  base colours: soil='" + layers[soilIndex].name + "' (" +
                          ToHtml(soilColor) + ", weight " + (weights[soilIndex] / Mathf.Max(1, samples)).ToString("F2") +
                          "), grass='" + layers[grassIndex].name + "' (" + ToHtml(grassColor) +
                          ", weight " + (weights[grassIndex] / Mathf.Max(1, samples)).ToString("F2") + ")");
    }

    static int HighestWeightIndex(double[] weights)
    {
        int best = 0;
        for (int i = 1; i < weights.Length; i++)
        {
            if (weights[i] > weights[best]) best = i;
        }
        return best;
    }

    static int NextHighestWeightIndex(double[] weights, int excluded)
    {
        int best = -1;
        for (int i = 0; i < weights.Length; i++)
        {
            if (i == excluded) continue;
            if (best < 0 || weights[i] > weights[best]) best = i;
        }
        return best < 0 ? 0 : best;
    }

    public static Color[] SampleBorderColors(
        Terrain terrain,
        Vector2 center,
        float innerHalf,
        int sampleCount,
        System.Text.StringBuilder report)
    {
        TerrainData data = terrain.terrainData;
        TerrainLayer[] layers = data.terrainLayers;
        Color[] layerColors = new Color[layers.Length];
        for (int i = 0; i < layers.Length; i++)
        {
            Texture2D texture = layers[i] != null ? layers[i].diffuseTexture : null;
            Color average = texture != null
                ? AverageTextureColor(texture).linear
                : new Color(0.30f, 0.25f, 0.18f).linear;
            layerColors[i] = average;
        }

        Vector3 position = terrain.transform.position;
        Vector3 size = data.size;
        int alphaResolution = Mathf.Max(1, data.alphamapResolution);
        float sampleRadius = Mathf.Max(0f, innerHalf - 35f);

        Color[] colors = new Color[Mathf.Max(1, sampleCount)];
        Color min = Color.white;
        Color max = Color.black;
        Color sum = Color.black;

        for (int i = 0; i < colors.Length; i++)
        {
            float s = (i + 0.5f) / colors.Length;
            Vector2 point = PerimeterPoint(sampleRadius, s);
            float worldX = center.x + point.x;
            float worldZ = center.y + point.y;

            int alphaX = Mathf.Clamp(
                Mathf.RoundToInt((worldX - position.x) / Mathf.Max(size.x, 0.001f) * (alphaResolution - 1)),
                0, alphaResolution - 1);
            int alphaZ = Mathf.Clamp(
                Mathf.RoundToInt((worldZ - position.z) / Mathf.Max(size.z, 0.001f) * (alphaResolution - 1)),
                0, alphaResolution - 1);

            float[,,] alpha = data.GetAlphamaps(alphaX, alphaZ, 1, 1);
            Color color = Color.black;
            for (int layer = 0; layer < layers.Length; layer++)
            {
                color += layerColors[layer] * alpha[0, 0, layer];
            }
            colors[i] = color;
            sum += color;
            if (color.r < min.r) min.r = color.r;
            if (color.g < min.g) min.g = color.g;
            if (color.b < min.b) min.b = color.b;
            if (color.r > max.r) max.r = color.r;
            if (color.g > max.g) max.g = color.g;
            if (color.b > max.b) max.b = color.b;
        }

        Color averageColor = sum / colors.Length;
        report.AppendLine("  border colours (" + colors.Length + " samples): avg sRGB=" +
                          ToHtml(averageColor.gamma) + " min=" + ToHtml(min.gamma) + " max=" + ToHtml(max.gamma));
        return colors;
    }

    static Vector2 PerimeterPoint(float half, float s)
    {
        float u = s * 4f;
        int side = Mathf.Min(3, Mathf.FloorToInt(u));
        float t = u - side;
        switch (side)
        {
            case 0: return new Vector2(-half + 2f * half * t, -half);
            case 1: return new Vector2(half, -half + 2f * half * t);
            case 2: return new Vector2(half - 2f * half * t, half);
            default: return new Vector2(-half, half - 2f * half * t);
        }
    }

    static Color AverageTextureColor(Texture2D texture)
    {
        const int Size = 8;
        RenderTexture target = RenderTexture.GetTemporary(
            Size, Size, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        Graphics.Blit(texture, target);

        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = target;
        Texture2D readable = new Texture2D(Size, Size, TextureFormat.RGBA32, false, false);
        readable.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
        readable.Apply(false, false);
        RenderTexture.active = previous;

        Color32[] pixels = readable.GetPixels32();
        double r = 0, g = 0, b = 0;
        for (int i = 0; i < pixels.Length; i++)
        {
            r += pixels[i].r;
            g += pixels[i].g;
            b += pixels[i].b;
        }

        double count = Mathf.Max(1, pixels.Length);
        Color result = new Color((float)(r / count) / 255f, (float)(g / count) / 255f, (float)(b / count) / 255f, 1f);

        Object.DestroyImmediate(readable);
        RenderTexture.ReleaseTemporary(target);
        return result;
    }

    static string ToHtml(Color color)
    {
        return "(" + Mathf.RoundToInt(color.r * 255f) + "," +
               Mathf.RoundToInt(color.g * 255f) + "," +
               Mathf.RoundToInt(color.b * 255f) + ")";
    }
}
