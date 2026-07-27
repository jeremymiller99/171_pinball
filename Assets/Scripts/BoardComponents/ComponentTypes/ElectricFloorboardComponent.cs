using UnityEngine;

/// <summary>
/// Tech roll-over: slows the ball slightly and Shocks it as it passes. When a
/// ball that already carries Charge rolls over, the floorboard drains it; once
/// the floorboard is Charged it triggers, consuming its Charge and permanently
/// raising all point scoring for the rest of the run.
/// </summary>
public class ElectricFloorboardComponent : BoardComponent
{
    [Header("Electric Floorboard")]
    [SerializeField] private int chargeNeeded = 1;
    [SerializeField] private int shocksPerPass = 1;
    [Tooltip("Multiplier applied to the ball's velocity as it rolls over.")]
    [SerializeField, Range(0f, 1f)] private float ballSlowFactor = 0.85f;
    [Tooltip("Permanent scoring gained per trigger (0.05 = +5%).")]
    [SerializeField] private float permanentScoreBonus = 0.05f;

    private ComponentChargeStatus _chargeStatus;

    protected override void Awake()
    {
        base.Awake();
        _chargeStatus = ChargeStatusUtility.GetOrAddComponentStatus(this);
    }

    protected override void OnTriggerEnter(Collider other)
    {
        base.OnTriggerEnter(other);

        Ball ball = other.GetComponent<Ball>();
        if (ball == null)
        {
            return;
        }

        ChargeStatusUtility.DrainBallInto(ball, _chargeStatus);
        TriggerIfCharged();
        SlowBall(other);
        ChargeStatusUtility.GetOrAddBallStatus(ball)?.Shock(shocksPerPass);
    }

    private void TriggerIfCharged()
    {
        if (_chargeStatus == null || _chargeStatus.Charge < chargeNeeded)
        {
            return;
        }

        _chargeStatus.TakeAllCharge();

        if (scoreManager == null)
        {
            scoreManager = ServiceLocator.Get<ScoreManager>();
        }
        scoreManager?.AddPermanentPointsBonus(permanentScoreBonus);

        ChargeDebug.Log(
            $"{name} triggers, scoring permanently up "
            + $"{permanentScoreBonus:P0} (now x{scoreManager?.PermanentPointsMultiplier:0.##})");
    }

    private void SlowBall(Collider other)
    {
        Rigidbody rb = other.attachedRigidbody;
        if (rb != null)
        {
            rb.linearVelocity *= ballSlowFactor;
        }
    }
}
