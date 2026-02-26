using UnityEngine;

public class EightBall : Ball
{
    [SerializeField] private int hitInterval = 8;
    [SerializeField] private float pointsMultiplierOnInterval = 3f;

    override protected void AddScore(float amount, TypeOfScore typeOfScore, Transform pos)
    {
        if (typeOfScore == TypeOfScore.points && componentHits > 0 && componentHits % hitInterval == 0)
        {
            amount *= pointsMultiplierOnInterval;
        }
        base.AddScore(amount, typeOfScore, pos);
    }
}
