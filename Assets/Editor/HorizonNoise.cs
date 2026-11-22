using UnityEngine;

// Deterministic value-noise helpers used by the far-horizon environment builder.
// Everything is hash based (no UnityEngine.Random) so meshes and textures are stable
// between runs and can be regenerated at any time without reshuffling the horizon.
public static class HorizonNoise
{
    public static float Hash(int x, int y, int seed)
    {
        unchecked
        {
            uint h = (uint)(x * 374761393) ^ (uint)(y * 668265263) ^ (uint)seed * 2654435761u;
            h = (h ^ (h >> 13)) * 1274126177u;
            h ^= h >> 16;
            return (h & 0xFFFFFFu) / 16777215f;
        }
    }

    public static float Hash(int x, int y, int z, int seed)
    {
        unchecked
        {
            uint h = (uint)(x * 374761393) ^ (uint)(y * 668265263) ^ (uint)(z * 2246822519) ^ (uint)seed * 2654435761u;
            h = (h ^ (h >> 13)) * 1274126177u;
            h ^= h >> 16;
            return (h & 0xFFFFFFu) / 16777215f;
        }
    }

    public static float Value(float x, float y, int seed)
    {
        int xi = Mathf.FloorToInt(x);
        int yi = Mathf.FloorToInt(y);
        float xf = x - xi;
        float yf = y - yi;
        float u = xf * xf * (3f - 2f * xf);
        float v = yf * yf * (3f - 2f * yf);

        float a = Hash(xi, yi, seed);
        float b = Hash(xi + 1, yi, seed);
        float c = Hash(xi, yi + 1, seed);
        float d = Hash(xi + 1, yi + 1, seed);
        return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v);
    }

    public static float Fbm(float x, float y, int octaves, int seed)
    {
        float sum = 0f;
        float amplitude = 0.5f;
        float total = 0f;
        float frequency = 1f;
        for (int i = 0; i < octaves; i++)
        {
            sum += Value(x * frequency, y * frequency, seed + i * 131) * amplitude;
            total += amplitude;
            frequency *= 2.03f;
            amplitude *= 0.5f;
        }
        return sum / Mathf.Max(total, 0.0001f);
    }

    public static float Ridged(float x, float y, int octaves, int seed)
    {
        return 1f - Mathf.Abs(Fbm(x, y, octaves, seed) * 2f - 1f);
    }

    // Seamless (tileable) value noise: lattice coordinates are wrapped modulo period.
    public static float ValuePeriodic(float x, float y, int period, int seed)
    {
        int xi = Mathf.FloorToInt(x);
        int yi = Mathf.FloorToInt(y);
        float xf = x - xi;
        float yf = y - yi;
        float u = xf * xf * (3f - 2f * xf);
        float v = yf * yf * (3f - 2f * yf);

        int p = Mathf.Max(1, period);
        int x0 = Mod(xi, p);
        int x1 = Mod(xi + 1, p);
        int y0 = Mod(yi, p);
        int y1 = Mod(yi + 1, p);

        float a = Hash(x0, y0, seed);
        float b = Hash(x1, y0, seed);
        float c = Hash(x0, y1, seed);
        float d = Hash(x1, y1, seed);
        return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v);
    }

    public static float FbmPeriodic(float x, float y, int basePeriod, int octaves, int seed)
    {
        float sum = 0f;
        float amplitude = 0.5f;
        float total = 0f;
        int period = Mathf.Max(1, basePeriod);
        for (int i = 0; i < octaves; i++)
        {
            sum += ValuePeriodic(x, y, period, seed + i * 197) * amplitude;
            total += amplitude;
            period *= 2;
            x *= 2f;
            y *= 2f;
            amplitude *= 0.5f;
        }
        return sum / Mathf.Max(total, 0.0001f);
    }

    static int Mod(int value, int period)
    {
        int result = value % period;
        return result < 0 ? result + period : result;
    }
}
