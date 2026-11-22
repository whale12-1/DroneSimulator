using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Mesh builders for the far horizon:
//   * BuildGroundMesh: a square-to-circle ring from just inside the main Terrain edge
//     out to a 10 km circle, with distance-graded ring spacing (dense near the seam).
//   * BuildForestMesh: one combined mesh of crossed forest-strip cards placed as long
//     belts on the far ridge crests (4.2-8.6 km from the terrain border).
public static class HorizonMeshGenerator
{
    static readonly float[] RingDistances =
    {
        0f, 2f, 4f, 7f, 11f, 17f, 25f, 36f, 50f, 68f, 91f, 121f, 160f, 210f,
        275f, 359f, 467f, 606f, 785f, 1015f, 1310f, 1687f, 2168f, 2780f,
        3560f, 4550f, 5800f, 7300f
    };

    public struct Stats
    {
        public int Vertices;
        public int Triangles;
        public int Cards;
        public float MinHeight;
        public float MaxHeight;
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

    public static Mesh BuildGroundMesh(
        HorizonHeightField field,
        float innerHalf,
        float outerRadius,
        int perimeterSamples,
        Color[] borderColors,
        out Stats stats)
    {
        int ringCount = RingDistances.Length + 1; // + outer circle ring
        int columns = perimeterSamples + 1;
        int vertexCount = ringCount * columns;

        Vector3[] vertices = new Vector3[vertexCount];
        Vector3[] normals = new Vector3[vertexCount];
        Vector2[] uvs = new Vector2[vertexCount];
        Color[] colors = new Color[vertexCount];
        Color fallbackBorderColor = new Color(0.30f, 0.25f, 0.18f).linear;

        Vector2 center = field.Center;
        float minHeight = float.MaxValue;
        float maxHeight = float.MinValue;

        for (int r = 0; r < ringCount; r++)
        {
            float distance = r < RingDistances.Length ? RingDistances[r] : -1f;
            float spacing;
            if (r == 0)
            {
                spacing = RingDistances[1] - RingDistances[0];
            }
            else if (distance < 0f)
            {
                spacing = RingDistances[RingDistances.Length - 1] - RingDistances[RingDistances.Length - 2];
            }
            else
            {
                spacing = distance - RingDistances[r - 1];
            }
            float epsilon = Mathf.Clamp(spacing * 0.6f, 5f, 400f);

            for (int j = 0; j < columns; j++)
            {
                float s = (j % perimeterSamples) / (float)perimeterSamples;
                Vector2 inner = PerimeterPoint(innerHalf, s);
                Vector2 direction = inner.sqrMagnitude > 0.0001f ? inner.normalized : Vector2.right;
                Vector2 outer = direction * outerRadius;

                float t = 1f;
                if (distance >= 0f)
                {
                    float rayLength = Vector2.Distance(inner, outer);
                    t = Mathf.Clamp01(distance / Mathf.Max(rayLength, 1f));
                }

                Vector2 offset = Vector2.Lerp(inner, outer, t);
                float worldX = center.x + offset.x;
                float worldZ = center.y + offset.y;

                float border;
                float height = field.HeightWithBorder(worldX, worldZ, out border) - 0.12f;
                int index = r * columns + j;

                // Near the seam the normals are flattened toward up: the ring must be lit
                // like the almost flat Terrain edge instead of turning into a dark band.
                Vector3 normal = field.Normal(worldX, worldZ, epsilon);
                float flatten = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(120f, 520f, border));
                normal = Vector3.Normalize(Vector3.Lerp(normal, Vector3.up, flatten * 0.65f));

                vertices[index] = new Vector3(offset.x, height, offset.y);
                normals[index] = normal;
                uvs[index] = new Vector2(border / 1000f, 0f);
                if (borderColors != null && borderColors.Length > 0)
                {
                    int colorIndex = Mathf.Clamp(
                        Mathf.FloorToInt(s * borderColors.Length), 0, borderColors.Length - 1);
                    colors[index] = borderColors[colorIndex];
                }
                else
                {
                    colors[index] = fallbackBorderColor;
                }

                if (height < minHeight) minHeight = height;
                if (height > maxHeight) maxHeight = height;
            }
        }

