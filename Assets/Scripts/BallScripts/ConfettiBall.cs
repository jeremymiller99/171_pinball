// no hit-count countdown UI; ball–ball does not advance burst hit count.
using UnityEngine;

/// <summary>
/// Splitter-class ball: every <see cref="hitsPerBurst"/> scoring component hits,
/// spawns <see cref="shardsPerBurst"/> colorful mini-balls that pop after
/// <see cref="ConfettiShardBall.HitsToPopDefault"/> hits. Fires
/// <see cref="BurstsPerBallDefault"/> shard bursts per main ball by default
/// (<see cref="maxBursts"/>).
/// </summary>
public sealed class ConfettiBall : Ball
{
    public const string DefinitionId = "Confetti";

    /// <summary>Design default: how many shard-spawn bursts each Confetti ball performs.</summary>
    public const int BurstsPerBallDefault = 4;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    [SerializeField] private GameObject shardPrefab;
    [SerializeField] private BallSpawner ballSpawner;

    [Header("Burst")]
    [Min(1)]
    [SerializeField] private int hitsPerBurst = 4;

    [Min(1)]
    [SerializeField] private int shardsPerBurst = 3;

    [Min(1)]
    [Tooltip("How many times this ball spawns a shard burst (default matches BurstsPerBallDefault).")]
    [SerializeField] private int maxBursts = BurstsPerBallDefault;

    [Tooltip("Extra outward speed added to each shard (world units/sec).")]
    [SerializeField] private float spawnBurstImpulse = 5f;

    [Tooltip("Uniform scale applied to each spawned shard (1 = prefab size).")]
    [Min(0.01f)]
    [SerializeField] private float shardSpawnUniformScale = 1.2f;

    private int _burstsRemaining;
    private int _lastBurstMilestone;

    private void Awake()
    {
        ballSpawner = ServiceLocator.Get<BallSpawner>();
        _burstsRemaining = maxBursts;
        _lastBurstMilestone = 0;
    }

    private void OnValidate()
    {
        hitsPerBurst = Mathf.Max(1, hitsPerBurst);
        shardsPerBurst = Mathf.Max(1, shardsPerBurst);
        maxBursts = Mathf.Max(1, maxBursts);
        shardSpawnUniformScale = Mathf.Max(0.01f, shardSpawnUniformScale);
    }

    protected override bool ShouldIgnoreBoardHitFromCollider(Collider collider)
    {
        Ball other = collider.GetComponentInParent<Ball>();
        return other != null && other != this;
    }

    private void LateUpdate()
    {
        if (_burstsRemaining <= 0)
        {
            return;
        }

        if (componentHits <= 0)
        {
            return;
        }

        if (componentHits % hitsPerBurst != 0)
        {
            return;
        }

        if (componentHits <= _lastBurstMilestone)
        {
            return;
        }

        _lastBurstMilestone = componentHits;
        SpawnShardBurst();
        _burstsRemaining--;
    }

    private void SpawnShardBurst()
    {
        if (shardPrefab == null)
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

        for (int i = 0; i < shardsPerBurst; i++)
        {
            float ang = i * (Mathf.PI * 2f / shardsPerBurst);
            Vector3 ring = new Vector3(Mathf.Cos(ang), 0.35f, Mathf.Sin(ang));
            Vector3 spawnPos = transform.position + ring * 0.18f;

            Quaternion rot = Quaternion.LookRotation(
                ring.sqrMagnitude > 0.0001f ? ring.normalized : Vector3.forward);

            GameObject shard = Instantiate(shardPrefab, spawnPos, rot);
            float s = shardSpawnUniformScale;
            shard.transform.localScale = new Vector3(s, s, s);
            Ball.EnsureOwnMaterials(shard);
            ApplyRandomShardColor(shard);

            Rigidbody rb = shard.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.mass *= s * s * s;
                Vector3 burst = Random.onUnitSphere * spawnBurstImpulse;
                burst.y = Mathf.Abs(burst.y) * 0.5f + 0.25f * spawnBurstImpulse;
                rb.linearVelocity = baseVel * 0.35f + burst;
            }

            if (ballSpawner != null)
            {
                ballSpawner.ActiveBalls.Add(shard);
            }
        }
    }

    private static void ApplyRandomShardColor(GameObject shard)
    {
        Color c = Random.ColorHSV(0f, 1f, 0.55f, 1f, 0.75f, 1f);
        Color emission = c * 0.45f;

        Renderer[] renderers = shard.GetComponentsInChildren<Renderer>(true);
        for (int r = 0; r < renderers.Length; r++)
        {
            Renderer renderer = renderers[r];
            if (renderer == null || renderer is TrailRenderer)
            {
                continue;
            }

            Material[] mats = renderer.materials;
            for (int i = 0; i < mats.Length; i++)
            {
                Material m = mats[i];
                if (m == null)
                {
                    continue;
                }

                if (m.HasProperty(BaseColorId))
                {
                    m.SetColor(BaseColorId, c);
                }

                if (m.HasProperty(ColorId))
                {
                    m.SetColor(ColorId, c);
                }

                if (m.HasProperty(EmissionColorId))
                {
                    m.EnableKeyword("_EMISSION");
                    m.SetColor(EmissionColorId, emission);
                }
            }

            renderer.materials = mats;
        }

        TrailRenderer trail = shard.GetComponent<TrailRenderer>();
        if (trail == null)
        {
            return;
        }

        Material[] trailMats = trail.materials;
        for (int i = 0; i < trailMats.Length; i++)
        {
            Material m = trailMats[i];
            if (m == null)
            {
                continue;
            }

            if (m.HasProperty(BaseColorId))
            {
                m.SetColor(BaseColorId, c);
            }

            if (m.HasProperty(ColorId))
            {
                m.SetColor(ColorId, c);
            }

            if (m.HasProperty(EmissionColorId))
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor(EmissionColorId, emission);
            }
        }

        trail.materials = trailMats;
    }
}
