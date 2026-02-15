// Updated with Cursor (GPT-5.2) by OpenAI assistant on 2026-02-15.
using UnityEngine;

/// <summary>
/// Attach to board components that should break during Frenzy.
/// When hit by a Ball while Frenzy is active:
/// - spawns a cube-shard burst
/// - awards a one-time bonus
/// - disables (or destroys) the component GameObject
/// </summary>
[DisallowMultipleComponent]
public sealed class FrenzyExplodable : MonoBehaviour
{
    private struct BoolState<T> where T : Component
    {
        public T component;
        public bool enabled;
    }

    private struct RigidbodyState
    {
        public Rigidbody rb;
        public bool isKinematic;
        public bool useGravity;
        public RigidbodyInterpolation interpolation;
        public CollisionDetectionMode collisionDetectionMode;
        public RigidbodyConstraints constraints;
    }

    [Header("Trigger")]
    [SerializeField] private string ballTag = "Ball";
    [SerializeField] private bool triggerOnCollision = true;
    [SerializeField] private bool triggerOnTrigger = true;

    [Header("Explosion")]
    [Min(0)]
    [SerializeField] private int shardCount = 18;
    [Min(0f)]
    [SerializeField] private float shardForce = 9f;
    [Min(0f)]
    [SerializeField] private float shardRadius = 0.55f;
    [Min(0f)]
    [SerializeField] private float shardUpwardsModifier = 0.15f;
    [Min(0f)]
    [SerializeField] private float shardTorque = 10f;
    [SerializeField] private Vector2 shardSizeRange = new Vector2(0.08f, 0.16f);
    [Min(0f)]
    [SerializeField] private float shardLifetime = 3.0f;

    [Header("Scoring")]
    [Min(0f)]
    [SerializeField] private float destructionBonusPoints = 250f;

    [Header("After explosion")]
    [Tooltip("If true, disables this entire GameObject after spawning shards.")]
    [SerializeField] private bool disableGameObjectOnExplode = true;
    [Tooltip("If true, destroys this GameObject instead of disabling it.")]
    [SerializeField] private bool destroyGameObjectOnExplode = false;

    private ScoreManager _score;
    private bool _exploded;

    // Cached initial state for round resets
    private bool _cached;
    private bool _initialActiveSelf;
    private BoolState<Collider>[] _colliderStates;
    private BoolState<Renderer>[] _rendererStates;
    private BoolState<Behaviour>[] _behaviourStates;
    private RigidbodyState[] _rigidbodyStates;

    private void Awake()
    {
        ResolveScoreManager();
        CacheInitialStateIfNeeded();
    }

    private void ResolveScoreManager()
    {
        if (_score != null) return;

#if UNITY_2022_2_OR_NEWER
        _score = FindFirstObjectByType<ScoreManager>();
#else
        _score = FindObjectOfType<ScoreManager>();
#endif
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!triggerOnCollision) return;
        if (collision == null || collision.collider == null) return;
        if (!string.IsNullOrWhiteSpace(ballTag) && !collision.collider.CompareTag(ballTag)) return;

