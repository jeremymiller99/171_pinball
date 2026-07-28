using UnityEngine;

/// <summary>
/// Tech ball: each component hit has a chance to Shock itself, making it a
/// self-charging carrier for Charge consumers (Engine, Capacitor, ...).
/// Resting contact with lane or wall geometry does not roll the chance.
/// </summary>
public class TransistorBall : Ball
{
    [SerializeField, Range(0f, 1f)] private float chanceToSelfShock = 0.2f;

    protected override void OnCollisionEnter(Collision collision)
    {
        base.OnCollisionEnter(collision);

        if (GetBoardComponentsForScoring(collision.collider).Length == 0)
        {
            return;
        }

        if (Random.value <= chanceToSelfShock)
        {
            ChargeStatusUtility.GetOrAddBallStatus(this)?.Shock();
        }
    }
}
