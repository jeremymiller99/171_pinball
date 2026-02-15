// Updated with Cursor (GPT-5.2) by OpenAI assistant on 2026-02-15.
using UnityEngine;

/// <summary>
/// Simple lifetime/cleanup for temporary cube shards.
/// Uses unscaled time so it still expires during slow-mo.
/// </summary>
[DisallowMultipleComponent]
public sealed class CubeShardAutoDestroy : MonoBehaviour
{
    [Min(0f)]
    [SerializeField] private float lifetimeSeconds = 3.5f;

    [Tooltip("If true, shrinks the shard to zero near the end of its lifetime.")]
    [SerializeField] private bool shrinkBeforeDestroy = true;

    [Min(0f)]
    [SerializeField] private float shrinkDurationSeconds = 0.35f;

    private Vector3 _initialScale;

    private Coroutine _lifetimeRoutine;

    private void Awake()
    {
        _initialScale = transform.localScale;
    }

    private void Start()
    {
        RestartRoutine();
    }

    public void SetLifetime(float seconds)
    {
        lifetimeSeconds = Mathf.Max(0f, seconds);

        // If configured after Start (rare), restart so the new value applies.
        if (_lifetimeRoutine != null)
        {
            RestartRoutine();
        }
    }

    public void SetShrink(bool shrink, float durationSeconds)
    {
        shrinkBeforeDestroy = shrink;
        shrinkDurationSeconds = Mathf.Max(0f, durationSeconds);

        if (_lifetimeRoutine != null)
        {
            RestartRoutine();
        }
    }

    private void RestartRoutine()
    {
        if (_lifetimeRoutine != null)
        {
            StopCoroutine(_lifetimeRoutine);
            _lifetimeRoutine = null;
        }

        _lifetimeRoutine = StartCoroutine(LifetimeRoutine());
    }

    private System.Collections.IEnumerator LifetimeRoutine()
    {
        float life = Mathf.Max(0f, lifetimeSeconds);
        if (life <= 0f)
        {
            Destroy(gameObject);
            yield break;
        }

        bool doShrink = shrinkBeforeDestroy && shrinkDurationSeconds > 0.0001f;
        float shrinkDur = Mathf.Max(0f, shrinkDurationSeconds);

        float wait = doShrink ? Mathf.Max(0f, life - shrinkDur) : life;
        if (wait > 0f)
        {
            yield return new WaitForSecondsRealtime(wait);
        }

        if (doShrink && shrinkDur > 0f)
        {
            float t = 0f;
            while (t < shrinkDur)
            {
                t += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(t / shrinkDur);

                // Ease-out to 0 (SmoothStep).
                float s = 1f - (u * u * (3f - 2f * u));
                transform.localScale = _initialScale * Mathf.Max(0f, s);

                yield return null;
            }
        }

        Destroy(gameObject);
    }
}

