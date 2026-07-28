using UnityEngine;

/// <summary>
/// Charge status for balls. Balls are the carriers of the charge system:
/// shock sources (Electric Floorboard) charge the ball, and consumer
/// components drain it on contact. Decay is inherited from ChargeStatus.
/// </summary>
[RequireComponent(typeof(Ball))]
public sealed class BallChargeStatus : ChargeStatus
{
}