        TryExplode(collision.collider.transform.position);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!triggerOnTrigger) return;
        if (other == null) return;
        if (!string.IsNullOrWhiteSpace(ballTag) && !other.CompareTag(ballTag)) return;

        TryExplode(other.transform.position);
    }

    private void TryExplode(Vector3 hitWorldPos)
    {
        if (_exploded) return;
        if (!FrenzyController.IsFrenzyActive) return;

        _exploded = true;

        CacheInitialStateIfNeeded();

        ResolveScoreManager();
        if (_score != null && destructionBonusPoints > 0f)
        {
            _score.AddPointsScaled(destructionBonusPoints);
        }

        Bounds b = ComputeWorldBounds();
        Vector3 origin = b.size.sqrMagnitude > 0.0001f ? b.center : transform.position;

        SpawnShards(origin, hitWorldPos);

        if (destroyGameObjectOnExplode)
        {
            Destroy(gameObject);
            return;
        }

        ApplyDestroyedState();
    }

    /// <summary>
    /// Restores this object back to its initial (round-start) state.
    /// Called by the round reset system.
    /// </summary>
    public void ResetToDefaultState()
    {
        if (!_cached)
            CacheInitialStateIfNeeded();

        _exploded = false;

        // Ensure object exists/enabled in scene.
        // Default expectation: board objects are active at round start.
        if (gameObject.activeSelf != _initialActiveSelf)
        {
            gameObject.SetActive(_initialActiveSelf);
        }

        RestoreStates();
    }

    private void CacheInitialStateIfNeeded()
    {
        if (_cached) return;
        _cached = true;

        _initialActiveSelf = gameObject.activeSelf;

        var cols = GetComponentsInChildren<Collider>(includeInactive: true);
        _colliderStates = new BoolState<Collider>[cols.Length];
        for (int i = 0; i < cols.Length; i++)
        {
            _colliderStates[i] = new BoolState<Collider> { component = cols[i], enabled = cols[i] != null && cols[i].enabled };
        }

        var rends = GetComponentsInChildren<Renderer>(includeInactive: true);
        _rendererStates = new BoolState<Renderer>[rends.Length];
        for (int i = 0; i < rends.Length; i++)
        {
            _rendererStates[i] = new BoolState<Renderer> { component = rends[i], enabled = rends[i] != null && rends[i].enabled };
        }

        // Cache behaviour scripts (so spinning/logic pauses while "destroyed").
        // Include this component too, but we'll never disable it.
        var behs = GetComponentsInChildren<Behaviour>(includeInactive: true);
        int count = 0;
        for (int i = 0; i < behs.Length; i++)
        {
            if (behs[i] == null) continue;
            if (behs[i] is Transform) continue;
            if (behs[i] is Collider) continue;
            if (behs[i] is Renderer) continue;
            count++;
        }
        _behaviourStates = new BoolState<Behaviour>[count];
        int w = 0;
        for (int i = 0; i < behs.Length; i++)
        {
            var b = behs[i];
            if (b == null) continue;
            if (b is Transform) continue;
            if (b is Collider) continue;
            if (b is Renderer) continue;
            _behaviourStates[w++] = new BoolState<Behaviour> { component = b, enabled = b.enabled };
        }

        var rbs = GetComponentsInChildren<Rigidbody>(includeInactive: true);
        _rigidbodyStates = new RigidbodyState[rbs.Length];
        for (int i = 0; i < rbs.Length; i++)
        {
            var rb = rbs[i];
            if (rb == null) continue;
            _rigidbodyStates[i] = new RigidbodyState
            {
                rb = rb,
                isKinematic = rb.isKinematic,
                useGravity = rb.useGravity,
                interpolation = rb.interpolation,
                collisionDetectionMode = rb.collisionDetectionMode,
                constraints = rb.constraints
            };
        }
    }

    private void ApplyDestroyedState()
    {
        // Disable collisions + visuals.
        if (_colliderStates != null)
        {
            for (int i = 0; i < _colliderStates.Length; i++)
            {
                var c = _colliderStates[i].component;
                if (c != null) c.enabled = false;
            }
        }
        if (_rendererStates != null)
        {
            for (int i = 0; i < _rendererStates.Length; i++)
            {
                var r = _rendererStates[i].component;
                if (r != null) r.enabled = false;
            }
        }

        // Freeze any rigidbodies so "destroyed" parts don't keep moving.
        if (_rigidbodyStates != null)
        {
            for (int i = 0; i < _rigidbodyStates.Length; i++)
            {
                var rb = _rigidbodyStates[i].rb;
                if (rb == null) continue;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.isKinematic = true;
            }
        }

        // Disable other behaviours (except this script) so logic stops while destroyed.
        if (_behaviourStates != null)
        {
            for (int i = 0; i < _behaviourStates.Length; i++)
            {
                var b = _behaviourStates[i].component;
                if (b == null) continue;
                if (b == this) continue;
                b.enabled = false;
            }
        }

        if (disableGameObjectOnExplode)
        {
            gameObject.SetActive(false);
        }
    }

    private void RestoreStates()
    {
        // Restore rigidbodies first (so colliders can come back safely)
        if (_rigidbodyStates != null)
        {
            for (int i = 0; i < _rigidbodyStates.Length; i++)
            {
                var s = _rigidbodyStates[i];
                if (s.rb == null) continue;
                s.rb.isKinematic = s.isKinematic;
                s.rb.useGravity = s.useGravity;
                s.rb.interpolation = s.interpolation;
                s.rb.collisionDetectionMode = s.collisionDetectionMode;
                s.rb.constraints = s.constraints;
                s.rb.linearVelocity = Vector3.zero;
                s.rb.angularVelocity = Vector3.zero;
            }
        }

        if (_colliderStates != null)
        {
            for (int i = 0; i < _colliderStates.Length; i++)
            {
                var s = _colliderStates[i];
                if (s.component != null) s.component.enabled = s.enabled;
            }
        }
        if (_rendererStates != null)
        {
            for (int i = 0; i < _rendererStates.Length; i++)
            {
                var s = _rendererStates[i];
                if (s.component != null) s.component.enabled = s.enabled;
            }
        }

        if (_behaviourStates != null)
        {
            for (int i = 0; i < _behaviourStates.Length; i++)
            {
                var s = _behaviourStates[i];
                if (s.component == null) continue;
                if (s.component == this) continue;
                s.component.enabled = s.enabled;
            }
        }
    }

    private Bounds ComputeWorldBounds()
    {
        var rends = GetComponentsInChildren<Renderer>(includeInactive: false);
        bool has = false;
        Bounds b = default;
        for (int i = 0; i < rends.Length; i++)
        {
            if (rends[i] == null) continue;
            if (!has) { b = rends[i].bounds; has = true; }
            else { b.Encapsulate(rends[i].bounds); }
        }

        if (has) return b;

        var cols = GetComponentsInChildren<Collider>(includeInactive: false);
        for (int i = 0; i < cols.Length; i++)
        {
            if (cols[i] == null) continue;
            if (!has) { b = cols[i].bounds; has = true; }
            else { b.Encapsulate(cols[i].bounds); }
        }

        if (has) return b;

        return new Bounds(transform.position, Vector3.zero);
    }

    private void SpawnShards(Vector3 origin, Vector3 hitWorldPos)
    {
        int n = Mathf.Max(0, shardCount);
        if (n == 0) return;

        float radius = Mathf.Max(0.01f, shardRadius);
        float force = Mathf.Max(0f, shardForce);
        float torque = Mathf.Max(0f, shardTorque);

        float minSize = Mathf.Max(0.01f, Mathf.Min(shardSizeRange.x, shardSizeRange.y));
        float maxSize = Mathf.Max(minSize, Mathf.Max(shardSizeRange.x, shardSizeRange.y));

        Vector3 pushDir = (origin - hitWorldPos);
        if (pushDir.sqrMagnitude < 0.0001f) pushDir = UnityEngine.Random.onUnitSphere;
        pushDir.Normalize();

        for (int i = 0; i < n; i++)
        {
            GameObject shard = GameObject.CreatePrimitive(PrimitiveType.Cube);
            shard.name = "FrenzyShard";

            Vector3 rand = UnityEngine.Random.insideUnitSphere * (radius * 0.35f);
            shard.transform.position = origin + rand;
            shard.transform.rotation = UnityEngine.Random.rotation;

            float s = UnityEngine.Random.Range(minSize, maxSize);
            shard.transform.localScale = new Vector3(s, s, s);

            var rb = shard.AddComponent<Rigidbody>();
            rb.mass = 0.05f;
            // These shards are purely visual flair. Use cheap physics settings to avoid frame drops.
            rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
            rb.interpolation = RigidbodyInterpolation.None;

            rb.AddExplosionForce(force, origin, radius * 2f, shardUpwardsModifier, ForceMode.Impulse);
            rb.AddForce(pushDir * (force * 0.15f), ForceMode.Impulse);
            rb.AddTorque(UnityEngine.Random.insideUnitSphere * torque, ForceMode.Impulse);

            var autoDestroy = shard.AddComponent<CubeShardAutoDestroy>();
            autoDestroy.SetLifetime(shardLifetime);
        }
    }
}

