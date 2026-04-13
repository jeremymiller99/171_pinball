// are not over-accelerated by contacts (light mass + same impulses => huge speed).
using UnityEngine;

/// <summary>
/// Splitter-class ball: every <see cref="hitsPerSplit"/> scoring component hits,
/// splits into <see cref="ballsPerSplit"/> smaller copies. Each copy can split again
/// until <see cref="terminalSplitGeneration"/> (exclusive) — default yields two split
/// waves and up to four smallest balls.
/// </summary>
public sealed class MatryoshkaBall : Ball
{
    public const string DefinitionId = "MatryoshkaDoll";

    /// <summary>Generations 0..(value-1) may split; generation 2 is terminal (4 end balls).</summary>
    public const int TerminalSplitGenerationDefault = 2;

    [SerializeField] private GameObject matryoshkaPrefab;
    [SerializeField] private BallSpawner ballSpawner;

    [Header("Split")]
    [Min(1)]
    [SerializeField] private int hitsPerSplit = 10;

    [Min(2)]
    [SerializeField] private int ballsPerSplit = 2;

    [Tooltip("Split while split generation is below this (2 => generations 0 and 1 split).")]
    [Min(1)]
    [SerializeField] private int terminalSplitGeneration = TerminalSplitGenerationDefault;

    [Tooltip("Each child scale = pow(this, childGeneration) × prefab base scale.")]
    [Range(0.25f, 0.95f)]
    [SerializeField] private float sizeScalePerSplit = 0.68f;

    [Tooltip("Horizontal ring radius for spawn offsets (world units).")]
    [SerializeField] private float spawnRingRadius = 0.14f;

    [Tooltip("Outward impulse added to each new ball (world units/sec).")]
    [SerializeField] private float splitBurstImpulse = 3.5f;

    [Tooltip(
        "Minimum rigidbody mass after scaling by volume. Keeps the smallest dolls from " +
        "being hurled by bumpers/flippers (same contact impulse → far higher speed when mass is tiny).")]
    [Min(0.01f)]
    [SerializeField] private float minimumRigidbodyMass = 1f;

    [Tooltip("Spawn velocity is multiplied by pow(this, child generation) (1 = unchanged).")]
    [Range(0.5f, 1f)]
    [SerializeField] private float spawnVelocityScalePerGeneration = 0.9f;

    private int _splitGeneration;
    private int _lastSplitMilestone;
    private float _baselineMass;

    private void Awake()
    {
        ballSpawner = ServiceLocator.Get<BallSpawner>();
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            _baselineMass = rb.mass;
        }
    }

    private void OnValidate()
    {
        hitsPerSplit = Mathf.Max(1, hitsPerSplit);
        ballsPerSplit = Mathf.Max(2, ballsPerSplit);
        terminalSplitGeneration = Mathf.Max(1, terminalSplitGeneration);
        sizeScalePerSplit = Mathf.Clamp(sizeScalePerSplit, 0.25f, 0.95f);
        minimumRigidbodyMass = Mathf.Max(0.01f, minimumRigidbodyMass);
    }

    /// <summary>
    /// Call immediately after <see cref="Object.Instantiate"/> for split children (not hand-spawned).
    /// </summary>
    public void InitializeFromSplit(int generation)
    {
        _splitGeneration = generation;
        ApplyGenerationScaleAndMass();
    }

    protected override bool ShouldIgnoreBoardHitFromCollider(Collider collider)
    {
        Ball other = collider.GetComponentInParent<Ball>();
        return other != null && other != this;
    }

    private void LateUpdate()
    {
        if (_splitGeneration >= terminalSplitGeneration)
        {
            return;
        }

        if (componentHits <= 0)
        {
            return;
        }

        if (componentHits % hitsPerSplit != 0)
        {
            return;
        }

        if (componentHits <= _lastSplitMilestone)
        {
            return;
        }

        _lastSplitMilestone = componentHits;
        PerformSplit();
    }

    private void ApplyGenerationScaleAndMass()
    {
        float s = Mathf.Pow(sizeScalePerSplit, _splitGeneration);
        transform.localScale = new Vector3(s, s, s);

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            float volumeMass = _baselineMass * (s * s * s);
            rb.mass = Mathf.Max(volumeMass, minimumRigidbodyMass);
        }
    }

    private void PerformSplit()
    {
        if (matryoshkaPrefab == null)
        {
            return;
        }

        if (ballSpawner == null)
        {
            ballSpawner = ServiceLocator.Get<BallSpawner>();
        }

        ServiceLocator.Get<AudioManager>()?.PlayBallSplit(transform.position);

        Rigidbody parentRb = GetComponent<Rigidbody>();
        Vector3 baseVel = parentRb != null ? parentRb.linearVelocity : Vector3.zero;

        int childGeneration = _splitGeneration + 1;

        for (int i = 0; i < ballsPerSplit; i++)
        {
            float ang = i * (Mathf.PI * 2f / ballsPerSplit);
            Vector3 ring = new Vector3(Mathf.Cos(ang), 0.3f, Mathf.Sin(ang));
            Vector3 spawnPos = transform.position + ring * spawnRingRadius;

            Quaternion rot = Quaternion.LookRotation(
                ring.sqrMagnitude > 0.0001f ? ring.normalized : Vector3.forward);

            GameObject child = Instantiate(matryoshkaPrefab, spawnPos, rot);
            Ball.EnsureOwnMaterials(child);

            MatryoshkaBall mb = child.GetComponent<MatryoshkaBall>();
            if (mb != null)
            {
                mb.InitializeFromSplit(childGeneration);
            }

            Rigidbody rb = child.GetComponent<Rigidbody>();
            if (rb != null)
            {
                Vector3 burst = Random.onUnitSphere * splitBurstImpulse;
                burst.y = Mathf.Abs(burst.y) * 0.5f + 0.2f * splitBurstImpulse;
                Vector3 v = baseVel * 0.4f + burst;
                float velScale = Mathf.Pow(spawnVelocityScalePerGeneration, childGeneration);
                rb.linearVelocity = v * velScale;
            }

            if (ballSpawner != null)
            {
                ballSpawner.ActiveBalls.Add(child);
            }
        }

        if (ballSpawner != null)
        {
            ballSpawner.DespawnBall(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
}
