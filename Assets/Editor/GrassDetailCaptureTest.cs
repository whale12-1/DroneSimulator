using System.IO;
using UnityEditor;
using UnityEngine;

// TEMPORARY test harness (can be deleted after verification).
// Enters Play Mode, temporarily hides other cameras/overlay UI, captures a few
// screenshots of the grass detail test patch, then restores everything and exits Play Mode.
public static class GrassDetailCaptureTest
{
    const string ActiveKey = "GrassDetailCapture.Active";
    const string StepKey = "GrassDetailCapture.Step";

    static Camera testCamera;
    static Camera[] hiddenCameras;
    static Canvas[] hiddenCanvases;
    static int waitUntilFrame;
    static int shotIndex;
    static int capturedCount;
    static float startTime;

    [InitializeOnLoadMethod]
    static void Init()
    {
        if (SessionState.GetBool(ActiveKey, false))
            EditorApplication.update += Tick;
    }

    [MenuItem("Tools/DroneSimulator/Grass Detail/Capture Test Shots")]
    public static void Start()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogWarning("GrassDetailCaptureTest: already in play mode.");
            return;
        }
        string dir = ShotFolder;
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
        SessionState.SetBool(ActiveKey, true);
        SessionState.SetInt(StepKey, 0);
        waitUntilFrame = 0;
        shotIndex = 0;
        capturedCount = 0;
        startTime = (float)EditorApplication.timeSinceStartup;
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
        EditorApplication.isPlaying = true;
        Debug.Log("GrassDetailCaptureTest: entering play mode...");
    }

    static string ShotFolder
    {
        get { return Path.GetFullPath("Assets/Screenshots/GrassDetailTest"); }
    }

    static float GroundY(float x, float z)
    {
        var go = GameObject.Find("Terrain");
        if (go == null) return 0f;
        var terrain = go.GetComponent<Terrain>();
        return terrain.SampleHeight(new Vector3(x, 0f, z)) + go.transform.position.y;
    }

    static void SetupCamera(int index)
    {
        float cx = -621f, cz = 569f;
        if (testCamera == null)
        {
            var go = new GameObject("~GrassDetailTestCamera");
            testCamera = go.AddComponent<Camera>();
            testCamera.fieldOfView = 60f;
            testCamera.nearClipPlane = 0.1f;
            testCamera.farClipPlane = 2000f;
            testCamera.depth = 100f;
            go.tag = "MainCamera";
            var listener = go.AddComponent<AudioListener>();
            listener.enabled = false;
        }

        Vector3 pos, target;
        if (index == 0)
        {
            pos = new Vector3(cx + 2.0f, 0f, cz - 4.5f);
            pos.y = GroundY(pos.x, pos.z) + 1.15f;
            target = new Vector3(cx, GroundY(cx, cz) + 0.25f, cz);
        }
        else if (index == 1)
        {
            pos = new Vector3(cx + 4.0f, 0f, cz - 9.0f);
            pos.y = GroundY(pos.x, pos.z) + 1.8f;
            target = new Vector3(cx, GroundY(cx, cz) + 0.30f, cz);
        }
        else
        {
            pos = new Vector3(cx, 0f, cz - 25f);
            pos.y = GroundY(pos.x, pos.z) + 6f;
            target = new Vector3(cx, GroundY(cx, cz), cz);
        }
        testCamera.transform.position = pos;
        testCamera.transform.rotation = Quaternion.LookRotation((target - pos).normalized, Vector3.up);
    }

    static void HideOtherRenderers()
    {
        var cams = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
        var list = new System.Collections.Generic.List<Camera>();
        foreach (var c in cams)
        {
            if (c == null || c == testCamera || !c.enabled) continue;
            c.enabled = false;
            list.Add(c);
        }
        hiddenCameras = list.ToArray();

        var canvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        var clist = new System.Collections.Generic.List<Canvas>();
        foreach (var c in canvases)
        {
            if (c == null || !c.enabled) continue;
            c.enabled = false;
            clist.Add(c);
        }
        hiddenCanvases = clist.ToArray();
    }

    static void RestoreRenderers()
    {
        if (hiddenCameras != null)
            foreach (var c in hiddenCameras) if (c != null) c.enabled = true;
        if (hiddenCanvases != null)
            foreach (var c in hiddenCanvases) if (c != null) c.enabled = true;
        hiddenCameras = null;
        hiddenCanvases = null;
    }

    static void Capture(int index)
    {
        string dir = ShotFolder;
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
        string name = index == 0 ? "grass_A_painted.png" : (index == 1 ? "grass_B_cleared.png" : "grass_C_restored.png");
        string path = Path.Combine(dir, name);
        if (File.Exists(path)) File.Delete(path);
        ScreenCapture.CaptureScreenshot(path);
        capturedCount++;
        Debug.Log("GrassDetailCaptureTest: requested shot " + index + " -> " + name + " frame=" + Time.frameCount + " screen=" + Screen.width + "x" + Screen.height);
    }

    static Terrain MainTerrain()
    {
        var go = GameObject.Find("Terrain");
        return go == null ? null : go.GetComponent<Terrain>();
    }

    static void SetPatch(int density)
    {
        var ter = MainTerrain();
        if (ter == null) return;
        var td = ter.terrainData;
        int patch = 12;
        int x0 = 107, y0 = 270;
        var cells = new int[patch, patch];
        for (int i = 0; i < patch; i++) for (int j = 0; j < patch; j++) cells[j, i] = density;
        td.SetDetailLayer(x0, y0, 0, cells);
        Debug.Log("GrassDetailCaptureTest: patch density set to " + density + " at runtime");
    }

    static void Tick()
    {
        if (!SessionState.GetBool(ActiveKey, false))
        {
            EditorApplication.update -= Tick;
            return;
        }
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;

        int step = SessionState.GetInt(StepKey, 0);

        if (step == 0)
        {
            if (!EditorApplication.isPlaying) return;
            waitUntilFrame = Time.frameCount + 10;
            SessionState.SetInt(StepKey, 1);
            return;
        }

        if (Time.frameCount < waitUntilFrame) return;

        if (step == 1)
        {
            SetupCamera(0);
            HideOtherRenderers();
            waitUntilFrame = Time.frameCount + 6;
            SessionState.SetInt(StepKey, 2);
            return;
        }
        if (step == 2)
        {
            Capture(0);
            waitUntilFrame = Time.frameCount + 4;
            SessionState.SetInt(StepKey, 3);
            return;
        }
        if (step == 3)
        {
            SetPatch(0);
            waitUntilFrame = Time.frameCount + 6;
            SessionState.SetInt(StepKey, 4);
            return;
        }
        if (step == 4)
        {
            Capture(1);
            waitUntilFrame = Time.frameCount + 4;
            SessionState.SetInt(StepKey, 5);
            return;
        }
        if (step == 5)
        {
            SetPatch(3);
            waitUntilFrame = Time.frameCount + 6;
            SessionState.SetInt(StepKey, 6);
            return;
        }
        if (step == 6)
        {
            Capture(2);
            waitUntilFrame = Time.frameCount + 8;
            SessionState.SetInt(StepKey, 7);
            return;
        }
        if (step == 7)
        {
            Debug.Log("GrassDetailCaptureTest: finished captures=" + capturedCount + " frame=" + Time.frameCount);
            RestoreRenderers();
            if (testCamera != null)
            {
                Object.Destroy(testCamera.gameObject);
                testCamera = null;
            }
            SessionState.SetBool(ActiveKey, false);
            SessionState.SetInt(StepKey, 0);
            EditorApplication.update -= Tick;
            EditorApplication.isPlaying = false;
            Debug.Log("GrassDetailCaptureTest: exiting play mode");
        }
    }
}
