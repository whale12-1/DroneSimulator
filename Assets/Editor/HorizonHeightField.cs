using UnityEngine;

// Height field for the far-horizon ring. It extends the main Terrain heights outwards,
// keeps the first ~320 m as a continuation of the border slope, then smoothly grows
// multi-layer ridges/valleys (40-130 m of relief) towards the 10 km circle.
public class HorizonHeightField
{
    public readonly Terrain Terrain;
    public readonly Vector2 Center;
    public readonly int Seed;

    readonly float minX;
    readonly float maxX;
    readonly float minZ;
    readonly float maxZ;
    readonly float sizeX;
    readonly float sizeZ;
    readonly float sizeY;
    readonly float baseY;
    readonly int heightResolution;
    readonly float[,] heights;

    public float MinX { get { return minX; } }
    public float MaxX { get { return maxX; } }
    public float MinZ { get { return minZ; } }
    public float MaxZ { get { return maxZ; } }

    public HorizonHeightField(Terrain terrain, int seed)
    {
        Terrain = terrain;
        Seed = seed;

        Vector3 position = terrain.transform.position;
        Vector3 size = terrain.terrainData.size;
        sizeX = size.x;
        sizeZ = size.z;
        sizeY = size.y;
        baseY = position.y;
        minX = position.x;
        minZ = position.z;
        maxX = position.x + size.x;
        maxZ = position.z + size.z;
        Center = new Vector2((minX + maxX) * 0.5f, (minZ + maxZ) * 0.5f);

        heightResolution = terrain.terrainData.heightmapResolution;
        heights = terrain.terrainData.GetHeights(0, 0, heightResolution, heightResolution);
    }

    public float BorderDistance(float x, float z)
    {
        float dx = Mathf.Max(minX - x, x - maxX);
        float dz = Mathf.Max(minZ - z, z - maxZ);
        dx = Mathf.Max(dx, 0f);
        dz = Mathf.Max(dz, 0f);
        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    // Bilinear height sample of the main terrain (clamped to its rectangle).
    public float SampleTerrain(float x, float z)
    {
        float u = Mathf.Clamp01((x - minX) / sizeX) * (heightResolution - 1);
        float v = Mathf.Clamp01((z - minZ) / sizeZ) * (heightResolution - 1);
        int x0 = Mathf.FloorToInt(u);
        int y0 = Mathf.FloorToInt(v);
        int x1 = Mathf.Min(x0 + 1, heightResolution - 1);
        int y1 = Mathf.Min(y0 + 1, heightResolution - 1);
        float fx = u - x0;
        float fy = v - y0;
        float h0 = Mathf.Lerp(heights[y0, x0], heights[y0, x1], fx);
        float h1 = Mathf.Lerp(heights[y1, x0], heights[y1, x1], fx);
        return baseY + Mathf.Lerp(h0, h1, fy) * sizeY;
    }

    // Terrain border height continued outwards with the local edge slope so the seam
    // does not turn into a flat shelf.
    public float BaseHeight(float x, float z, out float border)
    {
        border = BorderDistance(x, z);
        float clampedX = Mathf.Clamp(x, minX, maxX);
        float clampedZ = Mathf.Clamp(z, minZ, maxZ);
        float edge = SampleTerrain(clampedX, clampedZ);

        if (border > 0.01f)
        {
            float nx = (x - clampedX) / border;
            float nz = (z - clampedZ) / border;
            float inside = SampleTerrain(clampedX - nx * 12f, clampedZ - nz * 12f);
            float slope = Mathf.Clamp((edge - inside) / 12f, -0.22f, 0.22f);
            edge += slope * 60f * (1f - Mathf.Exp(-border / 60f));
        }

        return edge;
    }

    public float Ridge(float x, float z)
    {
        return 1f - Mathf.Abs(HorizonNoise.Fbm(x / 1100f, z / 1100f, 3, Seed + 23) * 2f - 1f);
    }

    public float GapNoise(float x, float z)
    {
        return HorizonNoise.Fbm(x / 1700f, z / 1700f, 2, Seed + 83);
    }

    // Multi-layer relief (hills and hollows) measured in meters above the extended base.
    public float Relief(float x, float z, float border)
    {
        float ramp = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(280f, 1000f, border));
        float amplitude = Mathf.Lerp(46f, 86f, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(600f, 6200f, border)));

        float ridge = Ridge(x, z);
        float ridgeSecondary = 1f - Mathf.Abs(HorizonNoise.Fbm(x / 520f, z / 520f, 2, Seed + 29) * 2f - 1f);
        float rolling = HorizonNoise.Fbm(x / 2300f, z / 2300f, 3, Seed + 37) - 0.5f;
        float detail = HorizonNoise.Fbm(x / 380f, z / 380f, 2, Seed + 51) - 0.5f;

        float relief = amplitude * (0.60f * ridge + 0.33f * ridgeSecondary + 1.00f * rolling) + 12f * detail;
        relief *= ramp;

        float rim = 27f
            * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(4200f, 9500f, border))
            * (0.55f + 0.45f * HorizonNoise.Fbm(x / 5200f, z / 5200f, 2, Seed + 67));

        float undulation = (HorizonNoise.Fbm(x / 210f, z / 210f, 2, Seed + 11) - 0.5f) * 4.4f;
        undulation *= Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(border / 80f));

        return relief + rim + undulation;
    }

    public float HeightWithBorder(float x, float z, out float border)
    {
        float baseHeight = BaseHeight(x, z, out border);
        return baseHeight + Relief(x, z, border);
    }

    public float Height(float x, float z)
    {
        float border;
        return HeightWithBorder(x, z, out border);
    }

    public Vector3 Normal(float x, float z, float epsilon)
    {
        float e = Mathf.Max(epsilon, 0.5f);
        float left = Height(x - e, z);
        float right = Height(x + e, z);
        float down = Height(x, z - e);
        float up = Height(x, z + e);
        Vector3 normal = new Vector3((left - right) / (2f * e), 1f, (down - up) / (2f * e));
        return normal.normalized;
    }
}
