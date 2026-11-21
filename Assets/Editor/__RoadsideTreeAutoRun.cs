using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// TEMPORARY auto-run bootstrap. Deleted after the tree rebuild.
[InitializeOnLoad]
static class RoadsideTreeAutoRun
{
    const string MarkerName = "Temp/RoadsideTreeAutoRun.enabled";
    const string ReportName = "Logs/RoadsideTreeAutoRun.txt";

    static RoadsideTreeAutoRun()
    {
        EditorApplication.delayCall += TryRun;
    }

    static void TryRun()
    {
        string root = Directory.GetCurrentDirectory();
        string marker = Path.Combine(root, MarkerName);
        if (!File.Exists(marker)) return;

        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.delayCall += TryRun;
            return;
        }

        try { File.Delete(marker); } catch { }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("started " + System.DateTime.Now.ToString("s"));
        try
        {
            RoadsideTreeBuilder.Generate();
            sb.AppendLine("generate ok");
            AppendStats(sb);
        }
        catch (System.Exception e)
        {
            sb.AppendLine("EXCEPTION: " + e);
        }

        Directory.CreateDirectory(Path.Combine(root, "Logs"));
        File.WriteAllText(Path.Combine(root, ReportName), sb.ToString());
    }

    static void AppendStats(System.Text.StringBuilder sb)
    {
        GameObject go = GameObject.Find("Terrain");
        if (go == null)
        {
            sb.AppendLine("NO TERRAIN OBJECT");
            return;
        }

        Terrain terrain = go.GetComponent<Terrain>();
        TerrainData d = terrain.terrainData;
        TreeInstance[] inst = d.treeInstances;
        Vector3 origin = terrain.transform.position;
        Vector3 size = d.size;
        int w = d.alphamapWidth, h = d.alphamapHeight;
        float cellX = size.x / w, cellZ = size.z / h;
        float[,,] maps = d.GetAlphamaps(0, 0, w, h);

        int grassA = -1, grassB = -1;
        for (int i = 0; i < d.terrainLayers.Length; i++)
        {
            if (d.terrainLayers[i] == null) continue;
            if (d.terrainLayers[i].name.IndexOf("grass", System.StringComparison.OrdinalIgnoreCase) < 0) continue;
            if (grassA < 0) grassA = i; else grassB = i;
        }
        if (grassA < 0) grassA = 0;
        if (grassB < 0) grassB = 0;

        bool[] mask = new bool[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                mask[y * w + x] = maps[y, x, 10] > 0.2f;

        float[] dist = new float[w * h];
        for (int i = 0; i < dist.Length; i++) dist[i] = mask[i] ? 0f : 1e9f;
        float D1 = 1f, D2 = 1.4142136f;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int i = y * w + x; float v = dist[i];
                if (x > 0) v = Mathf.Min(v, dist[i - 1] + D1);
                if (y > 0) v = Mathf.Min(v, dist[i - w] + D1);
                if (x > 0 && y > 0) v = Mathf.Min(v, dist[i - w - 1] + D2);
                if (x < w - 1 && y > 0) v = Mathf.Min(v, dist[i - w + 1] + D2);
                dist[i] = v;
            }
        for (int y = h - 1; y >= 0; y--)
            for (int x = w - 1; x >= 0; x--)
            {
                int i = y * w + x; float v = dist[i];
                if (x < w - 1) v = Mathf.Min(v, dist[i + 1] + D1);
                if (y < h - 1) v = Mathf.Min(v, dist[i + w] + D1);
                if (x < w - 1 && y < h - 1) v = Mathf.Min(v, dist[i + w + 1] + D2);
                if (x > 0 && y < h - 1) v = Mathf.Min(v, dist[i + w - 1] + D2);
                dist[i] = v;
            }
        float minCell = Mathf.Min(cellX, cellZ);
        for (int i = 0; i < dist.Length; i++)
            dist[i] = Mathf.Max(0f, dist[i] * minCell - minCell * 0.5f);

        int[] species = new int[3];
        int core = 0, back = 0, far = 0;
        int roadViol = 0, pureFarmViol = 0, farmWithGrass = 0;
        float maxRoad = 0f, maxFarmPure = 0f;
        float minHS = 9f, maxHS = -9f, ratioMin = 9f, ratioMax = -9f;
        float rotMin = 99f, rotMax = -99f, colMin = 9f, colMax = -9f;
        bool color32 = typeof(TreeInstance).GetField("color").FieldType == typeof(Color32);
        float colorScale = color32 ? 1f / 255f : 1f;
        float bucketSize = 5f;
        int bw = Mathf.CeilToInt(size.x / bucketSize), bh = Mathf.CeilToInt(size.z / bucketSize);
        var buckets = new List<int>[bw * bh];

        for (int i = 0; i < inst.Length; i++)
        {
            TreeInstance ti = inst[i];
            if (ti.prototypeIndex >= 0 && ti.prototypeIndex < 3) species[ti.prototypeIndex]++;

            float wx = origin.x + ti.position.x * size.x;
            float wz = origin.z + ti.position.z * size.z;
            int bx = Mathf.Clamp((int)((wx - origin.x) / bucketSize), 0, bw - 1);
            int bz = Mathf.Clamp((int)((wz - origin.z) / bucketSize), 0, bh - 1);
            int b = bz * bw + bx;
            if (buckets[b] == null) buckets[b] = new List<int>();
            buckets[b].Add(i);

            int ax = Mathf.Clamp((int)(ti.position.x * (w - 1)), 0, w - 1);
            int az = Mathf.Clamp((int)(ti.position.z * (h - 1)), 0, h - 1);
            float road = maps[az, ax, 10];
            float farm = maps[az, ax, 6];
            float grass = maps[az, ax, grassA] + maps[az, ax, grassB];
            if (road > maxRoad) maxRoad = road;
            if (road > 0.10f) roadViol++;
            if (farm > 0.15f)
            {
                if (grass > 0.20f) farmWithGrass++;
                else
                {
                    pureFarmViol++;
                    if (farm > maxFarmPure) maxFarmPure = farm;
                }
            }

            float dd = dist[az * w + ax];
            if (dd <= 26f) core++;
            else if (dd <= 44f) back++;
            else far++;

            if (ti.heightScale < minHS) minHS = ti.heightScale;
            if (ti.heightScale > maxHS) maxHS = ti.heightScale;
            float ratio = ti.widthScale / ti.heightScale;
            if (ratio < ratioMin) ratioMin = ratio;
            if (ratio > ratioMax) ratioMax = ratio;
            if (ti.rotation < rotMin) rotMin = ti.rotation;
            if (ti.rotation > rotMax) rotMax = ti.rotation;
            float lo = colorScale * Mathf.Min(ti.color.r, Mathf.Min(ti.color.g, ti.color.b));
            float hi = colorScale * Mathf.Max(ti.color.r, Mathf.Max(ti.color.g, ti.color.b));
            if (lo < colMin) colMin = lo;
            if (hi > colMax) colMax = hi;
        }

        float minSpacing = 1e9f;
        for (int bz = 0; bz < bh; bz++)
            for (int bx = 0; bx < bw; bx++)
            {
                List<int> list = buckets[bz * bw + bx];
                if (list == null) continue;
                for (int dz = -1; dz <= 1; dz++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int z = bz + dz, x = bx + dx;
                        if (z < 0 || z >= bh || x < 0 || x >= bw) continue;
                        List<int> other = buckets[z * bw + x];
                        if (other == null) continue;
                        for (int a = 0; a < list.Count; a++)
                            for (int b = 0; b < other.Count; b++)
                            {
                                int ia = list[a], ib = other[b];
                                if (ib <= ia) continue;
                                Vector3 pa = inst[ia].position, pb = inst[ib].position;
                                float ddx = (pa.x - pb.x) * size.x, ddz = (pa.z - pb.z) * size.z;
                                float sq = ddx * ddx + ddz * ddz;
                                if (sq < minSpacing) minSpacing = Mathf.Sqrt(sq);
                            }
                    }
            }

        sb.AppendLine("count=" + inst.Length + " core=" + core + " back=" + back + " far=" + far);
        sb.AppendLine("species=" + species[0] + "/" + species[1] + "/" + species[2]);
        sb.AppendLine("minSpacing=" + minSpacing.ToString("F2"));
        sb.AppendLine("roadMax=" + maxRoad.ToString("F3") + " roadViol=" + roadViol);
        sb.AppendLine("pureFarmViol=" + pureFarmViol + " maxPureFarm=" + maxFarmPure.ToString("F3") +
                      " farmWithGrass=" + farmWithGrass);
        sb.AppendLine("heightScale=" + minHS.ToString("F2") + ".." + maxHS.ToString("F2") +
                      " ratio=" + ratioMin.ToString("F2") + ".." + ratioMax.ToString("F2"));
        sb.AppendLine("rotation=" + rotMin.ToString("F2") + ".." + rotMax.ToString("F2") +
                      " color=" + colMin.ToString("F2") + ".." + colMax.ToString("F2"));
        sb.AppendLine("treePrototypes=" + d.treePrototypes.Length);
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        sb.AppendLine("scene=" + scene.path + " dirty=" + scene.isDirty);
        sb.AppendLine("grassLayers=" + grassA + "," + grassB);
    }
}
