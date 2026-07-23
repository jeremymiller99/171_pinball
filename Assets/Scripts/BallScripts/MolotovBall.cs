using UnityEngine;

/// <summary>
/// Catalyst: contact with a component or ball Fuels both sides (the other
/// side via BallFireStatus.fuelOtherOnContact on the prefab), and each pour
/// has a small chance to break the bottle and retire the ball. Passive:
/// while this waits in the queue, every ball that launches is Fueled once.
/// </summary>
[RequireComponent(typeof(BallFireStatus))]
public sealed class MolotovBall : Ball
{
    public const string DefinitionId = "UnfinishedMolotov";

    [Tooltip("Chance the bottle breaks each time the active Fuels it.")]
    [Range(0f, 1f)]
    [SerializeField] private float breakChance = 0.05f;
    [SerializeField] private int fuelPerContact = 1;
    [SerializeField] private int queuedFuelPerLaunch = 1;
    [SerializeField] private BallSpawner ballSpawner;

    private BallFireStatus _fireStatus;
    private bool _broken;

    private void Awake()
    {
        _fireStatus = GetComponent<BallFireStatus>();

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

    protected override void OnCollisionEnter(Collision collision)
    {
        base.OnCollisionEnter(collision);

        // Resting contact with lane or wall geometry never pours the bottle.
        bool hitComponent =
            GetBoardComponentsForScoring(collision.collider).Length > 0;
        bool hitBall =
            collision.collider.GetComponentInParent<Ball>() != null;
        if (!hitComponent && !hitBall)
        {
            return;
        }

        _fireStatus.Fuel(fuelPerContact);
        TryBreak();
    }

    private void OnBallActivated(GameObject launched)
    {
        if (launched == gameObject || ballSpawner == null)
        {
            return;
        }

        // The passive only applies while this Molotov is still queued.
        if (ballSpawner.GetSlotIndexForHandBall(gameObject) < 0)
        {
            return;
        }

        Ball launchedBall = launched != null ? launched.GetComponent<Ball>() : null;
        if (launchedBall == null)
        {
            return;
        }

        FireStatusUtility.GetOrAddBallStatus(launchedBall)?.Fuel(queuedFuelPerLaunch);
    }

    private void TryBreak()
    {
        if (_broken || Random.value >= breakChance)
        {
            return;
        }

        _broken = true;

        DrainHandler drainHandler = ServiceLocator.Get<DrainHandler>();
        if (drainHandler != null)
        {
            drainHandler.OnBallDrained(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
}
