using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Temporary GPU-optimization harness for the grass detail pass (project-local).
/// Menu: Tools/DroneSimulator/Grass Optimization
/// - Simplifies the heavy grass meshes ~2x (quad-row decimation, no UV/shape loss)
/// - Removes the two empty trailing detail prototypes from the terrain
/// - Captures fixed near/mid grass comparison screenshots (RenderTexture, play mode)
/// - Flips Depth Priming and runs the reproducible PerfBenchmarkRunner A/B
/// </summary>
public static class GrassOptimizationTools
{
    const string TerrainAssetPath = "Assets/New Terrain.asset";
    const string RendererAssetPath = "Assets/Settings/PC_Renderer.asset";
    const string ShotFolder = "Assets/Screenshots/GrassOptimization";

    static readonly string[] HeavyMeshPaths =
    {
        "Assets/TerrainSampleAssets/Models/Grass_B.asset",
        "Assets/TerrainSampleAssets/Models/Grass_C.asset"
    };

    // =====================================================================
    // 1. Mesh simplification
    // =====================================================================
    [MenuItem("Tools/DroneSimulator/Grass Optimization/1. Simplify Heavy Grass Meshes")]
    public static void SimplifyHeavyMeshes()
    {
        var report = new StringBuilder();
        for (int i = 0; i < HeavyMeshPaths.Length; i++)
        {
            report.AppendLine(SimplifyMesh(HeavyMeshPaths[i]));
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[GrassOpt] Mesh simplification finished\n" + report);
    }

    /// <summary>
    /// Pairs triangles that share an edge into quads, then merges neighbouring quads
    /// (a quad "row" becomes a single quad). Only vertices on the dropped interior rung
    /// disappear, so silhouette, volume and per-vertex UVs are preserved.
    /// </summary>
    static string SimplifyMesh(string path)
    {
        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (mesh == null)
        {
            Debug.LogError("[GrassOpt] Mesh not found: " + path);
            return path + ": NOT FOUND";
        }

        BackupMesh(path);

        Vector3[] verts = mesh.vertices;
        Vector3[] normals = mesh.normals;
        Vector2[] uv0 = mesh.uv;
        int[] srcTris = mesh.triangles;
        int triCount = srcTris.Length / 3;

        var edgeTris = new Dictionary<long, List<int>>(triCount * 3);
        for (int t = 0; t < triCount; t++)
        {
            int a = srcTris[t * 3], b = srcTris[t * 3 + 1], c = srcTris[t * 3 + 2];
            AddEdgeTri(edgeTris, a, b, t);
            AddEdgeTri(edgeTris, b, c, t);
            AddEdgeTri(edgeTris, c, a, t);
        }

        var used = new bool[triCount];
        var quads = new List<int[]>(triCount / 2);
        var loneTris = new List<int>();

        for (int t = 0; t < triCount; t++)
        {
            if (used[t]) continue;
            used[t] = true;

            int i0 = srcTris[t * 3], i1 = srcTris[t * 3 + 1], i2 = srcTris[t * 3 + 2];
            int[] tv = { i0, i1, i2 };
            int partner = -1, pw = 0, pu = 0, pv = 0, px = 0;

            for (int k = 0; k < 3; k++)
            {
                int u = tv[k];
                int v = tv[(k + 1) % 3];
                int w = tv[(k + 2) % 3];

                List<int> list;
                if (!edgeTris.TryGetValue(EdgeKey(u, v), out list) || list.Count != 2) continue;
                int other = list[0] == t ? list[1] : list[0];
                if (other == t || used[other]) continue;

                int ou0 = srcTris[other * 3], ou1 = srcTris[other * 3 + 1], ou2 = srcTris[other * 3 + 2];
                int idxU = IndexOf(ou0, ou1, ou2, u);
                int idxV = IndexOf(ou0, ou1, ou2, v);
                if (idxU < 0 || idxV < 0) continue;
                if ((idxU + 1) % 3 == idxV) continue; // same winding -> folded pair, skip

                int x = ou0;
                if (x == u || x == v) x = ou1;
                if (x == u || x == v) x = ou2;
                if (x == u || x == v || x == w) continue; // degenerate quad

                partner = other;
                pw = w;
                pu = u;
                pv = v;
                px = x;
                break;
            }

            if (partner < 0)
            {
                loneTris.Add(t);
                continue;
            }

            used[partner] = true;
            quads.Add(new[] { pw, pu, px, pv });
        }

        // Quad adjacency via shared boundary edges, then greedy disjoint pairing.
        int quadCount = quads.Count;
        var edgeQuads = new Dictionary<long, List<int>>(quadCount * 4);
        for (int qi = 0; qi < quadCount; qi++)
        {
            int[] q = quads[qi];
            for (int k = 0; k < 4; k++)
            {
                AddEdgeInt(edgeQuads, q[k], q[(k + 1) % 4], qi);
            }
        }

        var match = new int[quadCount];
        var merged = new int[quadCount][];
        for (int i = 0; i < quadCount; i++)
        {
            match[i] = -1;
        }

        int mergeCount = 0;
        for (int qi = 0; qi < quadCount; qi++)
        {
            if (match[qi] != -1) continue;
            int[] q = quads[qi];
            for (int k = 0; k < 4; k++)
            {
                List<int> list;
                if (!edgeQuads.TryGetValue(EdgeKey(q[k], q[(k + 1) % 4]), out list) || list.Count != 2) continue;
                int other = list[0] == qi ? list[1] : list[0];
                if (other == qi || match[other] != -1) continue;

                int[] candidate = TryMergeQuads(quads[qi], quads[other]);
                if (candidate == null) continue;

                match[qi] = other;
                match[other] = qi;
                merged[qi] = candidate;
                mergeCount++;
                break;
            }
        }

        var outTris = new List<int>(srcTris.Length / 2);
        for (int qi = 0; qi < quadCount; qi++)
        {
            if (match[qi] > qi) continue; // emitted together with its partner
            int[] q = merged[qi] != null ? merged[qi] : quads[qi];
            EmitQuad(outTris, q, verts);
        }

        for (int i = 0; i < loneTris.Count; i++)
        {
            int t = loneTris[i];
            EmitTriangle(outTris, srcTris[t * 3], srcTris[t * 3 + 1], srcTris[t * 3 + 2], verts);
        }

        // Compact the vertex buffer so unused rung vertices are dropped as well.
        var referenced = new bool[verts.Length];
        for (int i = 0; i < outTris.Count; i++)
        {
            referenced[outTris[i]] = true;
        }

        var remap = new int[verts.Length];
        int newVertexCount = 0;
        for (int i = 0; i < remap.Length; i++)
        {
            if (referenced[i]) remap[i] = newVertexCount++;
            else remap[i] = -1;
        }

        var compactTris = new List<int>(outTris.Count);
        for (int i = 0; i < outTris.Count; i++)
        {
            compactTris.Add(remap[outTris[i]]);
        }

        var newVerts = new Vector3[newVertexCount];
        var newNormals = normals != null && normals.Length == verts.Length ? new Vector3[newVertexCount] : null;
        var newUv0 = uv0 != null && uv0.Length == verts.Length ? new Vector2[newVertexCount] : null;
        for (int i = 0; i < remap.Length; i++)
        {
            int ni = remap[i];
            if (ni < 0) continue;
            newVerts[ni] = verts[i];
            if (newNormals != null) newNormals[ni] = normals[i];
            if (newUv0 != null) newUv0[ni] = uv0[i];
        }

        int srcTriCount = triCount;
        mesh.Clear();
        mesh.SetVertices(newVerts);
        if (newNormals != null) mesh.SetNormals(newNormals);
        if (newUv0 != null) mesh.SetUVs(0, newUv0);
        mesh.SetTriangles(compactTris, 0);
        mesh.RecalculateBounds();
        EditorUtility.SetDirty(mesh);

        string summary = string.Format(CultureInfo.InvariantCulture,
            "{0}: tris {1} -> {2} ({3:F1}%), verts {4} -> {5}, quads {6}, merges {7}, lone tris {8}",
            Path.GetFileNameWithoutExtension(path), srcTriCount, compactTris.Count / 3,
            100f * (compactTris.Count / 3) / Mathf.Max(1, srcTriCount), verts.Length, newVertexCount,
            quadCount, mergeCount, loneTris.Count);
        Debug.Log("[GrassOpt] " + summary);
        return summary;
    }

    static int[] TryMergeQuads(int[] a, int[] b)
    {
        for (int i = 0; i < 4; i++)
        {
            int a0 = a[i], a1 = a[(i + 1) % 4];
            for (int j = 0; j < 4; j++)
            {
                int b0 = b[j], b1 = b[(j + 1) % 4];
                if (a0 != b1 || a1 != b0) continue;

                int[] a2 = { a[(i + 2) % 4], a[(i + 3) % 4], a[i], a[(i + 1) % 4] };
                int[] b2 = { b[j], b[(j + 1) % 4], b[(j + 2) % 4], b[(j + 3) % 4] };
                int[] merged = { a2[0], a2[1], b2[2], b2[3] };

                if (!AreDistinct(merged)) return null;
                return merged;
            }
        }

        return null;
    }

    static bool AreDistinct(int[] q)
    {
        for (int i = 0; i < q.Length; i++)
        {
            for (int j = i + 1; j < q.Length; j++)
            {
                if (q[i] == q[j]) return false;
            }
        }

        return true;
    }

    static void EmitQuad(List<int> tris, int[] q, Vector3[] verts)
    {
        EmitTriangle(tris, q[0], q[1], q[2], verts);
        EmitTriangle(tris, q[0], q[2], q[3], verts);
    }

    static void EmitTriangle(List<int> tris, int a, int b, int c, Vector3[] verts)
    {
        Vector3 cross = Vector3.Cross(verts[b] - verts[a], verts[c] - verts[a]);
        if (cross.sqrMagnitude < 1e-14f) return; // drop degenerate slivers
        tris.Add(a);
        tris.Add(b);
        tris.Add(c);
    }

    static int IndexOf(int a, int b, int c, int value)
    {
        if (a == value) return 0;
        if (b == value) return 1;
        if (c == value) return 2;
        return -1;
    }

    static long EdgeKey(int a, int b)
    {
        int lo = a < b ? a : b;
        int hi = a < b ? b : a;
        return (long)lo * 1000000L + hi;
    }

    static void AddEdgeTri(Dictionary<long, List<int>> map, int a, int b, int tri)
    {
        long key = EdgeKey(a, b);
        List<int> list;
        if (!map.TryGetValue(key, out list))
        {
            list = new List<int>(2);
            map[key] = list;
        }

        list.Add(tri);
    }

    static void AddEdgeInt(Dictionary<long, List<int>> map, int a, int b, int value)
    {
        long key = EdgeKey(a, b);
        List<int> list;
        if (!map.TryGetValue(key, out list))
        {
            list = new List<int>(2);
            map[key] = list;
        }

        list.Add(value);
    }

    static void BackupMesh(string path)
    {
        string dir = Path.GetFullPath("Logs/GrassMeshBackup");
        Directory.CreateDirectory(dir);
        string target = Path.Combine(dir, Path.GetFileName(path) + ".bak");
        if (!File.Exists(target))
        {
            File.Copy(path, target);
            Debug.Log("[GrassOpt] Backup written: " + target);
        }
    }
    // =====================================================================
    // 2. Empty trailing detail prototypes
    // =====================================================================
    [MenuItem("Tools/DroneSimulator/Grass Optimization/2. Remove Empty Detail Prototypes")]
    public static void RemoveEmptyDetailPrototypes()
    {
        TerrainData data = AssetDatabase.LoadAssetAtPath<TerrainData>(TerrainAssetPath);
        if (data == null)
        {
            Debug.LogError("[GrassOpt] TerrainData not found: " + TerrainAssetPath);
            return;
        }

        BackupTerrainData(data);

        DetailPrototype[] before = data.detailPrototypes;
        int width = data.detailResolution;
        int height = data.detailResolution;

        var sums = new List<long>(before.Length);
        for (int i = 0; i < before.Length; i++)
        {
            sums.Add(SumDetailLayer(data, width, height, i));
        }

        int removeCount = 0;
        for (int i = before.Length - 1; i >= 0; i--)
        {
            if (sums[i] == 0) removeCount++;
            else break;
        }

        if (removeCount == 0)
        {
            Debug.Log("[GrassOpt] No trailing empty detail prototypes found.");
            return;
        }

        // The public API preserves every painted detail layer (verified on a temp copy
        // of this TerrainData): existing indices 0..N do not shift.
        var keep = new DetailPrototype[before.Length - removeCount];
        System.Array.Copy(before, keep, keep.Length);
        data.detailPrototypes = keep;
        EditorUtility.SetDirty(data);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(TerrainAssetPath, ImportAssetOptions.ForceUpdate);

        DetailPrototype[] after = data.detailPrototypes;
        var report = new StringBuilder();
        report.AppendLine("[GrassOpt] Detail prototypes: " + before.Length + " -> " + after.Length + " (removed " + removeCount + " empty tail entries)");
        for (int i = 0; i < after.Length; i++)
        {
            long sum = SumDetailLayer(data, width, height, i);
            report.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "  [{0}] {1,-12} densitySum={2} {3}", i,
                after[i].prototype != null ? after[i].prototype.name : "null", sum,
                sum == sums[i] ? "OK" : "CHANGED!"));
        }

