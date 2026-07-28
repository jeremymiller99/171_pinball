using UnityEngine;

/// <summary>
/// Tech sling: each ball hit has a chance to Shock the ball, making the
/// slings a steady Charge source for whatever consumers the board carries.
/// </summary>
public class GeneratorComponent : Bumper
{
    [Header("Generator")]
    [SerializeField, Range(0f, 1f)] private float chanceToShockBall = 0.3f;

    protected override void OnCollisionEnter(Collision collision)
    {
        base.OnCollisionEnter(collision);

        Ball ball = collision.collider.GetComponent<Ball>();
        if (ball == null || Random.value > chanceToShockBall)
        {
            return;
        }

        ChargeDebug.Log($"{name} shocks {ball.name}");
        ChargeStatusUtility.GetOrAddBallStatus(ball)?.Shock();
    }
}
