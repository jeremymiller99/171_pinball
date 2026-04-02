// Generated with Cursor (Composer) by assistant, for jjmil, on 2026-04-01.
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Drives a row of <see cref="BoardLight"/> components in a looping chase or wave. Use
/// <see cref="litEveryNthLight"/> = 1 for a single dot, or 2+ for a sliding pattern with off gaps
/// (e.g. 2 = on, off, on, off). Put this on a parent; leave <see cref="lights"/> empty to take one
/// <see cref="BoardLight"/> per direct child in sibling order, or assign the array in order.
/// </summary>
public sealed class BoardLightWaveSequence : MonoBehaviour
{
    [Header("Lights")]
    [Tooltip("Order = wave travel order. If empty, uses first BoardLight under each direct child.")]
    [SerializeField] private BoardLight[] lights;

    [Header("Pattern")]
    [Tooltip("1 = one lit dot traveling the row. 2 = every other (on, off, on, off) sliding. " +
             "Larger values add more off lights between each lit one.")]
    [SerializeField] [Min(1)] private int litEveryNthLight = 2;

    [Header("Timing")]
    [SerializeField] private float secondsPerStep = 0.15f;
    [Tooltip("If true, uses unscaled time (ignores pause / slow-mo).")]
    [SerializeField] private bool useUnscaledTime;

    [Header("Playback")]
    [SerializeField] private bool playOnAwake = true;
    [Tooltip("If true, the active index moves from last toward first each step.")]
    [SerializeField] private bool reverseOrder;
    [Tooltip("When Stop() runs or this component disables, turn every light off.")]
    [SerializeField] private bool turnAllOffWhenStopped = true;

    private Coroutine _routine;
    private BoardLight[] _resolvedLights;


    private void Awake()
    {
        ResolveLights();
    }

    private void OnEnable()
    {
        if (playOnAwake)
        {
            Play();
        }
    }

    private void OnDisable()
    {
        StopInternal(true);
    }

    /// <summary>Starts the wave loop. No-op if there are no lights.</summary>
    public void Play()
    {
        if (!isActiveAndEnabled)
        {
            return;
        }

        ResolveLights();
        if (_resolvedLights == null || _resolvedLights.Length == 0)
        {
            return;
        }

        StopInternal(false);
        SetAllLit(false);
        _routine = StartCoroutine(WaveRoutine());
    }

    /// <summary>Stops the coroutine and optionally turns all lights off.</summary>
    public void Stop()
    {
        StopInternal(turnAllOffWhenStopped);
    }

    private void StopInternal(bool applyStopVisuals)
    {
        if (_routine != null)
        {
            StopCoroutine(_routine);
            _routine = null;
        }

        if (applyStopVisuals && turnAllOffWhenStopped)
        {
            ResolveLights();
            SetAllLit(false);
        }
    }

    private void ResolveLights()
    {
        if (lights != null && lights.Length > 0)
        {
            var list = new List<BoardLight>();
            for (int i = 0; i < lights.Length; i++)
            {
                if (lights[i] != null)
                {
                    list.Add(lights[i]);
                }
            }

            _resolvedLights = list.ToArray();
            return;
        }

        var fromChildren = new List<BoardLight>();
        for (int c = 0; c < transform.childCount; c++)
        {
            BoardLight bl = transform.GetChild(c).GetComponentInChildren<BoardLight>(true);
            if (bl != null)
            {
                fromChildren.Add(bl);
            }
        }

        _resolvedLights = fromChildren.ToArray();
    }

    private void SetAllLit(bool lit)
    {
        if (_resolvedLights == null)
        {
            return;
        }

        for (int i = 0; i < _resolvedLights.Length; i++)
        {
            if (_resolvedLights[i] != null)
            {
                _resolvedLights[i].SetLit(lit);
            }
        }
    }

    private IEnumerator WaveRoutine()
    {
        float step = Mathf.Max(0.0001f, secondsPerStep);
        WaitForSeconds waitScaled = new WaitForSeconds(step);
        WaitForSecondsRealtime waitUnscaled = new WaitForSecondsRealtime(step);

        int n = _resolvedLights.Length;
        int dir = reverseOrder ? -1 : 1;
        int k = Mathf.Max(1, litEveryNthLight);
        int phase = reverseOrder && k <= 1 ? n - 1 : 0;

        while (true)
        {
            ApplyPatternFrame(n, k, phase);

            phase += dir;

            if (useUnscaledTime)
            {
                yield return waitUnscaled;
            }
            else
            {
                yield return waitScaled;
            }
        }
    }

    private void ApplyPatternFrame(int n, int k, int phase)
    {
        if (k <= 1)
        {
            int litIndex = Mod(phase, n);
            for (int i = 0; i < n; i++)
            {
                if (_resolvedLights[i] != null)
                {
                    _resolvedLights[i].SetLit(i == litIndex);
                }
            }

            return;
        }

        int phaseMod = Mod(phase, k);
        for (int i = 0; i < n; i++)
        {
            if (_resolvedLights[i] != null)
            {
                bool on = Mod(i, k) == phaseMod;
                _resolvedLights[i].SetLit(on);
            }
        }
    }

    private static int Mod(int dividend, int divisor)
    {
        int m = dividend % divisor;
        if (m < 0)
        {
            m += divisor;
        }

        return m;
    }

#if UNITY_EDITOR
    [ContextMenu("BoardLightWaveSequence/Play")]
    private void ContextPlay()
    {
        Play();
    }

    [ContextMenu("BoardLightWaveSequence/Stop")]
    private void ContextStop()
    {
        Stop();
    }
#endif
}
