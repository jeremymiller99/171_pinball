using UnityEngine;

/// <summary>
/// Mini-ball spawned by <see cref="ConfettiBall"/>. Pops (despawns) after a fixed
/// number of scoring board component hits (not ball–ball collisions).
/// </summary>
public sealed class ConfettiShardBall : Ball
{
    public const int HitsToPopDefault = 3;

    [SerializeField] private BallSpawner ballSpawner;

    [Min(1)]
    [SerializeField] private int hitsToPop = HitsToPopDefault;

    private bool _popped;

    private void Awake()
    {
        ballSpawner = ServiceLocator.Get<BallSpawner>();
    }

    private void OnValidate()
    {
        hitsToPop = Mathf.Max(1, hitsToPop);
    }

    protected override bool ShouldIgnoreBoardHitFromCollider(Collider collider)
    {
        Ball other = collider.GetComponentInParent<Ball>();
        return other != null && other != this;
    }

    private void LateUpdate()
    {
        if (_popped)
        {
            return;
        }

        if (componentHits < hitsToPop)
        {
            return;
        }

        _popped = true;
        ServiceLocator.Get<AudioManager>()?.PlayBallSplit(transform.position);

        if (ballSpawner == null)
        {
            ballSpawner = ServiceLocator.Get<BallSpawner>();
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
