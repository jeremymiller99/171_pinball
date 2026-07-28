// Updated by Claude Code (claude-opus-5) for jjmil on 2026-07-28.
// Change: unified ChargeStatus; Shock renamed to the plain Charge vocabulary.

using UnityEngine;

/// <summary>
/// Tech ball: each component hit has a chance to give itself a Charge, making it a
/// self-charging carrier for Charge consumers (Engine, Capacitor, ...).
/// Resting contact with lane or wall geometry does not roll the chance.
/// </summary>
public class TransistorBall : Ball
{
    [SerializeField, Range(0f, 1f)] private float chanceToSelfCharge = 0.2f;

    protected override void OnCollisionEnter(Collision collision)
    {
        base.OnCollisionEnter(collision);

        if (GetBoardComponentsForScoring(collision.collider).Length == 0)
        {
            return;
        }

        if (Random.value <= chanceToSelfCharge)
        {
            ChargeStatusUtility.GiveCharge(this, 1);
        }
    }
}
