// Updated by Claude Code (claude-opus-5) for jjmil on 2026-07-28.
// Change: Flammable gating removed — any ball it touches now catches.

using UnityEngine;

/// <summary>
/// Catalyst kicker: on collision it lights the ball on Fire and scores the usual
/// bumper hit. With Flammable gone there is nothing to qualify for, so every ball
/// that touches the lighter catches.
/// </summary>
public class LighterComponent : Bumper
{
    protected override void OnCollisionEnter(Collision collision)
    {
        base.OnCollisionEnter(collision);

        Ball ball = collision.collider.GetComponent<Ball>();
        if (ball == null)
        {
            return;
        }

        FireDebug.Log($"{name} lights {ball.name}");
        FireStatusUtility.LightBall(ball);
    }
}
