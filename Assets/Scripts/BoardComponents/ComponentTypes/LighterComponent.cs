using UnityEngine;

/// <summary>
/// Catalyst bumper: on collision it lights the ball and scores (scoring is the base bumper
/// hit). Igniting only takes if the ball is Flammable, so a plain ball is unaffected while a
/// Flammable one (Charcoal, a fueled ball, ...) goes On Fire. The lighter has no baseline
/// flammability of its own and does not explode.
/// </summary>
public class LighterComponent : Bumper
{
    new protected void OnCollisionEnter(Collision collision)
    {
        base.OnCollisionEnter(collision);

        Ball ball = collision.collider.GetComponent<Ball>();
        if (ball == null)
        {
            return;
        }

        // Ignite is a no-op unless the ball is Flammable, which gives us
        // "if the colliding ball is flammable, the ball will then be on fire".
        FireStatusUtility.GetOrAddBallStatus(ball)?.Ignite();
    }
}
