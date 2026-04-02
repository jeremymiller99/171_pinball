// Generated with Cursor (Composer) by assistant, for jjmil, on 2026-04-01.
using UnityEngine;

/// <summary>
/// Drives a row of <see cref="BoardLight"/> segments to mirror launcher charge (0 = all off,
/// full charge = all segments lit). Only calls <see cref="BoardLight.SetLit"/> when a segment
/// changes so charge ticks stay cheap.
/// </summary>
[DefaultExecutionOrder(10)]
public sealed class LauncherPowerMeter : MonoBehaviour
{
    [Header("Segments")]
    [Tooltip("Order: first element is the lowest charge segment, last is full charge.")]
    [SerializeField] private BoardLight[] segments;

    private bool[] _lastLit;


    private void Awake()
    {
        EnsureLastLitBuffer();
    }

    private void OnEnable()
    {
        EnsureLastLitBuffer();
        ClearMeterVisuals();
    }

    /// <summary>Normalized charge in 0..1 from the launcher.</summary>
    public void SetNormalizedCharge(float t01)
    {
        if (segments == null || segments.Length == 0)
        {
            return;
        }

        EnsureLastLitBuffer();
        t01 = Mathf.Clamp01(t01);
        int n = segments.Length;
        float scaled = t01 * n;

        for (int i = 0; i < n; i++)
        {
            bool want = scaled > i;

            if (_lastLit[i] == want)
            {
                continue;
            }

            _lastLit[i] = want;
            BoardLight light = segments[i];

            if (light != null)
            {
                light.SetLit(want);
            }
        }
    }

    /// <summary>Forces every segment off and clears the dirty cache (e.g. after scene load).</summary>
    public void ClearMeterVisuals()
    {
        if (segments == null || segments.Length == 0)
        {
            return;
        }

        EnsureLastLitBuffer();

        for (int i = 0; i < segments.Length; i++)
        {
            _lastLit[i] = false;
            BoardLight light = segments[i];

            if (light != null)
            {
                light.SetLit(false);
            }
        }
    }

    private void EnsureLastLitBuffer()
    {
        if (segments == null)
        {
            return;
        }

        if (_lastLit == null || _lastLit.Length != segments.Length)
        {
            _lastLit = new bool[segments.Length];
        }
    }
}
