using UnityEngine;

/// <summary>
/// Entropy/Tech bumper: charged balls that strike it discharge into it. Once
/// Charged it consumes every stack and Ignites itself. While it burns, each
/// activation raises its base points; every point gained this way is stripped
/// when the burn ends.
/// </summary>
public class EngineComponent : Bumper
{
    [Header("Engine")]
    [SerializeField] private int chargeNeeded = 1;
    [SerializeField] private float basePointsPerBurningHit = 25f;

    private ComponentChargeStatus _chargeStatus;
    private ComponentFireStatus _fireStatus;
    private float _burnBonusGained;

    new protected void Awake()
    {
        base.Awake();

        _chargeStatus = ChargeStatusUtility.GetOrAddComponentStatus(this);
        _chargeStatus.ChargeChanged += IgniteIfCharged;

        _fireStatus = GetComponent<ComponentFireStatus>();
        if (_fireStatus == null)
        {
            _fireStatus = FireStatusUtility.GetOrAddComponentStatus(this);
        }

        if (_fireStatus != null)
        {
            _fireStatus.StacksChanged += IgniteIfCharged;
            _fireStatus.BurnedOut += RemoveBurnBonus;
        }

        onBallHit += RampWhileBurning;
    }

    private void OnDestroy()
    {
        if (_chargeStatus != null)
        {
            _chargeStatus.ChargeChanged -= IgniteIfCharged;
        }

        if (_fireStatus != null)
        {
            _fireStatus.StacksChanged -= IgniteIfCharged;
            _fireStatus.BurnedOut -= RemoveBurnBonus;
        }

        onBallHit -= RampWhileBurning;
    }

    new protected void OnCollisionEnter(Collision collision)
    {
        base.OnCollisionEnter(collision);

        Ball ball = collision.collider.GetComponent<Ball>();
        if (ball != null)
        {
            ChargeStatusUtility.DrainBallInto(ball, _chargeStatus);
        }
    }

    private void IgniteIfCharged()
    {
        if (_chargeStatus.Charge < chargeNeeded || _fireStatus == null
            || _fireStatus.IsOnFire)
        {
            return;
        }

        if (!_fireStatus.IsFlammable)
        {
            ChargeDebug.Log(
                $"{name} charged ({_chargeStatus.Charge}/{chargeNeeded}) "
                + "but has no Flammable stacks to burn, holding");
            return;
        }

        int consumed = _chargeStatus.TakeAllCharge();
        ChargeDebug.Log($"{name} consumes {consumed} Charge and ignites itself");
        _fireStatus.Ignite();
    }

    private void RampWhileBurning()
    {
        if (_fireStatus == null || !_fireStatus.IsOnFire)
        {
            return;
        }

        amountToScore += basePointsPerBurningHit;
        _burnBonusGained += basePointsPerBurningHit;
        ChargeDebug.Log($"{name} ramps to {amountToScore} base points while burning");
    }

    private void RemoveBurnBonus()
    {
        if (_burnBonusGained == 0f)
        {
            return;
        }

        amountToScore -= _burnBonusGained;
        ChargeDebug.Log(
            $"{name} burn ends, sheds {_burnBonusGained} bonus base points "
            + $"(back to {amountToScore})");
        _burnBonusGained = 0f;
    }
}
