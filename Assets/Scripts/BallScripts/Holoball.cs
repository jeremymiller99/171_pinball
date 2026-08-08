using UnityEngine;

/// <summary>How a Holoball decides when to expire.</summary>
public enum HoloballLifetimeMode
{
    /// <summary>Despawn after a number of scoring component hits (activations).</summary>
    Activations,
    /// <summary>Despawn after a number of seconds.</summary>
    Time
}

/// <summary>
/// Longevity spec for a projected Holoball: either a number of activations (scoring
/// component collisions) or a duration in seconds. Build one via the <see cref="Activations"/>
/// or <see cref="Seconds"/> factory methods, e.g. <c>HoloballLifetime.Activations(3)</c>.
/// </summary>
public readonly struct HoloballLifetime
{
    public readonly HoloballLifetimeMode Mode;
    public readonly float Value;

    private HoloballLifetime(HoloballLifetimeMode mode, float value)
    {
        Mode = mode;
        Value = value;
    }

    /// <summary>Holoball pops after <paramref name="hits"/> scoring component hits.</summary>
    public static HoloballLifetime Activations(int hits) =>
        new HoloballLifetime(HoloballLifetimeMode.Activations, Mathf.Max(1, hits));

    /// <summary>Holoball pops after <paramref name="seconds"/> seconds.</summary>
    public static HoloballLifetime Seconds(float seconds) =>
        new HoloballLifetime(HoloballLifetimeMode.Time, Mathf.Max(0.01f, seconds));
}

/// <summary>
/// A temporary "Holoball" created by the Project keyword (see <see cref="Projector"/>).
/// It behaves like a ball for scoring, then despawns once its longevity condition is met:
/// a set number of activations (scoring component hits) or a set duration. Replaces the
/// old hard-coded Confetti shard; the expiry condition is supplied per spawn by the item
/// that projected it, via <see cref="Initialize"/>.
/// </summary>
public sealed class Holoball : Ball
{
    /// <summary>Default activation count when none is supplied.</summary>
    public const int DefaultActivations = 10;

    /// <summary>
    /// Global multiplier on every Holoball's longevity (duration or activation count).
    /// Modules push this — e.g. Astral Projection sets it to 2. Read live at the expiry
    /// check so it applies to Holoballs already in flight.
    /// </summary>
    public static float LifetimeMultiplier = 1f;

    private BallSpawner _ballSpawner;
    private HoloballLifetime _lifetime = HoloballLifetime.Activations(DefaultActivations);
    private float _elapsed;
    private bool _despawned;

    private void Awake()
    {
        _ballSpawner = ServiceLocator.Get<BallSpawner>();
    }

    /// <summary>
    /// Sets the expiry condition. Call immediately after Instantiate (the Projector does this).
    /// Safe to leave uncalled: the Holoball defaults to <see cref="DefaultActivations"/> activations.
    /// </summary>
    public void Initialize(HoloballLifetime lifetime)
    {
        _lifetime = lifetime;
        _elapsed = 0f;
    }

    // Ball-ball contacts don't count as board activations (matches the old shard).
    protected override bool ShouldIgnoreBoardHitFromCollider(Collider collider)
    {
        Ball other = collider.GetComponentInParent<Ball>();
        return other != null && other != this;
    }

    private void Update()
    {
        if (_despawned || _lifetime.Mode != HoloballLifetimeMode.Time)
        {
            return;
        }

        _elapsed += Time.deltaTime;
        if (_elapsed >= _lifetime.Value * Mathf.Max(0.01f, LifetimeMultiplier))
        {
            Despawn();
        }
    }

    // Activation checks run in LateUpdate so componentHits (bumped during collision
    // callbacks) has settled for the frame — same timing the old shard used.
    private void LateUpdate()
    {
        if (_despawned || _lifetime.Mode != HoloballLifetimeMode.Activations)
        {
            return;
        }

        if (componentHits >= Mathf.RoundToInt(_lifetime.Value * Mathf.Max(0.01f, LifetimeMultiplier)))
        {
            Despawn();
        }
    }

    private void Despawn()
    {
        if (_despawned)
        {
            return;
        }
        _despawned = true;

        ServiceLocator.Get<AudioManager>()?.PlayBallSplit(transform.position);

        // Route through the drain flow, like Fireball/Molotov: if other balls are still in
        // play it just despawns this one, but if this was the last ball it loads the next
        // ball into the launcher (rather than leaving the player with none).
        DrainHandler drainHandler = ServiceLocator.Get<DrainHandler>();
        if (drainHandler != null)
        {
            drainHandler.OnBallDrained(gameObject);
            return;
        }

        if (_ballSpawner == null)
        {
            _ballSpawner = ServiceLocator.Get<BallSpawner>();
        }

        if (_ballSpawner != null)
        {
            _ballSpawner.DespawnBall(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
}
