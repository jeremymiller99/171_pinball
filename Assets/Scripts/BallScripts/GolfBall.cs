using UnityEngine;

public class GolfBall : Ball
{
    [SerializeField] private float startingPoints;
    [SerializeField] private float startingMult;
    [SerializeField] private float lostPointsPerHit;
    [SerializeField] private float lostMultPerHit;


    override protected void AddScore(float amount, TypeOfScore typeOfScore, Transform pos)
    {
        if (componentHits == 1)
        {
            base.AddScore(startingPoints, TypeOfScore.points, pos);
            base.AddScore(startingMult, TypeOfScore.mult, pos);
        } else
        {
            base.AddScore(-lostPointsPerHit, TypeOfScore.points, pos);
            base.AddScore(-lostMultPerHit, TypeOfScore.mult, pos);
        }
        
    }
}
