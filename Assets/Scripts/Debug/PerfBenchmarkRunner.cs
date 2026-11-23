using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Unity.Profiling;
using UnityEngine;

/// <summary>
/// Reproducible play-mode benchmark for the power-line optimization audit.
/// Flies the active drone along a fixed route and records CPU/GPU frame time,
/// batches, set-pass calls, triangles/vertices and GC allocations.
/// Usage (play mode): PerfBenchmarkRunner.Begin("baseline");
/// </summary>
public sealed class PerfBenchmarkRunner : MonoBehaviour
{
    public static string Status = "idle";
    public static string ResultPath = string.Empty;
    public static string DroneName = string.Empty;

    private sealed class Sample
    {
        public float frameMs;
        public float cpuMainMs;
        public float cpuRenderMs;
        public float gpuMs;
        public float gfxWaitMs;
        public int batches;
        public int setPassCalls;
        public long triangles;
        public long vertices;
        public long gcAllocBytes;
    }

    private static readonly string[] DisabledScripts =
    {
        "DroneFlightController", "DroneMotors", "DroneCollision", "DroneCameraModule"
    };

    private readonly List<Sample> samples = new List<Sample>();
    private readonly FrameTiming[] frameTimings = new FrameTiming[1];

    private ProfilerRecorder batchesRecorder;
    private ProfilerRecorder setPassRecorder;
    private ProfilerRecorder trianglesRecorder;
    private ProfilerRecorder verticesRecorder;
    private ProfilerRecorder gcRecorder;
    private ProfilerRecorder playerLoopRecorder;
    private ProfilerRecorder renderThreadRecorder;
    private ProfilerRecorder gfxWaitRecorder;

    public static void Begin(string tag)
    {
        var go = new GameObject("[PerfBenchmarkRunner]");
        DontDestroyOnLoad(go);
        var runner = go.AddComponent<PerfBenchmarkRunner>();
        runner.StartCoroutine(runner.Run(tag));
    }

    private void OnDestroy()
    {
        DisposeRecorders();
    }

    private IEnumerator Run(string tag)
    {
        Status = "running:" + tag;
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = -1;
        Application.runInBackground = true;

        yield return new WaitForSecondsRealtime(2f);

        Camera cam = Camera.main;
        if (cam == null)
        {
            var cams = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
            for (int i = 0; i < cams.Length; i++)
            {
                if (cams[i].isActiveAndEnabled && cams[i].gameObject.activeInHierarchy)
                {
                    cam = cams[i];
                    break;
                }
            }
        }

        if (cam == null)
        {
            Status = "error:no-camera";
            yield break;
        }

        // Force a deterministic drone so "before" and "after" runs measure the same scene content.
        var gimbal = GameObject.Find("GimbalDrone Variant");
        if (gimbal != null)
        {
            var sceneRoots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
            for (int i = 0; i < sceneRoots.Length; i++)
            {
                var root = sceneRoots[i];
                if (root == gimbal || root == gameObject) continue;
                if (root.GetComponentInChildren<Camera>(true) != null)
                {
                    root.SetActive(false);
                }
            }

            gimbal.SetActive(true);
            var gimbalCamera = gimbal.GetComponentInChildren<Camera>(true);
            if (gimbalCamera != null)
            {
                cam = gimbalCamera;
            }
        }

        DroneName = cam.transform.root.name + " (" + cam.transform.root.GetComponentsInChildren<Renderer>(true).Length + " renderers)";

        Transform drone = cam.transform.root;
        Rigidbody body = drone.GetComponent<Rigidbody>();
        if (body != null)
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.useGravity = false;
            body.isKinematic = true;
        }

        var behaviours = drone.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour behaviour = behaviours[i];
            if (behaviour == null)
            {
                continue;
            }

