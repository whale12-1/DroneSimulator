using System.IO;
using UnityEditor;
using UnityEngine;

// Texture helpers for the horizon build: PNG asset creation, importer configuration
// and the forest-strip atlas assembled from the existing SpeedTree billboard textures.
public static class HorizonTextureGenerator
{
    public const int AtlasRows = 4;
    public const int AtlasColumns = 4;

    public static Texture2D WriteTextureAsset(string assetPath, Color32[] pixels, int width, int height)
    {
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false, false);
        texture.SetPixels32(pixels);
        texture.Apply(false, false);

        byte[] png = texture.EncodeToPNG();
        Object.DestroyImmediate(texture);

        string fullPath = Path.Combine(Directory.GetCurrentDirectory(), assetPath.Replace('/', Path.DirectorySeparatorChar));
        string directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllBytes(fullPath, png);
        AssetDatabase.ImportAsset(assetPath,
            ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        return AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
    }

    public static void ConfigureTexture(
        string assetPath,
        bool sRGB,
        int maxSize,
        TextureWrapMode wrapMode,
        int anisoLevel,
        bool preserveAlphaCoverage,
        float alphaCoverageReference)
    {
        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null)
        {
            return;
        }

        importer.textureType = TextureImporterType.Default;
        importer.sRGBTexture = sRGB;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.alphaIsTransparency = false;
        importer.mipmapEnabled = true;
        importer.wrapMode = wrapMode;
        importer.filterMode = FilterMode.Trilinear;
        importer.anisoLevel = Mathf.Max(1, anisoLevel);
        importer.maxTextureSize = Mathf.Max(32, maxSize);
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.mipMapsPreserveCoverage = preserveAlphaCoverage;
        importer.alphaTestReferenceValue = Mathf.Clamp01(alphaCoverageReference);
        importer.isReadable = false;
        importer.SaveAndReimport();
    }

    // Reads texture pixels through the GPU so the source textures do not need to be
    // flagged "Read/Write" (which would modify their .meta files).
    public static Color32[] ReadSourcePixels(Texture2D source)
    {
        int width = source.width;
        int height = source.height;
        RenderTexture target = RenderTexture.GetTemporary(
            width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        Graphics.Blit(source, target);

        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = target;
        Texture2D readable = new Texture2D(width, height, TextureFormat.RGBA32, false, false);
        readable.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        readable.Apply(false, false);
        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(target);

        Color32[] pixels = readable.GetPixels32();
        Object.DestroyImmediate(readable);
        return pixels;
    }

    static int[] FindBestCells(Color32[] pixels, int textureSize, int cellsPerAxis)
    {
        int cellSize = textureSize / cellsPerAxis;
        int[] order = new int[cellsPerAxis * cellsPerAxis];
        float[] coverage = new float[order.Length];

        for (int cell = 0; cell < order.Length; cell++)
        {
            order[cell] = cell;
            int cellX = (cell % cellsPerAxis) * cellSize;
            int cellY = (cell / cellsPerAxis) * cellSize;
            int covered = 0;
            int total = 0;
            for (int y = 0; y < cellSize; y += 4)
            {
                int rowOffset = (cellY + y) * textureSize;
                for (int x = 0; x < cellSize; x += 4)
                {
                    total++;
                    if (pixels[rowOffset + cellX + x].a > 40)
                    {
                        covered++;
                    }
                }
            }
            coverage[cell] = total > 0 ? (float)covered / total : 0f;
        }

        for (int i = 0; i < order.Length - 1; i++)
        {
            for (int j = i + 1; j < order.Length; j++)
            {
                if (coverage[order[j]] > coverage[order[i]])
                {
                    int tmp = order[i];
                    order[i] = order[j];
                    order[j] = tmp;
                }
            }
        }

        return order;
    }

    static Color32 TintPixel(Color32 source, Color tint, float brightness)
    {
        float alpha = source.a / 255f;
        float sharpened = Mathf.Clamp01((alpha - 0.12f) / 0.72f);
        byte r = (byte)Mathf.Clamp(Mathf.RoundToInt(source.r * tint.r * brightness), 0, 255);
        byte g = (byte)Mathf.Clamp(Mathf.RoundToInt(source.g * tint.g * brightness), 0, 255);
        byte b = (byte)Mathf.Clamp(Mathf.RoundToInt(source.b * tint.b * brightness), 0, 255);
        byte a = (byte)Mathf.Clamp(Mathf.RoundToInt(sharpened * 255f), 0, 255);
        return new Color32(r, g, b, a);
    }

    // Builds a 4x4 cell atlas out of the existing SpeedTree billboard textures:
    // rows 0-2 are conifer / pine / cypress strips, row 3 mixes the species.
    // Each cell contains four trees (the source frames are 4x4 grids of views), so a
    // single wide card reads as a continuous forest belt instead of a single tree.
    public static Color32[] BuildForestAtlas(int atlasSize, string[] sourceGuids, int seed, System.Text.StringBuilder report)
    {
        int columns = AtlasColumns;
        int rows = AtlasRows;
        int cellSize = atlasSize / columns;
        Color32[] atlas = new Color32[atlasSize * atlasSize];

        Color[] speciesTint =
        {
            new Color(0.58f, 0.66f, 0.52f),
            new Color(0.62f, 0.68f, 0.50f),
            new Color(0.56f, 0.64f, 0.48f)
        };

        Color32[][] sources = new Color32[3][];
        int[] sourceSizes = new int[3];
        int[][] bestCells = new int[3][];

        for (int s = 0; s < 3 && s < sourceGuids.Length; s++)
        {
            string path = AssetDatabase.GUIDToAssetPath(sourceGuids[s]);
            Texture2D source = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (source == null)
            {
                report.AppendLine("  missing source texture: " + path);
                continue;
            }

            sources[s] = ReadSourcePixels(source);
            sourceSizes[s] = source.width;
            bestCells[s] = FindBestCells(sources[s], source.width, 4);
            report.AppendLine("  " + source.name + " " + source.width + "x" + source.height +
                              " best frames: " + bestCells[s][0] + ", " + bestCells[s][1] + ", " +
                              bestCells[s][2] + ", " + bestCells[s][3]);
        }

        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < columns; col++)
            {
                int species;
                int cellSlot;
                if (row < 3)
                {
                    species = row;
                    cellSlot = col;
                }
                else
                {
                    species = col % 3;
                    cellSlot = 1 + (col / 3);
                }

                Color32[] pixels = sources[species];
                if (pixels == null)
                {
                    continue;
                }

                int[] cells = bestCells[species];
                int cellIndex = cells[Mathf.Clamp(cellSlot, 0, cells.Length - 1)];
                int sourceSize = sourceSizes[species];
                int sourceCellSize = sourceSize / 4;
                int sourceCellX = (cellIndex % 4) * sourceCellSize;
                int sourceCellY = (cellIndex / 4) * sourceCellSize;

                float brightness = 0.90f + HorizonNoise.Hash(col, row, seed + 701) * 0.18f;
                Color tint = speciesTint[species];

                for (int y = 0; y < cellSize; y++)
                {
                    int sourceY = sourceCellY + Mathf.Clamp(y * sourceCellSize / cellSize, 0, sourceCellSize - 1);
                    int destinationRow = (row * cellSize + y) * atlasSize + col * cellSize;
                    int sourceRow = sourceY * sourceSize + sourceCellX;
                    for (int x = 0; x < cellSize; x++)
                    {
                        int sourceX = Mathf.Clamp(x * sourceCellSize / cellSize, 0, sourceCellSize - 1);
                        atlas[destinationRow + x] = TintPixel(pixels[sourceRow + sourceX], tint, brightness);
                    }
                }
            }
        }

        return atlas;
    }
}
