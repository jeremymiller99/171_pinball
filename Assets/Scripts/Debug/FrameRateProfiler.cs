using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// In-game frame rate profiler. Press F9 while playing to start/stop a capture.
/// While capturing, every frame's unscaled delta time is recorded; on stop a
/// human-readable report (avg/min/max, 1% &amp; 0.1% lows, percentiles, stutter
/// counts, per-second timeline) plus a raw CSV are written to
/// <c>Application.persistentDataPath/FrameRateReports/</c>.
///
/// Self-installs at startup, so nothing needs to be placed in a scene.
/// </summary>
[DisallowMultipleComponent]
public sealed class FrameRateProfiler : MonoBehaviour
{
    internal const string RootObjectName = "__FrameRateProfiler";

    private const KeyCode LegacyToggleKey = KeyCode.F9;
    private const string ReportFolderName = "FrameRateReports";

    // Frame-time thresholds used to count "hitches" (in milliseconds).
    private const float StutterMs = 1000f / 30f;      // slower than 30 FPS
    private const float BadStutterMs = 1000f / 20f;   // slower than 20 FPS

    // Pre-allocated so a long capture doesn't allocate every frame.
    private readonly System.Collections.Generic.List<float> _frameMs =
        new System.Collections.Generic.List<float>(1 << 16);

    private bool _capturing;
    private float _elapsed;
    private string _sceneNameAtStart;
    private string _lastReportPath;
    private float _lastReportShownUntil;

    private void Update()
    {
        if (TogglePressed())
        {
            if (_capturing)
                StopCapture();
            else
                StartCapture();
        }

        if (!_capturing)
            return;

        float dt = Time.unscaledDeltaTime;
        if (dt <= 0f)
            return;

        _frameMs.Add(dt * 1000f);
        _elapsed += dt;
    }

    private void StartCapture()
    {
        _frameMs.Clear();
        _elapsed = 0f;
        _sceneNameAtStart = SceneManager.GetActiveScene().name;
        _capturing = true;
        Debug.Log("[FrameRateProfiler] Capture started (press F9 again to stop).");
    }

    private void StopCapture()
    {
        _capturing = false;

        if (_frameMs.Count == 0)
        {
            Debug.LogWarning("[FrameRateProfiler] Capture stopped with no frames recorded.");
            return;
        }

        try
        {
            _lastReportPath = WriteReport();
            _lastReportShownUntil = Time.unscaledTime + 6f;
            Debug.Log($"[FrameRateProfiler] Report written to: {_lastReportPath}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[FrameRateProfiler] Failed to write report: {e}");
        }
    }