            string typeName = behaviour.GetType().Name;
            for (int k = 0; k < DisabledScripts.Length; k++)
            {
                if (typeName == DisabledScripts[k])
                {
                    behaviour.enabled = false;
                    break;
                }
            }
        }

        cam.transform.localRotation = Quaternion.Euler(12f, 0f, 0f);

        CreateRecorders();

        Vector3 startPosition = drone.position;
        Quaternion startRotation = drone.rotation;
        Vector3 forward = cam.transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f)
        {
            forward = drone.forward;
            forward.y = 0f;
        }

        forward = forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;

        yield return StartCoroutine(FlyRoute(drone, startPosition, startRotation, forward));

        WriteReport(tag);
        DisposeRecorders();
        QualitySettings.vSyncCount = 1;
        Status = "done:" + tag;
    }

    private IEnumerator FlyRoute(Transform drone, Vector3 startPosition, Quaternion startRotation, Vector3 forward)
    {
        const float totalDuration = 9f;
        const float warmup = 1f;

        Vector3 forwardPoint = startPosition + forward * 150f;
        Vector3 highPoint = forwardPoint + Vector3.up * 120f;
        Quaternion turnedRotation = startRotation * Quaternion.Euler(0f, 180f, 0f);

        float time = 0f;
        while (time < totalDuration)
        {
            time += Time.unscaledDeltaTime;

            if (time <= 2.5f)
            {
                drone.position = Vector3.Lerp(startPosition, forwardPoint, Mathf.SmoothStep(0f, 1f, time / 2.5f));
                drone.rotation = startRotation;
            }
            else if (time <= 4.5f)
            {
                drone.position = Vector3.Lerp(forwardPoint, highPoint, Mathf.SmoothStep(0f, 1f, (time - 2.5f) / 2f));
                drone.rotation = startRotation;
            }
            else if (time <= 7f)
            {
                drone.position = highPoint;
                drone.rotation = Quaternion.Slerp(startRotation, turnedRotation, Mathf.SmoothStep(0f, 1f, (time - 4.5f) / 2.5f));
            }
            else
            {
                drone.position = highPoint;
                drone.rotation = turnedRotation;
            }

            if (time > warmup)
            {
                Capture();
            }

            yield return null;
        }
    }

    private void Capture()
    {
        var sample = new Sample
        {
            frameMs = Time.unscaledDeltaTime * 1000f,
            cpuMainMs = ReadMs(playerLoopRecorder),
            cpuRenderMs = ReadMs(renderThreadRecorder),
            gfxWaitMs = ReadMs(gfxWaitRecorder),
            gpuMs = ReadGpuMs(),
            batches = ReadInt(batchesRecorder),
            setPassCalls = ReadInt(setPassRecorder),
            triangles = ReadLong(trianglesRecorder),
            vertices = ReadLong(verticesRecorder),
            gcAllocBytes = ReadLong(gcRecorder)
        };

        samples.Add(sample);
    }

    private float ReadGpuMs()
    {
        FrameTimingManager.CaptureFrameTimings();
        uint count = FrameTimingManager.GetLatestTimings(1, frameTimings);
        if (count > 0 && frameTimings[0].gpuFrameTime > 0d)
        {
            return (float)frameTimings[0].gpuFrameTime;
        }

        return 0f;
    }

    private void CreateRecorders()
    {
        batchesRecorder = NewRecorder(ProfilerCategory.Render, "Batches Count", "Batches", "Draw Calls Count", "Draw Calls");
        setPassRecorder = NewRecorder(ProfilerCategory.Render, "SetPass Calls Count", "SetPass Calls");
        trianglesRecorder = NewRecorder(ProfilerCategory.Render, "Triangles Count", "Triangles");
        verticesRecorder = NewRecorder(ProfilerCategory.Render, "Vertices Count", "Vertices");
        gcRecorder = NewRecorder(ProfilerCategory.Memory, "GC Allocated In Frame", "GC Alloc");
        playerLoopRecorder = NewRecorder(ProfilerCategory.Internal, "PlayerLoop");
        renderThreadRecorder = NewRecorder(ProfilerCategory.Internal, "Render Thread", "CPU Render Thread Frame Time");
        gfxWaitRecorder = NewRecorder(ProfilerCategory.Internal, "Gfx.WaitForPresentOnGfxThread", "Gfx.WaitForPresent");
    }

    private static ProfilerRecorder NewRecorder(ProfilerCategory category, params string[] markers)
    {
        for (int i = 0; i < markers.Length; i++)
        {
            try
            {
                ProfilerRecorder recorder = ProfilerRecorder.StartNew(category, markers[i]);
                if (recorder.Valid)
                {
                    return recorder;
                }

                recorder.Dispose();
            }
            catch
            {
                // Marker is not available on this platform; try the next alias.
            }
        }

        return default;
    }

    private static float ReadMs(ProfilerRecorder recorder)
    {
        return recorder.Valid ? recorder.LastValue / 1000000f : 0f;
    }

    private static int ReadInt(ProfilerRecorder recorder)
    {
        return recorder.Valid ? (int)recorder.LastValue : 0;
    }

    private static long ReadLong(ProfilerRecorder recorder)
    {
        return recorder.Valid ? recorder.LastValue : 0L;
    }

    private void DisposeRecorders()
    {
        DisposeRecorder(ref batchesRecorder);
        DisposeRecorder(ref setPassRecorder);
        DisposeRecorder(ref trianglesRecorder);
        DisposeRecorder(ref verticesRecorder);
        DisposeRecorder(ref gcRecorder);
        DisposeRecorder(ref playerLoopRecorder);
        DisposeRecorder(ref renderThreadRecorder);
        DisposeRecorder(ref gfxWaitRecorder);
    }

    private static void DisposeRecorder(ref ProfilerRecorder recorder)
    {
        if (recorder.Valid)
        {
            recorder.Dispose();
        }
    }

    private void WriteReport(string tag)
    {
        Directory.CreateDirectory("Logs");

        string path = Path.Combine("Logs", "Perf_" + tag + ".txt");
        ResultPath = path;

        var frame = new List<float>();
        var cpuMain = new List<float>();
        var cpuRender = new List<float>();
        var gpu = new List<float>();
        var gfxWait = new List<float>();
        var batches = new List<float>();
        var setPass = new List<float>();
        var triangles = new List<float>();
        var vertices = new List<float>();
        var gc = new List<float>();

        var csv = new StringBuilder();
        csv.AppendLine("frameMs,cpuMainMs,cpuRenderMs,gpuMs,gfxWaitMs,batches,setPassCalls,triangles,vertices,gcAllocKB");

        for (int i = 0; i < samples.Count; i++)
        {
            Sample s = samples[i];
            frame.Add(s.frameMs);
            cpuMain.Add(s.cpuMainMs);
            cpuRender.Add(s.cpuRenderMs);
            gpu.Add(s.gpuMs);
            gfxWait.Add(s.gfxWaitMs);
            batches.Add(s.batches);
            setPass.Add(s.setPassCalls);
            triangles.Add(s.triangles);
            vertices.Add(s.vertices);
            gc.Add(s.gcAllocBytes / 1024f);

            csv.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "{0:F2},{1:F3},{2:F3},{3:F3},{4:F3},{5},{6},{7},{8},{9:F2}",
                s.frameMs, s.cpuMainMs, s.cpuRenderMs, s.gpuMs, s.gfxWaitMs,
                s.batches, s.setPassCalls, s.triangles, s.vertices, s.gcAllocBytes / 1024f));
        }

        var report = new StringBuilder();
        report.AppendLine("=== PerfBenchmark: " + tag + " ===");
        report.AppendLine("Date: " + System.DateTime.Now.ToString("s", CultureInfo.InvariantCulture));
        report.AppendLine("Frames sampled: " + samples.Count);
        report.AppendLine("Unity: " + Application.unityVersion + " | " + SystemInfo.graphicsDeviceName);
        report.AppendLine("Drone: " + DroneName);
        report.AppendLine("vSync forced off, targetFrameRate=-1, runInBackground=true");
        report.AppendLine("Route: 150m forward (2.5s) -> +120m climb (2s) -> 180 deg yaw (2.5s) -> hover (2s), 1s warmup skipped");
        report.AppendLine(string.Empty);

        AppendStat(report, "Frame time", frame, " ms");
        if (frame.Count > 0)
        {
            report.AppendLine("FPS avg: " + (1000f / Average(frame)).ToString("F1", CultureInfo.InvariantCulture));
            report.AppendLine("FPS worst (p95 frame): " + (1000f / Percentile(frame, 0.95f)).ToString("F1", CultureInfo.InvariantCulture));
        }

        AppendStat(report, "CPU main (PlayerLoop)", cpuMain, " ms");
        AppendStat(report, "CPU render thread", cpuRender, " ms");
        AppendStat(report, "GPU frame (FrameTiming)", gpu, " ms");
        AppendStat(report, "GPU wait (Gfx.WaitForPresent)", gfxWait, " ms");
        AppendStat(report, "Batches", batches, string.Empty);
        AppendStat(report, "SetPass Calls", setPass, string.Empty);
        AppendStat(report, "Triangles", triangles, string.Empty);
        AppendStat(report, "Vertices", vertices, string.Empty);
        AppendStat(report, "GC Alloc", gc, " KB/frame");

        File.WriteAllText(path, report.ToString());
        File.WriteAllText(path + ".csv", csv.ToString());

        Debug.Log("[PerfBenchmark] " + tag + " finished. Report: " + path);
        Debug.Log(report.ToString());
    }

    private static float Average(List<float> values)
    {
        if (values.Count == 0)
        {
            return 0f;
        }

        double sum = 0d;
        for (int i = 0; i < values.Count; i++)
        {
            sum += values[i];
        }

        return (float)(sum / values.Count);
    }

    private static float Percentile(List<float> values, float percentile)
    {
        if (values.Count == 0)
        {
            return 0f;
        }

        var copy = new List<float>(values);
        copy.Sort();
        int index = Mathf.Clamp(Mathf.RoundToInt((copy.Count - 1) * percentile), 0, copy.Count - 1);
        return copy[index];
    }

    private static void AppendStat(StringBuilder report, string label, List<float> values, string unit)
    {
        if (values.Count == 0)
        {
            report.AppendLine(label + ": n/a");
            return;
        }

        var copy = new List<float>(values);
        copy.Sort();

        float max = copy[copy.Count - 1];
        float min = copy[0];
        report.AppendLine(string.Format(CultureInfo.InvariantCulture,
            "{0}: avg {1:F2}{2}, p50 {3:F2}{2}, p95 {4:F2}{2}, min {5:F2}{2}, max {6:F2}{2}",
            label,
            Average(values), unit,
            Percentile(values, 0.5f), Percentile(values, 0.95f),
            min, max));
    }
}
