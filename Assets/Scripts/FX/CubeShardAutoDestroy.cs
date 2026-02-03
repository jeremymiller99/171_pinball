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

    private float _t0;
    private Vector3 _initialScale;

    private void Awake()
    {
        _t0 = Time.unscaledTime;
        _initialScale = transform.localScale;
    }

    public void SetLifetime(float seconds)
    {
        lifetimeSeconds = Mathf.Max(0f, seconds);
    }

    private void Update()
    {
        float age = Time.unscaledTime - _t0;
        float life = Mathf.Max(0f, lifetimeSeconds);

        if (life <= 0f)
        {
            Destroy(gameObject);
            return;
        }

        float remaining = life - age;
        if (shrinkBeforeDestroy && remaining <= Mathf.Max(0f, shrinkDurationSeconds))
        {
            float d = Mathf.Max(0.0001f, shrinkDurationSeconds);
            float u = Mathf.Clamp01(1f - (remaining / d));
            // Ease-out to 0
            float s = 1f - (u * u * (3f - 2f * u));
            transform.localScale = _initialScale * Mathf.Max(0f, s);
        }

        if (age >= life)
        {
            Destroy(gameObject);
        }
    }
}

