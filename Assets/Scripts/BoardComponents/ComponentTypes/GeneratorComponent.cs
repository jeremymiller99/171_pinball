// Updated by Claude Code (claude-opus-5) for jjmil on 2026-07-28.
// Change: unified ChargeStatus; Shock renamed to the plain Charge vocabulary.

using UnityEngine;

/// <summary>
/// Tech sling: each ball hit has a chance to give the ball a Charge, making the
/// slings a steady Charge source for whatever consumers the board carries.
/// </summary>
public class GeneratorComponent : Bumper
{
    [Header("Generator")]
    [SerializeField, Range(0f, 1f)] private float chanceToChargeBall = 0.3f;
    [SerializeField] private int chargeGiven = 1;

    protected override void OnCollisionEnter(Collision collision)
    {
        base.OnCollisionEnter(collision);

        Ball ball = collision.collider.GetComponent<Ball>();
        if (ball == null || Random.value > chanceToChargeBall)
        {
            return;
        }

        ChargeDebug.Log($"{name} charges {ball.name}");
        ChargeStatusUtility.GiveCharge(ball, chargeGiven);
    }
}
