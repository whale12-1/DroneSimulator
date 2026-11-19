using System.IO;
using UnityEditor;
using UnityEngine;

// TEMPORARY render-verification probe (RenderTexture based; ScreenCapture is NOT used).
public static class GrassRTProbe
{
    const string ActiveKey = "GrassRTProbe.Active";
    const string StepKey = "GrassRTProbe.Step";

    static Camera cam;
    static RenderTexture rt;
    static Texture2D readTex;
    static Camera[] hiddenCams;
    static Canvas[] hiddenCanvases;
    static int waitFrame;
    static Color32[] pxBase, pxSphere, pxGrass;

    const int GrassX0 = 107, GrassY0 = 270, GrassSize = 12;
    const int SphereX0 = 108, SphereY0 = 262, SphereSize = 4;
    const int W = 1280, H = 720;

    [InitializeOnLoadMethod]
    static void Init()
    {
        if (SessionState.GetBool(ActiveKey, false)) EditorApplication.update += Tick;
    }

    [MenuItem("Tools/DroneSimulator/Grass Detail/RT Probe")]
    public static void Start()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogWarning("GrassRTProbe: already in play mode");
            return;
        }
        SessionState.SetBool(ActiveKey, true);
        SessionState.SetInt(StepKey, 0);
        waitFrame = 0;
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
        EditorApplication.isPlaying = true;
        Debug.Log("GrassRTProbe: entering play mode");
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
        var td = ter.terrainData;
        var cells = new int[size, size];
        for (int i = 0; i < size; i++) for (int j = 0; j < size; j++) cells[j, i] = density;
        td.SetDetailLayer(x0, y0, layer, cells);
        Debug.Log("GrassRTProbe: layer " + layer + " patch (" + x0 + "," + y0 + ") size=" + size + " density=" + density);
    }


    static void SetupCamera()
    {
        var ter = MainTerrain();
        var td = ter.terrainData;
        Vector3 org = ter.transform.position;
        float cellX = td.size.x / td.detailWidth;
        float cellZ = td.size.z / td.detailHeight;
        float px = org.x + (GrassX0 + GrassSize * 0.5f) * cellX;
        float pz = org.z + (GrassY0 + GrassSize * 0.5f) * cellZ;
        float camZ = pz - 34f;
        float groundY = ter.SampleHeight(new Vector3(px, 0f, camZ)) + org.y;

        var go = new GameObject("~GrassRTProbeCamera");
        cam = go.AddComponent<Camera>();
        cam.fieldOfView = 55f;
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 600f;
        cam.depth = 100f;
        cam.allowHDR = false;
        cam.allowMSAA = false;
        go.tag = "MainCamera";
        var al = go.AddComponent<AudioListener>();
        al.enabled = false;
        cam.transform.position = new Vector3(px, groundY + 3.0f, camZ);

        Vector3 lookAt = new Vector3(px, ter.SampleHeight(new Vector3(px, 0f, pz)) + org.y + 0.6f, pz);
        cam.transform.rotation = Quaternion.LookRotation((lookAt - cam.transform.position).normalized, Vector3.up);

        rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        rt.name = "~GrassRTProbeRT";
        rt.antiAliasing = 1;
        rt.Create();
        cam.targetTexture = rt;
        readTex = new Texture2D(W, H, TextureFormat.RGBA32, false, false);
        Debug.Log("GrassRTProbe: camera pos=" + cam.transform.position.ToString("F2") + " lookAt=" + lookAt.ToString("F2") + " rtCreated=" + rt.IsCreated());
    }

    static void HideOtherCameras()
    {
        var all = Object.FindObjectsByType<Camera>();
        var list = new System.Collections.Generic.List<Camera>();
        foreach (var c in all)
        {
            if (c == null || c == cam || !c.enabled) continue;
            c.enabled = false;
            list.Add(c);
        }
        hiddenCams = list.ToArray();
        var canv = Object.FindObjectsByType<Canvas>();
        var clist = new System.Collections.Generic.List<Canvas>();
        foreach (var c in canv)
        {
            if (c == null || !c.enabled) continue;
            c.enabled = false;
            clist.Add(c);
        }
        hiddenCanvases = clist.ToArray();
        Debug.Log("GrassRTProbe: hidden cameras=" + hiddenCams.Length + " canvases=" + hiddenCanvases.Length);
    }

    static Color32[] Capture(string fileName)
    {
        RenderTexture.active = rt;
        readTex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
        readTex.Apply(false, false);
        RenderTexture.active = null;
        Color32[] px = readTex.GetPixels32();
        string dir = Path.GetFullPath("Assets/Screenshots/GrassDetailTest");
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
        File.WriteAllBytes(Path.Combine(dir, fileName), readTex.EncodeToPNG());
        Debug.Log("GrassRTProbe: capture " + fileName + " frame=" + Time.frameCount + " magentaPx=" + CountMagenta(px) + " avgRGB=" + AvgRgb(px));
        return px;
    }

    static int CountMagenta(Color32[] px)
    {
        int c = 0;
        for (int i = 0; i < px.Length; i++)
            if (px[i].r > 110 && px[i].b > 110 && px[i].g < 100) c++;
        return c;
    }

    static string AvgRgb(Color32[] px)
    {
        long r = 0, g = 0, b = 0;
        for (int i = 0; i < px.Length; i++) { r += px[i].r; g += px[i].g; b += px[i].b; }
        int n = px.Length;
        return (r / n) + "," + (g / n) + "," + (b / n);
    }


    static string Compare(Color32[] a, Color32[] b, string label)
    {
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
        if (cam != null) { cam.targetTexture = null; Object.Destroy(cam.gameObject); cam = null; }
        if (rt != null) { rt.Release(); Object.Destroy(rt); rt = null; }
        if (readTex != null) { Object.Destroy(readTex); readTex = null; }
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
            SetupCamera();
            HideOtherCameras();
            PaintPatch(0, GrassX0, GrassY0, GrassSize, 0);
            PaintPatch(1, SphereX0, SphereY0, SphereSize, 0);
            waitFrame = Time.frameCount + 6;
            SessionState.SetInt(StepKey, 2);
            return;
        }
        if (step == 2)
        {
            pxBase = Capture("rt_00_baseline_nodetails.png");
            waitFrame = Time.frameCount + 4;
            SessionState.SetInt(StepKey, 3);
            return;
        }
        if (step == 3)
        {
            PaintPatch(1, SphereX0, SphereY0, SphereSize, 3);
            waitFrame = Time.frameCount + 6;
            SessionState.SetInt(StepKey, 4);
            return;
        }
        if (step == 4)
        {
            pxSphere = Capture("rt_01_control_sphere.png");
            waitFrame = Time.frameCount + 4;
            SessionState.SetInt(StepKey, 5);
            return;
        }
        if (step == 5)
        {
            PaintPatch(1, SphereX0, SphereY0, SphereSize, 0);
            PaintPatch(0, GrassX0, GrassY0, GrassSize, 3);
            waitFrame = Time.frameCount + 6;
            SessionState.SetInt(StepKey, 6);
            return;
        }
        if (step == 6)
        {
            pxGrass = Capture("rt_02_grass.png");
            waitFrame = Time.frameCount + 4;
            SessionState.SetInt(StepKey, 7);
            return;
        }
        if (step == 7)
        {
            Debug.Log("GrassRTProbe RESULT A -> " + Compare(pxBase, pxSphere, "baseline vs control-sphere"));
            Debug.Log("GrassRTProbe RESULT B -> " + Compare(pxBase, pxGrass, "baseline vs grass"));
            Debug.Log("GrassRTProbe RESULT C -> magentaPx base=" + CountMagenta(pxBase) + " sphere=" + CountMagenta(pxSphere) + " grass=" + CountMagenta(pxGrass));
            Debug.Log("GrassRTProbe RESULT D -> avgRGB base=" + AvgRgb(pxBase) + " sphere=" + AvgRgb(pxSphere) + " grass=" + AvgRgb(pxGrass));
            Cleanup();
            SessionState.SetBool(ActiveKey, false);
            SessionState.SetInt(StepKey, 0);
            EditorApplication.update -= Tick;
            EditorApplication.isPlaying = false;
            Debug.Log("GrassRTProbe: done, exiting play mode");
        }
    }
}
