using UnityEngine;

public class LuckySevenBall : Ball
{
    [SerializeField] private int hitInterval = 7;
    [SerializeField] private float pointsMultiplierOnInterval = 2f;

    override protected void AddScore(float amount, TypeOfScore typeOfScore, Transform pos)
    {
        if (typeOfScore == TypeOfScore.points && componentHits > 0 && componentHits % hitInterval == 0)
        {
            amount *= pointsMultiplierOnInterval;
        }
        base.AddScore(amount, typeOfScore, pos);
    }

    public override float PointsAwardMultiplier
    {
        get
        {
            return (componentHits > 0 && componentHits % hitInterval == 0)
                ? pointMultiplier * pointsMultiplierOnInterval
                : pointMultiplier;
        }
    }
}
