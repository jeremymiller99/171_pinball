using UnityEngine;

public class GolfBall : Ball
{
    [SerializeField] private float startingPoints;
    [SerializeField] private float startingMult;
    [SerializeField] private float lostPointsPerHit;
    [SerializeField] private float lostMultPerHit;


    override protected void AddScore(float amount, TypeOfScore typeOfScore, Transform pos)
    {
        base.AddScore(startingPoints - (lostPointsPerHit * (componentHits - 1)), TypeOfScore.points, pos);
        base.AddScore(startingMult - (lostMultPerHit * (componentHits - 1)), TypeOfScore.mult, pos);
    }
}
