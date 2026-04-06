using UnityEngine;

public class VarietyFlipper : PinballFlipper
{
    [SerializeField] private float pointsToGive = 0;
    [SerializeField] private float amountToIncreaseBy = 5;

    protected override void OnCollisionEnter(Collision collision)
    {
        Ball ball = collision.collider.GetComponent<Ball>();
        if (ball)
        {
            if (latestBallHit && ball == latestBallHit)
            {
                pointsToGive = 0;
            } else
            {
                pointsToGive += amountToIncreaseBy;
                ServiceLocator.Get<ScoreManager>().AddScore(pointsToGive, TypeOfScore.points, transform);
            }
            latestBallHit = ball;
        }

    }

}
