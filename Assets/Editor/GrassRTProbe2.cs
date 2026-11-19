using System.IO;
using UnityEditor;
using UnityEngine;

// TEMPORARY render probe v2: recreates camera+RT before every capture so each read is a fresh frame.
public static class GrassRTProbe2
{
    const string ActiveKey = "GrassRTProbe2.Active";
    const string StepKey = "GrassRTProbe2.Step";

    static Camera cam;
    static RenderTexture rt;
    static Texture2D readTex;
    static Camera[] hiddenCams;
    static Canvas[] hiddenCanvases;
    static int waitFrame;

    static readonly Color32[][] px = new Color32[5][];
    static readonly string[] pxName = new string[5] {
        "rt2_00_baseline.png", "rt2_01_baseline_repeat.png", "rt2_02_camera_moved.png",
        "rt2_03_control_sphere.png", "rt2_04_grass.png" };
    static int pendingSlot = -1;
    static string pendingFile;

    static Vector3 camPos;
    static Quaternion camRot;

    const int GrassX0 = 107, GrassY0 = 270, GrassSize = 12;
    const int SphereX0 = 108, SphereY0 = 262, SphereSize = 4;
    const int W = 960, H = 540;

    [InitializeOnLoadMethod]
    static void Init()
    {
        if (SessionState.GetBool(ActiveKey, false)) EditorApplication.update += Tick;
    }