        int[] triangles = new int[perimeterSamples * (ringCount - 1) * 6];
        int triangleIndex = 0;
        for (int r = 0; r < ringCount - 1; r++)
        {
            for (int j = 0; j < perimeterSamples; j++)
            {
                int a = r * columns + j;
                int b = a + 1;
                int c = a + columns;
                int d = c + 1;
                triangles[triangleIndex++] = a;
                triangles[triangleIndex++] = b;
                triangles[triangleIndex++] = c;
                triangles[triangleIndex++] = b;
                triangles[triangleIndex++] = d;
                triangles[triangleIndex++] = c;
            }
        }

        // Guarantee the ring faces up regardless of the perimeter winding direction.
        Vector3 cross = Vector3.Cross(
            vertices[triangles[1]] - vertices[triangles[0]],
            vertices[triangles[2]] - vertices[triangles[0]]);
        if (cross.y < 0f)
        {
            for (int i = 0; i < triangles.Length; i += 3)
            {
                int swap = triangles[i + 1];
                triangles[i + 1] = triangles[i + 2];
                triangles[i + 2] = swap;
            }
        }

        Mesh mesh = new Mesh();
        mesh.name = "Horizon_Ground_Mesh";
        if (vertexCount > 65000)
        {
            mesh.indexFormat = IndexFormat.UInt32;
        }
        mesh.vertices = vertices;
        mesh.normals = normals;
        mesh.uv = uvs;
        mesh.colors = colors;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();

        stats.Vertices = vertexCount;
        stats.Triangles = triangles.Length / 3;
        stats.Cards = 0;
        stats.MinHeight = minHeight;
        stats.MaxHeight = maxHeight;
        return mesh;
    }

    public static Mesh BuildForestMesh(
        HorizonHeightField field,
        Terrain terrain,
        int seed,
        int maxCards,
        out Stats stats)
    {
        List<Vector3> vertices = new List<Vector3>(maxCards * 8);
        List<Vector3> normals = new List<Vector3>(maxCards * 8);
        List<Vector2> uvs = new List<Vector2>(maxCards * 8);
        List<Color> colors = new List<Color>(maxCards * 8);
        List<int> triangles = new List<int>(maxCards * 12);

        Vector2 center = field.Center;
        float[] treeDensity = BuildTerrainTreeDensity(terrain, 128);
        int cards = 0;
        float minHeight = float.MaxValue;
        float maxHeight = float.MinValue;

        // Near/mid plain: small single-tree cards (12-18 m) in organic clusters that
        // continue the main terrain forest and break the open land into groves.
        PlaceForestCards(field, terrain, treeDensity, center, seed, maxCards,
            380f, 2500f, 28f, 0.525f, 0.88f, 20f, 15f, 12f, 6f, false,
            vertices, normals, uvs, colors, triangles, ref cards, ref minHeight, ref maxHeight);

        // Far ridges: wider 4-tree strip cards (18-26 m) as long soft belts.
        PlaceForestCards(field, terrain, treeDensity, center, seed, maxCards,
            2500f, 7000f, 62f, 0.565f, 0.70f, 82f, 40f, 18f, 8f, true,
            vertices, normals, uvs, colors, triangles, ref cards, ref minHeight, ref maxHeight);

        Mesh mesh = new Mesh();
        mesh.name = "Horizon_Forest_Silhouette_Mesh";
        mesh.indexFormat = IndexFormat.UInt32;
        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);
        mesh.SetUVs(0, uvs);
        mesh.SetColors(colors);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();

