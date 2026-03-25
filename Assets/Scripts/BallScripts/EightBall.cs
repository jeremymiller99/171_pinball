// Updated with Cursor (claude-4.6-opus) by jjmil on 2026-03-24.
using UnityEngine;

public class EightBall : Ball
{
    [SerializeField] private int hitInterval = 8;
    [SerializeField] private float pointsMultiplierOnInterval = 3f;

    protected override int HitIntervalForPopup => hitInterval;

    override protected void AddScore(float amount, TypeOfScore typeOfScore, Transform pos)
    {
        if (typeOfScore == TypeOfScore.points && componentHits > 0 && componentHits % hitInterval == 0)
        {
            amount *= pointsMultiplierOnInterval;
            componentHits = 0;
        }
        base.AddScore(amount, typeOfScore, pos);
    }
}
