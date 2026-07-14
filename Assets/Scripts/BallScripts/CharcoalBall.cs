using UnityEngine;

/// <summary>
/// Catalyst: Flammable 5 and fuels whatever it touches (see
/// BallFireStatus.fuelOtherOnContact on the prefab). Passive: while Charcoal
/// waits in the queue, every ball that launches is Fueled twice.
/// </summary>
[RequireComponent(typeof(BallFireStatus))]
public sealed class CharcoalBall : Ball
{
    public const string DefinitionId = "Charcoal";

    private const int fuelPerLaunch = 2;

    [SerializeField] private BallSpawner ballSpawner;

    private void Awake()
    {
        if (ballSpawner == null)
        {
            ballSpawner = ServiceLocator.Get<BallSpawner>();
        }

        if (ballSpawner != null)
        {
            ballSpawner.ActivateBall += OnBallActivated;
        }
    }

    private void OnDestroy()
    {
        if (ballSpawner != null)
        {
            ballSpawner.ActivateBall -= OnBallActivated;
        }
    }

    private void OnBallActivated(GameObject launched)
    {
        if (launched == gameObject || ballSpawner == null)
        {
            return;
        }

        // The passive only applies while this Charcoal is still queued.
        if (ballSpawner.GetSlotIndexForHandBall(gameObject) < 0)
        {
            return;
        }

        Ball launchedBall = launched != null ? launched.GetComponent<Ball>() : null;
        if (launchedBall == null)
        {
            return;
        }

        FireStatusUtility.GetOrAddBallStatus(launchedBall)?.Fuel(fuelPerLaunch);
    }
}
