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

                vertices[index] = new Vector3(offset.x, height, offset.y);
                normals[index] = field.Normal(worldX, worldZ, epsilon);
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
        int seed,
        int maxCards,
        out Stats stats)
    {
        const float gridStep = 72f;
        const float minBorder = 4500f;
        const float maxBorder = 8600f;
        const float cardBaseWidth = 112f;
        const float cardBaseHeight = 18f;

        List<Vector3> vertices = new List<Vector3>(maxCards * 8);
        List<Vector3> normals = new List<Vector3>(maxCards * 8);
        List<Vector2> uvs = new List<Vector2>(maxCards * 8);
        List<Color> colors = new List<Color>(maxCards * 8);
        List<int> triangles = new List<int>(maxCards * 12);

        Vector2 center = field.Center;
        float halfSpan = maxBorder + 1100f;
        int cards = 0;
        float minHeight = float.MaxValue;
        float maxHeight = float.MinValue;

        for (float z = center.y - halfSpan; z <= center.y + halfSpan; z += gridStep)
        {
            for (float x = center.x - halfSpan; x <= center.x + halfSpan; x += gridStep)
            {
                if (cards >= maxCards)
                {
                    break;
                }

                float border = field.BorderDistance(x, z);
                if (border < minBorder || border > maxBorder)
                {
                    continue;
                }

                float ridge = field.Ridge(x, z);
                if (ridge < 0.60f)
                {
                    continue;
                }

                if (field.GapNoise(x, z) < 0.32f)
                {
                    continue;
                }

                int hashX = Mathf.FloorToInt((x - center.x) / gridStep);
                int hashZ = Mathf.FloorToInt((z - center.y) / gridStep);
                float place = HorizonNoise.Hash(hashX, hashZ, seed + 101);
                if (place > 0.94f)
                {
                    continue;
                }

                float height = field.HeightWithBorder(x, z, out border);
                float baseHeight = field.BaseHeight(x, z, out border);
                if (height - baseHeight < 10f)
                {
                    continue;
                }

                Vector3 normal = field.Normal(x, z, 18f);
                if (normal.y < 0.80f)
                {
                    continue;
                }

                // Tangent along the ridge crest (perpendicular to the ridge gradient).
                float e = 22f;
                float gx = field.Ridge(x + e, z) - field.Ridge(x - e, z);
                float gz = field.Ridge(x, z + e) - field.Ridge(x, z - e);
                Vector3 gradient = new Vector3(gx, 0f, gz);
                Vector3 tangent = gradient.sqrMagnitude > 0.000001f
                    ? Vector3.Cross(Vector3.up, gradient).normalized
                    : Vector3.right;

                float h1 = HorizonNoise.Hash(hashX, hashZ, seed + 103);
                float h2 = HorizonNoise.Hash(hashX, hashZ, seed + 107);
                float h3 = HorizonNoise.Hash(hashX, hashZ, seed + 109);
                float h4 = HorizonNoise.Hash(hashX, hashZ, seed + 113);

                tangent = Quaternion.Euler(0f, (h3 - 0.5f) * 36f, 0f) * tangent;

                float width = cardBaseWidth + h1 * 42f;
                float cardHeight = cardBaseHeight + h2 * 8f;

                int speciesRow;
                if (h4 > 0.94f)
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
                    0.76f + h1 * 0.22f,
                    0.80f + h2 * 0.18f,
                    0.70f + h3 * 0.18f,
                    1f);

                for (int quad = 0; quad < 2; quad++)
                {
                    Vector3 direction = Quaternion.Euler(0f, quad == 0 ? 0f : 62f, 0f) * tangent;
                    Vector3 left = new Vector3(x - direction.x * width * 0.5f, 0f, z - direction.z * width * 0.5f);
                    Vector3 right = new Vector3(x + direction.x * width * 0.5f, 0f, z + direction.z * width * 0.5f);

                    float leftHeight = field.Height(left.x, left.z);
                    float rightHeight = field.Height(right.x, right.z);
                    float bottom = Mathf.Min(leftHeight, rightHeight) - 2.5f;
                    float top = Mathf.Max(leftHeight, rightHeight) + cardHeight - 1.0f;

                    Vector3 rootOffset = new Vector3(-center.x, 0f, -center.y);
                    Vector3 v0 = new Vector3(left.x, bottom, left.z) + rootOffset;
                    Vector3 v1 = new Vector3(right.x, bottom, right.z) + rootOffset;
                    Vector3 v2 = new Vector3(right.x, top, right.z) + rootOffset;
                    Vector3 v3 = new Vector3(left.x, top, left.z) + rootOffset;

                    float u0 = speciesRow * 0.25f + 0.004f;
                    float u1 = (speciesRow + 1) * 0.25f - 0.004f;

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
                    uvs.Add(new Vector2(0.004f, u0));
                    uvs.Add(new Vector2(0.996f, u0));
                    uvs.Add(new Vector2(0.996f, u1));
                    uvs.Add(new Vector2(0.004f, u1));

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

            if (cards >= maxCards)
            {
                break;
            }
        }

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
}