        Debug.Log(report.ToString());
    }

    static long SumDetailLayer(TerrainData data, int width, int height, int layer)
    {
        if (layer >= data.detailPrototypes.Length) return 0;
        int[,] map = data.GetDetailLayer(0, 0, width, height, layer);
        long sum = 0;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                sum += map[y, x];
            }
        }

        return sum;
    }

    static void BackupTerrainData(TerrainData data)
    {
        string source = AssetDatabase.GetAssetPath(data);
        string dir = Path.GetFullPath("Logs/GrassMeshBackup");
        Directory.CreateDirectory(dir);
        string target = Path.Combine(dir, Path.GetFileName(source) + ".bak");
        if (!File.Exists(target))
        {
            File.Copy(source, target);
            Debug.Log("[GrassOpt] TerrainData backup written: " + target + " (" + new FileInfo(target).Length + " bytes)");
        }
    }


    // =====================================================================
    // 4. Depth Priming
    // =====================================================================
    [MenuItem("Tools/DroneSimulator/Grass Optimization/Depth Priming - Auto")]
    public static void SetDepthPrimingAuto()
    {
        SetDepthPrimingMode(1);
    }

    [MenuItem("Tools/DroneSimulator/Grass Optimization/Depth Priming - Disabled")]
    public static void SetDepthPrimingDisabled()
    {
        SetDepthPrimingMode(0);
    }

    [MenuItem("Tools/DroneSimulator/Grass Optimization/Depth Priming - Log Current")]
    public static void LogDepthPrimingMode()
    {
        Debug.Log("[GrassOpt] PC_Renderer Depth Priming mode = " + GetDepthPrimingMode() + " (0=Disabled, 1=Auto, 2=Forced)");
    }

    static void SetDepthPrimingMode(int mode)
    {
        var rendererData = AssetDatabase.LoadAssetAtPath<ScriptableObject>(RendererAssetPath);
        if (rendererData == null)
        {
            Debug.LogError("[GrassOpt] Renderer asset not found: " + RendererAssetPath);
            return;
        }

        var so = new SerializedObject(rendererData);
        SerializedProperty prop = so.FindProperty("m_DepthPrimingMode");
        if (prop == null)
        {
            Debug.LogError("[GrassOpt] m_DepthPrimingMode not found on " + RendererAssetPath);
            return;
        }

        prop.intValue = mode;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(rendererData);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(RendererAssetPath, ImportAssetOptions.ForceUpdate);
        Debug.Log("[GrassOpt] PC_Renderer Depth Priming set to " + mode + " (now " + prop.intValue + ")");
    }

    static int GetDepthPrimingMode()
    {
        var rendererData = AssetDatabase.LoadAssetAtPath<ScriptableObject>(RendererAssetPath);
        if (rendererData == null) return -1;
        var so = new SerializedObject(rendererData);
        SerializedProperty prop = so.FindProperty("m_DepthPrimingMode");
        return prop != null ? prop.intValue : -1;
    }


    // =====================================================================
    // 5. Fixed near/mid grass comparison capture
    // =====================================================================
    const string CaptureKey = "GrassOpt.Capture.Active";
    const string CaptureStepKey = "GrassOpt.Capture.Step";
    const string CaptureTagKey = "GrassOpt.Capture.Tag";
    static Camera captureCamera;
    static RenderTexture captureRT;
    static Texture2D captureRead;
    static Camera[] hiddenCameras;
    static Canvas[] hiddenCanvases;
    static int captureWaitFrame;

    [MenuItem("Tools/DroneSimulator/Grass Optimization/0. Capture Grass Comparison - BEFORE")]
    public static void CaptureGrassBefore()
    {
        StartCapture("before");
    }

    [MenuItem("Tools/DroneSimulator/Grass Optimization/0. Capture Grass Comparison - AFTER")]
    public static void CaptureGrassAfter()
    {
        StartCapture("after");
    }

    static void StartCapture(string tag)
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogWarning("[GrassOpt] Already in play mode.");
            return;
        }

        SessionState.SetString(CaptureTagKey, tag);
        SessionState.SetBool(CaptureKey, true);
        SessionState.SetInt(CaptureStepKey, 0);
        captureWaitFrame = 0;
        EditorApplication.update -= CaptureTick;
        EditorApplication.update += CaptureTick;
        EditorApplication.isPlaying = true;
        Debug.Log("[GrassOpt] Capture '" + tag + "': entering play mode");
    }

    [InitializeOnLoadMethod]
    static void InitCapture()
    {
        if (SessionState.GetBool(CaptureKey, false))
        {
            EditorApplication.update -= CaptureTick;
            EditorApplication.update += CaptureTick;
        }
    }

    static Terrain MainTerrain()
    {
        Terrain[] all = Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None);
        return all.Length > 0 ? all[0] : null;
    }

    static float GroundY(float x, float z)
    {
        Terrain terrain = MainTerrain();
        if (terrain == null) return 0f;
        return terrain.SampleHeight(new Vector3(x, 0f, z)) + terrain.transform.position.y;
    }

    static void SetupCaptureCamera(int index)
    {
        if (captureCamera == null)
        {
            var go = new GameObject("~GrassOptCaptureCamera");
            go.hideFlags = HideFlags.DontSave;
            captureCamera = go.AddComponent<Camera>();
            captureCamera.fieldOfView = 60f;
            captureCamera.nearClipPlane = 0.1f;
            captureCamera.farClipPlane = 2000f;
            captureCamera.depth = 100f;
            captureCamera.allowHDR = false;
            captureCamera.allowMSAA = false;
            go.tag = "MainCamera";
            var listener = go.AddComponent<AudioListener>();
            listener.enabled = false;

            captureRT = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            captureRT.name = "~GrassOptCaptureRT";
            captureRT.antiAliasing = 1;
            captureRT.Create();
            captureCamera.targetTexture = captureRT;
            captureRead = new Texture2D(1280, 720, TextureFormat.RGBA32, false, false);
        }

        // Open meadow window (dense grass, no trees within 40 m), found from the
        // terrain tree instances + grass detail layers.
        float cx = 178.5f, cz = 1329.8f;
        Vector3 look = new Vector3(cx, GroundY(cx, cz) + 0.25f, cz);
        Vector3 dir = new Vector3(1f, 0f, 0.35f).normalized;
        Vector3 pos;
        Vector3 target;
        if (index == 0)
        {
            // near: ~1.15 m above ground, 4.5 m from the sampled clump
            pos = look - dir * 4.5f;
            pos.y = GroundY(pos.x, pos.z) + 1.15f;
            target = look;
        }
        else
        {
            // mid: ~3.5 m above ground, 16 m back, looking across ~25 m of meadow
            pos = look - dir * 16f + Vector3.up * 3.5f;
            target = look + dir * 9f;
        }

        captureCamera.transform.position = pos;
        captureCamera.transform.rotation = Quaternion.LookRotation((target - pos).normalized, Vector3.up);
    }

    static void HideOtherCameras()
    {
        Camera[] cams = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
        var list = new List<Camera>();
        for (int i = 0; i < cams.Length; i++)
        {
            Camera c = cams[i];
            if (c == null || c == captureCamera || !c.enabled) continue;
            c.enabled = false;
            list.Add(c);
        }

        hiddenCameras = list.ToArray();

        Canvas[] canvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        var canvasList = new List<Canvas>();
        for (int i = 0; i < canvases.Length; i++)
        {
            Canvas c = canvases[i];
            if (c == null || !c.enabled) continue;
            c.enabled = false;
            canvasList.Add(c);
        }

        hiddenCanvases = canvasList.ToArray();
    }

    static void RestoreHidden()
    {
        if (hiddenCameras != null)
        {
            for (int i = 0; i < hiddenCameras.Length; i++)
            {
                if (hiddenCameras[i] != null) hiddenCameras[i].enabled = true;
            }
        }

        if (hiddenCanvases != null)
        {
            for (int i = 0; i < hiddenCanvases.Length; i++)
            {
                if (hiddenCanvases[i] != null) hiddenCanvases[i].enabled = true;
            }
        }

        hiddenCameras = null;
        hiddenCanvases = null;
    }

    static void CaptureShot(string fileName)
    {
        if (captureRT == null) return;
        RenderTexture.active = captureRT;
        captureRead.ReadPixels(new Rect(0, 0, captureRT.width, captureRT.height), 0, 0);
        captureRead.Apply(false, false);
        RenderTexture.active = null;

        string dir = Path.GetFullPath(ShotFolder);
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, fileName);
        File.WriteAllBytes(path, captureRead.EncodeToPNG());

        Color32[] px = captureRead.GetPixels32();
        long r = 0, g = 0, b = 0;
        for (int i = 0; i < px.Length; i++) { r += px[i].r; g += px[i].g; b += px[i].b; }
        int n = px.Length;
        Debug.Log("[GrassOpt] Capture " + fileName + " -> avgRGB " + (r / n) + "," + (g / n) + "," + (b / n));
    }

    static void CleanupCapture()
    {
        RestoreHidden();
        if (captureCamera != null)
        {
            captureCamera.targetTexture = null;
            Object.Destroy(captureCamera.gameObject);
            captureCamera = null;
        }

        if (captureRT != null)
        {
            captureRT.Release();
            Object.Destroy(captureRT);
            captureRT = null;
        }

        if (captureRead != null)
        {
            Object.Destroy(captureRead);
            captureRead = null;
        }
    }

    static void CaptureTick()
    {
        if (!SessionState.GetBool(CaptureKey, false))
        {
            EditorApplication.update -= CaptureTick;
            return;
        }

        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        int step = SessionState.GetInt(CaptureStepKey, 0);

        if (step == 0)
        {
            if (!EditorApplication.isPlaying) return;
            captureWaitFrame = Time.frameCount + 12;
            SessionState.SetInt(CaptureStepKey, 1);
            return;
        }

        if (Time.frameCount < captureWaitFrame) return;

        string tag = SessionState.GetString(CaptureTagKey, "shot");
        if (step == 1)
        {
            SetupCaptureCamera(0);
            HideOtherCameras();
            captureWaitFrame = Time.frameCount + 8;
            SessionState.SetInt(CaptureStepKey, 2);
            return;
        }

        if (step == 2)
        {
            CaptureShot(tag + "_near.png");
            SetupCaptureCamera(1);
            captureWaitFrame = Time.frameCount + 8;
            SessionState.SetInt(CaptureStepKey, 3);
            return;
        }

        if (step == 3)
        {
            CaptureShot(tag + "_mid.png");
            CleanupCapture();
            SessionState.SetBool(CaptureKey, false);
            SessionState.SetInt(CaptureStepKey, 0);
            EditorApplication.update -= CaptureTick;
            EditorApplication.isPlaying = false;
            Debug.Log("[GrassOpt] Capture '" + tag + "' finished, leaving play mode");
        }
    }


    // =====================================================================
    // 6. Depth Priming A/B through PerfBenchmarkRunner
    // =====================================================================
    const string BenchKey = "GrassOpt.Bench.Active";
    const string BenchStepKey = "GrassOpt.Bench.Step";
    const string BenchTagKey = "GrassOpt.Bench.Tag";
    static float benchStartTime;
    static int benchWaitFrame;

    [MenuItem("Tools/DroneSimulator/Grass Optimization/3. Benchmark - Depth Priming Auto")]
    public static void RunBenchmarkDepthPrimingAuto()
    {
        StartBenchmark(1, "after_meshes_dp_auto");
    }

    [MenuItem("Tools/DroneSimulator/Grass Optimization/3. Benchmark - Depth Priming Disabled")]
    public static void RunBenchmarkDepthPrimingDisabled()
    {
        StartBenchmark(0, "after_meshes_dp_disabled");
    }

    static void StartBenchmark(int depthPrimingMode, string tag)
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogWarning("[GrassOpt] Already in play mode.");
            return;
        }

        SetDepthPrimingMode(depthPrimingMode);
        SessionState.SetBool(BenchKey, true);
        SessionState.SetInt(BenchStepKey, 0);
        SessionState.SetString(BenchTagKey, tag);
        EditorApplication.update -= BenchTick;
        EditorApplication.update += BenchTick;
        EditorApplication.isPlaying = true;
        Debug.Log("[GrassOpt] Benchmark '" + tag + "': entering play mode (Depth Priming " + depthPrimingMode + ")");
    }

    static float editorBenchStart;

    [InitializeOnLoadMethod]
    static void InitBenchmark()
    {
        if (SessionState.GetBool(BenchKey, false))
        {
            EditorApplication.update -= BenchTick;
            EditorApplication.update += BenchTick;
        }
    }

    static void BenchTick()
    {
        if (!SessionState.GetBool(BenchKey, false))
        {
            EditorApplication.update -= BenchTick;
            return;
        }

        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;

        int step = SessionState.GetInt(BenchStepKey, 0);
        string tag = SessionState.GetString(BenchTagKey, "bench");

        if (step == 0)
        {
            if (!EditorApplication.isPlaying) return;
            editorBenchStart = (float)EditorApplication.timeSinceStartup;
            benchStartTime = Time.realtimeSinceStartup;
            benchWaitFrame = Time.frameCount + 15;
            SessionState.SetInt(BenchStepKey, 1);
            return;
        }

        if (step == 1 && Time.frameCount >= benchWaitFrame)
        {
            PerfBenchmarkRunner.Begin(tag);
            Debug.Log("[GrassOpt] PerfBenchmarkRunner started for '" + tag + "'");
            SessionState.SetInt(BenchStepKey, 2);
            return;
        }

        if (step == 2)
        {
            string status = PerfBenchmarkRunner.Status;
            bool finished = !string.IsNullOrEmpty(status) && (status.StartsWith("done:") || status.StartsWith("error:"));
            bool timedOut = (float)EditorApplication.timeSinceStartup - editorBenchStart > 240f;
            if (!finished && !timedOut) return;

            Debug.Log("[GrassOpt] Benchmark '" + tag + "' status=" + status + " report=" + PerfBenchmarkRunner.ResultPath);
            SessionState.SetBool(BenchKey, false);
            SessionState.SetInt(BenchStepKey, 0);
            EditorApplication.update -= BenchTick;
            EditorApplication.isPlaying = false;
        }
    }



}
