// no hit-count countdown UI; ball–ball does not advance burst hit count.
using UnityEngine;

/// <summary>
/// Splitter-class ball: every <see cref="hitsPerBurst"/> scoring component hits,
/// Projects <see cref="BallsOnSplit"/> colorful Holoballs that pop after
/// <see cref="holoballHitsToPop"/> activations. Fires
/// <see cref="BurstsPerBallDefault"/> bursts per main ball by default
/// (<see cref="maxBursts"/>).
/// </summary>
public sealed class ConfettiBall : Ball, ISplitter
{
    public const string DefinitionId = "Confetti";

    /// <summary>Design default: how many shard-spawn bursts each Confetti ball performs.</summary>
    public const int BurstsPerBallDefault = 10;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    [Header("Burst")]
    [Min(1)]
    [SerializeField] private int hitsPerBurst = 4;

    [Min(1)]
    [Tooltip("Activations (scoring hits) each projected Holoball lasts before it pops.")]
    [SerializeField] private int holoballHitsToPop = Holoball.DefaultActivations;

    [Min(1)]
    public int BallsOnSplit { get; set; } = 3;

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
        _burstsRemaining = maxBursts;
        _lastBurstMilestone = 0;
    }

    private void OnValidate()
    {
        hitsPerBurst = Mathf.Max(1, hitsPerBurst);
        BallsOnSplit = Mathf.Max(1, BallsOnSplit);
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
        ProjectHoloballBurst();
        _burstsRemaining--;
    }

    private void ProjectHoloballBurst()
    {
        ServiceLocator.Get<AudioManager>()?.PlayBallSplit(transform.position);

        Rigidbody parentRb = GetComponent<Rigidbody>();
        Vector3 baseVel = parentRb != null ? parentRb.linearVelocity : Vector3.zero;

        for (int i = 0; i < BallsOnSplit; i++)
        {
            // Project a Holoball that pops after a set number of activations. Position is
            // left to the Project default (a random point on a small radius around this
            // ball); Confetti then adds its own scale, colour, and outward burst.
            GameObject holo = Projector.Project(
                gameObject, HoloballLifetime.Activations(holoballHitsToPop));
            if (holo == null)
            {
                continue;
            }

            float s = shardSpawnUniformScale;
            holo.transform.localScale = new Vector3(s, s, s);
            ApplyRandomShardColor(holo);

            Rigidbody rb = holo.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.mass *= s * s * s;
                Vector3 burst = Random.onUnitSphere * spawnBurstImpulse;
                burst.y = Mathf.Abs(burst.y) * 0.5f + 0.25f * spawnBurstImpulse;
                rb.linearVelocity = baseVel * 0.35f + burst;
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