    private string WriteReport()
    {
        // Work on a sorted copy (ascending frame time = fastest first).
        var sorted = _frameMs.ToArray();
        Array.Sort(sorted);

        int n = sorted.Length;
        double sumMs = 0d;
        int stutters = 0;
        int badStutters = 0;
        for (int i = 0; i < n; i++)
        {
            sumMs += sorted[i];
            if (sorted[i] > BadStutterMs) badStutters++;
            else if (sorted[i] > StutterMs) stutters++;
        }
        // badStutters are also stutters; report the inclusive count.
        stutters += badStutters;

        float avgMs = (float)(sumMs / n);
        float avgFps = avgMs > 0f ? 1000f / avgMs : 0f;

        // sorted[0] is the fastest frame (lowest ms => highest FPS).
        float bestFps = 1000f / Mathf.Max(0.0001f, sorted[0]);
        float worstFps = 1000f / Mathf.Max(0.0001f, sorted[n - 1]);
        float medianMs = Percentile(sorted, 50f);

        // Lows: average the FPS of the slowest X% of frames (the tail).
        float onePctLow = LowAverageFps(sorted, 0.01f);
        float pointOnePctLow = LowAverageFps(sorted, 0.001f);

        var sb = new StringBuilder(2048);
        var ci = CultureInfo.InvariantCulture;
        string stamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", ci);

        sb.AppendLine("================ Frame Rate Report ================");
        sb.AppendLine($"Generated      : {stamp}");
        sb.AppendLine($"Scene          : {_sceneNameAtStart}");
        sb.AppendLine($"Platform       : {Application.platform} | Unity {Application.unityVersion}");
        sb.AppendLine($"Device         : {SystemInfo.deviceModel}");
        sb.AppendLine($"GPU            : {SystemInfo.graphicsDeviceName}");
        sb.AppendLine($"VSync / Target : vSyncCount={QualitySettings.vSyncCount}, targetFrameRate={Application.targetFrameRate}");
        sb.AppendLine();
        sb.AppendLine($"Duration       : {_elapsed.ToString("F2", ci)} s");
        sb.AppendLine($"Frames         : {n}");
        sb.AppendLine();
        sb.AppendLine("---- Frame rate (FPS) ----");
        sb.AppendLine($"Average        : {avgFps.ToString("F1", ci)}");
        sb.AppendLine($"Median         : {(1000f / Mathf.Max(0.0001f, medianMs)).ToString("F1", ci)}");
        sb.AppendLine($"Min / Max      : {worstFps.ToString("F1", ci)} / {bestFps.ToString("F1", ci)}");
        sb.AppendLine($"1% low         : {onePctLow.ToString("F1", ci)}");
        sb.AppendLine($"0.1% low       : {pointOnePctLow.ToString("F1", ci)}");
        sb.AppendLine();
        sb.AppendLine("---- Frame time (ms, lower is better) ----");
        sb.AppendLine($"Average        : {avgMs.ToString("F2", ci)}");
        sb.AppendLine($"Median (p50)   : {medianMs.ToString("F2", ci)}");
        sb.AppendLine($"p95            : {Percentile(sorted, 95f).ToString("F2", ci)}");
        sb.AppendLine($"p99            : {Percentile(sorted, 99f).ToString("F2", ci)}");
        sb.AppendLine($"Worst          : {sorted[n - 1].ToString("F2", ci)}");
        sb.AppendLine();
        sb.AppendLine("---- Hitches ----");
        sb.AppendLine($"> {StutterMs.ToString("F1", ci)} ms (<30 FPS) : {stutters} ({Pct(stutters, n).ToString("F2", ci)}%)");
        sb.AppendLine($"> {BadStutterMs.ToString("F1", ci)} ms (<20 FPS) : {badStutters} ({Pct(badStutters, n).ToString("F2", ci)}%)");
        sb.AppendLine();
        sb.AppendLine("---- Per-second timeline (avg FPS) ----");
        AppendTimeline(sb, ci);

        string dir = Path.Combine(Application.persistentDataPath, ReportFolderName);
        Directory.CreateDirectory(dir);

        string fileStamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", ci);
        string txtPath = Path.Combine(dir, $"fps_{fileStamp}.txt");
        File.WriteAllText(txtPath, sb.ToString());

        WriteCsv(Path.Combine(dir, $"fps_{fileStamp}.csv"), ci);

        return txtPath;
    }

    // Builds a per-second timeline from the captured frame times (in order).
    private void AppendTimeline(StringBuilder sb, CultureInfo ci)
    {
        float bucketSec = 0f;
        double bucketMs = 0d;
        int bucketFrames = 0;
        int second = 0;

        for (int i = 0; i < _frameMs.Count; i++)
        {
            float ms = _frameMs[i];
            bucketMs += ms;
            bucketSec += ms / 1000f;
            bucketFrames++;

            if (bucketSec >= 1f)
            {
                float fps = bucketFrames / (float)(bucketMs / 1000d);
                sb.AppendLine($"  t={second,4}s : {fps.ToString("F1", ci),6} FPS  ({bucketFrames} frames)");
                second++;
                bucketSec = 0f;
                bucketMs = 0d;
                bucketFrames = 0;
            }
        }

        if (bucketFrames > 0 && bucketMs > 0d)
        {
            float fps = bucketFrames / (float)(bucketMs / 1000d);
            sb.AppendLine($"  t={second,4}s : {fps.ToString("F1", ci),6} FPS  ({bucketFrames} frames, partial)");
        }
    }