    [MenuItem("Tools/DroneSimulator/Grass Detail/RT Probe v2")]
    public static void Start()
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning("GrassRTProbe2: already in play mode"); return; }
        SessionState.SetBool(ActiveKey, true);
        SessionState.SetInt(StepKey, 0);
        waitFrame = 0;
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
        EditorApplication.isPlaying = true;
        Debug.Log("GrassRTProbe2: entering play mode");
    }

    static Terrain MainTerrain()
    {
        var go = GameObject.Find("Terrain");
        return go == null ? null : go.GetComponent<Terrain>();
    }

    static void PaintPatch(int layer, int x0, int y0, int size, int density)
    {
        var ter = MainTerrain();
        if (ter == null) return;
        var cells = new int[size, size];
        for (int i = 0; i < size; i++) for (int j = 0; j < size; j++) cells[j, i] = density;
        ter.terrainData.SetDetailLayer(x0, y0, layer, cells);
        Debug.Log("GrassRTProbe2: layer " + layer + " patch (" + x0 + "," + y0 + ") size=" + size + " density=" + density);
    }

    static void SetCamPose(int pose)
    {
        var ter = MainTerrain();
        var td = ter.terrainData;
        Vector3 org = ter.transform.position;
        float cellX = td.size.x / td.detailWidth;
        float cellZ = td.size.z / td.detailHeight;
        float pxw = org.x + (GrassX0 + GrassSize * 0.5f) * cellX;
        float pzw = org.z + (GrassY0 + GrassSize * 0.5f) * cellZ;
        float camZ = pzw - 34f;
        if (pose == 1) camZ -= 8f;
        float groundY = ter.SampleHeight(new Vector3(pxw, 0f, camZ)) + org.y;
        camPos = new Vector3(pxw, groundY + 3.0f, camZ);
        Vector3 lookAt = new Vector3(pxw, ter.SampleHeight(new Vector3(pxw, 0f, pzw)) + org.y + 0.6f, pzw);
        camRot = Quaternion.LookRotation((lookAt - camPos).normalized, Vector3.up);
        Debug.Log("GrassRTProbe2: cam pose " + pose + " pos=" + camPos.ToString("F2"));
    }

    static void RebuildProbe()
    {
        DestroyProbe();
        var go = new GameObject("~GrassRTProbe2Camera");
        cam = go.AddComponent<Camera>();
        cam.fieldOfView = 55f;
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 600f;
        cam.depth = 200f;
        cam.allowHDR = false;
        cam.allowMSAA = false;
        cam.clearFlags = CameraClearFlags.Skybox;
        go.tag = "MainCamera";
        var al = go.AddComponent<AudioListener>();
        al.enabled = false;
        cam.transform.position = camPos;
        cam.transform.rotation = camRot;
        rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        rt.name = "~GrassRTProbe2RT";
        rt.antiAliasing = 1;
        rt.Create();
        cam.targetTexture = rt;
        if (readTex == null) readTex = new Texture2D(W, H, TextureFormat.RGBA32, false, false);
        Debug.Log("GrassRTProbe2: probe rebuilt, rtCreated=" + rt.IsCreated() + " cam.enabled=" + cam.enabled);
    }

    static void DestroyProbe()
    {
        if (cam != null) { cam.targetTexture = null; Object.Destroy(cam.gameObject); cam = null; }
        if (rt != null) { rt.Release(); Object.Destroy(rt); rt = null; }
    }

    static void BeginCapture(int slot, string file)
    {
        pendingSlot = slot;
        pendingFile = file;
        RebuildProbe();
    }

    static void EndCapture()
    {
        if (pendingSlot < 0 || rt == null) { Debug.LogWarning("GrassRTProbe2: EndCapture without BeginCapture"); return; }
        RenderTexture.active = rt;
        readTex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
        readTex.Apply(false, false);
        RenderTexture.active = null;
        px[pendingSlot] = readTex.GetPixels32();

        string dir = Path.GetFullPath("Assets/Screenshots/GrassDetailTest");
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
        File.WriteAllBytes(Path.Combine(dir, pendingFile), readTex.EncodeToPNG());

        Debug.Log("GrassRTProbe2: captured slot " + pendingSlot + " " + pendingFile + " frame=" + Time.frameCount +
                  " magentaPx=" + CountMagenta(px[pendingSlot]) + " avgRGB=" + AvgRgb(px[pendingSlot]));
        pendingSlot = -1;
    }

    static int CountMagenta(Color32[] p)
    {
        int c = 0;
        for (int i = 0; i < p.Length; i++)
            if (p[i].r > 100 && p[i].b > 100 && p[i].g < 100) c++;
        return c;
    }

    static string AvgRgb(Color32[] p)
    {
        long r = 0, g = 0, b = 0;
        for (int i = 0; i < p.Length; i++) { r += p[i].r; g += p[i].g; b += p[i].b; }
        int n = p.Length;
        return (r / n) + "," + (g / n) + "," + (b / n);
    }

    static string Compare(int ia, int ib, string label)
    {
        var a = px[ia];
        var b = px[ib];
        if (a == null || b == null) return label + ": missing data";
        int diff = 0; long sum = 0;
        int minX = int.MaxValue, maxX = -1, minY = int.MaxValue, maxY = -1;
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                int i = y * W + x;
                int d = Mathf.Abs(a[i].r - b[i].r) + Mathf.Abs(a[i].g - b[i].g) + Mathf.Abs(a[i].b - b[i].b);
                sum += d;
                if (d > 24)
                {
                    diff++;
                    if (x < minX) minX = x; if (x > maxX) maxX = x;
                    if (y < minY) minY = y; if (y > maxY) maxY = y;
                }
            }
        string bbox = diff > 0 ? (" bboxPx x[" + minX + ".." + maxX + "] y[" + minY + ".." + maxY + "]") : "";
        return label + ": changed=" + diff + "/" + (W * H) + " (" + (100f * diff / (W * H)).ToString("F2") + "%) meanAbsDiff=" + ((float)sum / (W * H)).ToString("F2") + bbox;
    }

    static void Cleanup()
    {
        if (hiddenCams != null) foreach (var c in hiddenCams) if (c != null) c.enabled = true;
        if (hiddenCanvases != null) foreach (var c in hiddenCanvases) if (c != null) c.enabled = true;
        hiddenCams = null; hiddenCanvases = null;
        DestroyProbe();
        if (readTex != null) { Object.Destroy(readTex); readTex = null; }
    }

    static void HideOtherCameras()
    {
        var all = Object.FindObjectsByType<Camera>();
        var list = new System.Collections.Generic.List<Camera>();
        foreach (var c in all) { if (c == null || c == cam || !c.enabled) continue; c.enabled = false; list.Add(c); }
        hiddenCams = list.ToArray();
        var canv = Object.FindObjectsByType<Canvas>();
        var clist = new System.Collections.Generic.List<Canvas>();
        foreach (var c in canv) { if (c == null || !c.enabled) continue; c.enabled = false; clist.Add(c); }
        hiddenCanvases = clist.ToArray();
        Debug.Log("GrassRTProbe2: hidden cameras=" + hiddenCams.Length + " canvases=" + hiddenCanvases.Length);
    }

    static void Tick()
    {
        if (!SessionState.GetBool(ActiveKey, false)) { EditorApplication.update -= Tick; return; }
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;

        int step = SessionState.GetInt(StepKey, 0);

        if (step == 0)
        {
            if (!EditorApplication.isPlaying) return;
            waitFrame = Time.frameCount + 8;
            SessionState.SetInt(StepKey, 1);
            return;
        }
        if (Time.frameCount < waitFrame) return;

        if (step == 1)
        {
            SetCamPose(0);
            PaintPatch(0, GrassX0, GrassY0, GrassSize, 0);
            PaintPatch(1, SphereX0, SphereY0, SphereSize, 0);
            RebuildProbe();
            HideOtherCameras();
            waitFrame = Time.frameCount + 6;
            SessionState.SetInt(StepKey, 2);
            return;
        }
        if (step == 2) { BeginCapture(0, pxName[0]); waitFrame = Time.frameCount + 5; SessionState.SetInt(StepKey, 3); return; }
        if (step == 3) { EndCapture(); waitFrame = Time.frameCount + 2; SessionState.SetInt(StepKey, 4); return; }
        if (step == 4) { BeginCapture(1, pxName[1]); waitFrame = Time.frameCount + 5; SessionState.SetInt(StepKey, 5); return; }
        if (step == 5) { EndCapture(); SetCamPose(1); waitFrame = Time.frameCount + 2; SessionState.SetInt(StepKey, 6); return; }
        if (step == 6) { BeginCapture(2, pxName[2]); waitFrame = Time.frameCount + 5; SessionState.SetInt(StepKey, 7); return; }
        if (step == 7) { EndCapture(); SetCamPose(0); PaintPatch(1, SphereX0, SphereY0, SphereSize, 3); waitFrame = Time.frameCount + 3; SessionState.SetInt(StepKey, 8); return; }
        if (step == 8) { BeginCapture(3, pxName[3]); waitFrame = Time.frameCount + 5; SessionState.SetInt(StepKey, 9); return; }
        if (step == 9) { EndCapture(); PaintPatch(1, SphereX0, SphereY0, SphereSize, 0); PaintPatch(0, GrassX0, GrassY0, GrassSize, 3); waitFrame = Time.frameCount + 3; SessionState.SetInt(StepKey, 10); return; }
        if (step == 10) { BeginCapture(4, pxName[4]); waitFrame = Time.frameCount + 5; SessionState.SetInt(StepKey, 11); return; }
        if (step == 11) { EndCapture(); waitFrame = Time.frameCount + 2; SessionState.SetInt(StepKey, 12); return; }
        if (step == 12)
        {
            Debug.Log("GrassRTProbe2 CHECK stability -> " + Compare(0, 1, "baseline vs baseline_repeat"));
            Debug.Log("GrassRTProbe2 CHECK liveness  -> " + Compare(0, 2, "baseline vs camera_moved"));
            Debug.Log("GrassRTProbe2 RESULT sphere   -> " + Compare(0, 3, "baseline vs control-sphere"));
            Debug.Log("GrassRTProbe2 RESULT grass    -> " + Compare(0, 4, "baseline vs grass"));
            Debug.Log("GrassRTProbe2 magenta: base=" + CountMagenta(px[0]) + " base2=" + CountMagenta(px[1]) + " moved=" + CountMagenta(px[2]) + " sphere=" + CountMagenta(px[3]) + " grass=" + CountMagenta(px[4]));
            Cleanup();
            SessionState.SetBool(ActiveKey, false);
            SessionState.SetInt(StepKey, 0);
            EditorApplication.update -= Tick;
            EditorApplication.isPlaying = false;
            Debug.Log("GrassRTProbe2: done, exiting play mode");
        }
    }
}