        stats.Vertices = vertices.Count;
        stats.Triangles = triangles.Count / 3;
        stats.Cards = cards;
        stats.MinHeight = minHeight == float.MaxValue ? 0f : minHeight;
        stats.MaxHeight = maxHeight == float.MinValue ? 0f : maxHeight;
        return mesh;
    }

    static void PlaceForestCards(
        HorizonHeightField field, Terrain terrain, float[] treeDensity, Vector2 center,
        int seed, int maxCards, float minBorder, float maxBorder, float gridStep,
        float threshold, float hashPass, float baseWidth, float widthRandom,
        float baseHeight, float heightRandom, bool fullRow,
        List<Vector3> vertices, List<Vector3> normals, List<Vector2> uvs,
        List<Color> colors, List<int> triangles, ref int cards,
        ref float minHeight, ref float maxHeight)
    {
        float halfSpan = maxBorder + 1100f;
        for (float z = center.y - halfSpan; z <= center.y + halfSpan; z += gridStep)
        {
            for (float x = center.x - halfSpan; x <= center.x + halfSpan; x += gridStep)
            {
                if (cards >= maxCards) return;

                float border = field.BorderDistance(x, z);
                if (border < minBorder || border > maxBorder) continue;

                // Organic clusters + anisotropic belts: natural groves and elongated
                // tree lines instead of a regular grid or a contour ring.
                float organic = OrganicScore(x, z, seed);
                float borderDensity = SampleDensity(treeDensity, 128, terrain, x, z);
                float borderBoost = (1f - Mathf.SmoothStep(0f, 1f,
                    Mathf.InverseLerp(700f, 2200f, border))) * borderDensity;
                if (organic < threshold - borderBoost * 0.16f) continue;
                if (HorizonNoise.Fbm(x / 420f, z / 420f, 2, seed + 223) < 0.36f) continue;

                int hashX = Mathf.FloorToInt((x - center.x) / gridStep);
                int hashZ = Mathf.FloorToInt((z - center.y) / gridStep);
                if (HorizonNoise.Hash(hashX, hashZ, seed + 101) > hashPass) continue;

                Vector3 normal = field.Normal(x, z, 16f);
                if (normal.y < 0.82f) continue;

                float h1 = HorizonNoise.Hash(hashX, hashZ, seed + 103);
                float h2 = HorizonNoise.Hash(hashX, hashZ, seed + 107);
                float h3 = HorizonNoise.Hash(hashX, hashZ, seed + 109);
                float h4 = HorizonNoise.Hash(hashX, hashZ, seed + 113);
                float h5 = HorizonNoise.Hash(hashX, hashZ, seed + 127);

                Vector3 tangent;
                if (fullRow)
                {
                    float e = 28f;
                    float gx = OrganicScore(x + e, z, seed) - OrganicScore(x - e, z, seed);
                    float gz = OrganicScore(x, z + e, seed) - OrganicScore(x, z - e, seed);
                    Vector3 gradient = new Vector3(gx, 0f, gz);
                    tangent = gradient.sqrMagnitude > 0.000001f
                        ? Vector3.Cross(Vector3.up, gradient).normalized
                        : Vector3.right;
                    tangent = Quaternion.Euler(0f, (h3 - 0.5f) * 30f, 0f) * tangent;
                }
                else
                {
                    tangent = Quaternion.Euler(0f, h3 * 360f, 0f) * Vector3.right;
                }

                float width = baseWidth + h1 * widthRandom;
                float cardHeight = baseHeight + h2 * heightRandom;

                int speciesRow;
                if (h4 > 0.95f)
                {
                    speciesRow = 3; // mixed row
                }
                else if (h4 < 0.45f)
                {
                    speciesRow = 0; // conifer
                }
                else if (h4 < 0.76f)
                {
                    speciesRow = 1; // pine
                }
                else
                {
                    speciesRow = 2; // cypress
                }

                Color tint = new Color(
                    0.74f + h1 * 0.22f,
                    0.78f + h2 * 0.18f,
                    0.68f + h3 * 0.18f,
                    1f);

                float vStart = speciesRow * 0.25f + 0.004f;
                float vEnd = (speciesRow + 1) * 0.25f - 0.004f;
                float uStart = 0.004f;
                float uEnd = 0.996f;
                if (!fullRow)
                {
                    // Near/mid cards show a single tree cell instead of a 4-tree strip.
                    int column = Mathf.Clamp((int)(h5 * 4f), 0, 3);
                    uStart = column * 0.25f + 0.006f;
                    uEnd = (column + 1) * 0.25f - 0.006f;
                }

                for (int quad = 0; quad < 2; quad++)
                {
                    Vector3 direction = Quaternion.Euler(0f, quad == 0 ? 0f : 64f, 0f) * tangent;
                    Vector3 left = new Vector3(x - direction.x * width * 0.5f, 0f, z - direction.z * width * 0.5f);
                    Vector3 right = new Vector3(x + direction.x * width * 0.5f, 0f, z + direction.z * width * 0.5f);

                    float leftHeight = field.Height(left.x, left.z);
                    float rightHeight = field.Height(right.x, right.z);
                    float bottom = Mathf.Min(leftHeight, rightHeight) - (fullRow ? 2.5f : 1.4f);
                    float top = Mathf.Max(leftHeight, rightHeight) + cardHeight - (fullRow ? 1.0f : 0.3f);

                    Vector3 rootOffset = new Vector3(-center.x, 0f, -center.y);
                    Vector3 v0 = new Vector3(left.x, bottom, left.z) + rootOffset;
                    Vector3 v1 = new Vector3(right.x, bottom, right.z) + rootOffset;
                    Vector3 v2 = new Vector3(right.x, top, right.z) + rootOffset;
                    Vector3 v3 = new Vector3(left.x, top, left.z) + rootOffset;

                    int baseIndex = vertices.Count;
                    vertices.Add(v0);
                    vertices.Add(v1);
                    vertices.Add(v2);
                    vertices.Add(v3);
                    for (int i = 0; i < 4; i++)
                    {
                        normals.Add(Vector3.up);
                        colors.Add(tint);
                    }
                    uvs.Add(new Vector2(uStart, vStart));
                    uvs.Add(new Vector2(uEnd, vStart));
                    uvs.Add(new Vector2(uEnd, vEnd));
                    uvs.Add(new Vector2(uStart, vEnd));

                    triangles.Add(baseIndex);
                    triangles.Add(baseIndex + 1);
                    triangles.Add(baseIndex + 2);
                    triangles.Add(baseIndex);
                    triangles.Add(baseIndex + 2);
                    triangles.Add(baseIndex + 3);

                    if (bottom < minHeight) minHeight = bottom;
                    if (top > maxHeight) maxHeight = top;
                }

                cards++;
            }
        }
    }

    static float OrganicScore(float x, float z, int seed)
    {
        float cluster = HorizonNoise.Fbm(x / 620f, z / 620f, 3, seed + 201);
        float belt = HorizonNoise.Fbm(x / 2500f, z / 820f, 3, seed + 211);
        return cluster * 0.62f + belt * 0.38f;
    }

    // Builds a smooth forest-density mask from the main terrain's SpeedTree instances.
    // Horizon trees get denser where the real forest touches the border, so the far
    // forest visually continues the playable forest instead of starting from nothing.
    static float[] BuildTerrainTreeDensity(Terrain terrain, int resolution)
    {
        int res = Mathf.Max(8, resolution);
        float[] density = new float[res * res];
        TreeInstance[] trees = terrain.terrainData.treeInstances;
        for (int i = 0; i < trees.Length; i++)
        {
            int x = Mathf.Clamp(Mathf.RoundToInt(trees[i].position.x * (res - 1)), 0, res - 1);
            int z = Mathf.Clamp(Mathf.RoundToInt(trees[i].position.z * (res - 1)), 0, res - 1);
            density[z * res + x] += 1f;
        }

        float[] blurred = new float[density.Length];
        for (int pass = 0; pass < 2; pass++)
        {
            for (int z = 0; z < res; z++)
            {
                int z0 = Mathf.Max(z - 1, 0);
                int z1 = Mathf.Min(z + 1, res - 1);
                for (int x = 0; x < res; x++)
                {
                    int x0 = Mathf.Max(x - 1, 0);
                    int x1 = Mathf.Min(x + 1, res - 1);
                    float sum = 0f;
                    for (int zz = z0; zz <= z1; zz++)
                    {
                        for (int xx = x0; xx <= x1; xx++)
                        {
                            sum += density[zz * res + xx];
                        }
                    }
                    blurred[z * res + x] = sum / 9f;
                }
            }

            float[] swap = density;
            density = blurred;
            blurred = swap;
        }

        float max = 0.0001f;
        for (int i = 0; i < density.Length; i++)
        {
            max = Mathf.Max(max, density[i]);
        }
        for (int i = 0; i < density.Length; i++)
        {
            density[i] = Mathf.Clamp01(density[i] / (max * 0.65f));
        }
        return density;
    }

    static float SampleDensity(float[] density, int resolution, Terrain terrain, float x, float z)
    {
        int res = Mathf.Max(8, resolution);
        Vector3 position = terrain.transform.position;
        Vector3 size = terrain.terrainData.size;
        int px = Mathf.Clamp((int)(((x - position.x) / Mathf.Max(size.x, 0.001f)) * (res - 1)), 0, res - 1);
        int pz = Mathf.Clamp((int)(((z - position.z) / Mathf.Max(size.z, 0.001f)) * (res - 1)), 0, res - 1);
        return density[pz * res + px];
    }
}