    private void WriteCsv(string path, CultureInfo ci)
    {
        var sb = new StringBuilder(_frameMs.Count * 12 + 32);
        sb.AppendLine("frame_index,frame_time_ms,instant_fps");
        for (int i = 0; i < _frameMs.Count; i++)
        {
            float ms = _frameMs[i];
            float fps = ms > 0f ? 1000f / ms : 0f;
            sb.Append(i.ToString(ci)).Append(',')
              .Append(ms.ToString("F3", ci)).Append(',')
              .Append(fps.ToString("F2", ci)).Append('\n');
        }
        File.WriteAllText(path, sb.ToString());
    }

    /// <summary>Average FPS of the slowest <paramref name="fraction"/> of frames.</summary>
    private static float LowAverageFps(float[] sortedAscMs, float fraction)
    {
        int n = sortedAscMs.Length;
        int count = Mathf.Max(1, Mathf.CeilToInt(n * fraction));
        double sumMs = 0d;
        for (int i = n - count; i < n; i++)
            sumMs += sortedAscMs[i];
        float avgMs = (float)(sumMs / count);
        return avgMs > 0f ? 1000f / avgMs : 0f;
    }

    /// <summary>Linear-interpolated percentile over frame-time samples sorted ascending.</summary>
    private static float Percentile(float[] sortedAsc, float percentile)
    {
        int n = sortedAsc.Length;
        if (n == 1) return sortedAsc[0];
        float rank = (percentile / 100f) * (n - 1);
        int lo = Mathf.FloorToInt(rank);
        int hi = Mathf.Min(lo + 1, n - 1);
        float frac = rank - lo;
        return Mathf.Lerp(sortedAsc[lo], sortedAsc[hi], frac);
    }

    private static float Pct(int part, int total) => total > 0 ? 100f * part / total : 0f;

    private static bool TogglePressed()
    {
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        return kb != null && kb.f9Key.wasPressedThisFrame;
#else
        return Input.GetKeyDown(LegacyToggleKey);
#endif
    }

    private void OnGUI()
    {
        string msg = null;
        if (_capturing)
        {
            int frames = _frameMs.Count;
            float fps = _elapsed > 0f ? frames / _elapsed : 0f;
            msg = $"● REC  fps profile  {_elapsed:F1}s  avg {fps:F0}";
        }
        else if (!string.IsNullOrEmpty(_lastReportPath) && Time.unscaledTime < _lastReportShownUntil)
        {
            msg = $"FPS report saved:\n{_lastReportPath}";
        }

        if (msg == null)
            return;

        var style = new GUIStyle(GUI.skin.label)
        {
            fontSize = 16,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.UpperLeft,
            wordWrap = true,
        };
        style.normal.textColor = _capturing ? Color.red : Color.green;

        const float w = 520f, h = 60f, pad = 12f;
        var rect = new Rect(pad, Screen.height - h - pad, w, h);

        var prev = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.55f);
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = prev;

        GUI.Label(new Rect(rect.x + 8f, rect.y + 4f, w - 16f, h - 8f), msg, style);
    }
}

internal static class FrameRateProfilerBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Init()
    {
        // Avoid duplicates across domain reloads / scene reloads.
        var existing = UnityEngine.Object.FindObjectsByType<FrameRateProfiler>(FindObjectsSortMode.None);
        if (existing != null && existing.Length > 0)
            return;

        var go = new GameObject(FrameRateProfiler.RootObjectName);
        UnityEngine.Object.DontDestroyOnLoad(go);
        go.AddComponent<FrameRateProfiler>();
    }
}
